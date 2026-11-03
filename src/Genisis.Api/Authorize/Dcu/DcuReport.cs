using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;

namespace Genisis.Api.Authorize.Dcu;

/// <summary>The criteria of <c>SearchRecords</c>: the boxes of the Selection Criteria panel. A blank text, an empty date and a false "empty" box are no criterion.</summary>
public class DcuCriteria
{
    /// <summary>HLT Type: S Sihat or N Non-Sihat.</summary>
    public string? HealthType { get; set; }

    /// <summary>INS Type (insured type code).</summary>
    public string? InsType { get; set; }

    /// <summary>Mem Type: P principal or S supplementary; none is both.</summary>
    public string? MemberType { get; set; }

    /// <summary>Supp. Type: C combined limit or S separated limit; none is both.</summary>
    public string? SupplementaryType { get; set; }

    /// <summary>Data Status: A active or D delete.</summary>
    public string? DataStatus { get; set; }

    /// <summary>Claimability: A accepted or R rejected.</summary>
    public string? Claimability { get; set; }

    /// <summary>Policy Status (code of PolicyStatus).</summary>
    public string? PolicyStatus { get; set; }

    /// <summary>Case Status: 0 open, 1 open and close (the default), 2 delete, 3 all.</summary>
    public int? CaseStatus { get; set; } = 1;

    /// <summary>Agent Code (contains).</summary>
    public string? AgentCode { get; set; }

    /// <summary>Policy No. (contains).</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Payor (code).</summary>
    public string? Payor { get; set; }

    /// <summary>Grp Company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>DOA from.</summary>
    public DateTime? DoaFrom { get; set; }

    /// <summary>DOA to.</summary>
    public DateTime? DoaTo { get; set; }

    /// <summary>DOD from.</summary>
    public DateTime? DodFrom { get; set; }

    /// <summary>DOD to.</summary>
    public DateTime? DodTo { get; set; }

    /// <summary>DORC from (case registration date).</summary>
    public DateTime? DorFrom { get; set; }

    /// <summary>DORC to.</summary>
    public DateTime? DorTo { get; set; }

    /// <summary>Final Diag from.</summary>
    public string? FinalDiagFrom { get; set; }

    /// <summary>Final Diag to.</summary>
    public string? FinalDiagTo { get; set; }

    /// <summary>Case Pending: N no or Y yes.</summary>
    public string? CasePending { get; set; }

    /// <summary>Clm Pending: B bordxed, C cancelled or P pending.</summary>
    public string? ClaimPending { get; set; }

    /// <summary>Plan Code from.</summary>
    public string? PlanFrom { get; set; }

    /// <summary>Plan Code to.</summary>
    public string? PlanTo { get; set; }

    /// <summary>Bordx No from (Claim reports only).</summary>
    public string? BordxNoFrom { get; set; }

    /// <summary>Bordx No to (Claim reports only).</summary>
    public string? BordxNoTo { get; set; }

    /// <summary>Eff Date from (payor effective date).</summary>
    public DateTime? PayorEffFrom { get; set; }

    /// <summary>Eff Date to.</summary>
    public DateTime? PayorEffTo { get; set; }

    /// <summary>Exp Date from (payor expiry date).</summary>
    public DateTime? PayorExpFrom { get; set; }

    /// <summary>Exp Date to.</summary>
    public DateTime? PayorExpTo { get; set; }

    /// <summary>Date Join from.</summary>
    public DateTime? JoinFrom { get; set; }

    /// <summary>Date Join to.</summary>
    public DateTime? JoinTo { get; set; }

    /// <summary>Bordx Date from (Claim reports only).</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx Date to (Claim reports only).</summary>
    public DateTime? BordxDateTo { get; set; }

    /// <summary>Claim No / Case No from.</summary>
    public string? CaseNoFrom { get; set; }

    /// <summary>Claim No / Case No to.</summary>
    public string? CaseNoTo { get; set; }

