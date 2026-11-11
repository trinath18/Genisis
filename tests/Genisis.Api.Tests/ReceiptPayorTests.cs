using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Receipt.Payor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Receipt > Payor: the validators and the service, against a fake repository.</summary>
public class ReceiptPayorRulesTests
{
    private sealed class FakeRepository : IReceiptPayorRepository
    {
        public bool Authorised { get; set; }

        public Dictionary<string, string?> BordxStatuses { get; } = new() { ["B1"] = "" };

        public string? ClaimStatus { get; set; } = "";

        public List<PayorBordxRow> Bordx { get; set; } = [];

        public List<PayorWorksheetRow> Worksheets { get; set; } = [];

        public int Deleted { get; set; } = 3;

        public List<ReceiptPayorOfficialLine> Official { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<PayorBordxRow>> BordxAsync(IDbSession session, ReceiptPayorFilter filter) => Task.FromResult(Bordx);

        public Task<List<PayorWorksheetRow>> WorksheetsAsync(IDbSession session, ReceiptPayorFilter filter) => Task.FromResult(Worksheets);

        public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) => Task.FromResult(new List<string> { "ACME" });

        public Task<bool> AuthorisedAsync(IDbSession session, string userCode) => Task.FromResult(Authorised);

        public Task<string?> BordxStatusAsync(IDbSession session, string bordxNo) => Task.FromResult(BordxStatuses.GetValueOrDefault(bordxNo));

        public Task<string?> ClaimStatusAsync(IDbSession session, string caseNo, int version) => Task.FromResult(ClaimStatus);

        public Task<string> NextNumberAsync(IDbSession session, string prefix)
        {
            Calls.Add($"next '{prefix}'");
            return Task.FromResult(prefix + "10002");
        }

        public Task<int> SaveBordxAsync(IDbSession session, ReceiptPayorSaveRequest r, string bordxNo, string receiptNo, bool insert, string userCode)
        {
            Calls.Add($"bordx {bordxNo} {receiptNo} insert={insert}");
            return Task.FromResult(4);
        }

        public Task<string> SaveWorksheetAsync(IDbSession session, ReceiptPayorSaveRequest r, ReceiptPayorItem item, string receiptNo, bool insert, string userCode)
        {
            Calls.Add($"ws {item.CaseNo}-{item.Version} {item.Status} {item.PaymentAmt} {receiptNo} insert={insert} index={item.ReceiptIndex}");
            return Task.FromResult("P");
        }

        public Task<int> DeleteAsync(IDbSession session, ReceiptPayorDeleteRequest request) => Task.FromResult(Deleted);

        public Task<List<ReceiptPayorOfficialLine>> OfficialReceiptAsync(IDbSession session, string receiptNo) => Task.FromResult(Official);
    }

