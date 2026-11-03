using System.Globalization;
using FluentValidation;
using Genisis.Api.ClaimBordx.Print;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.PaymentAdvice;

/// <summary>The criteria of the Hospital Report search (the claims of a range of bordx).</summary>
public class HospitalReportFilter
{
    /// <summary>Bordx No from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx No to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Hospital (its name).</summary>
    public string? Hospital { get; set; }

    /// <summary>Date of admission from.</summary>
    public DateTime? DoaFrom { get; set; }

    /// <summary>Date of admission to.</summary>
    public DateTime? DoaTo { get; set; }

    /// <summary>Date of discharge from.</summary>
    public DateTime? DodFrom { get; set; }

    /// <summary>Date of discharge to.</summary>
    public DateTime? DodTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? BordxDateTo { get; set; }
}

/// <summary>The claims found and the total as the list adds it.</summary>
public record HospitalReportSearchResult(List<PrintBordxRow> Rows, decimal TotalAmount, bool Truncated);

/// <summary>A block of a report file: a title, the headings and the lines (every value is text as the report shows it).</summary>
public record ReportSection(string Title, List<string> Header, List<List<string>> Rows);

/// <summary>The report of a bordx or of a range of bordx.</summary>
public record HospitalReportDocument(string Name, DateTime Date, List<ReportSection> Sections);

/// <summary>A hospital's payments (a line of the summary).</summary>
public record HospitalPaymentRow(string? HospitalCode, string? Hospital, int TotalCases, decimal? TotalActual, decimal? PayableToHospital);

/// <summary>A claim paid to a hospital.</summary>
public record HospitalClaimRow(string? HospitalCode, string? Hospital, string? Payee, string? EmployeeNo, string? MemberName, DateTime? DischargeDate, string? InvoiceNo, string? CaseNo, int ClaimVersion, decimal? TotalActual, decimal? PayableToHospital);

/// <summary>An amount paid to a member.</summary>
public record MemberPaymentRow(string? EmployeeNo, string? MemberName, string? Email, string? IcNo, string? CostCenter, string? CaseNo, int ClaimVersion, decimal? PayableToMember, string? Tax, decimal? BordxAmount);

/// <summary>An amount recovered from a member.</summary>
public record RecoveryRow(string? EmployeeNo, string? MemberName, string? PatientName, string? CaseNo, int ClaimVersion, string? Hospital, DateTime? DischargeDate, decimal? PayableToMember, decimal? BtbgAmount, string? CostCenter);

/// <summary>A cost centre's cases and amount.</summary>
public record CostCentreRow(int TotalCases, string? CostCenter, decimal? TotalAmount);

/// <summary>A group category's amount.</summary>
public record GroupRow(string? GroupCategory, decimal? TotalAmount);

/// <summary>An employee's amount in a cost centre (or group).</summary>
public record ExpenseRow(string? CostCenter, string? EmployeeNo, string? MemberName, string? IcNo, decimal? TotalAmount);

/// <summary>cmdSearch_Click's check: both ends of the bordx range.</summary>
public sealed class HospitalReportFilterValidator : RequestValidator<HospitalReportFilter>
{
    /// <summary>Declares the rules.</summary>
    public HospitalReportFilterValidator() =>
        RuleFor(f => f).Must(f => !string.IsNullOrWhiteSpace(f.BordxFrom) && !string.IsNullOrWhiteSpace(f.BordxTo)).WithMessage("Please enter Bordx Reference No");
}

/// <summary>The sets of the payment advice, in the order the desktop generated them.</summary>
public static class HospitalReportSections
{
    /// <summary>The hospital payment sets: key, title.</summary>
    public static readonly (string Key, string Title)[] Hospitals =
    [
        ("panel", "Payment Advice - Panel Hospitals"), ("maswing", "Payment Advice - MAS Wing"), ("sabah", "Payment Advice - AWUS (Sabah)"), ("sarawak", "Payment Advice - Sarawak"),
    ];
}

