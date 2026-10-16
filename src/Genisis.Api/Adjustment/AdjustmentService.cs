using System.Data;
using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Membership;
using Genisis.Api.Registration;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Adjustment;

public interface IAdjustmentService
{
    Task<LoadedMembership?> LoadAsync(string membershipNo, CancellationToken cancellationToken);

    /// <summary>Fees tab: principal and covered persons with their latest refund, then one line per reinstatement.</summary>
    Task<List<FeeRow>> GetFeesAsync(LoadedMembership membership, CancellationToken cancellationToken);

    /// <returns>The recalculated fees, or <c>null</c> when the membership does not exist.</returns>
    Task<Recalculation?> RecalculateAsync(string membershipNo, RecalculateRequest request, CancellationToken cancellationToken);

    Task<SupplementaryExpiry> GetSupplementaryExpiryAsync(SupplementaryExpiryRequest request, CancellationToken cancellationToken);

    /// <returns>The result, or <c>null</c> when the membership does not exist.</returns>
    Task<AdjustmentResult?> SaveAsync(string membershipNo, AdjustmentRequest request, string userCode, CancellationToken cancellationToken);
}

/// <summary>
/// Membership &gt; Adjustment (frmNewMBMAdjustment.CmdSave_Click and its helpers). Main, maintenance and clinical tables are written
/// on one connection inside one transaction, so a failure leaves nothing behind (the desktop used two transactions and wrote
/// clinical rows outside both).
/// </summary>
public sealed class AdjustmentService(DbConnections db, ILogger<AdjustmentService> logger) : IAdjustmentService
{
    private static string Code(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static string? TextOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<LoadedMembership?> LoadAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        return await LoadAsync(conn, null, membershipNo);
    }

    internal static async Task<LoadedMembership?> LoadAsync(SqlConnection conn, SqlTransaction? tx, string membershipNo)
    {
        using var grid = await conn.ProcMultipleAsync("genisis.Adjustment_Load", new { number = membershipNo.Trim() }, tx);
        var member = await grid.ReadFirstOrDefaultAsync<AdjustmentMember>();
        var covered = (await grid.ReadAsync<AdjustmentCovered>()).ToList();
        var clinic = await grid.ReadFirstOrDefaultAsync<ClinicalPlan>();
        var limit = await grid.ReadFirstOrDefaultAsync<PlanLimit>();
        return member is null ? null : new LoadedMembership(member, covered, clinic, limit);
    }

    public async Task<List<FeeRow>> GetFeesAsync(LoadedMembership membership, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.Adjustment_FeeAdjustments", new { number = membership.Member.MembershipNo });
        var refunds = (await grid.ReadAsync<(string CoverId, decimal? McoEffective, decimal? McoRefund, decimal? PremiumEffective, decimal? PremiumReduction)>())
            .GroupBy(r => AdjustmentRules.CoverId(r.CoverId)).ToDictionary(g => g.Key, g => g.Last());
        var reinstatements = (await grid.ReadAsync<(string CoverId, decimal? Mco, decimal? Premium, string? InvoiceNo, DateTime? InvoiceDate)>()).ToList();
        return FeeRows(membership, refunds, reinstatements);
    }

