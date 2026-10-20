using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet calculation engine, step 5b: the limit before a worksheet.</summary>
public class WorksheetBalanceLimitRulesTests
{
    [Theory]
    [InlineData(103446.35, 626.00, 104072.35)]
    [InlineData(20000, 0, 20000)]
    [InlineData(0, 11444.25, 11444.25)]
    [InlineData(-5, 100, 95)]
    public void The_limit_before_a_worksheet_is_the_available_limit_plus_what_the_case_has_approved(double balance, double approved, double expected) =>
        Assert.Equal((decimal)expected, BalanceLimitRules.PreviousBalance((decimal)balance, (decimal)approved));

    [Fact]
    public void A_covered_person_without_a_limit_counts_as_nothing_available() => Assert.Equal(250m, BalanceLimitRules.PreviousBalance(null, 250m));
}

/// <summary>The available limit read from the member, against the limit before the worksheet that the desktop saved.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetBalanceLimitOracleTests(ITestOutputHelper output)
{
    /// <summary>
    /// For the latest version of the latest case of each patient (a later case could have changed the limit), about one in thirty: the available limit plus the version's approved total against
    /// the saved previous balance. On the first 16,000 worksheets it was 76 per cent for principals and 83 to 85 for dependants; on 70,000 it is 69 per cent. The rule does not model what changes a limit after the claim, so the bar is low.
    /// </summary>
    [Fact]
    public async Task The_available_limit_plus_the_approved_total_reproduces_the_limit_the_desktop_saved_before_the_worksheet()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, decimal Saved, decimal Approved)>(
            """
            SELECT RTRIM(w.CASNumber), w.ClaimVersion, w.CLMPreviousBalance, ISNULL(w.CLMTotalApproved, 0)
            FROM genisis.WS_Claim w JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = w.CASNumber AND v.ClaimVersion = w.ClaimVersion
            WHERE w.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND w.CLMPreviousBalance IS NOT NULL AND v.CASDateAdmitted IS NOT NULL
              AND ABS(CHECKSUM(w.CASNumber, w.ClaimVersion)) % 30 = 0
              AND w.ClaimVersion = (SELECT MAX(x.ClaimVersion) FROM genisis.WS_Claim x WHERE x.CASNumber = w.CASNumber)
              AND NOT EXISTS (SELECT 1 FROM dbo.CAS c2 WHERE c2.MBMNumber = v.MBMNumber AND c2.MBMPatientCovID = v.MBMPatientCovID AND c2.CASDateAdmitted > v.CASDateAdmitted)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(sample.Count >= 150, $"only {sample.Count} versions in the sample");

        var service = new BalanceLimitService(TestDb.Sessions(), new BalanceLimitRepository());
        int found = 0, same = 0;
        foreach (var v in sample)
        {
            if (await service.BalanceAsync(v.Cas, v.Version) is not { Balance: not null } limit) continue;
            found++;
            same += Math.Abs(BalanceLimitRules.PreviousBalance(limit.Balance, v.Approved) - v.Saved) < 0.015m ? 1 : 0;
        }
        output.WriteLine($"{sample.Count} versions: balance found for {found}, previous balance reproduced for {same} ({100.0 * same / Math.Max(found, 1):0.0}%)");
        Assert.True(found >= sample.Count * 0.9, $"balance found for {found} of {sample.Count}");
        Assert.True(same >= found * 0.62, $"previous balance reproduced for {same} of {found}");
    }
}