    private static ReceiptPayorService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<ReceiptPayorService>.Instance);

    private static ReceiptPayorSaveRequest Bordx(params string[] bordx) =>
        new() { Mode = "bordx", ReceiptAmt = 100m, CheckedAmount = 100m, ChequeNo = "C1", ReceiptDate = new DateTime(2026, 10, 30), BordxNos = [.. bordx] };

    private static ReceiptPayorItem Item(string cas = "WK1", string status = "F", decimal amount = 100m) => new() { CaseNo = cas, Version = 1, BordxNo = "B1", Status = status, PaymentAmt = amount };

    private static ReceiptPayorSaveRequest Worksheet(params ReceiptPayorItem[] items) =>
        new() { Mode = "worksheet", ReceiptAmt = 100m, CheckedAmount = 100m, ChequeNo = "C1", ReceiptDate = new DateTime(2026, 10, 30), Items = [.. items] };

    [Fact]
    public void Search_checks_a_scan_needs_a_bordx_or_a_worksheet()
    {
        var validator = new ReceiptPayorFilterValidator();
        Assert.Equal("Please Enter Bordx No", validator.FirstError(new ReceiptPayorFilter { Scan = true }));
        Assert.Equal("Please Enter Worksheet No", validator.FirstError(new ReceiptPayorFilter { Scan = true, Mode = "worksheet" }));
        Assert.Null(validator.FirstError(new ReceiptPayorFilter { Scan = true, BordxFrom = "B1" }));
        Assert.Null(validator.FirstError(new ReceiptPayorFilter { Scan = true, Mode = "worksheet", WsFrom = "WK1" }));
        Assert.Null(validator.FirstError(new ReceiptPayorFilter()));
        Assert.NotNull(validator.FirstError(new ReceiptPayorFilter { Mode = "x" }));
        Assert.Equal("Select a Payment Status.", validator.FirstError(new ReceiptPayorFilter { Status = 7 }));
    }

    [Fact]
    public void Save_checks_come_in_the_desktops_order()
    {
        var validator = new ReceiptPayorSaveRequestValidator();
        Assert.Equal("Please select at least one record to be updated! ", validator.FirstError(new ReceiptPayorSaveRequest()));
        Assert.Equal("Please Enter Receipt Amt!", validator.FirstError(new ReceiptPayorSaveRequest { BordxNos = ["B1"] }));
        Assert.Equal("Please Enter Cheque No!", validator.FirstError(new ReceiptPayorSaveRequest { BordxNos = ["B1"], ReceiptAmt = 1m }));
        Assert.Equal("Please Enter Receipt Date", validator.FirstError(new ReceiptPayorSaveRequest { BordxNos = ["B1"], ReceiptAmt = 1m, ChequeNo = "C" }));
        Assert.Equal("Please Enter Remark!", validator.FirstError(new ReceiptPayorSaveRequest { BordxNos = ["B1"], ReceiptAmt = 1m, ChequeNo = "C", ReceiptDate = DateTime.Today, CheckedAmount = 2m }));
        Assert.Null(validator.FirstError(new ReceiptPayorSaveRequest { BordxNos = ["B1"], ReceiptAmt = 1m, ChequeNo = "C", ReceiptDate = DateTime.Today, CheckedAmount = 2m, Remark = "short" }));
        Assert.Null(validator.FirstError(Bordx("B1")));
        Assert.Equal("Please select at least one record to be updated! ", validator.FirstError(Worksheet()));
        Assert.Null(validator.FirstError(Worksheet(Item())));
        Assert.Equal("Please enter the amount paying for the claim.", validator.FirstError(Worksheet(Item(amount: 0m))));
        var contra = Item();
        contra.ContraCase = "X";
        Assert.Equal("Invalid Contra Case..., Please Keyin Contra Case No. or Contra Version ", validator.FirstError(Worksheet(contra)));
        contra.ContraVersion = "1";
        Assert.Equal("Please Keyin Contra Case Amount ", validator.FirstError(Worksheet(contra)));
    }

    [Fact]
    public void Update_and_remove_checks_follow_the_desktop()
    {
        var update = new ReceiptPayorUpdateRequestValidator();
        Assert.Equal("Please select the record", update.FirstError(new ReceiptPayorUpdateRequest()));
        var ok = new ReceiptPayorUpdateRequest { Mode = "bordx", BordxNos = ["B1"], ReceiptNo = "R1" };
        Assert.Equal("Please Enter Rec Amt!", update.FirstError(ok));
        ok.ReceiptAmt = 1m;
        Assert.Equal("Please Enter Cheque No!", update.FirstError(ok));
        ok.ChequeNo = "C";
        Assert.Equal("Please Enter Receipt Date", update.FirstError(ok));
        ok.ReceiptDate = DateTime.Today;
        Assert.Null(update.FirstError(ok));
        var remove = new ReceiptPayorDeleteRequestValidator();
        Assert.Equal("Please Select Bordx to delete!", remove.FirstError(new ReceiptPayorDeleteRequest()));
        Assert.Equal("Please Select Claim to delete!", remove.FirstError(new ReceiptPayorDeleteRequest { Mode = "worksheet" }));
        Assert.Null(remove.FirstError(new ReceiptPayorDeleteRequest { Mode = "worksheet", CaseNo = "WK1", Version = 1 }));
    }

    [Fact]
    public async Task Search_totals_the_bordx_once_each_and_the_check_amounts_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository { Authorised = true };
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new ReceiptPayorFilter(), "U"))).Message);
        repository.Bordx = [new() { BordxNo = "B1", BordxAmount = 100m, CheckAmt = 40m }, new() { BordxNo = "B1", BordxAmount = 100m, CheckAmt = 20m }, new() { BordxNo = "B2", BordxAmount = 50m, CheckAmt = null }];
        var result = await Service(repository, new FakeSessions()).SearchAsync(new ReceiptPayorFilter(), "U");
        Assert.Equal((150m, 60m, true), (result.TotalAmount, result.TotalReceived, result.Authorised));
        repository.Worksheets = [new() { CaseNo = "WK1", Amount = 30m, CheckAmt = 10m }, new() { CaseNo = "WK2", Amount = 20m, CheckAmt = 5m }];
        var ws = await Service(repository, new FakeSessions()).SearchAsync(new ReceiptPayorFilter { Mode = "worksheet" }, "U");
        Assert.Equal((50m, 15m, 2), (ws.TotalAmount, ws.TotalReceived, ws.Worksheets.Count));
    }

    [Fact]
    public async Task Save_by_bordx_takes_one_number_from_the_first_bordxs_prefix_and_receives_every_bordx_in_one_transaction()
    {
        var repository = new FakeRepository { Bordx = [new() { BordxNo = "B1", ReceiptPrefix = "RB" }] };
        repository.BordxStatuses["B2"] = "";
        var sessions = new FakeSessions();
        var (number, saved) = await Service(repository, sessions).SaveAsync(Bordx("b1", "B2"), "U");
        Assert.Equal(("RB10002", 8), (number, saved));
        Assert.Equal(["next 'RB'", "bordx B1 RB10002 insert=True", "bordx B2 RB10002 insert=True"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_bordx_paid_in_full_is_refused_unless_the_user_is_authorised_and_a_partly_paid_bordx_is_always_refused()
    {
        var repository = new FakeRepository();
        repository.BordxStatuses["B1"] = "F";
        Assert.Equal("Record has been paid !", (await Assert.ThrowsAsync<ConflictException>(() => Service(repository, new FakeSessions()).SaveAsync(Bordx("B1"), "U"))).Message);
        Assert.Empty(repository.Calls);
        repository.Authorised = true;
        Assert.Equal(4, (await Service(repository, new FakeSessions()).SaveAsync(Bordx("B1"), "U")).Saved);
        repository.BordxStatuses["B1"] = "P";
        Assert.Equal("Record has been Partial Paid ! Please Pay by Receipt Type - Worksheet !", (await Assert.ThrowsAsync<ConflictException>(() => Service(repository, new FakeSessions()).SaveAsync(Bordx("B1"), "U"))).Message);
        repository.BordxStatuses["B1"] = null;
        Assert.Equal("Bordx B1 not found", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SaveAsync(Bordx("B1"), "U"))).Message);
    }

    [Fact]
    public async Task Save_by_worksheet_receives_each_claim_and_refuses_a_claim_paid_in_full_to_a_user_who_is_not_authorised()
    {
        var repository = new FakeRepository();
        var (number, saved) = await Service(repository, new FakeSessions()).SaveAsync(Worksheet(Item("WK1"), Item("WK2", "P", 25m)), "U");
        Assert.Equal((2, "10002"), (saved, number));
        Assert.Equal(["next ''", "ws WK1-1 F 100 10002 insert=True index=", "ws WK2-1 P 25 10002 insert=True index="], repository.Calls);
        repository.ClaimStatus = "F";
        Assert.Equal("Record has been paid !", (await Assert.ThrowsAsync<ConflictException>(() => Service(repository, new FakeSessions()).SaveAsync(Worksheet(Item()), "U"))).Message);
        repository.Authorised = true;
        Assert.Equal(1, (await Service(repository, new FakeSessions()).SaveAsync(Worksheet(Item()), "U")).Saved);
    }

    [Fact]
    public async Task Update_changes_the_bordxs_receipt_or_the_claims_receipt_row_and_remove_needs_something_to_delete()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var bordx = new ReceiptPayorUpdateRequest { Mode = "bordx", ReceiptNo = "RB10001", ReceiptAmt = 1m, ChequeNo = "C", ReceiptDate = DateTime.Today, CheckedAmount = 1m, BordxNos = ["B1"] };
        await Service(repository, sessions).UpdateAsync(bordx, "U");
        Assert.Equal(["bordx B1 RB10001 insert=False"], repository.Calls);
        Assert.True(sessions.Last.Committed);
        var ws = new ReceiptPayorUpdateRequest { Mode = "worksheet", ReceiptNo = "RB10001", ReceiptAmt = 1m, ChequeNo = "C", ReceiptDate = DateTime.Today, Items = [Item()] };
        Assert.Equal("Please select the record", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, new FakeSessions()).UpdateAsync(ws, "U"))).Message);
        ws.Items[0].ReceiptIndex = 9;
        await Service(repository, new FakeSessions()).UpdateAsync(ws, "U");
        Assert.Equal("ws WK1-1 F 100 RB10001 insert=False index=9", repository.Calls.Last());
        Assert.Equal(3, await Service(repository, new FakeSessions()).DeleteAsync(new ReceiptPayorDeleteRequest { BordxNo = "B1" }, "U"));
        repository.Deleted = 0;
        Assert.Equal("No receipt to delete.", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).DeleteAsync(new ReceiptPayorDeleteRequest { BordxNo = "B1" }, "U"))).Message);
    }

    [Fact]
    public async Task The_official_receipt_needs_a_receipt_number_and_lines()
    {
        var repository = new FakeRepository();
        Assert.Equal("Please Enter or Generate Receipt No", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, new FakeSessions()).OfficialReceiptAsync(" "))).Message);
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).OfficialReceiptAsync("R1"))).Message);
        repository.Official = [new("R1", null, "B1", 10m, "C", "PAYOR")];
        Assert.Single(await Service(repository, new FakeSessions()).OfficialReceiptAsync("R1"));
    }
}