    /// <summary>Desktop FeesListing + ShowReinstatement.</summary>
    internal static List<FeeRow> FeeRows(LoadedMembership membership,
        Dictionary<string, (string CoverId, decimal? McoEffective, decimal? McoRefund, decimal? PremiumEffective, decimal? PremiumReduction)> refunds,
        List<(string CoverId, decimal? Mco, decimal? Premium, string? InvoiceNo, DateTime? InvoiceDate)> reinstatements)
    {
        static decimal? Round(decimal? value) => value is { } v ? FeeRules.VbRound(v) : null;
        var member = membership.Member;
        decimal? Disability(string coverId) =>
            AdjustmentRules.DisabilityLimitApplies(membership.Limit?.SuppLimitStatus, coverId) ? membership.Limit?.DisabilityLimit ?? 0 : null;

        var rows = new List<FeeRow>();
        void Add(string name, string? relationship, string coverId, string? status, string? plan, DateTime? effective, DateTime? expiry, DateTime? cancel,
            decimal? annual, decimal? available, decimal? basicPremium, decimal? premium, decimal? basicMco, decimal? mco, string? invoice,
            DateTime? invoiceDate, string? creditNote, DateTime? creditNoteDate)
        {
            refunds.TryGetValue(coverId, out var refund);
            rows.Add(new FeeRow(name, relationship, coverId, status, plan, effective, expiry, cancel, annual, available, Disability(coverId), basicPremium,
                premium, basicMco, mco, invoice, invoiceDate, creditNote, creditNoteDate, Round(refund.McoEffective), Round(refund.McoRefund),
                Round(refund.PremiumEffective), Round(refund.PremiumReduction), false));
        }

        Add((member.Name ?? "").Trim(), DataTypesForFees.Principal, CoverIds.Principal, member.Status, member.PlanCode, member.PayorEffectiveDate,
            member.PayorExpiryDate, member.CancelDate, membership.Limit?.AnnualLimit, member.AvailableLimit, member.BasicPremium, member.Premium,
            member.BasicMco, member.McoFee, member.InvoiceNo, member.InvoiceDate, member.CreditNote, member.CreditNoteDate);
        foreach (var c in membership.Covered)
            Add((c.Name ?? "").Trim(), c.Relationship, AdjustmentRules.CoverId(c.CoverId), c.Status, TextOrNull(c.PlanCode) ?? member.PlanCode,
                c.EffectiveDate ?? member.PayorEffectiveDate, c.ExpiryDate ?? member.PayorExpiryDate, c.CancelDate, c.AnnualLimit, c.AvailableLimit,
                c.BasicPremium, c.Premium, c.BasicMco, c.Mco, c.InvoiceNo, c.InvoiceDate, c.CreditNote, c.CreditNoteDate);

        foreach (var line in rows.ToList())
            foreach (var reinstatement in reinstatements.Where(x => AdjustmentRules.CoverId(x.CoverId) == line.CoverId))
                rows.Add(line with
                {
                    CancelDate = null, AnnualLimit = null, AvailableLimit = null, BasicPremium = 0, Premium = 0, BasicMco = 0, Mco = 0,
                    InvoiceNo = reinstatement.InvoiceNo, InvoiceDate = reinstatement.InvoiceDate, CreditNote = null, CreditNoteDate = null,
                    McoEffective = Round(reinstatement.Mco), McoRefund = 0, PremiumEffective = Round(reinstatement.Premium), PremiumReduction = 0,
                    Reinstatement = true,
                });
        return rows;
    }

    public async Task<Recalculation?> RecalculateAsync(string membershipNo, RecalculateRequest request, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var membership = await LoadAsync(conn, null, membershipNo);
        return membership is null ? null : await RecalculateAsync(conn, null, membership, request);
    }

