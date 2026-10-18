using Genisis.Api.Data;
using Genisis.Api.Membership;
using Genisis.Api.Registration;

namespace Genisis.Api.Bordx;

/// <summary>
/// Upload button of Upload Bordx (cmdUpload_Click): picks the processing for the header status and file type, and handles
/// cancellations (GenerateENDtMember) and reinstatements (GenerateReinstateMember). New members go to <see cref="BordxMemberImporter"/>.
/// </summary>
internal sealed class BordxUploader(BordxUploadContext context)
{
    private sealed record PlanRow(string PlanCode, string? LifetimeStatus, string? PayorCode, decimal? AgeLimit);
    private sealed record CancelRow(string MembershipNo, string? Status, string? PolicyNo, string? Ic, string? Name, DateTime? ExpiryDate, string? PlanCode,
        DateTime? EffectiveDate, decimal? Premium, decimal? Mco);
    private sealed record ReinstatePlan(string? RoundUpStatus, string? ProRate, string? PremGenderStatus);
    private sealed record Premium(string? SuppPrmStatus, decimal? PRMAmount, decimal? PRMAmountFemale, decimal? SuppPrmAmt);

    private const string ImportedMessage = "Record Import Successfully...";
    private const string NothingImportedMessage = "No records were imported.";

    private readonly BordxMemberImporter importer = new(context);

    public async Task<UploadResult> RunAsync()
    {
        var fileStatus = context.FileStatus;
        if ((fileStatus == FileStatuses.New && context.Settings.FileType == FileTypes.Endorsement) || fileStatus == FileStatuses.Cancellation)
            await CancellationsAsync();
        else if (fileStatus is FileStatuses.Renewal or FileStatuses.Reinstatement)
            await RenewalsAsync();
        else
            await NewMembersAsync();

        var anything = context.NewMembers + context.Cancelled + context.Reinstated > 0;
        return new UploadResult(anything ? ImportedMessage : NothingImportedMessage, context.Batch, context.NewMembers, context.Supplementaries,
            context.Cancelled, context.Reinstated, context.MembershipNumbers, context.Warnings);
    }

    private void EnsureFilePayorMatches()
    {
        if (!context.Format.IsZurichTakLayout && context.FilePayor != context.Settings.PayorCode)
            throw new BordxException("Payor Selected Is Not Match The File Payor");
    }

