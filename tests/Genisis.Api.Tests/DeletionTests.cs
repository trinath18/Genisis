using Dapper;
using Genisis.Api.Deletion;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class DeletionTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Messages_follow_the_form_order()
    {
        Assert.Equal(DeletionMessages.EnterHealthPlan, DeletionValidator.Validate(new DeletionCriteria()));
        Assert.Equal(DeletionMessages.EnterPayorCode, DeletionValidator.Validate(new DeletionCriteria { HealthCode = "N" }));
        Assert.Equal(DeletionMessages.EnterOption, DeletionValidator.Validate(new DeletionCriteria { HealthCode = "N", PayorCode = "AH", Option = "X" }));
        var c = new DeletionCriteria { HealthCode = "N", PayorCode = "AH", Option = "b", ReceiptDate = DateTime.Today };
        Assert.Equal(DeletionMessages.EnterReceiptDateOrBatch, DeletionValidator.Validate(c));
        c.BatchNo = 1;
        Assert.Null(DeletionValidator.Validate(c));
        c = new DeletionCriteria { HealthCode = "N", PayorCode = "AH", Option = "D", BordxDateFrom = DateTime.Today };
        Assert.Equal(DeletionMessages.EnterBordxDate, DeletionValidator.Validate(c));
        c = new DeletionCriteria { HealthCode = "N", PayorCode = "AH", Option = "M", NumberFrom = "A" };
        Assert.Equal(DeletionMessages.EnterMembershipNo, DeletionValidator.Validate(c));
        c.NumberTo = "B";
        Assert.Null(DeletionValidator.Validate(c));
    }

    [Fact]
    public void Listing_counts_like_the_desktop()
    {
        var principals = new List<DeletionPrincipal>
        {
            new() { MembershipNo = "F1", MemberType = "P", InsuredType = "H" },
            new() { MembershipNo = "Q1", MemberType = "Q", InsuredType = "I" },
        };
        var covered = new List<DeletionCovered> { new() { MembershipNo = "F1", CoverId = "01", MemberType = "C" }, new() { MembershipNo = "Q1", CoverId = "01" } };

        var result = DeletionService.Listing(principals, covered, truncated: false);

        Assert.Equal(["F1/00", "F1/01", "Q1/00"], result.Rows.Select(r => $"{r.MembershipNo}/{r.CoverId}"));
        // Principals of member type Q are deleted but not counted, as in the desktop.
        Assert.Equal((1, 1, 2, DeletionMessages.Listed), (result.TotalPrincipal, result.TotalSupplementary, result.Memberships, result.Message));
        Assert.Equal(DeletionMessages.NoRecord, DeletionService.Listing([], [], false).Message);
    }

    [Fact]
    public async Task Delete_archives_and_removes_a_family_membership_and_rolls_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var member = await conn.QueryFirstOrDefaultAsync<(string Number, string Health, string Payor)?>("""
            SELECT TOP (1) RTRIM(m.MBMNumber), RTRIM(m.HLTCode), RTRIM(m.PAYCode) FROM dbo.MBM m
            WHERE m.INSCode IN ('F', 'H') AND m.HLTCode IS NOT NULL AND m.PAYCode IS NOT NULL
              AND EXISTS (SELECT 1 FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = m.MBMNumber)
              AND EXISTS (SELECT 1 FROM dbo.MBMOthers o WHERE o.MBMNumber = m.MBMNumber)
            ORDER BY m.MBMBordxDate DESC
            """, transaction: tx);
        if (member is not { } m) return;
        var criteria = new DeletionCriteria { HealthCode = m.Health, PayorCode = m.Payor, Option = "M", NumberFrom = m.Number, NumberTo = m.Number };
        int Count(string sql) => conn.ExecuteScalar<int>(sql, new { m.Number }, tx);
        var covered = Count("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @Number");

        var list = await DeletionService.ListAsync(conn, tx, criteria);
        Assert.Equal(1, list.Memberships);
        Assert.Equal(m.Number, list.Rows[0].MembershipNo);

        try
        {
            var result = await DeletionService.DeleteAsync(conn, tx, criteria);
            Assert.Equal((DeletionMessages.Deleted, 1), (result.Message, result.Memberships));
            Assert.Equal(0, Count("SELECT COUNT(*) FROM dbo.MBM WHERE MBMNumber = @Number"));
            Assert.Equal(0, Count("SELECT COUNT(*) FROM dbo.MBMOthers WHERE MBMNumber = @Number"));
            Assert.Equal(0, Count("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @Number"));
            Assert.Equal(1, Count("SELECT COUNT(*) FROM genisis.Arch_MBM WHERE MBMNumber = @Number AND MBMStatus = 'D' AND MBMDataStatus = 'D'"));
            Assert.Equal(1, Count("SELECT COUNT(*) FROM genisis.Arch_MBMOthers WHERE MBMNumber = @Number"));
            Assert.Equal(covered, Count("SELECT COUNT(*) FROM genisis.Arch_MBMCoveredPersons WHERE MBMCNumber = @Number"));
            Assert.Equal(0, (await DeletionService.DeleteAsync(conn, tx, criteria)).Memberships);
        }
        catch (DeletionException e) when (e.Message == DeletionMessages.ArchiveUnavailable)
        {
            // This database's archive tables have a disabled primary key: the delete is refused and nothing is removed.
            Assert.Equal(1, Count("SELECT COUNT(*) FROM dbo.MBM WHERE MBMNumber = @Number"));
        }
        await tx.RollbackAsync();
    }
}
