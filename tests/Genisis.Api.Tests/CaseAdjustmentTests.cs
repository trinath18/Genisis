using System.Data;
using System.Security.Claims;
using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

public class CaseAdjustmentRulesTests
{
    internal static CaseAdjustmentRequest Valid() => new()
    {
        CaseNumber = "AQ00000001",
        MemberName = "TEST MEMBER",
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
    };

    private static CaseGLRequest GL() => new()
    {
        Purpose = "Admission",
        RequestDate = DateTime.Today, RequestTime = "09:00", RequestMode = "Email", RequestBy = "Hospital",
        GLDate = DateTime.Today, GLTime = "09:20", LgAmount = 2500, Remarks = "ok",
    };

    [Fact]
    public void Valid_adjustment_passes() => Assert.Null(CaseAdjustmentValidator.Validate(Valid()));

    [Theory]
    [InlineData("case", "Please Enter Case No!")]
    [InlineData("claimability", "Please Select Claimability!")]
    [InlineData("member", "Please Enter Member No!")]
    [InlineData("name", "Please Enter Member Name!")]
    [InlineData("doa", "Please Enter Admission Date")]
    [InlineData("toplan", "Please Enter Takeover Plan")]
    [InlineData("polyear", "Please Enter Policy Year")]
    [InlineData("age", "Please Enter Patient's Age")]
    [InlineData("unit", "Please Select Day/Year")]
    [InlineData("tilde", "You are not allowed to use the ~ symbol in the remarks")]
    public void Update_checks_follow_the_desktop_order(string missing, string message)
    {
        var r = Valid();
        switch (missing)
        {
            case "case": r.CaseNumber = " "; r.Claimability = null; break;
            case "claimability": r.Claimability = ""; r.MbmNumber = null; break;
            case "member": r.MbmNumber = ""; r.MemberName = null; break;
            case "name": r.MemberName = ""; r.DateAdmitted = null; break;
            case "doa": r.DateAdmitted = null; r.PolicyYear = null; break;
            case "toplan": r.IntExtTakeOver = "I"; r.PolicyYear = null; break;
            case "polyear": r.PolicyYear = ""; r.PatientAge = null; break;
            case "age": r.PatientAge = ""; r.AgeUnit = null; break;
            case "unit": r.AgeUnit = ""; break;
            case "tilde": r.Remarks = ["a~b"]; break;
        }
        Assert.Equal(message, CaseAdjustmentValidator.Validate(r));
    }

    [Fact]
    public void Repudiation_needs_code_and_selections_unless_reimbursement()
    {
        var r = Valid();
        Assert.Equal("Please Enter Repudiation Reason", CaseAdjustmentValidator.ValidateRepudiation(r));
        r.RepudiationCodes = ["RC1"];
        Assert.Equal("Please Enter Repudiation Selection Reason", CaseAdjustmentValidator.ValidateRepudiation(r));
        r.PayType = "R";
        Assert.Null(CaseAdjustmentValidator.ValidateRepudiation(r));
        r.PayType = "C";
        r.RepDocument = "D01";
        r.RepPersonnel = "P01";
        Assert.Null(CaseAdjustmentValidator.ValidateRepudiation(r));
    }

    [Fact]
    public void GL_dialog_passes_when_complete() => Assert.Null(CaseGLRules.Validate(GL(), null));

    [Theory]
    [InlineData("reqdate", "Please Enter Request Date")]
    [InlineData("reqtime", "Please Enter Request Time")]
    [InlineData("mode", "Please Enter Request Mode")]
    [InlineData("by", "Please Enter Request By")]
    [InlineData("gldate", "Please Enter GL Date")]
    [InlineData("gltime", "Please Enter GL Time")]
    [InlineData("amount", "Please Enter Reserve Amount")]
    [InlineData("purpose", "Please Enter purpose for GL")]
    [InlineData("remark", "Please Enter remark")]
    public void GL_dialog_checks_follow_the_desktop_order(string missing, string message)
    {
        var r = GL();
        switch (missing)
        {
            case "reqdate": r.RequestDate = null; r.RequestTime = null; break;
            case "reqtime": r.RequestTime = ""; r.RequestMode = null; break;
            case "mode": r.RequestMode = ""; r.RequestBy = null; break;
            case "by": r.RequestBy = ""; r.GLDate = null; break;
            case "gldate": r.GLDate = null; r.GLTime = null; break;
            case "gltime": r.GLTime = ""; r.LgAmount = null; break;
            case "amount": r.LgAmount = null; r.Purpose = "Discharge"; r.Remarks = null; break;
            case "purpose": r.Purpose = ""; r.Remarks = null; break;
            case "remark": r.Remarks = " "; break;
        }
        Assert.Equal(message, CaseGLRules.Validate(r, null));
    }

