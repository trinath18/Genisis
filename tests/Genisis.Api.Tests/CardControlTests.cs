using Dapper;
using Genisis.Api.CardControl;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class CardControlTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    private static CardControlUpdate Valid() => new()
    {
        SendingMode = "by post", SentDate = new DateTime(2026, 1, 5), Items = [new CardControlItem { MembershipNo = "X", CoverId = "00" }],
    };

    [Fact]
    public void Update_messages_follow_the_form_order()
    {
        Assert.Null(CardControlValidator.ValidateUpdate(Valid()));
        var u = Valid(); u.SendingMode = "Fax"; u.SentDate = null;
        Assert.Equal(CardControlMessages.EnterSendingMode, CardControlValidator.ValidateUpdate(u));
        u = Valid(); u.SentDate = null;
        Assert.Equal(CardControlMessages.EnterSentDate, CardControlValidator.ValidateUpdate(u));
        u = Valid(); u.Items = [new CardControlItem { MembershipNo = " ", CoverId = "00" }];
        Assert.Equal(CardControlMessages.SelectRecord, CardControlValidator.ValidateUpdate(u));
        u = Valid(); u.DocReference = new string('x', 51);
        Assert.NotNull(CardControlValidator.ValidateUpdate(u));
        Assert.Equal(SendingModes.ByPost, SendingModes.Normalize(" BY POST "));
        Assert.Equal(CardControlMessages.InvalidFilterMode, CardControlValidator.ValidateSearch(new CardControlSearch { SendingMode = "Fax" }));
        Assert.Null(CardControlValidator.ValidateSearch(new CardControlSearch()));
    }

    [Fact]
    public void Covered_persons_follow_their_family_principal()
    {
        var principals = new List<PrincipalRow>
        {
            new() { MembershipNo = "F1", InsuredType = "F", BordxDate = new DateTime(2025, 1, 1), BatchNo = 3, PolicyNo = "P1" },
            new() { MembershipNo = "I1", InsuredType = "I" },
        };
        var covered = new List<CoveredRow>
        {
            new() { MembershipNo = "F1", CoverId = "01", Name = "WIFE", SendingMode = "By Hand" },
            new() { MembershipNo = "I1", CoverId = "01", Name = "IGNORED" },
        };

        var result = CardControlService.Listing(principals, covered, truncated: false);

        Assert.Equal(["F1/00", "F1/01", "I1/00"], result.Rows.Select(r => $"{r.MembershipNo}/{r.CoverId}"));
        Assert.Equal((2, 1), (result.TotalPrincipal, result.TotalSupplementary));
        var wife = result.Rows[1];
        Assert.Equal((new DateTime(2025, 1, 1), 3, "P1", "By Hand"), (wife.BordxDate!.Value, wife.BatchNo!.Value, wife.PolicyNo, wife.SendingMode));
    }

    [Fact]
    public async Task Update_of_principal_and_covered_person_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var family = await conn.QueryFirstOrDefaultAsync<(string Number, string CoverId)?>("""
            SELECT TOP (1) RTRIM(m.MBMNumber), RTRIM(c2.MBMCCoverID) FROM dbo.MBM m
            JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
            JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = m.MBMNumber
            JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
            WHERE m.INSCode IN ('F', 'H') ORDER BY m.MBMBordxDate DESC
            """, transaction: tx);
        if (family is not { } member) return;

        var update = new CardControlUpdate
        {
            SendingMode = "courier", SentDate = new DateTime(2026, 1, 5), DocReference = " DOC-1 ",
            Items = [new() { MembershipNo = member.Number, CoverId = "00" }, new() { MembershipNo = member.Number, CoverId = member.CoverId }],
        };
        var result = await CardControlService.UpdateAsync(conn, tx, update);

        Assert.Equal((CardControlMessages.Updated, 2), (result.Message, result.Updated));
        var principal = await conn.QuerySingleAsync<(string Mode, DateTime Date, string DocRef)>(
            "SELECT MBMSendingMode, MBMCardDate, MBMDOCRef FROM dbo.MBMTwo WHERE MBMNumber = @Number", new { member.Number }, tx);
        Assert.Equal(("Courier", new DateTime(2026, 1, 5), "DOC-1"), principal);
        var person = await conn.QuerySingleAsync<(string Mode, DateTime Date)>(
            "SELECT MBMSendingMode, MBMCardDate FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @Number AND MBMCCoverID = @CoverId", new { member.Number, member.CoverId }, tx);
        Assert.Equal(("Courier", new DateTime(2026, 1, 5)), person);

        using var grid = await conn.QueryMultipleAsync("genisis.CardControl_Search",
            new { numberFrom = member.Number, numberTo = member.Number, mode = "Courier", limit = 10 }, tx, commandType: System.Data.CommandType.StoredProcedure);
        var listing = CardControlService.Listing((await grid.ReadAsync<PrincipalRow>()).ToList(), (await grid.ReadAsync<CoveredRow>()).ToList(), false);
        Assert.Equal(1, listing.TotalPrincipal);
        Assert.Contains(listing.Rows, r => r.CoverId == member.CoverId && r.SendingMode == "Courier");
        await tx.RollbackAsync();
    }
}
