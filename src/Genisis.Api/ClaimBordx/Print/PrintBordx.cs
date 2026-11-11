using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Print;

/// <summary>A claim of the listing (a line of <c>lstPrintBordx</c>).</summary>
public record PrintBordxRow
{
    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Insurance code.</summary>
    public string? InsCode { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Patient.</summary>
    public string? PatientName { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Hospital.</summary>
    public string? Hospital { get; set; }

    /// <summary>Amount payable to the hospital.</summary>
    public decimal? PayableToHospital { get; set; }

    /// <summary>Bill amount (the actual amount with the GST).</summary>
    public decimal? TotalActual { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal? TotalApproved { get; set; }

    /// <summary>Amount payable by the member (the recovery).</summary>
    public decimal? PayableToMember { get; set; }

    /// <summary>Debit / credit note reference.</summary>
    public string? DebitCreditRefNo { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Whether the bordx has been printed.</summary>
    public bool Printed { get; set; }

    /// <summary>Health type (S Sihat, N the others).</summary>
    public string? HealthType { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>BTBG (Y or N).</summary>
    public string? Btbg { get; set; }

    /// <summary>Paid to: H hospital, M member.</summary>
    public string? PayTo { get; set; }

    /// <summary>Case management fee.</summary>
    public decimal? CaseMgmtFees { get; set; }

    /// <summary>Total management fee.</summary>
    public decimal? TotalMgmtFee { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>The amount the list shows as Bordx Amt: for a BTBG claim what is payable to the hospital, for the others the approved amount.</summary>
    public decimal? BordxAmount => Btbg == "Y" ? PayableToHospital : TotalApproved;
}

/// <summary>A claim as the report prints it (<c>VIEW_PrintClaimBordx_06</c>).</summary>
public record PrintBordxReportRow
{
    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Patient (the member, or the covered person).</summary>
    public string? PatientName { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>IC of the member.</summary>
    public string? MemberIc { get; set; }

    /// <summary>IC of the patient.</summary>
    public string? PatientIc { get; set; }

    /// <summary>Hospital.</summary>
    public string? Hospital { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Insurance code.</summary>
    public string? InsCode { get; set; }

    /// <summary>Health type.</summary>
    public string? HealthType { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Department.</summary>
    public string? Department { get; set; }

    /// <summary>Employee number.</summary>
    public string? EmployeeNo { get; set; }

    /// <summary>Cost centre.</summary>
    public string? CostCenter { get; set; }

    /// <summary>Date admitted.</summary>
    public DateTime? DateAdmitted { get; set; }

    /// <summary>Date discharged.</summary>
    public DateTime? DateDischarged { get; set; }

    /// <summary>Diagnosis 1 code.</summary>
    public string? IcdCode1 { get; set; }

    /// <summary>Diagnosis 1 description.</summary>
    public string? IcdDesc1 { get; set; }

    /// <summary>Diagnosis 2 code.</summary>
    public string? IcdCode2 { get; set; }

    /// <summary>Diagnosis 2 description.</summary>
    public string? IcdDesc2 { get; set; }

    /// <summary>Diagnosis 3 code.</summary>
    public string? IcdCode3 { get; set; }

    /// <summary>Diagnosis 3 description.</summary>
    public string? IcdDesc3 { get; set; }

    /// <summary>Actual amount.</summary>
    public decimal? TotalActual { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal? TotalApproved { get; set; }

    /// <summary>Amount payable to the hospital.</summary>
    public decimal? PayableToHospital { get; set; }

    /// <summary>Amount payable by the member.</summary>
    public decimal? PayableToMember { get; set; }

    /// <summary>Case management fee.</summary>
    public decimal? CaseMgmtFees { get; set; }

    /// <summary>Total management fee.</summary>
    public decimal? TotalMgmtFee { get; set; }

    /// <summary>Amount of the claim in the bordx.</summary>
    public decimal? BordxAmount { get; set; }

    /// <summary>Debit / credit note reference.</summary>
    public string? DebitCreditRefNo { get; set; }

    /// <summary>Paid to: H hospital, M member.</summary>
    public string? PayTo { get; set; }

    /// <summary>BTBG.</summary>
    public string? Btbg { get; set; }

    /// <summary>Whether the bordx has been printed.</summary>
    public bool Printed { get; set; }

    /// <summary>Worksheet remarks.</summary>
    public string? Remarks { get; set; }
}

/// <summary>The claims of the range, the total as the desktop added it and what is printed already.</summary>
public record PrintBordxSearchResult(List<PrintBordxRow> Rows, decimal TotalAmount, int Printed, int NotPrinted, string PrintOption, bool Truncated);

/// <summary>The payor the bordx is for and the product of its plan.</summary>
public record PrintBordxPayor(string Code, string? Name, string? Address1, string? Address2, string? Address3, string? Product);

/// <summary>A line of the summary: one bordx.</summary>
public record PrintBordxSummaryLine(string? BordxNo, DateTime? BordxDate, int Claims, decimal TotalActual, decimal TotalApproved, decimal PayableToHospital, decimal PayableToMember, decimal MgmtFee, decimal BordxAmount);

/// <summary>The report: the payor, the claims of the range and the summary by bordx, and whether it marked the bordx as printed.</summary>
public record PrintBordxDocument(string Kind, string ReportType, string Btbg, DateTime PrintedDate, string BordxFrom, string BordxTo, PrintBordxPayor? Payor, List<PrintBordxReportRow> Rows, List<PrintBordxSummaryLine> Summary, bool Marked);

/// <summary>The bordx range of the search.</summary>
public class PrintBordxFilter
{
    /// <summary>Bordx No from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx No to.</summary>
    public string? BordxTo { get; set; }
}

/// <summary>What Print and Preview ask for: the range, the report and whether it is printed (the detail report is then marked as printed).</summary>
public class PrintBordxRequest : PrintBordxFilter
{
    /// <summary>detail or summary.</summary>
    public string? Kind { get; set; }

    /// <summary>"Report Type": single (one claim to a page) or multiple (many claims).</summary>
    public string? ReportType { get; set; } = "single";

    /// <summary>"BTBG Status": N or Y.</summary>
    public string? Btbg { get; set; } = "N";

    /// <summary>"Print Status": new or reprint.</summary>
    public string? PrintOption { get; set; } = "new";

    /// <summary>True: Print (the detail report marks the bordx as printed); false: Preview.</summary>
    public bool Print { get; set; }
}

/// <summary>cmdSearch_Click's check: both ends of the bordx range.</summary>
public sealed class PrintBordxFilterValidator : RequestValidator<PrintBordxFilter>
{
    /// <summary>Declares the rules.</summary>
    public PrintBordxFilterValidator() => RuleFor(f => f).Must(f => !string.IsNullOrWhiteSpace(f.BordxFrom) && !string.IsNullOrWhiteSpace(f.BordxTo)).WithMessage("Please enter Bordx Reference No");
}

/// <summary>The choices of Print and Preview.</summary>
public sealed class PrintBordxRequestValidator : RequestValidator<PrintBordxRequest>
{
    /// <summary>Declares the rules.</summary>
    public PrintBordxRequestValidator()
    {
        Include(new PrintBordxFilterValidator());
        RuleFor(r => r.Kind).Must(k => k is "detail" or "summary").WithMessage("Select the detail or the summary report.");
        RuleFor(r => r.ReportType).Must(t => t is "single" or "multiple").WithMessage("Select Single Claim or Multiple Claims.");
        RuleFor(r => r.Btbg).Must(b => b is "N" or "Y").WithMessage("BTBG Status is N or Y.");
        RuleFor(r => r.PrintOption).Must(o => o is "new" or "reprint").WithMessage("Select Print New or Reprint.");
    }
}

/// <summary>The listing's arithmetic, as the desktop did it.</summary>
public static class PrintBordxRules
{
    /// <summary>The total the screen showed: for a BTBG claim paid to the hospital what is payable to it, paid to the member what is payable by the member, for the others the total management fee.</summary>
    public static decimal Amount(PrintBordxRow r) => r.Btbg == "Y" && r.PayTo == "H" ? r.PayableToHospital ?? 0 : r.Btbg == "Y" && r.PayTo == "M" ? r.PayableToMember ?? 0 : r.TotalMgmtFee ?? 0;

    /// <summary>The Print Status the screen offers after a search: Reprint when the last claim listed was printed (the desktop set it row by row).</summary>
    public static string SuggestedOption(IReadOnlyList<PrintBordxRow> rows) => rows.Count > 0 && rows[^1].Printed ? "reprint" : "new";
}

/// <summary>Reads the claims of the bordx and marks them as printed.</summary>
public interface IPrintBordxRepository
{
    /// <summary>The claims in the range (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<PrintBordxRow>> SearchAsync(IDbSession session, PrintBordxFilter filter);

    /// <summary>The report's rows for the range (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<PrintBordxReportRow>> ReportAsync(IDbSession session, PrintBordxFilter filter);

    /// <summary>The payor and the product of the plan (null when there is no such payor).</summary>
    Task<PrintBordxPayor?> PayorAsync(IDbSession session, string payor, string? plan, string? health);

    /// <summary>Marks the bordx of the range as printed; returns the bordx, worksheets and invoices changed.</summary>
    Task<(int Bordx, int Claims, int Invoices)> MarkAsync(IDbSession session, PrintBordxFilter filter, string userCode);
}

/// <inheritdoc />
public sealed class PrintBordxRepository : IPrintBordxRepository
{
    private sealed record MarkRow(int Bordx, int Claims, int Invoices);

    private static string From(PrintBordxFilter f) => f.BordxFrom!.Trim().ToUpperInvariant();

    private static string To(PrintBordxFilter f) => f.BordxTo!.Trim().ToUpperInvariant();

    /// <inheritdoc />
    public Task<List<PrintBordxRow>> SearchAsync(IDbSession session, PrintBordxFilter f) =>
        session.Connection.ProcQueryAsync<PrintBordxRow>("genisis.PrintBordx_Search", new { bordxFrom = From(f), bordxTo = To(f), max = WorksheetLimits.FetchRows }, session.Transaction);

    /// <inheritdoc />
    public Task<List<PrintBordxReportRow>> ReportAsync(IDbSession session, PrintBordxFilter f) =>
        session.Connection.ProcQueryAsync<PrintBordxReportRow>("genisis.PrintBordx_Report", new { bordxFrom = From(f), bordxTo = To(f), max = WorksheetLimits.FetchRows }, session.Transaction);

    /// <inheritdoc />
    public async Task<PrintBordxPayor?> PayorAsync(IDbSession session, string payor, string? plan, string? health) =>
        await session.Connection.ProcFirstOrDefaultAsync<PrintBordxPayor>("genisis.PrintBordx_PayorInfo", new { payor, plan, health }, session.Transaction);

    /// <inheritdoc />
    public async Task<(int Bordx, int Claims, int Invoices)> MarkAsync(IDbSession session, PrintBordxFilter f, string userCode)
    {
        var row = await session.Connection.ProcSingleAsync<MarkRow>("genisis.PrintBordx_Mark", new { bordxFrom = From(f), bordxTo = To(f), userCode }, session.Transaction);
        return (row.Bordx, row.Claims, row.Invoices);
    }
}

/// <summary>Claim Bordx > Print Bordx (legacy <c>FrmPrintBordx</c>, "Print Bordx").</summary>
public class PrintBordxService(IDbSessions sessions, IPrintBordxRepository repository, ILogger<PrintBordxService> log)
{
    /// <summary>PrintBordxListing: the claims of the bordx in the range, the total, and how many are printed. Nothing found is a <see cref="NotFoundException"/> ("No record found ").</summary>
    public async Task<PrintBordxSearchResult> SearchAsync(PrintBordxFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return Summarise(await repository.SearchAsync(session, filter));
    }

    private static PrintBordxSearchResult Summarise(List<PrintBordxRow> found)
    {
        var capped = WorksheetLimits.Cap(found);
        if (capped.Rows.Count == 0) throw new NotFoundException("No record found ");
        var printed = capped.Rows.Count(r => r.Printed);
        return new PrintBordxSearchResult(capped.Rows, capped.Rows.Sum(PrintBordxRules.Amount), printed, capped.Rows.Count - printed, PrintBordxRules.SuggestedOption(capped.Rows), capped.Truncated);
    }

    /// <summary>
    /// Print and Preview of the detail and summary reports. Printing the detail report first checks the Print Status against the claims (a report with nothing printed needs "Print New", one with everything
    /// printed "Reprint") and then marks the bordx of the range, their worksheets and invoices as printed, in one transaction; the summary report and the previews write nothing. The report is made of the claims of
    /// the whole range (the desktop reported only the health type of the last claim listed).
    /// </summary>
    public async Task<PrintBordxDocument> PrintAsync(PrintBordxRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var listing = Summarise(await repository.SearchAsync(session, request));
        var mark = request.Print && request.Kind == "detail";
        if (mark && listing.Printed == 0 && listing.NotPrinted != 0 && request.PrintOption == "reprint") throw new BusinessRuleException("Please select 'Print New' option for this report");
        if (mark && listing.Printed != 0 && listing.NotPrinted == 0 && request.PrintOption == "new") throw new BusinessRuleException("This report already printed, please select 'Reprint' ");

        var rows = WorksheetLimits.Cap(await repository.ReportAsync(session, request)).Rows;
        var first = listing.Rows[0];
        var payor = string.IsNullOrWhiteSpace(first.PayorCode) ? null : await repository.PayorAsync(session, first.PayorCode.Trim(), first.PlanCode, first.HealthType);
        var summary = rows.GroupBy(r => (r.BordxNo, r.BordxDate)).Select(g => new PrintBordxSummaryLine(g.Key.BordxNo, g.Key.BordxDate, g.Count(), g.Sum(r => r.TotalActual ?? 0), g.Sum(r => r.TotalApproved ?? 0),
            g.Sum(r => r.PayableToHospital ?? 0), g.Sum(r => r.PayableToMember ?? 0), g.Sum(r => r.TotalMgmtFee ?? 0), g.Sum(r => r.BordxAmount ?? 0))).ToList();
        if (mark)
        {
            await repository.MarkAsync(session, request, userCode);
            await session.CommitAsync();
            log.LogInformation("Bordx {From} to {To} printed (detail) by {User}", request.BordxFrom, request.BordxTo, userCode);
        }

        return new PrintBordxDocument(request.Kind!, request.ReportType!, request.Btbg!, DateTime.Today, request.BordxFrom!.Trim().ToUpperInvariant(), request.BordxTo!.Trim().ToUpperInvariant(), payor, rows, summary, mark);
    }
}