    /// <summary>Version from (Claim reports only).</summary>
    public int? VersionFrom { get; set; }

    /// <summary>Version to (Claim reports only).</summary>
    public int? VersionTo { get; set; }

    /// <summary>Stay Type: I in-patient or O out-patient.</summary>
    public string? StayType { get; set; }

    /// <summary>Cash Type: C cashless or R reimbursement.</summary>
    public string? CashType { get; set; }

    /// <summary>Modality Code: B both, N non-surgical or S surgical.</summary>
    public string? Modality { get; set; }

    /// <summary>The "empty" boxes: only the rows where the field has no value (HLT Type).</summary>
    public bool BlankHealthType { get; set; }

    /// <summary>Empty: INS Type.</summary>
    public bool BlankInsType { get; set; }

    /// <summary>Empty: Data Status.</summary>
    public bool BlankDataStatus { get; set; }

    /// <summary>Empty: Claimability.</summary>
    public bool BlankClaimability { get; set; }

    /// <summary>Empty: Policy Status.</summary>
    public bool BlankPolicyStatus { get; set; }

    /// <summary>Empty: Case Status.</summary>
    public bool BlankCaseStatus { get; set; }

    /// <summary>Empty: Agent Code.</summary>
    public bool BlankAgentCode { get; set; }

    /// <summary>Empty: Policy No.</summary>
    public bool BlankPolicyNo { get; set; }

    /// <summary>Empty: Payor.</summary>
    public bool BlankPayor { get; set; }

    /// <summary>Empty: Grp Company.</summary>
    public bool BlankGroupCompany { get; set; }

    /// <summary>Empty: DOA.</summary>
    public bool BlankDoa { get; set; }

    /// <summary>Empty: DOD.</summary>
    public bool BlankDod { get; set; }

    /// <summary>Empty: DORC.</summary>
    public bool BlankDor { get; set; }

    /// <summary>Empty: Final Diag.</summary>
    public bool BlankFinalDiag { get; set; }

    /// <summary>Empty: Plan Code.</summary>
    public bool BlankPlan { get; set; }

    /// <summary>Empty: Bordx No (Claim reports only).</summary>
    public bool BlankBordxNo { get; set; }

    /// <summary>Empty: Eff Date.</summary>
    public bool BlankPayorEff { get; set; }

    /// <summary>Empty: Exp Date.</summary>
    public bool BlankPayorExp { get; set; }

    /// <summary>Empty: Date Join.</summary>
    public bool BlankJoin { get; set; }

    /// <summary>Empty: Bordx Date (Claim reports only).</summary>
    public bool BlankBordxDate { get; set; }
}

/// <summary>What Search asks for: the report (Claim or Case, report type, selection) and the criteria.</summary>
public class DcuReportRequest : DcuCriteria
{
    /// <summary>claim or case (cboClaimCase).</summary>
    public string? Mode { get; set; } = "claim";

    /// <summary>Index of the report type (cboReportType).</summary>
    public int ReportType { get; set; }

    /// <summary>Index of the selection (cboReportSelection); none for a type that has none.</summary>
    public int? Selection { get; set; }
}

/// <summary>A code with its description.</summary>
public record DcuCode(string Code, string Name);

/// <summary>A payor with the health type it belongs to (it narrows the list as the desktop's LoadPayor did).</summary>
public record DcuPayor(string Code, string Name, string? HealthType);

/// <summary>The lists of the form.</summary>
public record DcuLookups(List<DcuCode> InsTypes, List<DcuPayor> Payors, List<DcuCode> PolicyStatuses, List<DcuReportType> ReportTypes);

/// <summary>The heading of a column of the list.</summary>
public record DcuResultColumn(string Header, bool Right);

/// <summary>A report: its columns and rows, and whether more than the 5,000 rows shown matched.</summary>
public record DcuReportResult(string Title, List<DcuResultColumn> Columns, List<string[]> Rows, bool Truncated);

