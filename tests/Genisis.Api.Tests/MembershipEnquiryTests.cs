using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

/// <summary>Membership > Enquiry against the database: the search finds members without an MBMOthers or MBMTwo row, and Pre / Next step through dbo.MBM as the desktop does.</summary>
public class MembershipEnquiryDatabaseTests
{
    private static async Task<SqlConnection> OpenAsync()
    {
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb!);
        var conn = new SqlConnection(TestDb.MainDb);
        await conn.OpenAsync();
        return conn;
    }

    private static Task<List<dynamic>> SearchAsync(SqlConnection conn, string q) =>
        conn.ProcQueryAsync<dynamic>("genisis.Membership_Search", new { max = 200, by = "number", q, prefix = SqlText.EscapeLike(q) + "%" });

    [Fact]
    public async Task The_search_finds_a_member_without_an_MBMOthers_or_MBMTwo_row()
    {
        if (TestDb.MainDb is null) return;
        await using var conn = await OpenAsync();
        foreach (var table in new[] { "MBMOthers", "MBMTwo" })
        {
            var number = await conn.QueryFirstOrDefaultAsync<string>(
                $"SELECT TOP (1) m.MBMNumber FROM dbo.MBM m WHERE NOT EXISTS (SELECT 1 FROM dbo.{table} x WHERE x.MBMNumber = m.MBMNumber) ORDER BY m.MBMNumber");
            if (number is null) continue;
            var rows = await SearchAsync(conn, number.Trim());
            Assert.Contains(rows, r => ((string)r.MBMNumber).Trim() == number.Trim());
        }
    }

    [Fact]
    public async Task Previous_and_next_are_the_neighbouring_numbers_and_stop_at_the_ends()
    {
        if (TestDb.MainDb is null) return;
        await using var conn = await OpenAsync();
        var numbers = (await conn.QueryAsync<string>("SELECT TOP (3) MBMNumber FROM dbo.MBM ORDER BY MBMNumber")).ToList();
        if (numbers.Count < 3) return;
        Assert.Equal(numbers[2], await conn.ProcScalarAsync<string>("genisis.Membership_Adjacent", new { mbmNumber = numbers[1], next = true }));
        Assert.Equal(numbers[0], await conn.ProcScalarAsync<string>("genisis.Membership_Adjacent", new { mbmNumber = numbers[1], next = false }));
        Assert.Null(await conn.ProcScalarAsync<string>("genisis.Membership_Adjacent", new { mbmNumber = numbers[0], next = false }));
        var last = await conn.QuerySingleAsync<string>("SELECT MAX(MBMNumber) FROM dbo.MBM");
        Assert.Null(await conn.ProcScalarAsync<string>("genisis.Membership_Adjacent", new { mbmNumber = last, next = true }));
    }
}
