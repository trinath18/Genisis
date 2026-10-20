using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Adjustment;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet > Worksheet Adjustment, step 1: the Update button (payment details and remarks).</summary>
public class WorksheetAdjustmentTests
{
    private static UpdatePaymentRequest Complete() => new()
    {
        PayTo = "Member", Bank = "AFFIN BANK", AccountNo = "1234567", PaymentMode = "BANK-IN", Address1 = "1 Jalan Satu", PostCode = "50000", City = "Kuala Lumpur", State = "WILAYAH PERSEKUTUAN",
    };

    // ---------------- rules ----------------

    [Theory]
    [InlineData("H")]
    [InlineData(" h ")]
    [InlineData(null)]
    [InlineData("")]
    public void A_worksheet_not_paid_to_the_member_needs_no_payment_details(string? payTo) =>
        Assert.Null(AdjustmentRules.PaymentProblem(payTo, new UpdatePaymentRequest()));

    [Fact]
    public void A_worksheet_paid_to_the_member_with_everything_given_is_fine() => Assert.Null(AdjustmentRules.PaymentProblem("M", Complete()));

    [Fact]
    public void A_bank_without_an_account_number_and_an_account_number_without_a_bank_are_refused_first()
    {
        var noAccount = Complete();
        noAccount.AccountNo = "  ";
        Assert.Equal("Please Enter Bank Account Number", AdjustmentRules.PaymentProblem("M", noAccount));
        var noBank = Complete();
        noBank.Bank = null;
        Assert.Equal("Please Enter Bank Name", AdjustmentRules.PaymentProblem("m", noBank));
    }

    [Fact]
    public void The_address_checks_follow_in_the_desktops_order()
    {
        var r = new UpdatePaymentRequest();
        Assert.Equal("Please Enter Member's Address", AdjustmentRules.PaymentProblem("M", r));
        r.Address1 = "x";
        Assert.Equal("Please Enter Post Code", AdjustmentRules.PaymentProblem("M", r));
        r.PostCode = "50000";
        Assert.Equal("Please Enter City", AdjustmentRules.PaymentProblem("M", r));
        r.City = "KL";
        Assert.Equal("Please Enter State", AdjustmentRules.PaymentProblem("M", r));
        r.State = "SELANGOR";
        Assert.Null(AdjustmentRules.PaymentProblem("M", r));
    }

    [Fact]
    public void A_member_worksheet_without_any_bank_details_still_needs_the_address_but_not_a_bank()
    {
        var r = Complete();
        r.Bank = null;
        r.AccountNo = null;
        Assert.Null(AdjustmentRules.PaymentProblem("M", r));
    }

    // ---------------- validator ----------------

    [Fact]
    public void The_validator_holds_text_to_the_database_lengths_and_the_payment_mode_list()
    {
        var validator = new UpdatePaymentRequestValidator();
        Assert.True(validator.Validate(Complete()).IsValid);
        Assert.True(validator.Validate(new UpdatePaymentRequest()).IsValid);

        var bad = Complete();
        bad.PostCode = "1234567";
        Assert.Equal("Post Code can have at most 6 characters.", validator.Validate(bad).Errors[0].ErrorMessage);
        bad = Complete();
        bad.PaymentMode = "CASH";
        Assert.Equal("Payment Mode must be BY POST or BANK-IN.", validator.Validate(bad).Errors[0].ErrorMessage);
        bad = Complete();
        bad.WorksheetRemarks = new string('x', 201);
        Assert.Equal("Worksheet remarks can have at most 200 characters.", validator.Validate(bad).Errors[0].ErrorMessage);
        bad = Complete();
        bad.Note = new string('x', 100) + "   ";
        Assert.True(validator.Validate(bad).IsValid, "trailing spaces are trimmed before the length is checked");
    }

    // ---------------- service ----------------

    private sealed class FakeRepository : IAdjustmentRepository
    {
        public string? PayTo = "H";
        public int Result = StoredProcedures.Ok;
        public int Updates;
        public UpdatePaymentRequest? Saved;

        public Task<string?> PayToAsync(IDbSession session, string caseNo, int version) => Task.FromResult(PayTo);
        public Task<List<string>> BanksAsync(IDbSession session) => Task.FromResult(new List<string> { "AFFIN BANK" });
        public Task<List<string>> StatesAsync(IDbSession session) => Task.FromResult(new List<string> { "SELANGOR" });

        public Task<int> UpdatePaymentAsync(IDbSession session, string caseNo, int version, UpdatePaymentRequest request)
        {
            Updates++;
            Saved = request;
            return Task.FromResult(Result);
        }
    }

    private static WorksheetAdjustmentService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<WorksheetAdjustmentService>.Instance);

    [Fact]
    public async Task The_lookups_list_banks_states_and_the_two_payment_modes()
    {
        var lookups = await Service(new FakeSessions(), new FakeRepository()).LookupsAsync();
        Assert.Equal(["AFFIN BANK"], lookups.Banks);
        Assert.Equal(["SELANGOR"], lookups.States);
        Assert.Equal(["BY POST", "BANK-IN"], lookups.PaymentModes);
    }

    [Fact]
    public async Task Update_saves_and_commits_when_the_details_pass()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository { PayTo = "M" };
        await Service(sessions, repository).UpdatePaymentAsync(" AH00001134 ", 2, Complete(), "TESTER");
        Assert.Equal(1, repository.Updates);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Update_saves_nothing_when_a_worksheet_paid_to_the_member_lacks_its_address()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository { PayTo = "M" };
        var request = Complete();
        request.Address1 = null;
        var error = await Assert.ThrowsAsync<WorksheetException>(() => Service(sessions, repository).UpdatePaymentAsync("AH1", 1, request, "TESTER"));
        Assert.Equal("Please Enter Member's Address", error.Message);
        Assert.Equal(0, repository.Updates);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_missing_version_is_record_not_found_and_a_bad_version_number_is_refused()
    {
        var sessions = new FakeSessions();
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service(sessions, new FakeRepository { Result = StoredProcedures.NotFound }).UpdatePaymentAsync("X", 1, new UpdatePaymentRequest(), "TESTER"));
        Assert.Equal("Record not found!", error.Message);
        Assert.False(sessions.Last.Committed);
        var refused = await Assert.ThrowsAsync<WorksheetException>(() => Service(new FakeSessions(), new FakeRepository()).UpdatePaymentAsync("X", 0, new UpdatePaymentRequest(), "TESTER"));
        Assert.Equal("Please select a version nombor!", refused.Message);
    }
}

