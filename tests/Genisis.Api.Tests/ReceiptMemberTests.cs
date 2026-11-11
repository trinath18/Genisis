using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Receipt.Member;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Receipt > From Member: the validators and the service, against a fake repository.</summary>
public class ReceiptMemberRulesTests
{
    private sealed class FakeRepository : IReceiptMemberRepository
    {
        public bool Authorised { get; set; }

        public List<ReceiptMemberRow> Rows { get; set; } = [];

        public List<ReceiptMemberClaim> Claim { get; set; } = [new("TA1", 1, -100m, "DTA1", null, null, null, null)];

        public ReceiptMemberReceipt? Row { get; set; }

        public bool Updated { get; set; } = true;

        public List<ReceiptMemberOfficialLine> Official { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<ReceiptMemberRow>> SearchAsync(IDbSession session, ReceiptMemberFilter filter)
        {
            Calls.Add($"search {filter.CaseFrom}-{filter.CaseTo}");
            return Task.FromResult(Rows);
        }

        public Task<bool> AuthorisedAsync(IDbSession session, string userCode) => Task.FromResult(Authorised);

        public Task<List<ReceiptMemberClaim>> ClaimAsync(IDbSession session, string caseNo, int version, string? receiptNo) => Task.FromResult(Claim);

        public Task<int> SaveAsync(IDbSession session, ReceiptMemberSaveRequest receipt, ReceiptMemberItem item, string userCode, bool authorised)
        {
            Calls.Add($"save {item.CaseNo}-{item.Version} {item.Status} {item.PaymentAmt} authorised={authorised}");
            return Task.FromResult(1);
        }

        public Task<ReceiptMemberReceipt?> RowAsync(IDbSession session, int receiptIndex) => Task.FromResult(Row);

        public Task<bool> UpdateAsync(IDbSession session, ReceiptMemberUpdateRequest request, string userCode) => Task.FromResult(Updated);

        public Task<List<ReceiptMemberOfficialLine>> OfficialReceiptAsync(IDbSession session, string receiptNo) => Task.FromResult(Official);
    }

    private static ReceiptMemberService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<ReceiptMemberService>.Instance);

    private static ReceiptMemberItem Item(string cas = "TA1", string status = "F", decimal amount = 100m) => new() { CaseNo = cas, Version = 1, Status = status, PaymentAmt = amount };

    private static ReceiptMemberSaveRequest Request(params ReceiptMemberItem[] items) =>
        new() { ReceiptNo = "R1", ReceiptDate = new DateTime(2026, 10, 30), Amount = 100m, ChequeNo = "C1", Items = [.. items] };

    [Fact]
    public void Save_checks_come_in_the_desktops_order_and_each_claim_is_checked()
    {
        var validator = new ReceiptMemberSaveRequestValidator();
        Assert.Equal("Please Enter Receipt No", validator.FirstError(new ReceiptMemberSaveRequest()));
        Assert.Equal("Please Enter Receipt Date", validator.FirstError(new ReceiptMemberSaveRequest { ReceiptNo = "R1" }));
        Assert.Equal("Please Enter Amount", validator.FirstError(new ReceiptMemberSaveRequest { ReceiptNo = "R1", ReceiptDate = DateTime.Today }));
        Assert.Equal("Please Enter Cheque No", validator.FirstError(new ReceiptMemberSaveRequest { ReceiptNo = "R1", ReceiptDate = DateTime.Today, Amount = 1m }));
        Assert.Equal("Please tick the claims to be received.", validator.FirstError(Request()));
        Assert.Null(validator.FirstError(Request(Item())));
        Assert.Equal("Please enter the amount receiving for the claim.", validator.FirstError(Request(Item(amount: 0m))));
        Assert.Equal("The payment of a claim is Full or Partial.", validator.FirstError(Request(Item(status: "X"))));
        var contra = Item();
        contra.ContraCase = "TA2";
        Assert.Equal("Invalid Contra Case..., Please Keyin Contra Case No. or Contra Version ", validator.FirstError(Request(contra)));
        contra.ContraVersion = "1";
        Assert.Equal("Please Keyin Contra Case Amount ", validator.FirstError(Request(contra)));
        contra.ContraAmt = 5m;
        Assert.Null(validator.FirstError(Request(contra)));
    }