    /// <summary>Desktop MemberCallAge + MemberCallFees + the annual limit prompt in cboPLNCode_LostFocus.</summary>
    internal static async Task<Recalculation> RecalculateAsync(SqlConnection conn, SqlTransaction? tx, LoadedMembership membership, RecalculateRequest request)
    {
        var member = membership.Member;
        var plan = Code(TextOrNull(request.PlanCode) ?? member.PlanCode);
        var insured = Code(member.InsuredType);
        if (plan == "") { plan = Code(membership.Clinic?.PlanCode); insured = Code(membership.Clinic?.InsuredType); }
        var health = Code(member.HealthCode);
        var payor = Code(member.PayorCode);
        if (request.PayorEffectiveDate is not { } effective || request.DateOfBirth is not { } dateOfBirth)
            throw new AdjustmentException(AdjustmentMessages.InvalidDateOrAge);
        var expiry = request.PayorExpiryDate ?? effective.AddYears(1).AddDays(-1);
        var warnings = new List<string>();

        var bands = await conn.ProcQueryAsync<LegacyRules.AgeBand>("genisis.Registration_AgeBands", new { plan }, tx);
        var band = LegacyRules.PickAgeBand(bands, LegacyRules.Age(effective, dateOfBirth), plan) ?? throw new AdjustmentException(AdjustmentMessages.InvalidDateOrAge);
        var ageCode = band.AgeCode.Trim();
        var adultChild = AdultChild.ForAgeBand(ageCode);

        decimal premium = 0, basicPremium = 0, mco = 0;
        decimal? annual = null;
        if (Code(request.PlanCode ?? member.PlanCode) != "" && insured != "")
        {
            var rates = await conn.ProcFirstOrDefaultAsync<PlanRates>("genisis.Adjustment_PlanRates", new { plan, hlt = health, pay = payor, eff = effective }, tx);
            var mcoRates = await conn.ProcFirstOrDefaultAsync<McoRates>("genisis.Registration_Mco", new { ins = insured, hlt = health, plan, eff = effective }, tx);
            mco = LegacyRules.Money((adultChild == AdultChild.Child ? mcoRates?.MCOAmountChild : mcoRates?.MCOAmountAdult) ?? 0);
            if (YesNo.IsNotNo(rates?.ProRate)) mco = FeeRules.ProRateMoney(effective, expiry, mco);
            if (YesNo.IsNotNo(rates?.RoundUpStatus)) mco = FeeRules.VbRound(mco);

            if (YesNo.IsYes(rates?.PremiumInd))
            {
                var row = await conn.ProcFirstOrDefaultAsync<PremiumRates>("genisis.Registration_Premium",
                    new { ins = insured, hlt = health, plan, ageCode, eff = effective, pay = payor }, tx);
                if (row is null) warnings.Add("Age Out of Premium Range! Please check your Premium Table.");
                var female = YesNo.IsYes(rates?.PremGenderStatus) && Code(request.Sex) == Sex.Female;
                basicPremium = LegacyRules.Money((female ? row?.PRMAmountFemale : row?.PRMAmount) ?? 0);
            }
            premium = basicPremium;
            var discount = await conn.ProcFirstOrDefaultAsync<PlanDiscount>("genisis.Adjustment_DiscPlan", new { plan }, tx);
            if (discount?.PolicyDisc is > 0) premium = premium * (100 - discount.PolicyDisc.Value) / 100;
            if (discount?.RenewalDisc is > 0) premium = premium * (100 - discount.RenewalDisc.Value) / 100;
            premium = FeeRules.ProRateMoney(effective, expiry, LegacyRules.Money(premium));

            var limit = await conn.ProcFirstOrDefaultAsync<AnnualLimitRates>("genisis.Registration_AnnualLimit", new { ins = insured, plan, hlt = health, eff = effective }, tx);
            if (limit is null) warnings.Add("Annual Limit Information Is Not Correct! Please check you Maintenance Table");
            annual = limit?.AnnLimit ?? 0;
        }

        if (AdjustmentRules.PolicyLengthWarning(effective, expiry) is { } lengthWarning) warnings.Add(lengthWarning);
        return new Recalculation(ageCode, adultChild, basicPremium, premium, mco, annual, warnings);
    }

    public async Task<SupplementaryExpiry> GetSupplementaryExpiryAsync(SupplementaryExpiryRequest request, CancellationToken cancellationToken)
    {
        if (request.DateOfBirth is not { } dateOfBirth || request.EffectiveDate is not { } effective || request.ExpiryDate is not { } expiry
            || TextOrNull(request.PlanCode) is not { } plan)
            return new SupplementaryExpiry(request.ExpiryDate, false, 0);
        await using var conn = db.MainDb();
        var settings = await conn.ProcFirstOrDefaultAsync<PlanSettings>("genisis.Adjustment_PlanSettings", new { plan = Code(plan) });
        return AdjustmentRules.SupplementaryExpiry(dateOfBirth, (int)(settings?.AgeLimit ?? 0), effective, expiry);
    }

