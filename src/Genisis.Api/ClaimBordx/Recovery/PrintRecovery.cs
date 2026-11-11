using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Recovery;

/// <summary>A BTBG client (a group company) and the layout of the recovery listing it is sent: 1 standard, 2 MDEC.</summary>
public record RecoveryCompany(string Name, string? ReportType);

/// <summary>One recovery: a worksheet whose amount payable by the member is negative.</summary>
public record RecoveryRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Patient.</summary>
    public string? PatientName { get; set; }

    /// <summary>Patient IC.</summary>
    public string? PatientIc { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Member IC / BC / passport.</summary>
    public string? MemberIc { get; set; }

    /// <summary>Employee number.</summary>
    public string? EmployeeNo { get; set; }

    /// <summary>Department code.</summary>
    public string? DeptCode { get; set; }

    /// <summary>Department.</summary>
    public string? Department { get; set; }

    /// <summary>Cost centre.</summary>
    public string? CostCenter { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Date admitted.</summary>
    public DateTime? DateAdmitted { get; set; }

    /// <summary>Date discharged.</summary>
    public DateTime? DateDischarged { get; set; }

    /// <summary>Date the worksheet was opened.</summary>
    public DateTime? ClaimOpenDate { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Hospital.</summary>
    public string? Hospital { get; set; }

    /// <summary>Final diagnosis 1.</summary>
    public string? Diagnosis1 { get; set; }

    /// <summary>Final diagnosis 2.</summary>
    public string? Diagnosis2 { get; set; }

    /// <summary>Final diagnosis 3.</summary>
    public string? Diagnosis3 { get; set; }

    /// <summary>Debit note number.</summary>
    public string? DebitNoteNo { get; set; }

    /// <summary>Amount in the bordx.</summary>
    public decimal? BordxAmount { get; set; }

    /// <summary>The recovery as the database holds it (negative).</summary>
    public decimal? Recovery { get; set; }

    /// <summary>Actual amount.</summary>
    public decimal? ActualAmount { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Payor company name.</summary>
    public string? PayorName { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Payor effective date.</summary>
    public DateTime? PayorEffDate { get; set; }

    /// <summary>Payor expiry date.</summary>
    public DateTime? PayorExpDate { get; set; }
}

/// <summary>The recoveries with the group company's layout and the totals the screen and the listing show.</summary>
public record RecoveryResult(string? ReportType, List<RecoveryRow> Rows, decimal TotalBordxAmount, decimal TotalRecovery, decimal TotalActual, bool Truncated);

/// <summary>The criteria of the recovery search.</summary>
public class RecoveryFilter
{
    /// <summary>Grp Company (required).</summary>
    public string? Company { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? BordxDateTo { get; set; }

    /// <summary>WS Reg Date from (the date the worksheet was opened).</summary>
    public DateTime? OpenFrom { get; set; }

    /// <summary>WS Reg Date to.</summary>
    public DateTime? OpenTo { get; set; }
}

/// <summary>CmdSearch_Click's check: a group company ("Please select Group Company").</summary>
public sealed class RecoveryFilterValidator : RequestValidator<RecoveryFilter>
{
    /// <summary>Declares the rules.</summary>
    public RecoveryFilterValidator() => RuleFor(f => f.Company).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please select Group Company");
}

/// <summary>Reads the recoveries.</summary>
public interface IPrintRecoveryRepository
{
    /// <summary>The BTBG clients.</summary>
    Task<List<RecoveryCompany>> CompaniesAsync(IDbSession session);

    /// <summary>The recoveries of a group company (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<RecoveryRow>> SearchAsync(IDbSession session, RecoveryFilter filter);
}

/// <inheritdoc />
public sealed class PrintRecoveryRepository : IPrintRecoveryRepository
{
    /// <inheritdoc />
    public async Task<List<RecoveryCompany>> CompaniesAsync(IDbSession session) => (await session.Connection.ProcQueryAsync<RecoveryCompany>("genisis.PrintRecovery_Companies", null, session.Transaction)).ToList();

    /// <inheritdoc />
    public Task<List<RecoveryRow>> SearchAsync(IDbSession session, RecoveryFilter f) =>
        session.Connection.ProcQueryAsync<RecoveryRow>("genisis.PrintRecovery_Search", new
        {
            company = f.Company!.Trim(), bordxFrom = WorksheetLimits.Text(f.BordxFrom), bordxTo = WorksheetLimits.Text(f.BordxTo), bordxDateFrom = WorksheetLimits.Day(f.BordxDateFrom),
            bordxDateTo = WorksheetLimits.Day(f.BordxDateTo), openFrom = WorksheetLimits.Day(f.OpenFrom), openTo = WorksheetLimits.Day(f.OpenTo), max = WorksheetLimits.FetchRows,
        }, session.Transaction);
}

/// <summary>Claim Bordx > Print Recovery (legacy <c>frmGenerateRecovery</c>, "Generate Recovery Listing").</summary>
public class PrintRecoveryService(IDbSessions sessions, IPrintRecoveryRepository repository)
{
    /// <summary>combolist: the group companies that are BTBG clients.</summary>
    public async Task<List<RecoveryCompany>> CompaniesAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.CompaniesAsync(session);
    }

    /// <summary>
    /// SearchRecords + PrintBordxListing: the recoveries of the group company and the layout it is sent. A company that is not a BTBG client is a <see cref="NotFoundException"/>
    /// (the desktop's drop-down only held the clients). The totals add the rows listed.
    /// </summary>
    public async Task<RecoveryResult> SearchAsync(RecoveryFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var company = (await repository.CompaniesAsync(session)).FirstOrDefault(c => string.Equals(c.Name, filter.Company!.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Group Company not found");
        var capped = WorksheetLimits.Cap(await repository.SearchAsync(session, new RecoveryFilter
        {
            Company = company.Name, BordxFrom = filter.BordxFrom, BordxTo = filter.BordxTo, BordxDateFrom = filter.BordxDateFrom, BordxDateTo = filter.BordxDateTo, OpenFrom = filter.OpenFrom, OpenTo = filter.OpenTo,
        }));
        return new RecoveryResult(company.ReportType, capped.Rows, capped.Rows.Sum(r => r.BordxAmount ?? 0), capped.Rows.Sum(r => r.Recovery ?? 0), capped.Rows.Sum(r => r.ActualAmount ?? 0), capped.Truncated);
    }
}
