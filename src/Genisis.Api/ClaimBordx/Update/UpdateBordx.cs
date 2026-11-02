using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Update;

/// <summary>A worksheet of the pending list (a line of <c>lstWorksheetTrackList</c>).</summary>
public record UpdateBordxRow
{
    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Actual amount.</summary>
    public decimal? TotalActual { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal? TotalApproved { get; set; }

    /// <summary>Amount payable to the hospital.</summary>
    public decimal? PayableToHospital { get; set; }

    /// <summary>Amount payable by the member.</summary>
    public decimal? PayableToMember { get; set; }

    /// <summary>Date admitted.</summary>
    public DateTime? DateAdmitted { get; set; }

    /// <summary>Date discharged.</summary>
    public DateTime? DateDischarged { get; set; }

    /// <summary>Patient (the member, or the covered person).</summary>
    public string? PatientName { get; set; }

    /// <summary>Hospital.</summary>
    public string? Hospital { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>ICD code.</summary>
    public string? IcdCode { get; set; }

    /// <summary>Claim status.</summary>
    public string? ClaimStatus { get; set; }

    /// <summary>Bordx number the worksheet is in already (empty when it is in none).</summary>
    public string? BordxNo { get; set; }
}

/// <summary>The worksheets found and the total of their approved amounts.</summary>
public record UpdateBordxSearchResult(List<UpdateBordxRow> Rows, decimal TotalApproved, bool Truncated);

/// <summary>The criteria of the Update Bordx search.</summary>
public class UpdateBordxFilter
{
    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>le: approved up to 2000, gt: above 2000, empty: any.</summary>
    public string? Amount { get; set; }

    /// <summary>"Bordx Status": only the worksheets that are in no bordx (the desktop's box; on by default).</summary>
    public bool UnbordxedOnly { get; set; } = true;

    /// <summary>Date admitted from.</summary>
    public DateTime? DoaFrom { get; set; }

    /// <summary>Date admitted to.</summary>
    public DateTime? DoaTo { get; set; }

    /// <summary>Date discharged from.</summary>
    public DateTime? DodFrom { get; set; }

    /// <summary>Date discharged to.</summary>
    public DateTime? DodTo { get; set; }

    /// <summary>Case number from.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Case number to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }
}

/// <summary>The amount choice has to be the one the drop-down offers.</summary>
public sealed class UpdateBordxFilterValidator : RequestValidator<UpdateBordxFilter>
{
    /// <summary>Declares the rules.</summary>
    public UpdateBordxFilterValidator() => RuleFor(f => f.Amount).Must(a => string.IsNullOrWhiteSpace(a) || a is "le" or "gt").WithMessage("Select an amount up to 2000, above 2000 or none.");
}

/// <summary>A worksheet to put into the bordx.</summary>
public record UpdateBordxClaim(string CaseNo, int Version);

/// <summary>What Update saves: the worksheets, the bordx and the dates and the MNRB receipt.</summary>
public class UpdateBordxRequest
{
    /// <summary>The checked worksheets.</summary>
    public List<UpdateBordxClaim> Claims { get; set; } = [];

    /// <summary>Bordx number (a new one, or one that exists).</summary>
    public string? BordxNo { get; set; }

    /// <summary>Bordx date (of a new bordx).</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>A/C period month the bordx is sent to the payor for.</summary>
    public string? AcMonth { get; set; }

    /// <summary>A/C period year.</summary>
    public string? AcYear { get; set; }

    /// <summary>Date sent to the payor.</summary>
    public DateTime? ToPayorDate { get; set; }

    /// <summary>Date received from the payor.</summary>
    public DateTime? FromPayorDate { get; set; }

    /// <summary>Date sent to MNRB.</summary>
    public DateTime? ToMnrbDate { get; set; }

    /// <summary>Date received from MNRB.</summary>
    public DateTime? FromMnrbDate { get; set; }

    /// <summary>MNRB receipt number (the receipt is written only when it is given).</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>MNRB receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>MNRB cheque amount.</summary>
    public decimal? ChequeAmount { get; set; }

    /// <summary>MNRB cheque number.</summary>
    public string? ChequeNo { get; set; }
}

/// <summary>The bordx that was updated, the worksheets put into it and the total of their approved amounts.</summary>
public record UpdateBordxResult(string BordxNo, int Claims, decimal TotalApproved);

/// <summary>cmdBordx_Click's checks, in the desktop's order (and the receipt's).</summary>
public sealed class UpdateBordxRequestValidator : RequestValidator<UpdateBordxRequest>
{
    /// <summary>Declares the rules.</summary>
    public UpdateBordxRequestValidator()
    {
        RuleFor(r => r.Claims).Must(c => c is { Count: > 0 } && c.All(x => !string.IsNullOrWhiteSpace(x.CaseNo) && x.Version > 0)).WithMessage("Please select at least one record to be updated! ");
        RuleFor(r => r.BordxNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Please Enter Bordx No! ");
        RuleFor(r => r.BordxNo).Must(n => n!.Trim().Length <= 40).WithMessage("Bordx No can be at most 40 characters.");
        RuleFor(r => r.BordxDate).Must(d => d is not null).WithMessage("Please Enter Bordx Date! ");
        RuleFor(r => r).Must(r => !string.IsNullOrWhiteSpace(r.ReceiptNo) || (r.ReceiptDate is null && r.ChequeAmount is null && string.IsNullOrWhiteSpace(r.ChequeNo))).WithMessage("Please Enter Receipt No!");
        RuleFor(r => r.ReceiptNo).Must(n => (n ?? "").Trim().Length <= 30).WithMessage("Receipt No can be at most 30 characters.");
        RuleFor(r => r.ChequeNo).Must(n => (n ?? "").Trim().Length <= 40).WithMessage("Cheque No can be at most 40 characters.");
        RuleFor(r => r.AcMonth).Must(m => string.IsNullOrWhiteSpace(m) || (int.TryParse(m, out var n) && n is >= 1 and <= 12)).WithMessage("A/C Period Mth is 1 to 12.");
        RuleFor(r => r.AcYear).Must(y => string.IsNullOrWhiteSpace(y) || (int.TryParse(y, out var n) && n is >= 1999 and <= 3000)).WithMessage("A/C Period Yr is 1999 to 3000.");
    }
}

/// <summary>Reads the pending worksheets and writes the bordx.</summary>
public interface IUpdateBordxRepository
{
    /// <summary>The worksheets that match (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<UpdateBordxRow>> SearchAsync(IDbSession session, UpdateBordxFilter filter);

    /// <summary>Puts one worksheet into the bordx; returns 0 done, 1 no such worksheet, 2 in a bordx already, with its payor and approved amount.</summary>
    Task<(int Result, string? Payor, decimal Approved)> ApplyAsync(IDbSession session, UpdateBordxClaim claim, string bordxNo);

    /// <summary>Creates or updates the bordx; returns 0 saved, 1 it belongs to another payor.</summary>
    Task<int> SaveBordxAsync(IDbSession session, UpdateBordxRequest request, string payor, string userCode);

    /// <summary>Writes the MNRB receipt of the bordx.</summary>
    Task SaveMnrbReceiptAsync(IDbSession session, UpdateBordxRequest request, UpdateBordxClaim last, decimal checkedAmount, string userCode, DateTime today);
}

/// <inheritdoc />
public sealed class UpdateBordxRepository : IUpdateBordxRepository
{
    private sealed record ApplyRow(int Result, string? PayorCode, decimal Approved);

    private static string? Text(string? value) => WorksheetLimits.Text(value);

    private static string? Upper(string? value) => Text(value)?.ToUpperInvariant();

    /// <inheritdoc />
    public Task<List<UpdateBordxRow>> SearchAsync(IDbSession session, UpdateBordxFilter f) =>
        session.Connection.ProcQueryAsync<UpdateBordxRow>("genisis.UpdateBordx_Search", new
        {
            payor = Upper((f.Payor ?? "").Split(" - ")[0]), amount = f.Amount is "le" or "gt" ? f.Amount : null, unbordxedOnly = f.UnbordxedOnly, doaFrom = WorksheetLimits.Day(f.DoaFrom), doaTo = WorksheetLimits.Day(f.DoaTo),
            dodFrom = WorksheetLimits.Day(f.DodFrom), dodTo = WorksheetLimits.Day(f.DodTo), caseFrom = Text(f.CaseFrom), caseTo = Text(f.CaseTo), bordxFrom = Text(f.BordxFrom), bordxTo = Text(f.BordxTo),
            max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<(int Result, string? Payor, decimal Approved)> ApplyAsync(IDbSession session, UpdateBordxClaim claim, string bordxNo)
    {
        var row = await session.Connection.ProcSingleAsync<ApplyRow>("genisis.UpdateBordx_Apply", new { cas = claim.CaseNo.Trim().ToUpperInvariant(), version = claim.Version, bordxNo }, session.Transaction);
        return (row.Result, row.PayorCode, row.Approved);
    }

    /// <inheritdoc />
    public async Task<int> SaveBordxAsync(IDbSession session, UpdateBordxRequest r, string payor, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.UpdateBordx_Save", new
        {
            bordxNo = r.BordxNo!.Trim(), payor, bordxDate = WorksheetLimits.Day(r.BordxDate), userCode, acMonth = Text(r.AcMonth), acYear = Text(r.AcYear), toPayorDate = WorksheetLimits.Day(r.ToPayorDate),
            fromPayorDate = WorksheetLimits.Day(r.FromPayorDate), toMnrbDate = WorksheetLimits.Day(r.ToMnrbDate), fromMnrbDate = WorksheetLimits.Day(r.FromMnrbDate),
        }, session.Transaction);

    /// <inheritdoc />
    public async Task SaveMnrbReceiptAsync(IDbSession session, UpdateBordxRequest r, UpdateBordxClaim last, decimal checkedAmount, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.UpdateBordx_SaveMnrbReceipt", new
        {
            bordxNo = r.BordxNo!.Trim(), receiptNo = r.ReceiptNo!.Trim().ToUpperInvariant(), receiptDate = WorksheetLimits.Day(r.ReceiptDate), chequeAmount = r.ChequeAmount, chequeNo = Upper(r.ChequeNo),
            caseNo = last.CaseNo.Trim().ToUpperInvariant(), version = last.Version.ToString(), checkedAmount, userCode, today = today.Date,
        }, session.Transaction);
}

/// <summary>Claim Bordx > Update Bordx (legacy <c>frmUpdateWS2Bordx</c>, "Update Bordx").</summary>
public class UpdateBordxService(IDbSessions sessions, IUpdateBordxRepository repository, ILogger<UpdateBordxService> log)
{
    /// <summary>WorksheetTrackingListing: the pending worksheets that match, and the total of their approved amounts.</summary>
    public async Task<UpdateBordxSearchResult> SearchAsync(UpdateBordxFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var capped = WorksheetLimits.Cap(await repository.SearchAsync(session, filter));
        return new UpdateBordxSearchResult(capped.Rows, capped.Rows.Sum(r => r.TotalApproved ?? 0), capped.Truncated);
    }

    /// <summary>
    /// cmdBordx_Click: every checked worksheet goes into the bordx, then the bordx is saved and the MNRB receipt written, in one transaction. A worksheet that is not there is a
    /// <see cref="NotFoundException"/>, one that is in a bordx already a <see cref="ConflictException"/>; worksheets of different payors, or of another payor than the bordx, are refused; nothing is saved then.
    /// </summary>
    public async Task<UpdateBordxResult> UpdateAsync(UpdateBordxRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var bordxNo = request.BordxNo!.Trim();
        var claims = request.Claims.DistinctBy(c => (c.CaseNo.Trim().ToUpperInvariant(), c.Version)).ToList();
        var (total, payors) = (0m, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        foreach (var claim in claims)
        {
            var (result, payor, approved) = await repository.ApplyAsync(session, claim, bordxNo);
            if (result == 1) throw new NotFoundException($"Worksheet {claim.CaseNo}-{claim.Version} not found");
            if (result == 2) throw new ConflictException($"Worksheet {claim.CaseNo}-{claim.Version} is in a bordx already");
            total += approved;
            if (!string.IsNullOrWhiteSpace(payor)) payors.Add(payor.Trim());
        }

        if (payors.Count != 1) throw new BusinessRuleException(payors.Count == 0 ? "The payor of the worksheets was not found" : "All the worksheets of a bordx must be of the same payor");
        if (await repository.SaveBordxAsync(session, request, payors.Single(), userCode) == 1) throw new ConflictException($"Bordx {bordxNo} belongs to another payor");
        if (!string.IsNullOrWhiteSpace(request.ReceiptNo)) await repository.SaveMnrbReceiptAsync(session, request, claims[^1], total, userCode, DateTime.Today);
        await session.CommitAsync();
        log.LogInformation("{Count} worksheet(s) put into bordx {Bordx} by {User}", claims.Count, bordxNo, userCode);
        return new UpdateBordxResult(bordxNo, claims.Count, total);
    }
}