    public async Task<AdjustmentResult?> SaveAsync(string membershipNo, AdjustmentRequest request, string userCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await SaveAsync(conn, tx, membershipNo, request, userCode);
        if (result is null) return null;
        await tx.CommitAsync(cancellationToken);
        logger.LogInformation("Adjustment {Type} saved on {MembershipNo} by {User}", Code(request.EndorsementType), membershipNo.Trim(), userCode);
        return result;
    }

    /// <summary>The save inside a caller-owned transaction (used by the tests, which roll back).</summary>
    internal static async Task<AdjustmentResult?> SaveAsync(SqlConnection conn, SqlTransaction tx, string membershipNo, AdjustmentRequest request, string userCode)
    {
        await conn.ExecuteScalarAsync<string>(
            "SELECT MBMNumber FROM dbo.MBM WITH (UPDLOCK, HOLDLOCK) WHERE MBMNumber = @number",
            new { number = membershipNo.Trim() }, tx);
        var current = await LoadAsync(conn, tx, membershipNo);
        if (current is null) return null;
        var dates = await conn.ProcFirstOrDefaultAsync<AdjustmentDateRanges>("genisis.Adjustment_ValidDates", tx: tx) ?? AdjustmentDateRanges.Unbounded;
        if (AdjustmentValidator.Validate(current, request, dates) is { } error) throw new AdjustmentException(error);

        var type = Code(request.EndorsementType);
        var save = new AdjustmentSave(type, request.EndtBordxDate!.Value.Date, request.EndtEffectiveDate!.Value.Date,
            await conn.ProcSingleAsync<DateTime>("genisis.Server_Today", tx: tx), await conn.ProcSingleAsync<DateTime>("genisis.Adjustment_Now", tx: tx), userCode);
        var writer = new AdjustmentWriter(conn, tx, current, request, save);
        var stored = current.Member;

        switch (type)
        {
            case EndorsementTypes.Cancellation:
                await writer.WriteHistoryAsync(MemberStatus.Cancelled, stored, finalExpiry: save.EndorsementEffectiveDate);
                await writer.SetCoveredAdjustmentAsync(MemberStatus.Cancelled, null, save.EndorsementEffectiveDate);
                await writer.PrincipalRefundAsync(stored);
                break;
            case EndorsementTypes.DeleteSupplementary:
                await writer.WriteHistoryAsync(Code(stored.Status), stored);
                await writer.DeleteSupplementaryAsync();
                break;
            case EndorsementTypes.AddSupplementary:
                await writer.WriteHistoryAsync(Code(stored.Status), stored);
                await writer.AddSupplementaryAsync();
                break;
            case EndorsementTypes.CancelSupplementary:
                await writer.CancelSupplementaryAsync();
                break;
            case EndorsementTypes.Suspend:
                await writer.WriteHistoryAsync(MemberStatus.Suspended, stored);
                await writer.SetCoveredAdjustmentAsync(MemberStatus.Suspended, null, null);
                break;
            case EndorsementTypes.Amendment or EndorsementTypes.PlanChange:
                await writer.WriteHistoryAsync(type, request.Member);
                await writer.SetCoveredAdjustmentAsync(type, null, null);
                await writer.SavePrincipalAsync(r => RecalculateAsync(conn, tx, current, r));
                await writer.SaveCoveredAsync();
                await writer.UpdateMcoPendingAsync();
                if (type == EndorsementTypes.PlanChange) await writer.PrincipalRefundAsync(request.Member);
                break;
            case EndorsementTypes.DeletePolicy:
                await writer.WriteHistoryAsync(MemberStatus.Deleted, stored);
                await writer.SetCoveredAdjustmentAsync(MemberStatus.Deleted, null, null);
                break;
        }
        return new AdjustmentResult(AdjustmentMessages.Saved(type), writer.Warnings);
    }

    /// <summary>Relationship shown for the principal on the Fees tab.</summary>
    private static class DataTypesForFees
    {
        public const string Principal = "P";
    }
}