    [Fact]
    public void Update_checks_come_in_the_desktops_order()
    {
        var validator = new ReceiptMemberUpdateRequestValidator();
        Assert.Equal("Please select the record", validator.FirstError(new ReceiptMemberUpdateRequest()));
        Assert.Equal("Please Enter Receipt No!", validator.FirstError(new ReceiptMemberUpdateRequest { ReceiptIndex = 1 }));
        Assert.Equal("Please Enter Receipt Date", validator.FirstError(new ReceiptMemberUpdateRequest { ReceiptIndex = 1, ReceiptNo = "R" }));
        Assert.Equal("Please Enter Cheque No!", validator.FirstError(new ReceiptMemberUpdateRequest { ReceiptIndex = 1, ReceiptNo = "R", ReceiptDate = DateTime.Today }));
        Assert.Equal("Please Enter Cheque Amt!", validator.FirstError(new ReceiptMemberUpdateRequest { ReceiptIndex = 1, ReceiptNo = "R", ReceiptDate = DateTime.Today, ChequeNo = "C" }));
        Assert.Null(validator.FirstError(new ReceiptMemberUpdateRequest { ReceiptIndex = 1, ReceiptNo = "R", ReceiptDate = DateTime.Today, ChequeNo = "C", Amount = 1m }));
    }