/// <summary>cmdSearch_Click's checks.</summary>
public sealed class DcuReportRequestValidator : RequestValidator<DcuReportRequest>
{
    private static readonly string[] Letters = ["S", "N"];

    /// <summary>Declares the rules.</summary>
    public DcuReportRequestValidator()
    {
        RuleFor(r => r.Mode).Must(m => m is "claim" or "case").WithMessage("Please select a Claim or Case Report.");
        RuleFor(r => r).Must(r => DcuReportCatalog.Instance.Find(r.Mode, r.ReportType, r.Selection) is not null).WithMessage("Select a report type and selection.");
        RuleFor(r => r.HealthType).Must(v => FinanceLimits.IsBlankOr(v, Letters)).WithMessage("Select a valid HLT Type.");
        RuleFor(r => r.MemberType).Must(v => FinanceLimits.IsBlankOr(v, "P", "S")).WithMessage("Select a valid Mem Type.");
        RuleFor(r => r.SupplementaryType).Must(v => FinanceLimits.IsBlankOr(v, "C", "S")).WithMessage("Select a valid Supp. Type.");
        RuleFor(r => r.DataStatus).Must(v => FinanceLimits.IsBlankOr(v, "A", "D")).WithMessage("Select a valid Data Status.");
        RuleFor(r => r.Claimability).Must(v => FinanceLimits.IsBlankOr(v, "A", "R")).WithMessage("Select a valid Claimability.");
        RuleFor(r => r.CaseStatus).Must(v => v is null or >= 0 and <= 3).WithMessage("Select a valid Case Status.");
        RuleFor(r => r.CasePending).Must(v => FinanceLimits.IsBlankOr(v, "N", "Y")).WithMessage("Select a valid Case Pending.");
        RuleFor(r => r.ClaimPending).Must(v => FinanceLimits.IsBlankOr(v, "B", "C", "P")).WithMessage("Select a valid Clm Pending.");
        RuleFor(r => r.StayType).Must(v => FinanceLimits.IsBlankOr(v, "I", "O")).WithMessage("Select a valid Stay Type.");
        RuleFor(r => r.CashType).Must(v => FinanceLimits.IsBlankOr(v, "C", "R")).WithMessage("Select a valid Cash Type.");
        RuleFor(r => r.Modality).Must(v => FinanceLimits.IsBlankOr(v, "B", "N", "S")).WithMessage("Select a valid Modality Code.");
        RuleFor(r => r.VersionFrom).Must(v => v is null or >= 0).WithMessage("Version must be a number.");
        RuleFor(r => r.VersionTo).Must(v => v is null or >= 0).WithMessage("Version must be a number.");
        foreach (var (name, get) in TextBoxes) RuleFor(r => r).Must(r => (get(r)?.Length ?? 0) <= 200).WithMessage($"{name} is too long.");
    }

    private static readonly (string Name, Func<DcuReportRequest, string?> Get)[] TextBoxes =
    [
        ("INS Type", r => r.InsType), ("Policy Status", r => r.PolicyStatus), ("Agent Code", r => r.AgentCode), ("Policy No.", r => r.PolicyNo), ("Payor", r => r.Payor), ("Grp Company", r => r.GroupCompany),
        ("Final Diag", r => r.FinalDiagFrom), ("Final Diag", r => r.FinalDiagTo), ("Plan Code", r => r.PlanFrom), ("Plan Code", r => r.PlanTo), ("Bordx No", r => r.BordxNoFrom), ("Bordx No", r => r.BordxNoTo),
        ("Claim No", r => r.CaseNoFrom), ("Claim No", r => r.CaseNoTo),
    ];
}

/// <summary>The criteria as the XML document genisis.DcuReport_Where reads (an element per criterion that is set; a Case report has no bordx or version criteria, as the desktop's Option14 disabled them).</summary>
public static class DcuCriteriaXml
{
    /// <summary>Builds the document.</summary>
    public static string Build(DcuCriteria c, string mode)
    {
        var caseMode = mode == "case";
        var root = new XElement("c");
        void Text(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) root.Add(new XElement(name, Clean(value.Trim())));
        }