    [Fact]
    public void Outpatient_GL_needs_no_amount()
    {
        var r = GL();
        r.Purpose = "Outpatient";
        r.LgAmount = null;
        Assert.Null(CaseGLRules.Validate(r, null));
    }

    [Fact]
    public void Late_GL_needs_a_delay_reason()
    {
        var r = GL();
        r.GLTime = "09:31";
        Assert.Equal("Please Enter reason for delay", CaseGLRules.Validate(r, null));
        r.GLTime = "09:30";
        Assert.Null(CaseGLRules.Validate(r, null));
        r.GLDate = DateTime.Today.AddDays(1);
        Assert.Equal("Please Enter reason for delay", CaseGLRules.Validate(r, null));
        r.DelayReason = "Late Admission";
        Assert.Null(CaseGLRules.Validate(r, null));
    }

    [Theory]
    [InlineData("Admission", "Discharge", false)]
    [InlineData("Admission", "Outpatient", true)]
    [InlineData("Admission", "Executive Medical Checkup", true)]
    [InlineData("Discharge", "Admission", true)]
    [InlineData("Discharge", "Follow-up", false)]
    [InlineData("Outpatient", "Admission", false)]
    [InlineData("Executive Medical Checkup", "Follow-up", true)]
    [InlineData(null, "Outpatient", false)]
    public void GL_purpose_rules_follow_cbPurpose_Change(string? last, string purpose, bool blocked)
    {
        Assert.Equal(blocked, CaseGLRules.PurposeBlocked(last, purpose));
        var r = GL();
        r.Purpose = purpose;
        if (purpose == "Outpatient") r.LgAmount = null;
        Assert.Equal(blocked ? CaseGLRules.NotAllowed : null, CaseGLRules.Validate(r, last));
    }

    [Fact]
    public void Progress_actions_match_the_desktop_buttons()
    {
        Assert.Equal("CASE APPROVED FOR REFERRAL DOCTOR", CaseAdjustmentService.ProgressActions["referral-accept"].Progress);
        Assert.Equal("CASE DECLINED FOR REFERRAL DOCTOR", CaseAdjustmentService.ProgressActions["referral-reject"].Progress);
        Assert.Null(CaseAdjustmentService.ProgressActions["clear-doc"].Progress);
        Assert.Equal("PENDING DOCUMENT FROM HOSPITAL", CaseAdjustmentService.ProgressActions["doc-discharge"].Progress);
    }

    [Theory]
    [InlineData("00000000001100000000", true)]
    [InlineData("00000000001000000000", true)]
    [InlineData("00000000000100000000", true)]
    [InlineData("00000000000010000000", false)]
    [InlineData("00000000000000000000", false)]
    public void Shared_endpoints_accept_registration_or_adjustment_access(string access, bool allowed)
    {
        var context = new AuthorizationFilterContext(
            new ActionContext(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(TokenService.AccessClaim, access)], "test")),
            }, new RouteData(), new ActionDescriptor()), []);
        new RequireAccessAttribute(AccessRight.CaseRegistration, AccessRight.CaseAdjustment).OnAuthorization(context);
        Assert.Equal(allowed, context.Result is null);
    }

    [Fact]
    public void Document_names_cannot_leave_the_root()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(root, "ScannedDoc", "AQ1"));
        File.WriteAllText(Path.Combine(root, "ScannedDoc", "AQ1", "a.pdf"), "x");
        File.WriteAllText(Path.Combine(root, "secret.txt"), "x");
        var files = new CaseFilesService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Documents:RootPath"] = root,
        }).Build(), NullLogger<CaseFilesService>.Instance);

        Assert.Equal("a.pdf", Assert.Single(files.ListCase("AQ1")["scanned"]).Name);
        Assert.NotNull(files.CasePath("scanned", "AQ1", "a.pdf"));
        Assert.Null(files.CasePath("scanned", "AQ1", "..\\..\\secret.txt"));
        Assert.Null(files.CasePath("scanned", "..", "secret.txt"));
        Assert.Null(files.CasePath("other", "AQ1", "a.pdf"));
        Directory.Delete(root, true);
    }
}

