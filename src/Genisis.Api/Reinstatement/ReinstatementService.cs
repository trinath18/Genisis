using System.Data;
using System.Globalization;
using Genisis.Api.Adjustment;
using Genisis.Api.Data;
using Genisis.Api.Membership;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Reinstatement;

public interface IReinstatementService
{
    Task<ReinstatementView?> LoadAsync(string membershipNo, CancellationToken cancellationToken);
    Task<ReinstatementResult?> ReinstateAsync(string membershipNo, ReinstatementRequest request, string userCode, CancellationToken cancellationToken);
}

/// <summary>Membership &gt; Member Reinstatement (legacy frmMBMReintstatement: cmdSearch_Click, cmdReintstate_Click, MemberCallFees).</summary>
public sealed class ReinstatementService(DbConnections db) : IReinstatementService
{
    private const int InsuredTypeLength = 1;
    private const int CodeLength = 2;

    public async Task<ReinstatementView?> LoadAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var member = await conn.ProcFirstOrDefaultAsync<ReinstatementMember>("genisis.Reinstatement_Load", new { number = membershipNo.Trim() });
        if (member is null) return null;
        var covered = await conn.ProcQueryAsync<ReinstatementCovered>("genisis.Reinstatement_Covered", new { number = member.MembershipNo });
        return new ReinstatementView(member, covered.Count);
    }

    public async Task<ReinstatementResult?> ReinstateAsync(string membershipNo, ReinstatementRequest request, string userCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await ReinstateAsync(conn, tx, membershipNo, request, userCode);
        if (result is null) await tx.RollbackAsync(cancellationToken);
        else await tx.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>Runs inside the caller's transaction (the tests roll it back).</summary>
    internal static async Task<ReinstatementResult?> ReinstateAsync(SqlConnection conn, SqlTransaction tx, string membershipNo, ReinstatementRequest request, string userCode)
    {
        var member = await conn.ProcFirstOrDefaultAsync<ReinstatementMember>("genisis.Reinstatement_Load", new { number = membershipNo.Trim() }, tx);
        if (member is null) return null;
        if (ReinstatementValidator.Blocked(member) is { } blocked) throw new ReinstatementException(blocked);

        var lapsed = request.LapsedDate!.Value.Date;
        var expiry = request.ExpiryDate!.Value.Date;
        var endtBordx = request.EndtBordxDate!.Value.Date;
        var reinstate = request.ReinstateDate!.Value.Date;
        var batch = request.BatchNo!.Trim();
        var today = await conn.ProcSingleAsync<DateTime>("genisis.Server_Today", tx: tx);
        var number = member.MembershipNo;
        var calculator = new FeeCalculator(conn, tx, member, lapsed, expiry);
        var covers = new List<ReinstatedCover>();

        async Task InsertAsync(string coverId, ReinstatementFeeInput person)
        {
            var (mco, premium) = await calculator.NextAsync(person);
            await conn.ProcExecAsync("genisis.Reinstatement_Insert", new
            {
                number, coverId, endtBordx, batch, mco, premium, lapsed, reinstate, user = userCode, today,
            }, tx);
            covers.Add(new ReinstatedCover(coverId, mco, premium));
        }

        await InsertAsync(CoverIds.Principal, new ReinstatementFeeInput(member.AdultChild, member.AgeCode, member.Sex));
        await conn.ProcExecAsync("genisis.Adjustment_SetStatus", new { number, status = MemberStatus.Active, finalExpiry = expiry }, tx);

        if (InsuredTypes.IsFamily(member.InsuredType))
        {
            foreach (var person in await conn.ProcQueryAsync<ReinstatementCovered>("genisis.Reinstatement_Covered", new { number }, tx))
                await InsertAsync(person.CoverId, new ReinstatementFeeInput(person.AdultChild, person.AgeCode, person.Sex));
            await conn.ProcExecAsync("genisis.Adjustment_SetCoveredStatus", new { number, status = MemberStatus.Active }, tx);
        }

        await conn.ProcExecAsync("genisis.Adjustment_InsertAmendHistory", new
        {
            number, policy = member.PolicyNo, ic = member.IcBcPp, name = member.Name, expiry, plan = member.PlanCode,
            type = ReinstatementCodes.AmendType, endtBordx, endtEff = reinstate, user = userCode, today,
            batch = int.Parse(batch, CultureInfo.InvariantCulture),
        }, tx);

        return new ReinstatementResult(ReinstatementMessages.Reinstated, covers, calculator.Warnings);
    }

    /// <summary>
    /// The desktop MemberCallFees keeps its last MCO fee and premium in form-level variables: a person without an adult/child code
    /// keeps the previous person's MCO fee, and nothing is recalculated without a plan and insured type. This carries the same values.
    /// </summary>
    private sealed class FeeCalculator(SqlConnection conn, SqlTransaction tx, ReinstatementMember member, DateTime lapsed, DateTime expiry)
    {
        private decimal mco;
        private decimal premium;
        public List<string> Warnings { get; } = [];

        public async Task<(decimal Mco, decimal Premium)> NextAsync(ReinstatementFeeInput person)
        {
            var plan = member.PlanCode?.Trim() ?? "";
            var ins = ReinstatementRules.Prefix(member.InsuredType, InsuredTypeLength);
            if (plan == "" || ins == "") return (mco, premium);
            var hlt = member.HealthCode?.Trim() ?? "";
            var pay = member.PayorCode?.Trim() ?? "";

            var rates = await conn.ProcFirstOrDefaultAsync<PlanRates>("genisis.Adjustment_PlanRates", new { plan, hlt, pay, eff = lapsed }, tx);
            if (!string.IsNullOrWhiteSpace(person.AdultChild))
            {
                var mcoRow = await conn.ProcFirstOrDefaultAsync<McoRates>("genisis.Registration_Mco", new { ins, hlt, plan, eff = lapsed }, tx);
                var annual = ReinstatementRules.McoRate(person.AdultChild, mcoRow?.MCOAmountAdult, mcoRow?.MCOAmountChild);
                mco = ReinstatementRules.McoFee(annual, YesNo.IsNotNo(rates?.ProRate), lapsed, expiry);
            }
            mco = ReinstatementRules.RoundMcoFee(mco, YesNo.IsNotNo(rates?.RoundUpStatus));

            var row = await conn.ProcFirstOrDefaultAsync<PremiumRates>("genisis.Registration_Premium", new
            {
                ins, hlt, plan, ageCode = ReinstatementRules.Prefix(person.AgeCode, CodeLength), eff = lapsed,
                pay = ReinstatementRules.Prefix(pay, CodeLength),
            }, tx);
            if (row is null && !Warnings.Contains(ReinstatementMessages.AgeOutOfPremiumRange)) Warnings.Add(ReinstatementMessages.AgeOutOfPremiumRange);
            var annualPremium = ReinstatementRules.PremiumRate(YesNo.IsYes(rates?.PremGenderStatus), person.Sex, row?.PRMAmount, row?.PRMAmountFemale);
            premium = ReinstatementRules.Premium(annualPremium, lapsed, expiry);
            return (mco, premium);
        }
    }
}