        void Day(string name, DateTime? value)
        {
            if (value is { } d) root.Add(new XElement(name, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        void Number(string name, int? value)
        {
            if (value is { } n) root.Add(new XElement(name, n.ToString(CultureInfo.InvariantCulture)));
        }

        void Flag(string name, bool value)
        {
            if (value) root.Add(new XElement(name, "true"));
        }

        Text("healthType", c.HealthType); Text("insType", c.InsType); Text("memberType", c.MemberType); Text("supplementaryType", c.SupplementaryType); Text("dataStatus", c.DataStatus);
        Text("claimability", c.Claimability); Text("policyStatus", c.PolicyStatus); Number("caseStatus", c.CaseStatus); Text("agentCode", c.AgentCode); Text("policyNo", c.PolicyNo);
        Text("payor", c.Payor); Text("groupCompany", c.GroupCompany);
        Day("doaFrom", c.DoaFrom); Day("doaTo", c.DoaTo); Day("dodFrom", c.DodFrom); Day("dodTo", c.DodTo); Day("dorFrom", c.DorFrom); Day("dorTo", c.DorTo);
        Text("finalDiagFrom", c.FinalDiagFrom); Text("finalDiagTo", c.FinalDiagTo); Text("casePending", c.CasePending); Text("claimPending", c.ClaimPending);
        Text("planFrom", c.PlanFrom); Text("planTo", c.PlanTo);
        Day("payorEffFrom", c.PayorEffFrom); Day("payorEffTo", c.PayorEffTo); Day("payorExpFrom", c.PayorExpFrom); Day("payorExpTo", c.PayorExpTo); Day("joinFrom", c.JoinFrom); Day("joinTo", c.JoinTo);
        Text("caseNoFrom", c.CaseNoFrom); Text("caseNoTo", c.CaseNoTo);
        Text("stayType", c.StayType); Text("cashType", c.CashType); Text("modality", c.Modality);
        if (!caseMode)
        {
            Text("bordxNoFrom", c.BordxNoFrom); Text("bordxNoTo", c.BordxNoTo); Day("bordxDateFrom", c.BordxDateFrom); Day("bordxDateTo", c.BordxDateTo);
            Number("versionFrom", c.VersionFrom); Number("versionTo", c.VersionTo); Flag("blankBordxNo", c.BlankBordxNo); Flag("blankBordxDate", c.BlankBordxDate);
        }

        Flag("blankHealthType", c.BlankHealthType); Flag("blankInsType", c.BlankInsType); Flag("blankDataStatus", c.BlankDataStatus); Flag("blankClaimability", c.BlankClaimability);
        Flag("blankPolicyStatus", c.BlankPolicyStatus); Flag("blankCaseStatus", c.BlankCaseStatus); Flag("blankAgentCode", c.BlankAgentCode); Flag("blankPolicyNo", c.BlankPolicyNo);
        Flag("blankPayor", c.BlankPayor); Flag("blankGroupCompany", c.BlankGroupCompany); Flag("blankDoa", c.BlankDoa); Flag("blankDod", c.BlankDod); Flag("blankDor", c.BlankDor);
        Flag("blankFinalDiag", c.BlankFinalDiag); Flag("blankPlan", c.BlankPlan); Flag("blankPayorEff", c.BlankPayorEff); Flag("blankPayorExp", c.BlankPayorExp); Flag("blankJoin", c.BlankJoin);
        return root.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>A list of values (ICD codes, case numbers) as the XML document the lookup procedures read.</summary>
    public static string List(IEnumerable<string> values) => new XElement("l", values.Select(v => new XElement("i", Clean(v)))).ToString(SaveOptions.DisableFormatting);

    /// <summary>Removes the characters XML cannot hold.</summary>
    private static string Clean(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var ch in value) if (XmlConvertIsLegal(ch)) text.Append(ch);
        return text.ToString();
    }

    private static bool XmlConvertIsLegal(char ch) => ch is '\t' or '\n' or '\r' || (ch >= 0x20 && !char.IsSurrogate(ch) && ch != 0xFFFE && ch != 0xFFFF);
}

/// <summary>Reads the lists and runs the queries of the DCU report.</summary>
public interface IDcuReportRepository
{
    /// <summary>ComboListing: insured types, payors and policy statuses.</summary>
    Task<(List<DcuCode> Ins, List<DcuPayor> Payors, List<DcuCode> PolicyStatuses)> LookupsAsync(IDbSession session);

    /// <summary>The group companies of a payor.</summary>
    Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor);

    /// <summary>One case of the report's queries, at most <paramref name="max"/> rows.</summary>
    Task<List<DcuRow>> RunAsync(IDbSession session, int caseNo, string criteriaXml, string? orderBy, int max);

    /// <summary>The ageing counts of an efficiency report (DIR, BORDX, INV, BORDXINV) or the claims by bordx month (OSBORDX).</summary>
    Task<List<DcuRow>> EfficiencyAsync(IDbSession session, string kind, string criteriaXml);

    /// <summary>The descriptions of ICD codes.</summary>
    Task<Dictionary<string, string>> IcdDescriptionsAsync(IDbSession session, IReadOnlyCollection<string> codes);

    /// <summary>The approved amounts of the claims of cases, not yet in a bordx and in one.</summary>
    Task<Dictionary<string, (decimal? NotYetBordx, decimal? Bordxed)>> CaseBordxAsync(IDbSession session, IReadOnlyCollection<string> cases);
}

/// <inheritdoc />
public sealed class DcuReportRepository : IDcuReportRepository
{
    /// <summary>The desktop gave each query 300 seconds.</summary>
    private const int Timeout = 300;