    /// <summary>Desktop GenerateMember: every principal / supplementary line of a new-member file.</summary>
    private async Task NewMembersAsync()
    {
        EnsureFilePayorMatches();
        await context.AllocateBatchAsync(context.Format.IsZurichTakLayout ? context.FilePayor : context.Settings.PayorCode);
        var coverNumber = 0;
        foreach (var line in context.Lines.Skip(1))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (line.RecordType is RecordTypes.NewFileHeader or RecordTypes.Trailer) continue;
            coverNumber = line.RecordType == RecordTypes.Principal ? 0 : coverNumber + 1;
            await importer.ImportAsync(line, CoverIds.Format(coverNumber));
        }
    }

    /// <summary>Desktop GenerateRRMember: renewal / reinstatement files are only processed for the ZURICH / TAK layout.</summary>
    private async Task RenewalsAsync()
    {
        EnsureFilePayorMatches();
        if (!context.Format.IsZurichTakLayout)
        {
            context.Warnings.Add("Renewal / reinstatement files (header status R or I) are only processed for the ZURICH / TAK layout; nothing was imported.");
            return;
        }
        await context.AllocateBatchAsync(context.FilePayor);
        var coverNumber = 0;
        foreach (var line in context.Lines.Skip(1))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (line.RecordType == RecordTypes.Trailer) continue;
            if (line.RecordType == RecordTypes.Reinstatement)
            {
                await ReinstateAsync(line);
                continue;
            }
            coverNumber = line.RecordType == RecordTypes.Principal ? 0 : coverNumber + 1;
            await importer.ImportAsync(line, CoverIds.Format(coverNumber));
        }
    }

    private async Task<PlanRow?> FindPayorPlanAsync(string payorPlan)
    {
        var row = await context.Connection.ProcFirstOrDefaultAsync<PlanRow>("genisis.Bordx_Plan", new { plan = (string?)null, payorPlan }, context.Transaction);
        if (row is not null) context.PlanPayor = (row.PayorCode ?? "").Trim();
        return row;
    }

    /// <summary>Desktop GenerateReinstateMember / NewUpdateReinstatement / MemberCallFees.</summary>
    private async Task ReinstateAsync(BordxLine line)
    {
        var effective = line.Date(MemberLayout.EffectiveDate) ?? throw new BordxException($"Line {line.Number}: invalid effective date.");
        var expiry = line.Date(MemberLayout.ExpiryDate) ?? throw new BordxException($"Line {line.Number}: invalid expiry date.");
        var existing = await context.Connection.ProcScalarAsync<string?>("genisis.Bordx_FindForReinstate",
            new { policy = line.Text(MemberLayout.PolicyNo), exp = expiry }, context.Transaction);
        if (existing is null)
        {
            await importer.ImportAsync(line, CoverIds.Principal);
            return;
        }

        var plan = (await FindPayorPlanAsync(line.Text(MemberLayout.PayorPlanCode)))?.PlanCode ?? "";
        var hlt = context.Settings.HealthCode;
        var insured = line.Text(MemberLayout.InsuredTypeFor(context.Format.IsZurichTakLayout));
        var ageCode = "";
        if (line.Date(MemberLayout.DateOfBirth) is { } dateOfBirth)
        {
            var bands = await context.Connection.ProcQueryAsync<LegacyRules.AgeBand>("genisis.Registration_AgeBands", new { plan }, context.Transaction);
            ageCode = LegacyRules.PickAgeBand(bands, LegacyRules.Age(effective, dateOfBirth), plan)?.AgeCode.Trim() ?? "";
        }
        var rates = await context.Connection.ProcFirstOrDefaultAsync<ReinstatePlan>("genisis.Bordx_ReinstatePlan",
            new { plan, hlt, pay = context.PlanPayor, eff = effective }, context.Transaction);
        var premiumRow = await context.Connection.ProcFirstOrDefaultAsync<Premium>("genisis.Registration_Premium",
            new { ins = insured, hlt, plan, ageCode, eff = effective, pay = context.PlanPayor }, context.Transaction);
        var female = YesNo.IsYes(rates?.PremGenderStatus) && line.Text(MemberLayout.Sex) == Sex.Female;
        var premium = FeeRules.ProRateMoney(effective, expiry, LegacyRules.Money((female ? premiumRow?.PRMAmountFemale : premiumRow?.PRMAmount) ?? 0));
        var (gstAmount, gstPercent) = context.Gst(line);
        await context.ExecAsync("genisis.Bordx_Reinstate", new
        {
            number = existing.Trim(), bordx = context.SubmissionDate, mco = line.Amount(MemberLayout.PayorAmount1), premium, reinstateDate = effective,
            user = context.UserCode, today = context.Today, batch = context.Batch, fileName = context.FileName,
            endtRefNo = line.Text(MemberLayout.EndorsementReferenceNo), gstAmount, gstPercent,
        });
        context.Reinstated++;
    }

    /// <summary>Desktop GenerateENDtMember / CancelENDtData / EndtCancelPrincipalData / EndtMBMRefund.</summary>
    private async Task CancellationsAsync()
    {
        foreach (var line in context.Lines.Skip(1))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (line.RecordType != RecordTypes.Trailer) await CancelAsync(line);
        }
    }

    private async Task CancelAsync(BordxLine line)
    {
        var payorPlan = line.Text(MemberLayout.PayorPlanCode);
        await FindPayorPlanAsync(payorPlan);
        var cancelDate = line.Date(MemberLayout.EffectiveDate) ?? throw new BordxException($"Line {line.Number}: invalid cancellation (effective) date.");
        var cancelExpiry = line.Date(MemberLayout.ExpiryDate) ?? throw new BordxException($"Line {line.Number}: invalid expiry date.");
        var row = await context.Connection.ProcFirstOrDefaultAsync<CancelRow>("genisis.Bordx_FindForCancel",
            new { policy = line.Text(MemberLayout.PolicyNo), exp = cancelExpiry, payorPlan = payorPlan == "" ? null : payorPlan }, context.Transaction);
        if (row is null || row.Status?.Trim() != MemberStatus.Active) return;

        await context.ExecAsync("genisis.Bordx_Cancel", new
        {
            number = row.MembershipNo, today = context.Today, user = context.UserCode, bordx = context.SubmissionDate, cancelDate,
            fileName = context.FileName, policy = (row.PolicyNo ?? "").Trim(), ic = (row.Ic ?? "").Trim(), name = (row.Name ?? "").Trim(),
            exp = row.ExpiryDate, plan = (row.PlanCode ?? "").Trim(), batch = context.Batch,
        });

        var effective = row.EffectiveDate ?? cancelDate;
        var expiry = row.ExpiryDate ?? cancelExpiry;
        var premium = FeeRules.VbRound(row.Premium ?? 0);
        var mco = FeeRules.VbRound(row.Mco ?? 0);
        var premiumReduction = LegacyRules.Money(FeeRules.PremiumReduction(expiry, cancelDate, effective, premium));
        // The file carries the payor's MCO refund (negative); the desktop stores it as a positive refund.
        var mcoRefund = -line.Amount(MemberLayout.PayorAmount1);
        var (gstAmount, gstPercent) = context.Gst(line);
        await context.ExecAsync("genisis.Bordx_StageRefund", new
        {
            runKey = context.StagingKey, number = row.MembershipNo, coverId = CoverIds.Principal,
            premiumEffective = LegacyRules.Money(premium - premiumReduction), premiumReduction, mcoEffective = LegacyRules.Money(mco - mcoRefund), mcoRefund,
            pay = context.Settings.PayorCode, bordx = context.SubmissionDate, batch = context.Batch, hlt = context.Settings.HealthCode,
            fileName = context.FileName, endtRefNo = line.Text(MemberLayout.EndorsementReferenceNo), cancelDate, gstAmount, gstPercent,
        });
        context.Cancelled++;
        context.MembershipNumbers.Add(row.MembershipNo);
    }
}
