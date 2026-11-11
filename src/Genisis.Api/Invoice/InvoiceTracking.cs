using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Invoice;

/// <summary>One row of "3 - General Listing(Detail)": an invoice with its case, or a case that has none.</summary>
public record InvoiceTrackingDetailRow
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Worksheet version.</summary>
    public string? Version { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Invoice amount.</summary>
    public decimal? InvoiceAmount { get; set; }

    /// <summary>"DOR": the date the case was registered.</summary>
    public DateTime? RegDate { get; set; }

    /// <summary>Date admitted.</summary>
    public DateTime? Doa { get; set; }

    /// <summary>Date discharged.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Who the invoice is paid to.</summary>
    public string? Payee { get; set; }

    /// <summary>Group company of the member.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>"Inv. Reg": the date the invoice was registered.</summary>
    public DateTime? InvoiceRegistered { get; set; }

    /// <summary>Who entered the invoice.</summary>
    public string? EnteredBy { get; set; }

    /// <summary>Stay type: I, O, F or P.</summary>
    public string? StayType { get; set; }

    /// <summary>Pay type: C cashless, R reimbursement.</summary>
    public string? PayType { get; set; }
}

/// <summary>One row of the four summaries. A report leaves the columns it does not have empty.</summary>
public record InvoiceTrackingSummaryRow
{
    /// <summary>The hospital's code (reports 1, 2 and 4).</summary>
    public string? HospitalCode { get; set; }

    /// <summary>The hospital's name (reports 1, 2 and 4).</summary>
    public string? Hospital { get; set; }

    /// <summary>Invoice number (report 2).</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date (report 2).</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Invoice year (reports 0 and 1).</summary>
    public int? InvYear { get; set; }

    /// <summary>Invoice month (reports 0 and 1).</summary>
    public int? InvMonth { get; set; }

    /// <summary>Worksheets counted.</summary>
    public int TotalCases { get; set; }

    /// <summary>Payable to the hospital, each worksheet once.</summary>
    public decimal? PayableToHospital { get; set; }

    /// <summary>Payable to the member, each worksheet once.</summary>
    public decimal? PayableToMember { get; set; }

    /// <summary>Invoice amount.</summary>
    public decimal? InvoiceAmount { get; set; }
}

/// <summary>What a search gives: the rows of the chosen report, and whether the list was cut off.</summary>
/// <param name="Report">The report number, 0 to 4.</param>
/// <param name="Detail">The rows of report 3 (empty for the others).</param>
/// <param name="Summary">The rows of the other reports (empty for report 3).</param>
/// <param name="Truncated">True when more rows matched than are listed.</param>
public record InvoiceTrackingResult(string Report, List<InvoiceTrackingDetailRow> Detail, List<InvoiceTrackingSummaryRow> Summary, bool Truncated);

/// <summary>The criteria of Invoice Tracking. Every "from"/"to" pair is inclusive and ignored when empty; an "Empty" flag keeps only rows where the column is empty.</summary>
public class InvoiceTrackingFilter
{
    /// <summary>The report: 0 Invoice Month (All Hospitals), 1 Invoice Month (By Hospitals), 2 General Listing, 3 General Listing (Detail), 4 Summary (By Hospitals). Required.</summary>
    public string? Report { get; set; }

    /// <summary>Invoice number from.</summary>
    public string? InvoiceNoFrom { get; set; }

    /// <summary>Invoice number to.</summary>
    public string? InvoiceNoTo { get; set; }

    /// <summary>Invoice number is empty.</summary>
    public bool InvoiceNoEmpty { get; set; }

    /// <summary>Stay type, by the desktop's numbers: 0 in-patient, 1 out-patient, 2 follow-up, 3 pre-hospitalisation, 4 follow-up and pre-hospitalisation.</summary>
    public string? StayType { get; set; }

    /// <summary>Pay type: C cashless, R reimbursement.</summary>
    public string? PayType { get; set; }

    /// <summary>Worksheet version from.</summary>
    public int? VersionFrom { get; set; }

    /// <summary>Worksheet version to.</summary>
    public int? VersionTo { get; set; }

    /// <summary>Worksheet version is empty.</summary>
    public bool VersionEmpty { get; set; }

    /// <summary>Pay to: H hospital, M member.</summary>
    public string? PayTo { get; set; }

