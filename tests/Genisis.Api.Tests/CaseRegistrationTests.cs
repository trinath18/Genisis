using System.Data;
using Dapper;
using Genisis.Api.Case;
using Genisis.Api.Data;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

/// <summary>Database tests that hold write transactions on MBM / CAS run one at a time so they do not deadlock each other.</summary>
[CollectionDefinition(Name)]
public class DatabaseWriteCollection
{
    public const string Name = "Database writes";
}

public class CaseRegistrationRulesTests
{
    internal static CaseRegistrationRequest Valid() => new()
    {
        MbmNumber = "AQHA0232331*01",
        CoverId = "00",
        Claimability = "AA",
        HospitalCode = "H1",
        ClaimNature = "ILL",
        StayType = "I",
        TreatmentMode = "S",
        PayType = "C",
        DateAdmitted = DateTime.Today.AddDays(-2),
        ReserveAmount = 1600,
        LgAmount = 2500,
        PolicyYear = "1",
        PatientAge = "34",
        AgeUnit = "Y",
        PrelimDiagnoses = [new("DENGUE FEVER", null, "A90")],
    };

    private static readonly CaseMemberInfo Member = new("AQ", "P1", "A", null, null, new DateTime(2021, 7, 28), new DateTime(2073, 7, 28), "");
    private static readonly CaseHospitalInfo Hospital = new("PANTAI HOSPITAL", "P", null, false);
    private static readonly CasePreChecks NoFlags = new(false, false, false);

    [Fact]
    public void Valid_request_passes() => Assert.Null(CaseRegistrationValidator.Validate(Valid()));

    [Theory]
    [InlineData("member", "Please Enter Membership Information")]
    [InlineData("patient", "Please Enter Patient Information")]
    [InlineData("hospital", "Please Enter Hospital Information")]
    [InlineData("doa", "Please Enter Admission Date")]
    [InlineData("icd", "Please Enter ICD Code")]
    [InlineData("year", "Please Enter Policy Year")]
    [InlineData("age", "Please Enter Patient's Age")]
    public void Required_fields_use_desktop_messages(string field, string message)
    {
        var r = Valid();
        switch (field)
        {
            case "member": r.MbmNumber = " "; break;
            case "patient": r.CoverId = null; break;
            case "hospital": r.HospitalCode = ""; break;
            case "doa": r.DateAdmitted = null; break;
            case "icd": r.PrelimDiagnoses = []; break;
            case "year": r.PolicyYear = ""; break;
            case "age": r.PatientAge = null; break;
        }
        Assert.Equal(message, CaseRegistrationValidator.Validate(r));
    }

    [Fact]
    public void Reimbursement_does_not_need_icd_or_policy_year()
    {
        var r = Valid();
        r.PayType = "R";
        r.PrelimDiagnoses = [];
        r.PolicyYear = "";
        Assert.Null(CaseRegistrationValidator.Validate(r));
    }

    [Fact]
    public void Repudiated_case_needs_code_and_letter_selections()
    {
        var r = Valid();
        r.Claimability = "RR";
        Assert.Equal("Please Enter Repudiate Code", CaseRegistrationValidator.Validate(r));
        r.RepudiationCodes = ["RC1"];
        Assert.Equal("Please Enter Repudiation Selection Reason", CaseRegistrationValidator.Validate(r));
        r.RepDocument = "D0001";
        Assert.Equal("Please Enter Repudiation Selection Reason", CaseRegistrationValidator.Validate(r));
        r.RepPersonnel = "P0001";
        Assert.Null(CaseRegistrationValidator.Validate(r));
    }

    [Fact]
    public void Remarks_reject_the_line_break_marker() =>
        Assert.Equal("You are not allowed to use the ~ symbol in the remarks",
            CaseRegistrationValidator.Validate(new Func<CaseRegistrationRequest>(() => { var r = Valid(); r.Remarks = ["a~b"]; return r; })()));

