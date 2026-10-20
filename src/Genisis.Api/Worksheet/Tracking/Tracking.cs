using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Tracking;

/// <summary>One worksheet of the tracking list (a row of the FrmWorksheetTracking list).</summary>
public record TrackingRow
{
    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Worksheet status.</summary>
    public string? WksStatus { get; set; }

    /// <summary>Pending status: B bordxed, C cancelled, P pending.</summary>
    public string? Pending { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal? ApprovedAmt { get; set; }

    /// <summary>Payable to the hospital.</summary>
    public decimal? PayableToHospital { get; set; }

    /// <summary>Payable to the member.</summary>
    public decimal? PayableToMember { get; set; }

    /// <summary>Date the case was registered (DOR).</summary>
    public DateTime? Dor { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Doa { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Date the worksheet was closed.</summary>
    public DateTime? DClose { get; set; }

    /// <summary>Date passed on (DPO).</summary>
    public DateTime? Dpo { get; set; }

    /// <summary>Date sent to accounts (DTA/C).</summary>
    public DateTime? DtaC { get; set; }

    /// <summary>The patient: the principal for cover ID 00, otherwise the covered person.</summary>
    public string? Patient { get; set; }

    /// <summary>Hospital name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Days from registration to close; see <see cref="TrackingRules.DaysOpen"/>.</summary>
    public int? DaysOpen { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Bordx amount.</summary>
    public decimal? BordxAmt { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Debit / credit reference number.</summary>
    public string? DebitCreditRefNo { get; set; }

    /// <summary>First diagnosis code.</summary>
    public string? FirstDiag { get; set; }

    /// <summary>Final diagnosis code.</summary>
    public string? FinalDiag { get; set; }

    /// <summary>Reason for admission.</summary>
    public string? AdmissionReason { get; set; }

    /// <summary>The worksheet is already in a bordx and can no longer be updated.</summary>
    public bool Bordxed { get; set; }
}

/// <summary>The criteria of the tracking search: every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class TrackingFilter
{
    /// <summary>"Use Scan": one worksheet (case) number instead of a range.</summary>
    public bool UseScan { get; set; } = true;

    /// <summary>Case number, or the start of the range.</summary>
    public string? WksNoFrom { get; set; }

    /// <summary>End of the case number range (range mode only).</summary>
    public string? WksNoTo { get; set; }

    /// <summary>Claim version from.</summary>
    public int? VersionFrom { get; set; }

    /// <summary>Claim version to.</summary>
    public int? VersionTo { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>I/C number from.</summary>
    public string? IcFrom { get; set; }

    /// <summary>I/C number to.</summary>
    public string? IcTo { get; set; }

    /// <summary>Only worksheets with no I/C number.</summary>
    public bool IcEmpty { get; set; }

    /// <summary>Group company (exact).</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Claimability: A accepted, R rejected.</summary>
    public string? Claimability { get; set; }

    /// <summary>Pending status: B bordxed, C cancelled, P pending.</summary>
    public string? Pending { get; set; }

    /// <summary>Only worksheets with no pending status.</summary>
    public bool PendingEmpty { get; set; }

    /// <summary>Plan code from.</summary>
    public string? PlanFrom { get; set; }

    /// <summary>Plan code to.</summary>
    public string? PlanTo { get; set; }

    /// <summary>Approved amount from.</summary>
    public decimal? ApprovedFrom { get; set; }

    /// <summary>Approved amount to.</summary>
    public decimal? ApprovedTo { get; set; }

    /// <summary>Payable to member from.</summary>
    public decimal? PayMemberFrom { get; set; }

    /// <summary>Payable to member to.</summary>
    public decimal? PayMemberTo { get; set; }

    /// <summary>Payable to hospital from.</summary>
    public decimal? PayHospitalFrom { get; set; }

    /// <summary>Payable to hospital to.</summary>
    public decimal? PayHospitalTo { get; set; }

    /// <summary>Policy number from.</summary>
    public string? PolicyFrom { get; set; }

    /// <summary>Policy number to.</summary>
    public string? PolicyTo { get; set; }

    /// <summary>Member number from.</summary>
    public string? MemberFrom { get; set; }

    /// <summary>Member number to.</summary>
    public string? MemberTo { get; set; }

    /// <summary>Insured type code from.</summary>
    public string? InsFrom { get; set; }

    /// <summary>Insured type code to.</summary>
    public string? InsTo { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Only worksheets with no bordx number.</summary>
    public bool BordxEmpty { get; set; }

    /// <summary>Date passed on (DPO) from.</summary>
    public DateTime? DpoFrom { get; set; }

    /// <summary>Date passed on (DPO) to.</summary>
    public DateTime? DpoTo { get; set; }

    /// <summary>Only worksheets with no DPO.</summary>
    public bool DpoEmpty { get; set; }

    /// <summary>Date of admission from.</summary>
    public DateTime? DoaFrom { get; set; }

    /// <summary>Date of admission to.</summary>
    public DateTime? DoaTo { get; set; }

    /// <summary>Only worksheets with no admission date.</summary>
    public bool DoaEmpty { get; set; }

    /// <summary>Date of discharge from.</summary>
    public DateTime? DodFrom { get; set; }

    /// <summary>Date of discharge to.</summary>
    public DateTime? DodTo { get; set; }

    /// <summary>Only worksheets with no discharge date.</summary>
    public bool DodEmpty { get; set; }

    /// <summary>First diagnosis code from.</summary>
    public string? FirstFrom { get; set; }

    /// <summary>First diagnosis code to.</summary>
    public string? FirstTo { get; set; }

    /// <summary>Only worksheets with no first diagnosis.</summary>
    public bool FirstEmpty { get; set; }

    /// <summary>Final diagnosis code from.</summary>
    public string? FinalFrom { get; set; }

    /// <summary>Final diagnosis code to.</summary>
    public string? FinalTo { get; set; }

    /// <summary>Only worksheets with no final diagnosis.</summary>
    public bool FinalEmpty { get; set; }
}

/// <summary>The ticked worksheets and the pending status to give them (CmdUpdate_Click).</summary>
public class TrackingUpdateRequest
{
    /// <summary>"Date Update": the pending date.</summary>
    public DateTime? Date { get; set; }

    /// <summary>"Pending Status": C cancelled or P pending.</summary>
    public string? Status { get; set; }

    /// <summary>The ticked worksheets.</summary>
    public List<WorksheetPick> Items { get; set; } = [];
}

/// <summary>FrmWorksheetTracking's rules.</summary>
public static class TrackingRules
{
    /// <summary>Pending statuses a search can filter on.</summary>
    public static readonly string[] SearchPending = ["B", "C", "P"];

    /// <summary>Pending statuses an update can set.</summary>
    public static readonly string[] UpdatePending = ["C", "P"];

    /// <summary>Claimabilities a search can filter on.</summary>
    public static readonly string[] Claimabilities = ["A", "R"];

    /// <summary>"No of Days (DOR - DClose)": a worksheet closed the day it was registered counts as one day.</summary>
    public static int? DaysOpen(int? days) => days == 0 ? 1 : days;
}

/// <summary>CmdSearch_Click's check, plus the fixed lists the drop-downs offer.</summary>
public sealed class TrackingFilterValidator : RequestValidator<TrackingFilter>
{
    /// <summary>Declares the rules.</summary>
    public TrackingFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.WksNoFrom)).WithMessage("Please Enter Worksheet No");
        RuleFor(f => f.Claimability).Must(c => string.IsNullOrWhiteSpace(c) || TrackingRules.Claimabilities.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase)).WithMessage("Select a valid claimability.");
        RuleFor(f => f.Pending).Must(p => string.IsNullOrWhiteSpace(p) || TrackingRules.SearchPending.Contains(p.Trim(), StringComparer.OrdinalIgnoreCase)).WithMessage("Select a valid pending status.");
    }
}

/// <summary>CmdUpdate_Click's checks, in the desktop's order.</summary>
public sealed class TrackingUpdateRequestValidator : RequestValidator<TrackingUpdateRequest>
{
    /// <summary>Declares the rules.</summary>
    public TrackingUpdateRequestValidator()
    {
        RuleFor(r => r.Date).NotNull().WithMessage("Please Enter Date");
        RuleFor(r => r.Status).Must(s => TrackingRules.UpdatePending.Contains((s ?? "").Trim(), StringComparer.OrdinalIgnoreCase)).WithMessage("Please Enter Pending Status");
        RuleFor(r => r.Items).Must(items => items.Count > 0).WithMessage("Please select at least one record to be updated!");
    }
}

/// <summary>Reads and updates the worksheets of the tracking list.</summary>
public interface ITrackingRepository
{
    /// <summary>The worksheets that match the filter (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<TrackingRow>> SearchAsync(IDbSession session, TrackingFilter filter);

    /// <summary>The group companies that have worksheets for a payor.</summary>
    Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor);

    /// <summary>Sets the pending status of one worksheet; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> UpdateAsync(IDbSession session, WorksheetPick pick, DateTime date, string status, string userCode);
}

/// <inheritdoc />
public sealed class TrackingRepository : ITrackingRepository
{
    /// <inheritdoc />
    public Task<List<TrackingRow>> SearchAsync(IDbSession session, TrackingFilter f) =>
        session.Connection.ProcQueryAsync<TrackingRow>("genisis.WorksheetTracking_Search", new
        {
            scan = f.UseScan, wksFrom = WorksheetLimits.Text(f.WksNoFrom), wksTo = f.UseScan ? null : WorksheetLimits.Text(f.WksNoTo),
            versionFrom = f.VersionFrom, versionTo = f.VersionTo, payor = WorksheetLimits.Text(f.Payor),
            icFrom = WorksheetLimits.Text(f.IcFrom), icTo = WorksheetLimits.Text(f.IcTo), icEmpty = f.IcEmpty,
            groupCompany = WorksheetLimits.Text(f.GroupCompany), claimability = WorksheetLimits.Text(f.Claimability),
            pending = WorksheetLimits.Text(f.Pending), pendingEmpty = f.PendingEmpty,
            planFrom = WorksheetLimits.Text(f.PlanFrom), planTo = WorksheetLimits.Text(f.PlanTo),
            approvedFrom = f.ApprovedFrom, approvedTo = f.ApprovedTo, payMFrom = f.PayMemberFrom, payMTo = f.PayMemberTo,
            payHFrom = f.PayHospitalFrom, payHTo = f.PayHospitalTo,
            policyFrom = WorksheetLimits.Text(f.PolicyFrom), policyTo = WorksheetLimits.Text(f.PolicyTo),
            memberFrom = WorksheetLimits.Text(f.MemberFrom), memberTo = WorksheetLimits.Text(f.MemberTo),
            insFrom = WorksheetLimits.Text(f.InsFrom), insTo = WorksheetLimits.Text(f.InsTo),
            bordxFrom = WorksheetLimits.Text(f.BordxFrom), bordxTo = WorksheetLimits.Text(f.BordxTo), bordxEmpty = f.BordxEmpty,
            dpoFrom = WorksheetLimits.Day(f.DpoFrom), dpoTo = WorksheetLimits.Day(f.DpoTo), dpoEmpty = f.DpoEmpty,
            doaFrom = WorksheetLimits.Day(f.DoaFrom), doaTo = WorksheetLimits.Day(f.DoaTo), doaEmpty = f.DoaEmpty,
            dodFrom = WorksheetLimits.Day(f.DodFrom), dodTo = WorksheetLimits.Day(f.DodTo), dodEmpty = f.DodEmpty,
            firstFrom = WorksheetLimits.Text(f.FirstFrom), firstTo = WorksheetLimits.Text(f.FirstTo), firstEmpty = f.FirstEmpty,
            finalFrom = WorksheetLimits.Text(f.FinalFrom), finalTo = WorksheetLimits.Text(f.FinalTo), finalEmpty = f.FinalEmpty,
            max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) =>
        session.Connection.ProcQueryAsync<string>("genisis.WorksheetTracking_GroupCompanies", new { payor }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> UpdateAsync(IDbSession session, WorksheetPick pick, DateTime date, string status, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.WorksheetTracking_Update",
            new { cas = pick.CaseNo.Trim(), version = pick.ClaimVersion, date = date.Date, status, userCode }, session.Transaction);
}

/// <summary>Worksheet > Worksheet Tracking (legacy FrmWorksheetTracking).</summary>
public class TrackingService(IDbSessions sessions, ITrackingRepository repository, ILogger<TrackingService> log)
{
    /// <summary>CmdSearch_Click: the worksheets that match the criteria.</summary>
    public async Task<SearchResult<TrackingRow>> SearchAsync(TrackingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter);
        return WorksheetLimits.Cap(rows.Select(r => r with { DaysOpen = TrackingRules.DaysOpen(r.DaysOpen) }).ToList());
    }

    /// <summary>ChkGrpCom_Click: the group companies of a payor.</summary>
    public async Task<List<string>> GroupCompaniesAsync(string payor)
    {
        if (string.IsNullOrWhiteSpace(payor)) return [];
        await using var session = await sessions.OpenAsync();
        return await repository.GroupCompaniesAsync(session, payor.Trim());
    }

    /// <summary>
    /// CmdUpdate_Click: gives the ticked worksheets the pending status in one transaction. Throws <see cref="WorksheetException"/>
    /// when a worksheet is gone or already in a bordx; nothing is saved then. Returns how many worksheets were updated.
    /// </summary>
    public async Task<int> UpdateAsync(TrackingUpdateRequest request, string userCode)
    {
        var picks = request.Items.DistinctBy(p => (p.CaseNo.Trim(), p.ClaimVersion)).ToList();
        var status = request.Status!.Trim().ToUpperInvariant();
        await using var session = await sessions.BeginAsync();
        foreach (var pick in picks)
        {
            var result = await repository.UpdateAsync(session, pick, request.Date!.Value, status, userCode);
            if (result == StoredProcedures.NotFound) throw new WorksheetException($"Worksheet {pick.WksNo} was not found. Search again.");
            if (result == StoredProcedures.Conflict) throw new WorksheetException($"{pick.WksNo}: This claim has been bordxed!");
        }
        await session.CommitAsync();
        log.LogInformation("Worksheet pending status {Status} set by {User} on {Count} worksheet(s)", status, userCode, picks.Count);
        return picks.Count;
    }
}