    /// <summary>Pay to is empty.</summary>
    public bool PayToEmpty { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Bordx number is empty.</summary>
    public bool BordxEmpty { get; set; }

    /// <summary>Invoice date from.</summary>
    public DateTime? InvoiceDateFrom { get; set; }

    /// <summary>Invoice date to.</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Invoice date is empty.</summary>
    public bool InvoiceDateEmpty { get; set; }

    /// <summary>Invoice registration date from.</summary>
    public DateTime? RegisteredFrom { get; set; }

    /// <summary>Invoice registration date to.</summary>
    public DateTime? RegisteredTo { get; set; }

    /// <summary>Invoice registration date is empty.</summary>
    public bool RegisteredEmpty { get; set; }

    /// <summary>The case's hospital, by code.</summary>
    public string? HospitalCode { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Group company is empty.</summary>
    public bool GroupCompanyEmpty { get; set; }

    /// <summary>Invoice amount from.</summary>
    public decimal? AmountFrom { get; set; }

    /// <summary>Invoice amount to.</summary>
    public decimal? AmountTo { get; set; }

    /// <summary>Invoice amount is empty.</summary>
    public bool AmountEmpty { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Payor is empty.</summary>
    public bool PayorEmpty { get; set; }

    /// <summary>Worksheet (case) number from.</summary>
    public string? WorksheetFrom { get; set; }

    /// <summary>Worksheet (case) number to.</summary>
    public string? WorksheetTo { get; set; }

    /// <summary>Worksheet number is empty.</summary>
    public bool WorksheetEmpty { get; set; }

    /// <summary>Date admitted from.</summary>
    public DateTime? DoaFrom { get; set; }

    /// <summary>Date admitted to.</summary>
    public DateTime? DoaTo { get; set; }

    /// <summary>Date admitted is empty.</summary>
    public bool DoaEmpty { get; set; }

    /// <summary>Date discharged from.</summary>
    public DateTime? DodFrom { get; set; }

    /// <summary>Date discharged to.</summary>
    public DateTime? DodTo { get; set; }

    /// <summary>Date discharged is empty.</summary>
    public bool DodEmpty { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? BordxDateTo { get; set; }

    /// <summary>Bordx date is empty.</summary>
    public bool BordxDateEmpty { get; set; }
}

/// <summary>Values the Invoice Tracking search accepts.</summary>
public static class InvoiceTrackingRules
{
    /// <summary>The five reports of the "Rpt Selection" drop-down.</summary>
    public static readonly string[] Reports = ["0", "1", "2", "3", "4"];

    /// <summary>The message the desktop shows when no report is chosen.</summary>
    public const string NoReport = "Please select a Report.";

    /// <summary>The desktop's stay type numbers 0 to 4.</summary>
    public static readonly string[] StayTypes = ["0", "1", "2", "3", "4"];

    /// <summary>The stay type the procedure takes (<c>PF</c> for follow-up and pre-hospitalisation), or <c>null</c> for none.</summary>
    public static string? StayCode(string? stayType) => stayType?.Trim() switch { "0" => "I", "1" => "O", "2" => "F", "3" => "P", "4" => "PF", _ => null };
}

/// <summary>SearchRecord's check ("Please select a Report.") and the fixed lists of the drop-downs.</summary>
public sealed class InvoiceTrackingFilterValidator : RequestValidator<InvoiceTrackingFilter>
{
    /// <summary>Declares the rules.</summary>
    public InvoiceTrackingFilterValidator()
    {
        RuleFor(f => f.Report).Must(r => InvoiceTrackingRules.Reports.Contains(r?.Trim())).WithMessage(InvoiceTrackingRules.NoReport);
        RuleFor(f => f.StayType).Must(s => string.IsNullOrWhiteSpace(s) || InvoiceTrackingRules.StayTypes.Contains(s.Trim())).WithMessage("Select a valid stay type.");
        RuleFor(f => f.PayType).Must(p => string.IsNullOrWhiteSpace(p) || p.Trim() is "C" or "R").WithMessage("Select a valid pay type.");
        RuleFor(f => f.PayTo).Must(p => string.IsNullOrWhiteSpace(p) || p.Trim() is "H" or "M").WithMessage("Select a valid pay to.");
    }
}

/// <summary>Reads the invoices the Invoice Tracking reports are made of.</summary>
public interface IInvoiceTrackingRepository
{
    /// <summary>Report 3: the invoices and cases that match (at most <paramref name="max"/>).</summary>
    Task<List<InvoiceTrackingDetailRow>> DetailAsync(IDbSession session, InvoiceTrackingFilter filter, int max);

    /// <summary>Reports 0, 1, 2 and 4: the invoices counted per month, hospital or invoice (at most <paramref name="max"/> groups).</summary>
    Task<List<InvoiceTrackingSummaryRow>> SummaryAsync(IDbSession session, InvoiceTrackingFilter filter, int report, int max);

    /// <summary>The group companies that have worksheets for a payor.</summary>
    Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor);

    /// <summary>The hospitals of the drop-down.</summary>
    Task<List<InvoiceOption>> HospitalsAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class InvoiceTrackingRepository : IInvoiceTrackingRepository
{
    private static object Criteria(InvoiceTrackingFilter f, int max) => new
    {
        invNoFrom = WorksheetLimits.Text(f.InvoiceNoFrom), invNoTo = WorksheetLimits.Text(f.InvoiceNoTo), invNoEmpty = f.InvoiceNoEmpty,
        stayType = InvoiceTrackingRules.StayCode(f.StayType), payType = WorksheetLimits.Text(f.PayType),
        versionFrom = f.VersionFrom, versionTo = f.VersionTo, versionEmpty = f.VersionEmpty, payTo = WorksheetLimits.Text(f.PayTo), payToEmpty = f.PayToEmpty,
        bordxFrom = WorksheetLimits.Text(f.BordxFrom), bordxTo = WorksheetLimits.Text(f.BordxTo), bordxEmpty = f.BordxEmpty,
        invDateFrom = f.InvoiceDateFrom?.Date, invDateTo = f.InvoiceDateTo?.Date, invDateEmpty = f.InvoiceDateEmpty,
        regFrom = f.RegisteredFrom?.Date, regTo = f.RegisteredTo?.Date, regEmpty = f.RegisteredEmpty,
        hospital = WorksheetLimits.Text(f.HospitalCode), groupCompany = WorksheetLimits.Text(f.GroupCompany), groupCompanyEmpty = f.GroupCompanyEmpty,
        amountFrom = f.AmountFrom, amountTo = f.AmountTo, amountEmpty = f.AmountEmpty, payor = WorksheetLimits.Text(f.Payor), payorEmpty = f.PayorEmpty,
        wksFrom = WorksheetLimits.Text(f.WorksheetFrom), wksTo = WorksheetLimits.Text(f.WorksheetTo), wksEmpty = f.WorksheetEmpty,
        doaFrom = f.DoaFrom?.Date, doaTo = f.DoaTo?.Date, doaEmpty = f.DoaEmpty, dodFrom = f.DodFrom?.Date, dodTo = f.DodTo?.Date, dodEmpty = f.DodEmpty,
        bordxDateFrom = f.BordxDateFrom?.Date, bordxDateTo = f.BordxDateTo?.Date, bordxDateEmpty = f.BordxDateEmpty, max,
    };

    /// <inheritdoc />
    public Task<List<InvoiceTrackingDetailRow>> DetailAsync(IDbSession session, InvoiceTrackingFilter filter, int max) =>
        session.Connection.ProcQueryAsync<InvoiceTrackingDetailRow>("genisis.InvoiceTracking_Detail", Criteria(filter, max), session.Transaction);

    /// <inheritdoc />
    public async Task<List<InvoiceTrackingSummaryRow>> SummaryAsync(IDbSession session, InvoiceTrackingFilter filter, int report, int max)
    {
        var p = new Dapper.DynamicParameters(Criteria(filter, max));
        p.Add("report", report);
        return await session.Connection.ProcQueryAsync<InvoiceTrackingSummaryRow>("genisis.InvoiceTracking_Summary", p, session.Transaction);
    }

    /// <inheritdoc />
    public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) =>
        session.Connection.ProcQueryAsync<string>("genisis.WorksheetTracking_GroupCompanies", new { payor }, session.Transaction);

    /// <inheritdoc />
    public Task<List<InvoiceOption>> HospitalsAsync(IDbSession session) => session.Connection.ProcQueryAsync<InvoiceOption>("genisis.Invoice_Hospitals", null, session.Transaction);
}

/// <summary>Invoice > Invoice Tracking (legacy FrmInvoiceTracking2).</summary>
public class InvoiceTrackingService(IDbSessions sessions, IInvoiceTrackingRepository repository)
{
    /// <summary>Most rows a report lists (the desktop loaded every row into its list).</summary>
    public const int MaxRows = 5000;

    /// <summary>CmdSearch_Click / SearchRecord: the rows of the chosen report, cut off at <see cref="MaxRows"/>.</summary>
    public async Task<InvoiceTrackingResult> SearchAsync(InvoiceTrackingFilter filter)
    {
        var report = filter.Report!.Trim();
        await using var session = await sessions.OpenAsync();
        if (report == "3")
        {
            var rows = await repository.DetailAsync(session, filter, MaxRows + 1);
            return new InvoiceTrackingResult(report, rows.Take(MaxRows).ToList(), [], rows.Count > MaxRows);
        }
        var summary = await repository.SummaryAsync(session, filter, int.Parse(report), MaxRows + 1);
        return new InvoiceTrackingResult(report, [], summary.Take(MaxRows).ToList(), summary.Count > MaxRows);
    }

    /// <summary>ChkGrpCom_Click: the group companies that have worksheets for a payor.</summary>
    public async Task<List<string>> GroupCompaniesAsync(string? payor)
    {
        if (string.IsNullOrWhiteSpace(payor)) return [];
        await using var session = await sessions.OpenAsync();
        return await repository.GroupCompaniesAsync(session, payor.Trim());
    }

    /// <summary>ComboListing: the hospitals of the drop-down.</summary>
    public async Task<List<InvoiceOption>> HospitalsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.HospitalsAsync(session);
    }
}