/// <summary>Runs against the development databases when GENISIS_TEST_MAINDB is set. Registers a case and adjusts it in
/// one transaction that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class CaseAdjustmentDatabaseTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private static readonly string? Maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");

    private static DbConnections Db() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:MainDb"] = MainDb,
        ["ConnectionStrings:Maintenance"] = Maintenance ?? MainDb,
    }).Build());

    [Fact]
    public async Task Update_rewrites_case_tables_and_rolls_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = Db();
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var hospital = await conn.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(HOSCode) FROM dbo.HOS WHERE HOSStatus <> 'P' AND HOSName NOT LIKE '%CLINIC%' AND HOSName NOT LIKE '%HOSPITAL' ORDER BY HOSCode");
        var registration = new CaseRegistrationService(db, NullLogger<CaseRegistrationService>.Instance);

        await using var tx = (Microsoft.Data.SqlClient.SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var reg = CaseRegistrationRulesTests.Valid();
        reg.HospitalCode = hospital;
        var caseNumber = await registration.SaveAsync(conn, tx, reg, "GENISIS");

        var r = CaseAdjustmentRulesTests.Valid();
        r.CaseNumber = caseNumber;
        r.HospitalCode = hospital;
        r.LinkCaseNo = "LINK1";
        r.Remarks = ["adjusted\nnote", "", "page three"];
        r.FinalDiagnoses = [new("DENGUE FEVER", DateTime.Today, "A90")];
        r.Claimability = "RR";
        r.RepudiationCodes = ["RC1 - test"];
        r.RepDocument = "D01 - doc";
        r.RepPersonnel = "P01";
        r.PendingReason = "awaiting bill";
        await CaseAdjustmentService.UpdateAsync(conn, tx, r, "GENISIS");

        Assert.Equal("LINK1", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(LINKCASNo) FROM dbo.CAS WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("RR", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(CASClaimability) FROM dbo.CAS WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("adjusted~\nnote", await conn.ExecuteScalarAsync<string>("SELECT CASORemarks FROM dbo.CASRemarks WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("page three", await conn.ExecuteScalarAsync<string>("SELECT CASRemarks FROM dbo.CASRemarkThree WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("DENGUE FEVER", await conn.ExecuteScalarAsync<string>("SELECT DIAFinalDiag1 FROM dbo.DIAFinal WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("RC1", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(CASREPCode1) FROM dbo.CASRepudiation WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("D01", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(CASDocument) FROM dbo.CASRepudiation WHERE CASNumber = @caseNumber", new { caseNumber }, tx));
        Assert.Equal("LINK1", await conn.ExecuteScalarAsync<string>($"SELECT RTRIM(LINKCASNo) FROM {maintenanceDb}.dbo.CASCrossReference WHERE CASNumber = @caseNumber", new { caseNumber }, tx));

        var closed = CaseAdjustmentRulesTests.Valid();
        closed.CaseNumber = caseNumber;
        await conn.ExecuteAsync("UPDATE dbo.CAS SET CASStatus = 'C' WHERE CASNumber = @caseNumber", new { caseNumber }, tx);
        var error = await Assert.ThrowsAsync<CaseRegistrationException>(() => CaseAdjustmentService.UpdateAsync(conn, tx, closed, "GENISIS"));
        Assert.Equal("Closed or deleted cases cannot be updated.", error.Message);

        await tx.RollbackAsync();
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.CAS WHERE CASNumber = @caseNumber", new { caseNumber }));
    }

    [Fact]
    public async Task Navigation_and_lists_read_real_cases()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = Db();
        await using var conn = db.MainDb();
        var cas = await conn.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(CASENo) FROM genisis.Maint_CASLG l WHERE EXISTS (SELECT 1 FROM dbo.CAS c WHERE c.CASNumber = l.CASENo) ORDER BY LGDate DESC");
        if (cas is null) return;
        var service = new CaseAdjustmentService(db, new CaseRegistrationService(db, NullLogger<CaseRegistrationService>.Instance),
            NullLogger<CaseAdjustmentService>.Instance);

        var view = await service.LoadAsync(cas, "NOBODY");
        Assert.NotNull(view);
        Assert.True(view.HasLg);
        Assert.NotEmpty(await service.LgListAsync(cas));
        var next = await service.NavigateAsync(cas, next: true);
        Assert.True(next is null || string.CompareOrdinal(next, cas) > 0);
        var prev = await service.NavigateAsync(cas, next: false);
        Assert.True(prev is null || string.CompareOrdinal(prev, cas) < 0);
        Assert.Null(await service.LoadAsync("NO-SUCH-CASE", "NOBODY"));
    }
}