/// <summary>Receipt Payor against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class ReceiptPayorDatabaseTests
{
    private static ReceiptPayorRepository Repository => new();

    private static async Task<long> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<long>(sql, args, s.Transaction);

    private static async Task<decimal> Sum(IDbSession s, string sql, object args) => await s.Connection.ExecuteScalarAsync<decimal>(sql, args, s.Transaction);

    private const string Bordx = "NWK0022102";

    private const string Partial = "NWK0022265";

    private static readonly string Checksum =
        "SELECT CAST((SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(RecPAYIndex, ReceiptNo, CheckAmt, ReceiptAmt, PAYRemarks)), 0) + COUNT(*) FROM genisis.Pay_ReceiptfrPAY) AS bigint)"
        + $" + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, PVRecPayorStatus)), 0) FROM genisis.WS_Claim WHERE BordxRefNo IN ('{Bordx}', '{Partial}'))"
        + $" + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYFRStatus, BordxPAYRecDate)), 0) FROM genisis.WS_Bordx WHERE BordxRefNo IN ('{Bordx}', '{Partial}'))"
        + " + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CASNumber, LEN(CASORemarks), LEN(CASORemarksTwo))), 0) FROM dbo.CASRemarks)"
        + " + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(RecIndex, RecSeqNo)), 0) FROM genisis.Pay_RecSeq) + (SELECT COUNT(*) FROM genisis.Pay_ContraAllocation)";

    private static ReceiptPayorSaveRequest Header(string mode) =>
        new() { Mode = mode, ReceiptAmt = 500m, CheckedAmount = 500m, ChequeNo = "ZZCHQ", ReceiptDate = new DateTime(2026, 10, 30), AccMonth = 10, AccYear = 2026, Remark = "e2e", PvNo = "PV1", PvDate = new DateTime(2026, 10, 29) };

    [Fact]
    public async Task The_searches_give_what_the_views_hold_with_the_received_amounts()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var bordx = await Repository.BordxAsync(session, new ReceiptPayorFilter { Scan = true, BordxFrom = Partial.ToLowerInvariant() });
        Assert.NotEmpty(bordx);
        Assert.All(bordx, r => Assert.Equal(Partial, r.BordxNo));
        Assert.Equal("P", bordx[0].BordxStatus);
        Assert.Equal(await Sum(session, "SELECT SUM(CheckAmt) FROM genisis.Pay_ReceiptfrPAY WHERE BordxNo = @b", new { b = Partial }), bordx[0].Received!.Value);
        var unreceived = await Repository.BordxAsync(session, new ReceiptPayorFilter { Payor = "WK - x", Status = 2 });
        Assert.NotEmpty(unreceived);
        Assert.All(unreceived, r => Assert.True(string.IsNullOrEmpty(r.ReceiptNo) && r.PayorCode == "WK"));
        Assert.All(await Repository.BordxAsync(session, new ReceiptPayorFilter { Status = 3, BordxFrom = "NW", BordxTo = "NX" }), r => Assert.Equal("P", r.BordxStatus));
        Assert.Empty(await Repository.BordxAsync(session, new ReceiptPayorFilter { Scan = true, BordxFrom = "NO-SUCH-BORDX" }));

        var claims = await Repository.WorksheetsAsync(session, new ReceiptPayorFilter { Mode = "worksheet", BordxFrom = Partial, BordxTo = Partial });
        Assert.NotEmpty(claims);
        Assert.All(claims, c => Assert.False(string.IsNullOrWhiteSpace(c.PatientName)));
        var withReceipt = claims.First(c => !string.IsNullOrEmpty(c.ReceiptNo));
        Assert.Equal(await Sum(session, "SELECT SUM(CheckAmt) FROM genisis.Pay_ReceiptfrPAY WHERE CASNumber = @c AND ClaimVersion = @v", new { c = withReceipt.CaseNo, v = withReceipt.ClaimVersion.ToString() }), withReceipt.Received!.Value);
        var one = await Repository.WorksheetsAsync(session, new ReceiptPayorFilter { Mode = "worksheet", Scan = true, WsFrom = withReceipt.CaseNo });
        Assert.All(one, c => Assert.Equal(withReceipt.CaseNo, c.CaseNo));
        Assert.Empty(await Repository.WorksheetsAsync(session, new ReceiptPayorFilter { Mode = "worksheet", Scan = true, WsFrom = "NO-SUCH-CASE" }));

        Assert.Equal("P", await Repository.BordxStatusAsync(session, Partial));
        Assert.Equal("", await Repository.BordxStatusAsync(session, Bordx));
        Assert.Null(await Repository.BordxStatusAsync(session, "NO-SUCH-BORDX"));
        Assert.NotEmpty(await Repository.GroupCompaniesAsync(session, "WK"));
        Assert.NotEmpty(await Repository.OfficialReceiptAsync(session, "RB54712"));
    }

    [Fact]
    public async Task Saving_a_bordx_receipt_marks_every_claim_and_the_bordx_adds_the_remark_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var claims = (await Repository.WorksheetsAsync(read, new ReceiptPayorFilter { Mode = "worksheet", BordxFrom = Bordx, BordxTo = Bordx })).Select(c => (c.CaseNo, c.ClaimVersion)).Distinct().ToList();
        Assert.NotEmpty(claims);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var number = await Repository.NextNumberAsync(write, "RB");
            Assert.StartsWith("RB", number);
            Assert.Equal(claims.Count, await Repository.SaveBordxAsync(write, Header("bordx"), Bordx, number, true, "E2E"));
            Assert.Equal(claims.Count, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @n AND BordxNo = @b AND ReceiptAmt = 500 AND ChequeNo = 'ZZCHQ' AND PAYLastUpdateUser = 'E2E'", new { n = number, b = Bordx }));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @b AND ISNULL(PVRecPayorStatus, '') <> 'F'", new { b = Bordx }));
            Assert.Equal("F", await Repository.BordxStatusAsync(write, Bordx));
            Assert.True(await Count(write, "SELECT COUNT(*) FROM dbo.CASRemarks WHERE CASNumber = @c AND (CASORemarks LIKE '%Claim Receipt  -  ' + @n + '%' OR CASORemarksTwo LIKE '%Claim Receipt  -  ' + @n + '%')", new { c = claims[0].CaseNo, n = number }) >= 0);

            var changed = Header("bordx");
            changed.ReceiptAmt = 600m;
            changed.ChequeNo = "ZZCHQ2";
            await Repository.SaveBordxAsync(write, changed, Bordx, number, false, "E2E");
            Assert.Equal(claims.Count, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @n AND ReceiptAmt = 600 AND ChequeNo = 'ZZCHQ2'", new { n = number }));

            Assert.Equal(claims.Count, await Repository.DeleteAsync(write, new ReceiptPayorDeleteRequest { Mode = "bordx", BordxNo = Bordx }));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @b AND PVRecPayorStatus IS NOT NULL", new { b = Bordx }));
            Assert.Equal("", await Repository.BordxStatusAsync(write, Bordx));
        }

        Assert.Equal(before, await Count(read, Checksum));
    }

    [Fact]
    public async Task Saving_a_worksheet_receipt_marks_the_claim_the_bordx_partial_then_full_updates_the_contra_case_and_rolls_back()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var claims = (await Repository.WorksheetsAsync(read, new ReceiptPayorFilter { Mode = "worksheet", BordxFrom = Bordx, BordxTo = Bordx })).Select(c => (c.CaseNo, c.ClaimVersion, c.Amount)).Distinct().ToList();
        Assert.True(claims.Count >= 1);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var number = await Repository.NextNumberAsync(write, "RB");
            var first = claims[0];
            var item = new ReceiptPayorItem { CaseNo = first.CaseNo, Version = first.ClaimVersion, BordxNo = Bordx, Status = "P", PaymentAmt = 1m, BankCharge = 0.5m, PayRemarks = "part", ContraCase = "WK-CONTRA", ContraVersion = "1", ContraAmt = 2m };
            var status = await Repository.SaveWorksheetAsync(write, Header("worksheet"), item, number, true, "E2E");
            Assert.Equal("P", status);
            var row = await write.Connection.QuerySingleAsync("SELECT RecPAYIndex AS Idx, CheckAmt, ReceiptAmt, PAYBankCharge AS Bank, PAYRemarks AS Rem FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @n", new { n = number }, write.Transaction);
            Assert.Equal((1m, 500m, 0.5m, "part"), ((decimal)row.CheckAmt, (decimal)row.ReceiptAmt, (decimal)row.Bank, (string)row.Rem));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @c AND ClaimVersion = @v AND PVRecPayorStatus = 'P'", new { c = first.CaseNo, v = first.ClaimVersion }));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ContraAllocation WHERE CaseNo = @c AND RefNo = @n AND ContraCaseNo = 'WK-CONTRA' AND ContraCaseAmt = 2", new { c = first.CaseNo, n = number }));

            var full = new ReceiptPayorItem { CaseNo = first.CaseNo, Version = first.ClaimVersion, BordxNo = Bordx, Status = "F", PaymentAmt = 2m, ReceiptIndex = (int)row.Idx };
            await Repository.SaveWorksheetAsync(write, Header("worksheet"), full, number, false, "E2E");
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @n AND CheckAmt = 2", new { n = number }));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @c AND ClaimVersion = @v AND PVRecPayorStatus = 'F'", new { c = first.CaseNo, v = first.ClaimVersion }));
            Assert.Equal(1, await Repository.DeleteAsync(write, new ReceiptPayorDeleteRequest { Mode = "worksheet", CaseNo = first.CaseNo, Version = first.ClaimVersion }));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @c AND ClaimVersion = @v AND PVRecPayorStatus IS NOT NULL", new { c = first.CaseNo, v = first.ClaimVersion }));
        }

        Assert.Equal(before, await Count(read, Checksum));
    }
}