    [Fact]
    public async Task Search_needs_a_worksheet_to_scan_totals_the_receipts_and_reports_the_ticking_rule()
    {
        var repository = new FakeRepository { Authorised = true };
        Assert.Equal("Please Enter Worksheet No", new ReceiptMemberFilterValidator().FirstError(new ReceiptMemberFilter { Scan = true }));
        Assert.Null(new ReceiptMemberFilterValidator().FirstError(new ReceiptMemberFilter { Scan = true, CaseFrom = "TA1" }));
        Assert.Equal("Select a Payment Status.", new ReceiptMemberFilterValidator().FirstError(new ReceiptMemberFilter { Status = 9 }));
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new ReceiptMemberFilter { Scan = true, CaseFrom = "TA1" }, "U"))).Message);
        repository.Rows =
        [
            new("DTA1", "P", "TA1", 1, -100m, 40m, 7, "R1", null, 1, 2026, null, null, null, null, null, null, null, null, null, null, null, "M1", "N1", "TA"),
            new("DTA2", null, "TA2", 1, -50m, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "M2", "N2", "TA"),
        ];
        var result = await Service(repository, new FakeSessions()).SearchAsync(new ReceiptMemberFilter { CaseFrom = "TA1" }, "U");
        Assert.Equal((40m, true, false), (result.ReceivedAmount, result.Authorised, result.Truncated));
        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public async Task Save_writes_every_ticked_claim_in_one_transaction_and_tells_the_repository_whether_the_user_is_authorised()
    {
        var repository = new FakeRepository { Authorised = true };
        var sessions = new FakeSessions();
        Assert.Equal(2, await Service(repository, sessions).SaveAsync(Request(Item("TA1"), Item("TA1", "P", 25m)), "U"));
        Assert.Equal(["save TA1-1 F 100 authorised=True", "save TA1-1 P 25 authorised=True"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_user_who_is_not_authorised_cannot_receive_a_claim_that_is_paid_in_full_but_an_authorised_user_can()
    {
        var paid = new List<ReceiptMemberClaim> { new("TA1", 1, -100m, "DTA1", 3, "R0", 100m, "F") };
        var repository = new FakeRepository { Claim = paid };
        var refused = await Assert.ThrowsAsync<ConflictException>(() => Service(repository, new FakeSessions()).SaveAsync(Request(Item()), "U"));
        Assert.Equal("Payment has been made!", refused.Message);
        Assert.Empty(repository.Calls);
        repository.Authorised = true;
        Assert.Equal(1, await Service(repository, new FakeSessions()).SaveAsync(Request(Item()), "U"));
    }

    [Fact]
    public async Task A_claim_that_is_not_a_member_debit_note_is_refused_and_a_missing_claim_is_not_found()
    {
        var repository = new FakeRepository { Claim = [new("TA1", 1, 50m, "DTA1", null, null, null, null)] };
        Assert.Equal("Claim TA1-1 is not a debit note of a member.", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, new FakeSessions()).SaveAsync(Request(Item()), "U"))).Message);
        repository.Claim = [new("TA1", 1, -50m, "CTA1", null, null, null, null)];
        Assert.Equal("Claim TA1-1 is not a debit note of a member.", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, new FakeSessions()).SaveAsync(Request(Item()), "U"))).Message);
        repository.Claim = [];
        Assert.Equal("Claim TA1-1 not found", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SaveAsync(Request(Item()), "U"))).Message);
    }

    [Fact]
    public async Task Edit_loads_a_receipt_row_and_update_changes_it_and_a_row_with_no_payment_is_not_found()
    {
        var repository = new FakeRepository();
        Assert.Equal("Payment hasn't been made!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).ReceiptAsync(5))).Message);
        repository.Row = new ReceiptMemberReceipt("R1", null, 10m, "C1", null, 1, 2026, "TA1", 1, null, null, null, null, null, null, null, null, null, null, null);
        Assert.Equal("R1", (await Service(repository, new FakeSessions()).ReceiptAsync(5)).ReceiptNo);
        var update = new ReceiptMemberUpdateRequest { ReceiptIndex = 5, ReceiptNo = "R1", ReceiptDate = DateTime.Today, ChequeNo = "C", Amount = 1m };
        var sessions = new FakeSessions();
        await Service(repository, sessions).UpdateAsync(update, "U");
        Assert.True(sessions.Last.Committed);
        repository.Updated = false;
        Assert.Equal("Payment hasn't been made!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).UpdateAsync(update, "U"))).Message);
    }

    [Fact]
    public async Task The_official_receipt_needs_a_receipt_number_and_lines()
    {
        var repository = new FakeRepository();
        Assert.Equal("Please Enter or Generate Receipt No", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, new FakeSessions()).OfficialReceiptAsync(" "))).Message);
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).OfficialReceiptAsync("R1"))).Message);
        repository.Official = [new("TA1", "1", "R1", null, "N", "C1", 10m, "DTA1", "M", "P")];
        Assert.Single(await Service(repository, new FakeSessions()).OfficialReceiptAsync("R1"));
    }
}

