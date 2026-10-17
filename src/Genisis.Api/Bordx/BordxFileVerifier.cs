using System.Globalization;
using Genisis.Api.Data;
using Genisis.Api.Membership;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Bordx;

/// <summary>
/// Verify button of Upload Bordx (VerifyNewMember / VerifyENDtMember): lists every problem in the file with its line, in the
/// desktop's messages. Read-only.
/// </summary>
internal sealed class BordxFileVerifier(SqlConnection conn, IReadOnlyList<BordxLine> lines, BordxSettings settings, string fileName, BordxFormat format,
    CancellationToken cancellationToken)
{
    private sealed record PlanRow(string PlanCode, string? LifetimeStatus, string? PayorCode, decimal? AgeLimit);
    private sealed record ActiveMembership(string MembershipNo, string? Ic, DateTime? ExpiryDate);
    private sealed record EndorsementState(bool OpenCase, bool Cancelled);

    private const string DisplayDateFormat = "dd/MM/yyyy";
    private const string HisProduct = "HIS";
    private const string ClinicalProduct = "CIS";

    private readonly List<BordxError> errors = [];
    private readonly Dictionary<string, PlanRow?> plansByCode = [], plansByPayorPlan = [];
    private HashSet<string> insuredTypes = [], relationships = [], renewalTypes = [];

    private static string Code(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static string Display(DateTime? date) => date?.ToString(DisplayDateFormat, CultureInfo.InvariantCulture) ?? "";

    private void Error(BordxLine line, string? policy, string? name, string message) =>
        errors.Add(new BordxError(line.Number, (policy ?? "").Trim(), (name ?? "").Trim(), message.Trim(), fileName));

    public Task<VerifyResult> VerifyAsync() => settings.FileType == FileTypes.New ? VerifyNewFileAsync() : VerifyEndorsementFileAsync();

    private async Task LoadCodesAsync()
    {
        using var grid = await conn.ProcMultipleAsync("genisis.Bordx_Codes");
        insuredTypes = (await grid.ReadAsync<string>()).Select(Code).ToHashSet();
        relationships = (await grid.ReadAsync<string>()).Select(Code).ToHashSet();
        renewalTypes = (await grid.ReadAsync<string>()).Select(Code).ToHashSet();
    }

    private async Task<PlanRow?> FindPlanAsync(string code, bool byPayorPlan)
    {
        var cache = byPayorPlan ? plansByPayorPlan : plansByCode;
        if (!cache.TryGetValue(code, out var row))
            cache[code] = row = await conn.ProcFirstOrDefaultAsync<PlanRow>("genisis.Bordx_Plan",
                new { plan = byPayorPlan ? null : code, payorPlan = byPayorPlan ? code : null });
        return row;
    }

    private async Task CheckActiveIcAsync(BordxLine line, string policy, string name, string ic)
    {
        if (!settings.VerifyIc || ic == "") return;
        foreach (var active in await conn.ProcQueryAsync<ActiveMembership>("genisis.Bordx_ActiveByIc", new { ic }))
            Error(line, policy, name, $"Same IC {(active.Ic ?? "").Trim()} Existed in Membership No. :{active.MembershipNo} - Expiry Date : {Display(active.ExpiryDate)}");
    }

    /// <summary>Desktop VerifyMBMByPassingPA: insured type and cover period of the HIS or clinical part.</summary>
    private void CheckCoverPeriod(BordxLine line, string product, string policy, string name, string insured, DateTime? effective, DateTime? expiry)
    {
        if (!insuredTypes.Contains(Code(insured))) Error(line, policy, name, $"No Such {product} Insured Type - '{insured}' !");
        if (effective is null) Error(line, policy, name, $"Invalid {product} Date format ~ Effective Date");
        if (expiry is null) Error(line, policy, name, $"Invalid {product} Date format ~ Expiry Date");
        if (effective is { } from && expiry is { } to && (to - from).Days is < BordxRules.MinimumCoverageDays or > BordxRules.MaximumCoverageDays)
            Error(line, policy, name, $"{product} Policy Coverage must be > {BordxRules.MinimumCoverageDays} and < {BordxRules.MaximumCoverageDays} days");
    }

    private void CheckHeader(BordxLine header, char expectedRecordType, bool checkPayor, string invalidDateMessage)
    {
        if (header.RecordType != expectedRecordType) Error(header, "", "", $"1st Record must be header with Data Type '{RecordTypes.NewFileHeader}'");
        var filePayor = header.Text(HeaderLayout.PayorCode);
        if (checkPayor && (filePayor == "" || filePayor != settings.PayorCode)) Error(header, "", "", "Invalid Insurer's Code " + filePayor);
        if (header.Date(HeaderLayout.SubmissionDate) is null) Error(header, "", "", invalidDateMessage);
    }

    /// <summary>Principal / supplementary values carried to the following supplementary lines (desktop form-level variables).</summary>
    private sealed class PrincipalContext
    {
        public DateTime? Effective, Expiry, AgeEffective, ClinicalEffective, ClinicalExpiry;
        public string Plan = "", Insured = "", ClinicalPlan = "", ClinicalInsured = "";
    }

    private async Task<VerifyResult> VerifyNewFileAsync()
    {
        await LoadCodesAsync();
        var header = lines[0];
        CheckHeader(header, RecordTypes.NewFileHeader, checkPayor: !format.IsZurichTakLayout, "Invalid Submission Date !");

        int principals = 0, supplementaries = 0;
        var principal = new PrincipalContext();
        var seenIcs = new HashSet<string>();
        var clinicalOnlyPayor = YesNo.IsYes(format.Payor?.ClinicalStatus);

        foreach (var line in lines.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.RecordType == RecordTypes.Principal) principals++;
            else if (line.RecordType is not (RecordTypes.Trailer or RecordTypes.NewFileHeader)) supplementaries++;
            if (line.RecordType == RecordTypes.Trailer) continue;
            await VerifyMemberLineAsync(line, principal, seenIcs, clinicalOnlyPayor);
        }
        return new VerifyResult(Display(header.Date(HeaderLayout.SubmissionDate)), principals, supplementaries, errors);
    }

    private async Task VerifyMemberLineAsync(BordxLine line, PrincipalContext principal, HashSet<string> seenIcs, bool clinicalOnlyPayor)
    {
        var zurichTak = format.IsZurichTakLayout;
        var dataType = line.Text(MemberLayout.DataType);
        var isPrincipal = dataType == DataTypes.Principal;
        var policy = line.Text(MemberLayout.PolicyNoFor(zurichTak));
        var name = line.Text(MemberLayout.Name);
        var ic = line.Text(MemberLayout.IcNumber);
        var dateOfBirth = line.Date(MemberLayout.DateOfBirth);
        var effective = line.Date(MemberLayout.EffectiveDate);
        var expiry = line.Date(MemberLayout.ExpiryDate);
        var effectiveBlank = line.IsBlank(MemberLayout.EffectiveDate);
        if (dataType is DataTypes.Principal or DataTypes.Reinstatement) principal.AgeEffective = effective;

        var plan = zurichTak
            ? (await FindPlanAsync(line.Text(MemberLayout.PayorPlanCode), byPayorPlan: true))?.PlanCode ?? ""
            : line.Text(MemberLayout.PlanCode);
        var insured = line.Text(MemberLayout.InsuredTypeFor(zurichTak));
        var ageFrom = effectiveBlank ? principal.AgeEffective : effective;
        var age = ageFrom is { } on && dateOfBirth is { } born ? FeeRules.CalendarYearAge(on, born) : 0;

        if (dataType is DataTypes.Principal or DataTypes.Reinstatement)
        {
            principal.Effective = effective; principal.Expiry = expiry; principal.Plan = plan; principal.Insured = insured;
        }
        else
        {
            if (plan == "") plan = principal.Plan;
            if (insured == "") insured = principal.Insured;
            if (plan != "" && principal.Effective is not null)
            {
                effective ??= principal.Effective;
                expiry ??= principal.Expiry;
            }
        }

        if (clinicalOnlyPayor && plan != "") Error(line, policy, name, "Invalid Plan Code for 'DC' !! ");
        await CheckActiveIcAsync(line, policy, name, ic);
        if (!seenIcs.Add(ic)) Error(line, policy, name, "Same IC Number Found in upload File! ");
        if (name == "") Error(line, policy, name, "Name Is Blank");
        if (ic == "") Error(line, policy, name, "IC,BC or PP Is Blank");
        if (dateOfBirth is null) Error(line, policy, name, "Invalid Date format ~ Date Of Birth");
        if (line.IsBlank(MemberLayout.Sex)) Error(line, policy, name, "Sex Is Blank");
        if (plan != "")
        {
            var row = await FindPlanAsync(plan, byPayorPlan: false);
            if (row is null) Error(line, policy, name, $"No such Plan Code - ' {plan}' !");
            else if (YesNo.IsYes(row.LifetimeStatus)
                     && line.Text(MemberLayout.GuaranteeRenewal) is not (GuaranteeRenewal.Guaranteed or GuaranteeRenewal.LifetimeTracked))
                Error(line, policy, name, "Invalid Guarantee Renewal Code !");
            CheckCoverPeriod(line, HisProduct, policy, name, insured, effective, expiry);
        }

        if (isPrincipal) CheckPrincipal(line, policy, name, plan, age);
        else await CheckSupplementaryAsync(line, policy, name, dataType, insured, plan, dateOfBirth, effective, age);

        if (!format.IsZurichTakLayout) CheckClinical(line, policy, name, isPrincipal, clinicalOnlyPayor, principal);
    }

    private void CheckPrincipal(BordxLine line, string policy, string name, string plan, int age)
    {
        if (line.IsBlank(MemberLayout.Address)) Error(line, policy, name, "Address Is Blank");
        if (line.IsBlank(MemberLayout.PostCode)) Error(line, policy, name, "Post Code Is Blank");
        if (line.IsBlank(MemberLayout.State)) Error(line, policy, name, "State Is Blank");
        if (line.IsBlank(MemberLayout.City)) Error(line, policy, name, "City Is Blank");
        if (plan == "") Error(line, policy, name, "HIS Plan Code is null");
        else if (policy == "") Error(line, policy, name, "Policy No Is Blank");
        var renewal = line.Text(MemberLayout.RenewalType);
        if (!renewalTypes.Contains(Code(renewal))) Error(line, policy, name, $"No such Renewal Code - '{renewal}'");
        if (age > BordxRules.PrincipalAgeWarning) Error(line, policy, name, "Please check Principal's Age");
    }

    private async Task CheckSupplementaryAsync(BordxLine line, string policy, string name, string dataType, string insured, string plan,
        DateTime? dateOfBirth, DateTime? effective, int age)
    {
        if (insured is InsuredTypes.Individual or InsuredTypes.GroupIndividual) Error(line, policy, name, $"Wrong Isured Type fpr Supplementary - '{insured}'");
        if (!relationships.Contains(Code(dataType))) Error(line, policy, name, $"No Such Data Type - ' {dataType}' !");
        else if (plan != "" && dateOfBirth is { } born && effective is { } from)
        {
            var row = await FindPlanAsync(plan, byPayorPlan: false);
            if (row is null) Error(line, policy, name, "Invalid HIS Plan Code");
            else
            {
                var limit = (int)(row.AgeLimit ?? 0);
                var years = FeeRules.CalendarYearAge(from, born);
                if (limit != 0 && (years > limit || (years == limit && FeeRules.BirthdayIn(from.Year, born) <= from)))
                    Error(line, policy, name, $"Supplementary age is above {limit}");
            }
        }
        if (dataType is DataTypes.Son or DataTypes.Daughter && age > BordxRules.ChildAgeWarning) Error(line, policy, name, "Please check Dependent's Age");
        if (dataType is DataTypes.Husband or DataTypes.Wife && age > BordxRules.SpouseAgeWarning) Error(line, policy, name, "Please check Dependent's Age");
    }

    private void CheckClinical(BordxLine line, string policy, string name, bool isPrincipal, bool clinicalOnlyPayor, PrincipalContext principal)
    {
        var plan = line.Text(MemberLayout.ClinicalPlan);
        var insured = line.Text(MemberLayout.ClinicalInsuredType);
        var effective = line.Date(MemberLayout.ClinicalEffectiveDate);
        var expiry = line.Date(MemberLayout.ClinicalExpiryDate);
        if (plan == "" && isPrincipal) Error(line, policy, name, "Clinical Plan Is NULL ! ");
        if (clinicalOnlyPayor && isPrincipal && plan == "") Error(line, policy, name, "Clinical Plan Code cannot be null !! ");
        if (isPrincipal)
        {
            principal.ClinicalPlan = plan; principal.ClinicalInsured = insured; principal.ClinicalEffective = effective; principal.ClinicalExpiry = expiry;
        }
        else
        {
            if (plan == "") plan = principal.ClinicalPlan;
            if (insured == "") insured = principal.ClinicalInsured;
            effective ??= principal.ClinicalEffective;
            expiry ??= principal.ClinicalExpiry;
        }
        if (plan == "") return;
        CheckCoverPeriod(line, ClinicalProduct, policy, name, insured, effective, expiry);
        if (line.IsBlank(MemberLayout.ClinicalCardType)) Error(line, policy, name, "Clinical Card Type is blank !! ");
    }

    private async Task<VerifyResult> VerifyEndorsementFileAsync()
    {
        await LoadCodesAsync();
        var header = lines[0];
        CheckHeader(header, RecordTypes.EndorsementHeader, checkPayor: true, "Invalid Date" + header.Text(HeaderLayout.PayorCode));

        int principals = 0, supplementaries = 0;
        var last = header;
        foreach (var line in lines.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = line;
            if (line.Raw(EndorsementLayout.DataType) == DataTypes.Principal) principals++;
            else if (line.RecordType != RecordTypes.Trailer) supplementaries++;
            if (line.RecordType != RecordTypes.Trailer) await VerifyEndorsementLineAsync(line);
        }

        if (last.RecordType != RecordTypes.Trailer) Error(last, "", "", $"1st Record must be trailer with Data Type '{RecordTypes.Trailer}'");
        else if (last.Text(TrailerLayout.TotalPrincipals) != principals.ToString(CultureInfo.InvariantCulture))
            Error(last, "", "", "Total Principal Record Not Match Trailer Total Principal");
        else if (last.Text(TrailerLayout.TotalSupplementaries) != supplementaries.ToString(CultureInfo.InvariantCulture))
            Error(last, "", "", "Total Supplementary Record Not Match Trailer Total Supplementary");
        return new VerifyResult(Display(header.Date(HeaderLayout.SubmissionDate)), principals, supplementaries, errors);
    }

    private async Task VerifyEndorsementLineAsync(BordxLine line)
    {
        var policy = line.Raw(EndorsementLayout.PolicyNo);
        var name = line.Raw(EndorsementLayout.Name);
        var dataType = line.Raw(EndorsementLayout.DataType);
        var number = line.Text(EndorsementLayout.MembershipNo);
        if (line.Date(EndorsementLayout.EffectiveDate) is null) Error(line, policy, name, "Invalid date format ~ Endorsement Effective Date");
        if (dataType.Trim() == "") Error(line, policy, name, "Data Type is blank");
        if (number == "") Error(line, policy, name, "Membership No Is Blank");
        if (name.Trim() == "") Error(line, policy, name, "Name Is Blank");

        if (line.RecordType == RecordTypes.Cancellation)
        {
            if (dataType != DataTypes.Principal) return;
            var state = await conn.ProcSingleAsync<EndorsementState>("genisis.Bordx_EndorsementState", new { number });
            if (state.OpenCase) Error(line, policy, name, "There is at least an outstanding case under this member!");
            if (state.Cancelled) Error(line, policy, name, $"Membership Number : {number} has been cancelled !");
            return;
        }

        var ic = line.Text(EndorsementLayout.IcNumber);
        var plan = line.Text(EndorsementLayout.PlanCode);
        var insured = line.Text(EndorsementLayout.InsuredType);
        if (plan != "" && policy.Trim() == "") Error(line, policy, name, "Policy No Is Blank");
        await CheckActiveIcAsync(line, policy, name, ic);
        if (ic == "") Error(line, policy, name, "IC/BC/PP Is Blank");
        if (line.Date(EndorsementLayout.DateOfBirth) is null) Error(line, policy, name, "Invalid Date format ~ DOB ");
        if (insured != "" && !insuredTypes.Contains(Code(insured))) Error(line, policy, name, $"No such Plan Code - ' {plan}' !");
        if (dataType != DataTypes.Principal) return;
        if (line.IsBlank(EndorsementLayout.Address)) Error(line, policy, name, "Address Is Blank");
        if (await FindPlanAsync(plan, byPayorPlan: false) is null) Error(line, policy, name, $"No such Plan Code - ' {plan}' !");
        else CheckCoverPeriod(line, HisProduct, policy, name, insured, line.Date(EndorsementLayout.PayorEffectiveDate), line.Date(EndorsementLayout.PayorExpiryDate));
    }
}
