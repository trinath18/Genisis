using Dapper;
using Genisis.Api.Reinstatement;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class ReinstatementTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    private static ReinstatementRequest Valid() => new()
    {
        EndtBordxDate = new DateTime(2025, 3, 1), BatchNo = "12", ReinstateDate = new DateTime(2025, 3, 1),
        LapsedDate = new DateTime(2025, 1, 1), ExpiryDate = new DateTime(2025, 12, 31),
    };

    [Fact]
    public void Request_messages_follow_the_form_order()
    {
        Assert.Null(ReinstatementValidator.ValidateRequest(Valid()));
        var r = Valid(); r.EndtBordxDate = null; r.BatchNo = null;
        Assert.Equal(ReinstatementMessages.EnterEndtBordxDate, ReinstatementValidator.ValidateRequest(r));
        r = Valid(); r.BatchNo = " ";
        Assert.Equal(ReinstatementMessages.EnterBatchNo, ReinstatementValidator.ValidateRequest(r));
        r.BatchNo = "123456";
        Assert.Equal(ReinstatementMessages.InvalidBatchNo, ReinstatementValidator.ValidateRequest(r));
        r.BatchNo = "1A";
        Assert.Equal(ReinstatementMessages.InvalidBatchNo, ReinstatementValidator.ValidateRequest(r));
        r = Valid(); r.ReinstateDate = null;
        Assert.Equal(ReinstatementMessages.EnterReinstateDate, ReinstatementValidator.ValidateRequest(r));
        r = Valid(); r.LapsedDate = null;
        Assert.Equal(ReinstatementMessages.EnterLapsedDate, ReinstatementValidator.ValidateRequest(r));
        r = Valid(); r.ExpiryDate = null;
        Assert.Equal(ReinstatementMessages.EnterExpiryDate, ReinstatementValidator.ValidateRequest(r));
    }

    [Theory]
    [InlineData("C", null)]
    [InlineData(" c ", null)]
    [InlineData("A", ReinstatementMessages.StillActive)]
    [InlineData("S", ReinstatementMessages.StillActive)]
    [InlineData(null, ReinstatementMessages.StillActive)]
    public void Only_cancelled_memberships_can_be_reinstated(string? status, string? expected) =>
        Assert.Equal(expected, ReinstatementValidator.Blocked(new ReinstatementMember { Status = status }));

    [Fact]
    public void Fees_follow_member_call_fees()
    {
        var lapsed = new DateTime(2025, 7, 1); var expiry = new DateTime(2025, 12, 31);
        // 184 days of 365.
        Assert.Equal(184m, ReinstatementRules.McoFee(365, true, lapsed, expiry));
        Assert.Equal(100.46m, ReinstatementRules.McoFee(100.456m, false, lapsed, expiry));
        Assert.Equal(50.41m, ReinstatementRules.McoFee(100, true, lapsed, expiry));
        Assert.Equal(50m, ReinstatementRules.RoundMcoFee(50.41m, true));
        Assert.Equal(50.41m, ReinstatementRules.RoundMcoFee(50.41m, false));
        Assert.Equal(2m, ReinstatementRules.RoundMcoFee(2.5m, true));
        Assert.Equal(50.41m, ReinstatementRules.Premium(100, lapsed, expiry));

        Assert.Equal(10, ReinstatementRules.McoRate("A", 10, 5));
        Assert.Equal(5, ReinstatementRules.McoRate("c", 10, 5));
        Assert.Equal(0, ReinstatementRules.McoRate("X", 10, 5));
        Assert.Equal(0, ReinstatementRules.McoRate("A", null, 5));
        Assert.Equal(90, ReinstatementRules.PremiumRate(true, "F", 100, 90));
        Assert.Equal(100, ReinstatementRules.PremiumRate(false, "F", 100, 90));
        Assert.Equal(100, ReinstatementRules.PremiumRate(true, "M", 100, 90));
        Assert.Equal("AH", ReinstatementRules.Prefix(" AHX ", 2));
        Assert.Equal("A", ReinstatementRules.Prefix("A", 2));
        Assert.Equal("", ReinstatementRules.Prefix(null, 2));
    }

    [Fact]
    public async Task Reinstating_a_cancelled_family_member_writes_every_table_and_rolls_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var number = await conn.ExecuteScalarAsync<string?>("""
            SELECT TOP (1) m.MBMNumber FROM dbo.MBM m JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
            WHERE m.MBMStatus = 'C' AND m.INSCode IN ('F', 'H') AND m.PLNCode IS NOT NULL AND t.MBMCancelDate < m.MBMPayorExpDate
              AND EXISTS (SELECT 1 FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = m.MBMNumber)
            ORDER BY t.MBMCancelDate DESC
            """, transaction: tx);
        if (number is null) return;
        var member = (await conn.QuerySingleAsync("SELECT t.MBMCancelDate, m.MBMPayorExpDate FROM dbo.MBM m JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber WHERE m.MBMNumber = @number",
            new { number }, tx));
        var covered = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number", new { number }, tx);
        var request = new ReinstatementRequest
        {
            EndtBordxDate = DateTime.Today, BatchNo = "77", ReinstateDate = DateTime.Today,
            LapsedDate = (DateTime)member.MBMCancelDate, ExpiryDate = (DateTime)member.MBMPayorExpDate,
        };

        var result = await ReinstatementService.ReinstateAsync(conn, tx, number, request, "TEST");

        Assert.NotNull(result);
        Assert.Equal(ReinstatementMessages.Reinstated, result.Message);
        Assert.Equal(1 + covered, result.Covers.Count);
        Assert.Equal("00", result.Covers[0].CoverId);
        Assert.Equal("A", (await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMStatus) FROM dbo.MBM WHERE MBMNumber = @number", new { number }, tx)));
        Assert.Equal(request.ExpiryDate, await conn.ExecuteScalarAsync<DateTime>("SELECT MBMFinalExpiryDate FROM dbo.MBM WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal("A", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMStatus) FROM genisis.Maint_MBMCrossReference WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND MBMCStatus <> 'A'", new { number }, tx));

        var rows = (await conn.QueryAsync<(string CoverId, decimal Mco, decimal Premium, string Batch)>(
            "SELECT RTRIM(MBMCoverID), MBMMCOFee, MBMPremium, RTRIM(MBMEndtBordxBatch) FROM dbo.MBMReinstatement WHERE MBMNumber = @number AND LastUpdateUSR = 'TEST' ORDER BY MBMIndex",
            new { number }, tx)).ToList();
        Assert.Equal(result.Covers.Select(c => (c.CoverId, c.McoFee, c.Premium, "77")), rows);
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.MBMAmendHistory WHERE MBMNumber = @number AND MBMAmendEdtType = 'R' AND MBMAmendUser = 'TEST' AND MBMAEndtBatchNo = 77",
            new { number }, tx));

        var again = await Assert.ThrowsAsync<ReinstatementException>(() => ReinstatementService.ReinstateAsync(conn, tx, number, request, "TEST"));
        Assert.Equal(ReinstatementMessages.StillActive, again.Message);
        Assert.Null(await ReinstatementService.ReinstateAsync(conn, tx, "NO-SUCH-MEMBER", request, "TEST"));
        await tx.RollbackAsync();
    }
}