/// <summary>Receipt From Member against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class ReceiptMemberDatabaseTests
{
    private static ReceiptMemberRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string Checksum = "SELECT (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(ReceiptIndex, ReceiptNo, PVRecMBMStatus, ReceiptAmt)), 0) + COUNT(*) FROM genisis.Pay_ReceiptfrMBM) + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CONIndex, RefNo, ContraCaseNo, ContraCaseAmt)), 0) + COUNT(*) FROM genisis.Pay_ContraAllocation)";

    private static ReceiptMemberSaveRequest Receipt(ReceiptMemberItem item) =>
        new() { ReceiptNo = "ZZ-RM-1", ReceiptDate = new DateTime(2026, 10, 30), Amount = 10m, ChequeNo = "ZZCHQ", Remarks = "e2e", AccMonth = 10, AccYear = 2026, Items = [item] };

    [Fact]
    public async Task The_search_gives_the_member_debit_notes_by_payment_status_and_matches_the_view()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var unpaid = await Repository.SearchAsync(session, new ReceiptMemberFilter { Payor = "TA - x", Status = 2 });
        Assert.NotEmpty(unpaid);
        Assert.All(unpaid, r => Assert.True(string.IsNullOrEmpty(r.PaymentStatus) && r.PayorCode == "TA" && r.DebitNo!.StartsWith('D') && r.PayableByMedix <= 0));
        var partial = await Repository.SearchAsync(session, new ReceiptMemberFilter { Status = 3, CaseFrom = "ST", CaseTo = "ST9" });
        Assert.All(partial, r => Assert.Equal("P", r.PaymentStatus));
        Assert.Equal(partial.Count, await Count(session, "SELECT COUNT(*) FROM genisis.Pay_ViewReceiptfrMBM WHERE CLMPayableByMedix2M <= 0 AND SUBSTRING(DebitCreditRefNo, 1, 1) = 'D' AND PVRecMBMStatus = 'P' AND CASNumber >= 'ST' AND CASNumber <= 'ST9'"));
        var scan = unpaid.First(r => r.PayableByMedix < 0);
        var one = await Repository.SearchAsync(session, new ReceiptMemberFilter { Scan = true, CaseFrom = scan.CaseNo });
        Assert.All(one, r => Assert.Equal(scan.CaseNo, r.CaseNo));
        var amount = (-scan.PayableByMedix!.Value);
        Assert.Contains(await Repository.SearchAsync(session, new ReceiptMemberFilter { Scan = true, CaseFrom = scan.CaseNo, AmtFrom = amount, AmtTo = amount }), r => r.ClaimVersion == scan.ClaimVersion);
        Assert.Empty(await Repository.SearchAsync(session, new ReceiptMemberFilter { Scan = true, CaseFrom = scan.CaseNo, AmtFrom = amount + 1 }));
        Assert.Empty(await Repository.SearchAsync(session, new ReceiptMemberFilter { Scan = true, CaseFrom = "NO-SUCH-CASE" }));
    }

    [Fact]
    public async Task The_user_authorised_flag_follows_the_users_status()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var yes = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(USRCode) FROM genisis.Maint_USR WHERE RTRIM(AuthorisedStatus) = 'Y'", transaction: session.Transaction);
        var no = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(USRCode) FROM genisis.Maint_USR WHERE RTRIM(AuthorisedStatus) = 'N'", transaction: session.Transaction);
        Assert.True(await Repository.AuthorisedAsync(session, yes));
        Assert.False(await Repository.AuthorisedAsync(session, no));
        Assert.False(await Repository.AuthorisedAsync(session, "NO-SUCH-USER"));
    }

    [Fact]
    public async Task Saving_a_receipt_adds_or_updates_the_row_the_contra_allocation_and_the_first_rows_status_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var claim = (await Repository.SearchAsync(read, new ReceiptMemberFilter { Payor = "TA", Status = 2 })).First(r => r.PayableByMedix < 0 && r.ContraCaseNo is null);
        var item = new ReceiptMemberItem { CaseNo = claim.CaseNo, Version = claim.ClaimVersion, Status = "P", PaymentAmt = 4m, BankCharge = 1.5m, Remarks = "part", ContraCase = "TA-CONTRA", ContraVersion = "1", ContraAmt = 2m };
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var index = await Repository.SaveAsync(write, Receipt(item), item, "E2E", false);
            var row = await write.Connection.QuerySingleAsync("SELECT ReceiptNo, PVRecMBMStatus AS Status, ReceiptAmt, MBMBankCharge, MBMRemarks, RECMBMAccPeriodMth AS Mth, ReceiptLastUpdateUser AS Usr FROM genisis.Pay_ReceiptfrMBM WHERE ReceiptIndex = @index", new { index }, write.Transaction);
            Assert.Equal(("ZZ-RM-1", "P", 4m, 1.5m, "part", 10m, "E2E"), ((string)row.ReceiptNo, ((string)row.Status).Trim(), (decimal)row.ReceiptAmt, (decimal)row.MBMBankCharge, (string)row.MBMRemarks, (decimal)row.Mth, (string)row.Usr));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ContraAllocation WHERE CaseNo = @c AND RefNo = 'ZZ-RM-1' AND ContraCaseNo = 'TA-CONTRA' AND ContraCaseAmt = 2 AND CaseAmt = 4", new { c = claim.CaseNo }));

            var again = new ReceiptMemberItem { CaseNo = claim.CaseNo, Version = claim.ClaimVersion, Status = "F", PaymentAmt = 6m };
            var sameRow = await Repository.SaveAsync(write, Receipt(again), again, "E2E", false);
            Assert.Equal(index, sameRow);
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ReceiptfrMBM WHERE CASNumber = @c AND ClaimVersion = @v", new { c = claim.CaseNo, v = claim.ClaimVersion.ToString() }));
            Assert.Equal("F", (await write.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(PVRecMBMStatus) FROM genisis.Pay_ReceiptfrMBM WHERE ReceiptIndex = @index", new { index }, write.Transaction)));

            var authorised = await Repository.SaveAsync(write, Receipt(again), again, "E2E", true);
            Assert.NotEqual(index, authorised);
            Assert.Equal(2, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ReceiptfrMBM WHERE CASNumber = @c AND ClaimVersion = @v", new { c = claim.CaseNo, v = claim.ClaimVersion.ToString() }));
            var claims = await Repository.ClaimAsync(write, claim.CaseNo!, claim.ClaimVersion, "ZZ-RM-1");
            Assert.Equal(2, claims.Count);
            Assert.All(claims, c => Assert.Equal("F", c.PaymentStatus));
        }

        Assert.Equal(before, await Count(read, Checksum));
    }

    [Fact]
    public async Task Update_changes_the_row_and_its_contra_allocation_and_a_row_that_does_not_exist_is_not_changed()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var partial = (await Repository.SearchAsync(read, new ReceiptMemberFilter { Status = 3, CaseFrom = "ST", CaseTo = "ST9" })).First(r => r.ReceiptIndex is not null && r.ContraCaseNo is null);
        Assert.NotNull(await Repository.RowAsync(read, partial.ReceiptIndex!.Value));
        Assert.Null(await Repository.RowAsync(read, int.MaxValue));
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var update = new ReceiptMemberUpdateRequest
            {
                ReceiptIndex = partial.ReceiptIndex, ReceiptNo = "ZZ-RM-2", ReceiptDate = new DateTime(2026, 10, 31), Amount = 77m, ChequeNo = "ZZ2", BankCharge = 3m, Remarks = "changed", ContraCase = "ST-CONTRA", ContraVersion = "2", ContraAmt = 9m,
            };
            Assert.True(await Repository.UpdateAsync(write, update, "E2E"));
            var row = await Repository.RowAsync(write, partial.ReceiptIndex.Value);
            Assert.Equal(("ZZ-RM-2", 77m, "ZZ2", 3m, "changed", "ST-CONTRA", 9m, null, null), (row!.ReceiptNo, row.ReceiptAmt, row.ChequeNo, row.BankCharge, row.Remarks, row.ContraCaseNo, row.ContraCaseAmt, row.GainLoss, row.Misc));
            Assert.False(await Repository.UpdateAsync(write, new ReceiptMemberUpdateRequest { ReceiptIndex = int.MaxValue, ReceiptNo = "x", ReceiptDate = DateTime.Today, ChequeNo = "x", Amount = 1m }, "E2E"));
        }

        Assert.Equal(before, await Count(read, Checksum));
    }

    [Fact]
    public async Task The_official_receipt_lists_the_rows_of_a_receipt_number()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var number = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(ReceiptNo) FROM genisis.Pay_ViewReceiptfrMBMRec2 WHERE ReceiptNo <> ''", transaction: session.Transaction);
        var lines = await Repository.OfficialReceiptAsync(session, number);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(number, l.ReceiptNo));
        Assert.Empty(await Repository.OfficialReceiptAsync(session, "NO-SUCH-RECEIPT"));
    }
}