    [Fact]
    public void Remarks_store_line_breaks_like_the_desktop() =>
        Assert.Equal("line 1~\nline 2", CaseRegistrationService.Remark("line 1\r\nline 2"));

    [Fact]
    public void Named_hospital_needs_typed_name()
    {
        var h = Hospital with { HOSName = "GOVERNMENT HOSPITAL" };
        Assert.Equal("Please Enter Hospital Name or Clinic Name!", CaseRegistrationService.CheckAgainstMember(Valid(), Member, h, NoFlags, DateTime.Now));
        var r = Valid();
        r.HospitalName = "HOSPITAL KUALA LUMPUR";
        Assert.Null(CaseRegistrationService.CheckAgainstMember(r, Member, h, NoFlags, DateTime.Now));
    }

    [Fact]
    public void Private_hospital_off_panel_is_refused_when_payor_checks_hospitals()
    {
        var h = Hospital with { PAYChkHosp = "Y" };
        Assert.Equal("The selected hospital is not on the panel!", CaseRegistrationService.CheckAgainstMember(Valid(), Member, h, NoFlags, DateTime.Now));
        Assert.Null(CaseRegistrationService.CheckAgainstMember(Valid(), Member, h with { OnPanel = true }, NoFlags, DateTime.Now));
        var reimbursement = Valid();
        reimbursement.PayType = "R";
        Assert.Null(CaseRegistrationService.CheckAgainstMember(reimbursement, Member, h, NoFlags, DateTime.Now));
    }

    [Fact]
    public void Admission_date_is_checked_against_today_and_policy()
    {
        var r = Valid();
        r.DateAdmitted = DateTime.Today.AddDays(1);
        Assert.Equal("Admission Date cannot greater than system date !", CaseRegistrationService.CheckAgainstMember(r, Member, Hospital, NoFlags, DateTime.Now));
        r.DateAdmitted = new DateTime(2020, 1, 1);
        Assert.Equal("Admission Date cannot be less than Effective Date!", CaseRegistrationService.CheckAgainstMember(r, Member, Hospital, NoFlags, DateTime.Now));
        r.CheckAdmitted = false;
        Assert.Null(CaseRegistrationService.CheckAgainstMember(r, Member, Hospital, NoFlags, DateTime.Now));
    }

    [Fact]
    public void Expired_member_is_allowed_only_with_both_date_checks_off()
    {
        var expired = Member with { ExpDate = DateTime.Today.AddDays(-10) };
        var r = Valid();
        r.CheckAdmitted = false;
        Assert.Equal("Membership has been expired!", CaseRegistrationService.CheckAgainstMember(r, expired, Hospital, NoFlags, DateTime.Now));
        r.CheckDischarged = false;
        Assert.Null(CaseRegistrationService.CheckAgainstMember(r, expired, Hospital, NoFlags, DateTime.Now));
    }

    [Fact]
    public void Cancelled_and_inactive_policies_are_refused()
    {
        Assert.Equal("Policy has been cancelled!", CaseRegistrationService.CheckAgainstMember(Valid(), Member with { Status = "C" }, Hospital, NoFlags, DateTime.Now));
        Assert.Equal("Policy not yet activated!", CaseRegistrationService.CheckAgainstMember(Valid(), Member, Hospital, NoFlags with { NotActivated = true }, DateTime.Now));
    }
}