    /// <inheritdoc />
    public async Task<(List<DcuCode> Ins, List<DcuPayor> Payors, List<DcuCode> PolicyStatuses)> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.DcuReport_Lookups", null, session.Transaction);
        return ((await grid.ReadAsync<DcuCode>()).ToList(), (await grid.ReadAsync<DcuPayor>()).ToList(), (await grid.ReadAsync<DcuCode>()).ToList());
    }

    /// <inheritdoc />
    public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) => session.Connection.ProcQueryAsync<string>("genisis.DcuReport_GroupCompanies", new { payor }, session.Transaction);

    private static async Task<List<DcuRow>> QueryAsync(IDbSession session, string procedure, object parameters)
    {
        var rows = await Dapper.SqlMapper.QueryAsync(session.Connection, procedure, parameters, session.Transaction, Timeout, System.Data.CommandType.StoredProcedure);
        return rows.Select(r => new DcuRow((IDictionary<string, object?>)r)).ToList();
    }

    /// <inheritdoc />
    public Task<List<DcuRow>> RunAsync(IDbSession session, int caseNo, string criteriaXml, string? orderBy, int max) =>
        QueryAsync(session, "genisis.DcuReport_Run", new { @case = caseNo, criteria = criteriaXml, orderBy, max });

    /// <inheritdoc />
    public Task<List<DcuRow>> EfficiencyAsync(IDbSession session, string kind, string criteriaXml) => QueryAsync(session, "genisis.DcuReport_Efficiency", new { kind, criteria = criteriaXml });

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> IcdDescriptionsAsync(IDbSession session, IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return [];
        var rows = await session.Connection.ProcQueryAsync<(string Code, string Description)>("genisis.DcuReport_IcdDescriptions", new { codes = DcuCriteriaXml.List(codes) }, session.Transaction);
        return rows.ToDictionary(r => r.Code, r => r.Description, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, (decimal? NotYetBordx, decimal? Bordxed)>> CaseBordxAsync(IDbSession session, IReadOnlyCollection<string> cases)
    {
        if (cases.Count == 0) return [];
        var rows = await session.Connection.ProcQueryAsync<(string CaseNo, decimal? NotYetBordx, decimal? Bordxed)>("genisis.DcuReport_CaseBordx", new { cases = DcuCriteriaXml.List(cases) }, session.Transaction);
        return rows.ToDictionary(r => r.CaseNo, r => (r.NotYetBordx, r.Bordxed), StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Authorize Claims > DCU Tracking > Claim Report (legacy <c>FrmDCUReport1</c>, "DCU Reports").</summary>
public class DcuReportService(IDbSessions sessions, IDcuReportRepository repository, ILogger<DcuReportService> log)
{
    /// <summary>ComboListing: the lists, and the report types with their selections for Claim and for Case.</summary>
    public async Task<DcuLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        var (ins, payors, statuses) = await repository.LookupsAsync(session);
        return new DcuLookups(ins, payors, statuses, DcuReportCatalog.Instance.Types());
    }

    /// <summary>cboPayorCode_Click: the group companies of a payor.</summary>
    public async Task<List<string>> GroupCompaniesAsync(string? payor)
    {
        if (string.IsNullOrWhiteSpace(payor)) return [];
        await using var session = await sessions.OpenAsync();
        return await repository.GroupCompaniesAsync(session, payor.Trim());
    }

    /// <summary>
    /// cmdSearch_Click: the list of the report asked for. The query runs once for the whole list, and so do the ICD descriptions, the case bordx amounts and, for a Case report, the duration of stay of its groups (the desktop
    /// asked each of them again for every row). Nothing found is a <see cref="NotFoundException"/> ("No record found!"); more than 5,000 rows are cut and said so.
    /// </summary>
    public async Task<DcuReportResult> RunAsync(DcuReportRequest request, string userCode)
    {
        var report = DcuReportCatalog.Instance.Find(request.Mode, request.ReportType, request.Selection) ?? throw new BusinessRuleException("Select a report type and selection.");
        var criteria = DcuCriteriaXml.Build(request, report.Mode);
        await using var session = await sessions.OpenAsync();
        List<string[]> rows;
        var truncated = false;
        if (report.Efficiency is not null && report.Efficiency != "OS")
            rows = DcuEfficiency.AgeingRows(await repository.EfficiencyAsync(session, report.Efficiency, criteria));
        else if (report.Efficiency == "OS")
            rows = DcuEfficiency.OutstandingRows(await repository.RunAsync(session, report.Case, criteria, report.OrderBy, int.MaxValue), await repository.EfficiencyAsync(session, "OSBORDX", criteria));
        else
        {
            var data = await repository.RunAsync(session, report.Case, criteria, report.OrderBy, FinanceLimits.FetchRows);
            truncated = data.Count > FinanceLimits.MaxRows;
            if (truncated) data = data.Take(FinanceLimits.MaxRows).ToList();
            var durations = report.DurationCase is { } durationCase ? DcuReportEngine.SumDurations(await repository.RunAsync(session, durationCase, criteria, null, int.MaxValue), report.DurationKeys) : [];
            var icd = await repository.IcdDescriptionsAsync(session, DcuReportEngine.IcdCodes(report, data));
            var bordx = await repository.CaseBordxAsync(session, DcuReportEngine.CaseNumbers(report, data));
            rows = DcuReportEngine.Build(report, data, new DcuLookupData(durations, icd, bordx));
        }

        if (rows.Count == 0) throw new NotFoundException("No record found!");
        log.LogInformation("DCU report {Report} ({Routine}): {Rows} row(s) for {User}", report.Id, report.Routine, rows.Count, userCode);
        return new DcuReportResult(Title(report), report.Columns.Select(c => new DcuResultColumn(c.Header, c.Right)).ToList(), rows, truncated);
    }

    private static string Title(DcuReportDefinition report) => $"{(report.Mode == "claim" ? "Claim" : "Case")} - {report.Type}{(report.Selection is null ? "" : " - " + report.Selection)}";
}