/// <summary>The Update button against the development databases. Every write happens in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetAdjustmentDatabaseTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    private static async Task<(string Cas, int Version)> WorksheetWithOthersRow(IDbSession session) =>
        await session.Connection.QueryFirstAsync<(string, int)>(
            """
            SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c
            WHERE EXISTS (SELECT 1 FROM genisis.WS_ClaimOthers o WHERE o.CASNumber = c.CASNumber AND o.CLaimVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_CASInvoice i WHERE i.CASNumber = c.CASNumber AND i.INVWSversion = CAST(c.ClaimVersion AS nvarchar(10)) AND i.InvPayTo = N'H')
            ORDER BY c.CASNumber DESC, c.ClaimVersion
            """, transaction: session.Transaction);

    [Fact]
    public async Task The_lookups_come_from_the_maintenance_and_main_databases()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = new WorksheetAdjustmentService(TestDb.Sessions(), new AdjustmentRepository(), NullLogger<WorksheetAdjustmentService>.Instance);
        var lookups = await service.LookupsAsync();
        Assert.Contains("AFFIN BANK", lookups.Banks);
        Assert.True(lookups.States.Count >= 10);
        Assert.Equal(lookups.Banks.Distinct().Count(), lookups.Banks.Count);
    }

    [Fact]
    public async Task Update_writes_the_claim_and_the_others_row_and_a_missing_version_is_reported()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new AdjustmentRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var (cas, version) = await WorksheetWithOthersRow(session);
        var args = new { cas, ver = version };
        var before = await session.Connection.QueryFirstAsync<DateTime?>("SELECT CLMLastDocDate FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args, session.Transaction);

        Assert.Equal("H", await repository.PayToAsync(session, cas, version));
        var request = new UpdatePaymentRequest
        {
            PayTo = " Test Payee ", GpName = "Dr Test", Bank = "AFFIN BANK", AccountNo = "9876543210", PaymentMode = "BY POST", Attention = "Accounts",
            Address1 = "1 Jalan Satu", Address2 = "Taman Dua", Address3 = "", PostCode = "50450", City = "Kuala Lumpur", State = "WILAYAH PERSEKUTUAN",
            Note = "a note", WorksheetRemarks = "ws remarks", CreditDebitNoteRemarks = "cd remarks",
        };
        Assert.Equal(StoredProcedures.Ok, await repository.UpdatePaymentAsync(session, cas, version, request));

        var claim = await session.Connection.QueryFirstAsync<(string, string, string, string, DateTime?)>(
            "SELECT RTRIM(CLMPayTo), RTRIM(CLMAccountNo), RTRIM(CLMBank), RTRIM(CLMGPName), CLMLastDocDate FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args, session.Transaction);
        Assert.Equal(("Test Payee", "9876543210", "AFFIN BANK", "Dr Test"), (claim.Item1, claim.Item2, claim.Item3, claim.Item4));
        Assert.Equal(before, claim.Item5);

        var others = await session.Connection.QueryFirstAsync<(string, string, string, string, string, string, string, string, string, string, string)>(
            """
            SELECT RTRIM(CLMWSRemarks), RTRIM(CLMDCNoteRemarks), RTRIM(MBMAdd1), RTRIM(MBMAdd2), RTRIM(MBMAdd3), RTRIM(MBMPostCode), RTRIM(MBMCity), RTRIM(MBMState), RTRIM(CLMNote), RTRIM(CLMPayMode), RTRIM(CLMAttention)
            FROM genisis.WS_ClaimOthers WHERE CASNumber = @cas AND CLaimVersion = @ver
            """, args, session.Transaction);
        Assert.Equal(("ws remarks", "cd remarks", "1 Jalan Satu", "Taman Dua", "", "50450", "Kuala Lumpur", "WILAYAH PERSEKUTUAN", "a note", "BY POST", "Accounts"),
            (others.Item1, others.Item2, others.Item3, others.Item4, others.Item5, others.Item6, others.Item7, others.Item8, others.Item9, others.Item10, others.Item11));

        request.LastDocumentDate = new DateTime(2026, 10, 1, 15, 30, 0);
        Assert.Equal(StoredProcedures.Ok, await repository.UpdatePaymentAsync(session, cas, version, request));
        Assert.Equal(new DateTime(2026, 10, 1), await session.Connection.QueryFirstAsync<DateTime>("SELECT CLMLastDocDate FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args, session.Transaction));

        Assert.Equal(StoredProcedures.NotFound, await repository.UpdatePaymentAsync(session, "NO SUCH CASE", 1, request));
    }

    [Fact]
    public async Task The_service_refuses_a_missing_version_and_leaves_the_data_alone()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = new WorksheetAdjustmentService(TestDb.Sessions(), new AdjustmentRepository(), NullLogger<WorksheetAdjustmentService>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdatePaymentAsync("NO SUCH CASE", 1, new UpdatePaymentRequest(), "TESTER"));
    }
}