/// <summary>Runs against the development databases when GENISIS_TEST_MAINDB / GENISIS_TEST_MAINTENANCE are set
/// (otherwise returns immediately). The save test rolls back. Fixture member: AQHA0232331*01 (payor AQ, active).
/// </summary>
[Collection(DatabaseWriteCollection.Name)]
public class CaseRegistrationDatabaseTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private static readonly string? Maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");

    private static DbConnections Db() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:MainDb"] = MainDb,
        ["ConnectionStrings:Maintenance"] = Maintenance ?? MainDb,
    }).Build());

    private static CaseRegistrationService Service(DbConnections? db = null) =>
        new(db ?? Db(), Microsoft.Extensions.Logging.Abstractions.NullLogger<CaseRegistrationService>.Instance);

    [Fact]
    public async Task Member_loads_with_principal_as_first_patient()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var view = await Service().LoadMemberAsync("AQHA0232331*01");
        Assert.NotNull(view);
        Assert.Equal("00", view.Patients[0].CoverId);
        Assert.True(view.CanRegister);
        Assert.Null(await Service().LoadMemberAsync("NO-SUCH-MEMBER"));
    }

    [Fact]
    public async Task Member_without_policy_dates_loads_instead_of_failing_and_cannot_register()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = Db().MainDb();
        await conn.OpenAsync();
        // Roughly half of HISDB's members have no payor effective / expiry date (members registered through the web, imports).
        var member = await conn.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(m.MBMNumber) FROM dbo.MBM m JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber WHERE m.MBMPayorEffDate IS NULL AND m.MBMPayorExpDate IS NULL AND m.MBMNumber LIKE 'DCHA%'");
        Assert.NotNull(member);

        var view = await Service().LoadMemberAsync(member);

        Assert.NotNull(view);
        Assert.False(view.CanRegister);
        Assert.Contains(view.Alerts, a => a.Text.StartsWith("Membership has no policy period"));
        Assert.DoesNotContain(view.Alerts, a => a.Text == "Membership has been expired!");
    }

    [Fact]
    public async Task Save_writes_case_tables_and_rolls_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = Db();
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var hospital = await conn.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(HOSCode) FROM dbo.HOS WHERE HOSStatus <> 'P' AND HOSName NOT LIKE '%CLINIC%' AND HOSName NOT LIKE '%HOSPITAL' ORDER BY HOSCode");
        var r = CaseRegistrationRulesTests.Valid();
        r.HospitalCode = hospital;
        r.Remarks = ["first page\nsecond line", "page two"];
        r.AttendingDoctors = ["DR TEST"];
        r.FinalDiagnoses = [new("DENGUE", DateTime.Today, "A90")];

        await using var tx = (Microsoft.Data.SqlClient.SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var caseNumber = await Service(db).SaveAsync(conn, tx, r, "GENISIS");
        Assert.Matches("^AQ[0-9]{8,}$", caseNumber);

        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.CAS WHERE CASNumber = @caseNumber AND CASStatus = 'O' AND CaseOpenBy = 'GENISIS'", new { caseNumber }, tx));
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {maintenanceDb}.dbo.CASCrossReference WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("first page~\nsecond line", await conn.ExecuteScalarAsync<string>("SELECT CASORemarks FROM dbo.CASRemarks WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT CASTotalDoctors FROM dbo.CASOthersTwo WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        foreach (var table in new[] { "DIAFirst", "DIAFinal", "CAS_UD", "CASOthers", "CASRemarkThree" })
            Assert.Equal(1, await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{table} WHERE CASNumber = @caseNumber", new { caseNumber }, tx));

        await tx.RollbackAsync();
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.CAS WHERE CASNumber = @caseNumber", new { caseNumber }));
    }

    [Fact]
    public async Task Opcs_search_lists_procedures_and_investigations()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = Db().MainDb();
        var procedures = (await conn.QueryAsync("genisis.CaseReg_OpcsSearch", new { max = 10, code = (string?)null, desc = (string?)null, part = (string?)null },
            commandType: CommandType.StoredProcedure)).ToList();
        Assert.NotEmpty(procedures);
        var investigations = (await conn.QueryAsync("genisis.CaseReg_OpcsSearch", new { max = 10, code = (string?)null, desc = "X-RAY", part = "A" },
            commandType: CommandType.StoredProcedure)).ToList();
        Assert.All(investigations, row => Assert.Contains("X-RAY", (string)row.OPCSNarrative, StringComparison.OrdinalIgnoreCase));
    }
}