/// <summary>How a report shows its values.</summary>
public static class ReportFormat
{
    /// <summary>"#,##0.00".</summary>
    public static string Money(decimal? value) => (value ?? 0m).ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>"dd/mm/yyyy".</summary>
    public static string Day(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";

    /// <summary>The tax of a claim as the desktop's Format showed it: a number as money, anything else (a dash) as it is.</summary>
    public static string Tax(string? tax) => decimal.TryParse((tax ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? Money(n) : (tax ?? "").Trim();

    /// <summary>"000": the number of a hospital in the summary.</summary>
    public static string Sequence(int n) => n.ToString("000", CultureInfo.InvariantCulture);
}

/// <summary>Reads the sets of the report.</summary>
public interface IHospitalReportRepository
{
    /// <summary>The claims of the bordx in the range (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<PrintBordxRow>> SearchAsync(IDbSession session, HospitalReportFilter filter);

    /// <summary>The payments to each hospital of a bordx in a section (panel, maswing, sabah, sarawak).</summary>
    Task<List<HospitalPaymentRow>> HospitalsAsync(IDbSession session, string bordxNo, string section);

    /// <summary>The claims behind them.</summary>
    Task<List<HospitalClaimRow>> ClaimsAsync(IDbSession session, string bordxNo, string section);

    /// <summary>The amounts paid to the members of a bordx.</summary>
    Task<List<MemberPaymentRow>> MembersAsync(IDbSession session, string bordxNo);

    /// <summary>The amounts recovered from the members of a bordx.</summary>
    Task<List<RecoveryRow>> RecoveryAsync(IDbSession session, string bordxNo);

    /// <summary>The cost centres of the bordx in the range.</summary>
    Task<List<CostCentreRow>> CostCentresAsync(IDbSession session, HospitalReportFilter filter);

    /// <summary>The group categories of the bordx in the range.</summary>
    Task<List<GroupRow>> GroupsAsync(IDbSession session, HospitalReportFilter filter);

    /// <summary>The amount by employee (the MAS Wing groups when <paramref name="maswing"/>).</summary>
    Task<List<ExpenseRow>> ExpensesAsync(IDbSession session, HospitalReportFilter filter, bool maswing);
}

/// <inheritdoc />
public sealed class HospitalReportRepository : IHospitalReportRepository
{
    private static string? Text(string? value) => WorksheetLimits.Text(value);

    private static object Range(HospitalReportFilter f) => new
    {
        bordxFrom = Text(f.BordxFrom)?.ToUpperInvariant(), bordxTo = Text(f.BordxTo)?.ToUpperInvariant(), bordxDateFrom = WorksheetLimits.Day(f.BordxDateFrom), bordxDateTo = WorksheetLimits.Day(f.BordxDateTo),
    };

    /// <inheritdoc />
    public Task<List<PrintBordxRow>> SearchAsync(IDbSession session, HospitalReportFilter f) =>
        session.Connection.ProcQueryAsync<PrintBordxRow>("genisis.HospitalReport_Search", new
        {
            bordxFrom = Text(f.BordxFrom)!.ToUpperInvariant(), bordxTo = Text(f.BordxTo)!.ToUpperInvariant(), hospital = Text(f.Hospital), doaFrom = WorksheetLimits.Day(f.DoaFrom), doaTo = WorksheetLimits.Day(f.DoaTo),
            dodFrom = WorksheetLimits.Day(f.DodFrom), dodTo = WorksheetLimits.Day(f.DodTo), bordxDateFrom = WorksheetLimits.Day(f.BordxDateFrom), bordxDateTo = WorksheetLimits.Day(f.BordxDateTo), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<HospitalPaymentRow>> HospitalsAsync(IDbSession session, string bordxNo, string section) =>
        session.Connection.ProcQueryAsync<HospitalPaymentRow>("genisis.HospitalReport_Hospitals", new { bordxNo, section }, session.Transaction);

    /// <inheritdoc />
    public Task<List<HospitalClaimRow>> ClaimsAsync(IDbSession session, string bordxNo, string section) =>
        session.Connection.ProcQueryAsync<HospitalClaimRow>("genisis.HospitalReport_Claims", new { bordxNo, section }, session.Transaction);

    /// <inheritdoc />
    public Task<List<MemberPaymentRow>> MembersAsync(IDbSession session, string bordxNo) =>
        session.Connection.ProcQueryAsync<MemberPaymentRow>("genisis.HospitalReport_Members", new { bordxNo }, session.Transaction);

    /// <inheritdoc />
    public Task<List<RecoveryRow>> RecoveryAsync(IDbSession session, string bordxNo) =>
        session.Connection.ProcQueryAsync<RecoveryRow>("genisis.HospitalReport_Recovery", new { bordxNo }, session.Transaction);

    /// <inheritdoc />
    public Task<List<CostCentreRow>> CostCentresAsync(IDbSession session, HospitalReportFilter f) => session.Connection.ProcQueryAsync<CostCentreRow>("genisis.HospitalReport_CostCentres", Range(f), session.Transaction);

    /// <inheritdoc />
    public Task<List<GroupRow>> GroupsAsync(IDbSession session, HospitalReportFilter f) => session.Connection.ProcQueryAsync<GroupRow>("genisis.HospitalReport_Groups", Range(f), session.Transaction);

    /// <inheritdoc />
    public Task<List<ExpenseRow>> ExpensesAsync(IDbSession session, HospitalReportFilter f, bool maswing) =>
        session.Connection.ProcQueryAsync<ExpenseRow>("genisis.HospitalReport_Expenses", new { bordxFrom = Text(f.BordxFrom)?.ToUpperInvariant(), bordxTo = Text(f.BordxTo)?.ToUpperInvariant(), bordxDateFrom = WorksheetLimits.Day(f.BordxDateFrom), bordxDateTo = WorksheetLimits.Day(f.BordxDateTo), maswing }, session.Transaction);
}

/// <summary>Claim Bordx > Payment Advice > Hospital Report (legacy <c>frmPayAdviceHosRpt</c>, "Payment Advice- By Hospital").</summary>
public class HospitalReportService(IDbSessions sessions, IHospitalReportRepository repository)
{
    /// <summary>PrintBordxListing: the claims of the bordx in the range. Nothing found is a <see cref="NotFoundException"/> ("No record found ").</summary>
    public async Task<HospitalReportSearchResult> SearchAsync(HospitalReportFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var capped = WorksheetLimits.Cap(await repository.SearchAsync(session, filter));
        if (capped.Rows.Count == 0) throw new NotFoundException("No record found ");
        return new HospitalReportSearchResult(capped.Rows, capped.Rows.Sum(PrintBordxRules.Amount), capped.Truncated);
    }

    private static string MemberPaid(decimal? actual, decimal? payable) => ReportFormat.Money((actual ?? 0) - (payable ?? 0));

    private static ReportSection HospitalSummary(string title, List<HospitalPaymentRow> rows)
    {
        var lines = rows.Select((r, i) => new List<string> { ReportFormat.Sequence(i + 1), r.Hospital ?? "", r.TotalCases.ToString(CultureInfo.InvariantCulture), ReportFormat.Money(r.TotalActual), MemberPaid(r.TotalActual, r.PayableToHospital), ReportFormat.Money(r.PayableToHospital) }).ToList();
        lines.Add(["", "Total", rows.Sum(r => r.TotalCases).ToString(CultureInfo.InvariantCulture), ReportFormat.Money(rows.Sum(r => r.TotalActual ?? 0)), ReportFormat.Money(rows.Sum(r => (r.TotalActual ?? 0) - (r.PayableToHospital ?? 0))), ReportFormat.Money(rows.Sum(r => r.PayableToHospital ?? 0))]);
        return new ReportSection(title, ["No", "Hospital", "Cases", "Actual Amt", "Paid by Member", "Payable to Hospital"], lines);
    }

    private static ReportSection HospitalClaims(string title, List<HospitalClaimRow> rows)
    {
        var lines = new List<List<string>>();
        foreach (var hospital in rows.GroupBy(r => (r.HospitalCode, r.Hospital)))
        {
            foreach (var r in hospital) lines.Add([r.Hospital ?? "", r.EmployeeNo ?? "", r.MemberName ?? "", ReportFormat.Day(r.DischargeDate), r.InvoiceNo ?? "", $"{r.CaseNo}-{r.ClaimVersion}", ReportFormat.Money(r.TotalActual), MemberPaid(r.TotalActual, r.PayableToHospital), ReportFormat.Money(r.PayableToHospital)]);
            lines.Add([hospital.Key.Hospital ?? "", "", "", "", "", "Sub Total", ReportFormat.Money(hospital.Sum(r => r.TotalActual ?? 0)), ReportFormat.Money(hospital.Sum(r => (r.TotalActual ?? 0) - (r.PayableToHospital ?? 0))), ReportFormat.Money(hospital.Sum(r => r.PayableToHospital ?? 0))]);
            lines.Add([hospital.Key.Hospital ?? "", "Payable to", hospital.First().Payee ?? "", "", "", "", "", "", ""]);
        }

        return new ReportSection(title, ["Hospital", "Employee No", "Name", "Discharge Date", "Invoice No", "Claim No", "Actual Amt", "Paid by Member", "Payable to Hospital"], lines);
    }

    /// <summary>
    /// GeneratePaymentSum and the generators it calls, for the bordx selected in the list: the payments to the panel hospitals, to the MAS Wing group, to the Sabah (AWUS) and to the Sarawak hospitals (a summary by
    /// hospital and the claims behind it, each set only when it has claims), then the payments to the members, the recoveries, the staff claims (the tax differs from the amount payable to the member) and the daily
    /// allowances (it equals it). The desktop filled Excel and Word templates; the sets are the sections of one file.
    /// </summary>
    public async Task<HospitalReportDocument> PaymentAdviceAsync(string? bordxNo)
    {
        if (string.IsNullOrWhiteSpace(bordxNo)) throw new BusinessRuleException("Please Select Bordx");
        var no = bordxNo.Trim().ToUpperInvariant();
        await using var session = await sessions.OpenAsync();
        var head = (await repository.SearchAsync(session, new HospitalReportFilter { BordxFrom = no, BordxTo = no })).FirstOrDefault() ?? throw new NotFoundException($"Bordx {no} not found");
        var sections = new List<ReportSection>();
        foreach (var (key, title) in HospitalReportSections.Hospitals)
        {
            var hospitals = await repository.HospitalsAsync(session, no, key);
            if (hospitals.Count == 0) continue;
            sections.Add(HospitalSummary(title, hospitals));
            sections.Add(HospitalClaims($"{title} - claims", await repository.ClaimsAsync(session, no, key)));
        }

        var members = await repository.MembersAsync(session, no);
        if (members.Count > 0)
            sections.Add(new ReportSection("Payment to Members", ["Employee No", "Name", "Cost Centre", "IC No", "Claim No", "Payable to Member", "Tax", "Bordx Amt"],
                members.Select(m => new List<string> { m.EmployeeNo ?? "", m.MemberName ?? "", m.CostCenter ?? "", m.IcNo ?? "", $"{m.CaseNo}-{m.ClaimVersion}", ReportFormat.Money(m.PayableToMember), ReportFormat.Tax(m.Tax), ReportFormat.Money(m.BordxAmount) }).ToList()));
        var recovery = await repository.RecoveryAsync(session, no);
        if (recovery.Count > 0)
            sections.Add(new ReportSection("Recovery", ["Employee No", "Name", "Patient", "Claim No", "Hospital", "Discharge Date", "Payable to Member", "BTBG Amt", "Cost Centre"],
                recovery.Select(r => new List<string> { r.EmployeeNo ?? "", r.MemberName ?? "", r.PatientName ?? "", $"{r.CaseNo}-{r.ClaimVersion}", r.Hospital ?? "", ReportFormat.Day(r.DischargeDate), ReportFormat.Money(r.PayableToMember), ReportFormat.Money(r.BtbgAmount), r.CostCenter ?? "" }).ToList()));
        var staff = members.Where(m => ReportFormat.Tax(m.Tax) != ReportFormat.Money(m.PayableToMember)).ToList();
        if (staff.Count > 0)
            sections.Add(new ReportSection("Staff Claim", ["Employee No", "Name", "Email", "Amount", "Claim No", "Date"],
                staff.Select(m => new List<string> { m.EmployeeNo ?? "", m.MemberName ?? "", m.Email ?? "", ReportFormat.Money(m.PayableToMember), $"{m.CaseNo}-{m.ClaimVersion}", ReportFormat.Day(head.BordxDate) }).ToList()));
        var daily = members.Where(m => ReportFormat.Tax(m.Tax) == ReportFormat.Money(m.PayableToMember)).ToList();
        if (daily.Count > 0)
            sections.Add(new ReportSection("Daily Allowance", ["Employee No", "Name", "Email", "Amount", "Claim No", "Date"],
                daily.Select(m => new List<string> { m.EmployeeNo ?? "", m.MemberName ?? "", m.Email ?? "", ReportFormat.Tax(m.Tax), $"{m.CaseNo}{m.ClaimVersion}", ReportFormat.Day(head.BordxDate) }).ToList()));
        return new HospitalReportDocument(no, head.BordxDate ?? DateTime.Today, sections);
    }

    private static ReportSection Expenses(string title, string firstHeading, List<ExpenseRow> rows, ref decimal grand)
    {
        var lines = new List<List<string>>();
        foreach (var employee in rows.GroupBy(r => r.EmployeeNo))
        {
            foreach (var r in employee) lines.Add([r.CostCenter ?? "", r.EmployeeNo ?? "", r.MemberName ?? "", r.IcNo ?? "", ReportFormat.Money(r.TotalAmount)]);
            var sub = employee.Sum(r => r.TotalAmount ?? 0);
            grand += sub;
            lines.Add(["", "", "", "Sub Total", ReportFormat.Money(sub)]);
        }

        return new ReportSection(title, [firstHeading, "Employee No", "Name", "IC No", "Amount"], lines);
    }

    /// <summary>
    /// CC Report (GenerateCCReports): the cost allocation of the bordx in the range as MAS uploads it: a line per cost centre (account 10HEQ2510 / 95010000, MYR, one EA per case) and per group category (account
    /// 51702 00), then the expenses by employee in cost centre order and those of the MAS Wing groups, with their totals and the grand total. The account codes and texts are the ones the desktop wrote.
    /// </summary>
    public async Task<HospitalReportDocument> CostCentreReportAsync(HospitalReportFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var today = DateTime.Today;
        var period = $"{today.Month}{today.Year}";
        var centres = await repository.CostCentresAsync(session, filter);
        var groups = await repository.GroupsAsync(session, filter);
        var others = await repository.ExpensesAsync(session, filter, false);
        var wing = await repository.ExpensesAsync(session, filter, true);
        if (centres.Count == 0 && groups.Count == 0 && others.Count == 0 && wing.Count == 0) throw new NotFoundException("No record found ");
        var sections = new List<ReportSection>
        {
            new("Cost Allocation", ["Account", "Cost Account", "Amount", "Currency", "Cost Centre", "Cases", "Unit", "Text"],
                centres.Select(c => new List<string> { "10HEQ2510", "95010000", ReportFormat.Money(c.TotalAmount), "MYR", c.CostCenter ?? "", c.TotalCases.ToString(CultureInfo.InvariantCulture), "EA", $"Cost Allocation-GHSS {period}" }).ToList()),
            new("Group Allocation", ["Account", "Sub Account", "Group", "Amount", "Text", "Key", "Document", "Text 2"],
                groups.Select(g => new List<string> { "51702", "00", Mid(g.GroupCategory), ReportFormat.Money(g.TotalAmount), $"JXXXXXX INS {period}", "5170200" + Mid(g.GroupCategory), "JXXXXXX", $"INS {period}" }).ToList()),
        };
        decimal grand = 0;
        sections.Add(Expenses($"MAS Monthly Expenses for {today.Month}/{today.Year}", "Cost Centre", others, ref grand));
        sections.Add(Expenses("MAS Wing Expenses", "Group", wing.Select(w => w with { CostCenter = Mid(w.CostCenter) }).ToList(), ref grand));
        sections.Add(new ReportSection("Grand Total", ["", "Amount"], [["Grand Total", ReportFormat.Money(grand)]]));
        return new HospitalReportDocument("cost-centre", today, sections);
    }

    private static string Mid(string? text) => (text ?? "")[..Math.Min(7, (text ?? "").Length)];
}
