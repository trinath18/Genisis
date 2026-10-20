using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Registration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class TempRegistrationTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Previous_membership_becomes_a_renewal_draft()
    {
        var member = new RenewalSourceMember
        {
            MembershipNo = "AXHA1*01", HealthCode = "N", PayorCode = "AX", PolicyNo = "P-OLD", PolicyYear = 3, Name = "ALI", InsuredType = "H",
            PlanCode = "AW13", TakeOver = " ", Address1 = "1 JALAN",
        };
        var covered = new List<RenewalSourceCovered> { new() { CoverId = "01", Name = "SITI", Relationship = "W", DateOfBirth = new DateTime(1990, 1, 1) } };

        var draft = TempRegistrationService.Draft(member, covered);

        var r = draft.Request;
        Assert.Equal(("R", "AXHA1*01", "P-OLD"), (r.Renewal, r.PreviousMembershipNo, r.PreviousPolicyNo));
        Assert.Null(r.PolicyNo);
        Assert.Null(r.PayorEffectiveDate);
        Assert.Null(r.BordxDate);
        Assert.Equal(("ALI", "AW13", "H", "1 JALAN", "N"), (r.Name, r.PlanCode, r.InsuredType, r.Address1, r.TakeOver));
        Assert.Equal(4, draft.PolicyYear);
        Assert.Equal("SITI", Assert.Single(r.CoveredPersons).Name);
        Assert.Equal(1, TempRegistrationService.Draft(new RenewalSourceMember { MembershipNo = "X" }, []).PolicyYear);
    }

    [Fact]
    public async Task A_real_family_membership_loads_with_its_covered_persons()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        string? number;
        int covered;
        await using (var conn = new SqlConnection(MainDb))
        {
            number = await conn.ExecuteScalarAsync<string?>("""
                SELECT TOP (1) RTRIM(m.MBMNumber) FROM dbo.MBM m
                WHERE m.INSCode IN ('F', 'H') AND EXISTS (SELECT 1 FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = m.MBMNumber AND RTRIM(c.MBMCCoverID) <> '00')
                ORDER BY m.MBMBordxDate DESC
                """);
            if (number is null) return;
            covered = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND RTRIM(MBMCCoverID) <> '00'", new { number });
        }
        var db = new DbConnections(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build());

        var draft = await new TempRegistrationService(db).GetDraftAsync(number, CancellationToken.None);

        Assert.NotNull(draft);
        Assert.Equal(number, draft.Request.PreviousMembershipNo);
        Assert.Equal(covered, draft.Request.CoveredPersons.Count);
        Assert.Null(await new TempRegistrationService(db).GetDraftAsync("NO-SUCH-MEMBER", CancellationToken.None));
    }
}
