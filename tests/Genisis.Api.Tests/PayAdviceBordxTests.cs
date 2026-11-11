using Dapper;
using Genisis.Api.Case.Listing;
using Genisis.Api.ClaimBordx.PaymentAdvice;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Payment Advice > Generate Bordx No: the validators, the rules and the service, against fakes.</summary>
public class PayAdviceBordxRulesTests
{
    private sealed class FakeDocuments(bool available, bool has) : IScannedDocuments
    {
        public bool Available => available;

        public bool HasDocuments(string cas) => has;
    }

    private sealed class FakeRepository : IPayAdviceRepository
    {
        public Dictionary<string, PayAdviceItem> Items { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<PayAdviceRow>> SearchAsync(IDbSession session, PayAdviceFilter filter) => Task.FromResult(new List<PayAdviceRow> { new() { CaseNo = "MH1", ClaimVersion = 2, CoverId = "00", MemberName = "M", CoveredName = "C" } });

        public Task<PayAdviceItem?> GetItemAsync(IDbSession session, string cas, int version) => Task.FromResult(Items.GetValueOrDefault($"{cas}-{version}"));

        public Task<string> NextBordxNoAsync(IDbSession session, string payor)
        {
            Calls.Add($"next {payor}");
            return Task.FromResult("NMH0000009");
        }

        public Task ApplyAsync(IDbSession session, string cas, int version, string bordxNo, string userCode)
        {
            Calls.Add($"apply {cas}-{version} {bordxNo}");
            return Task.CompletedTask;
        }

        public Task SaveBordxAsync(IDbSession session, string bordxNo, decimal amount, int claims, string cashType, string userCode)
        {
            Calls.Add($"save {bordxNo} {amount} {claims} {cashType}");
            return Task.CompletedTask;
        }
    }

    private static PayAdviceItem Item(string cas, int version = 1, string cash = "H", decimal approved = 100, decimal fee = 5, string btbg = "N") => new()
    {
        CaseNo = cas, ClaimVersion = version, Payor = "MH", Dod = new DateTime(2026, 10, 1), Hospital = "H1", IcdCode = "A09", Claimability = "Y", CostCenter = "CC", GroupCategory = "GC", InsuredType = "I", Btbg = btbg, CashType = cash,
        Payable2H = 80, Payable2M = 20, TotalApproved = approved, CaseMgmtFees = fee,
    };

    private static PayAdviceBordxService Service(FakeRepository repository, FakeSessions sessions, bool docsAvailable = true, bool hasDocs = true) =>
        new(sessions, repository, new FakeDocuments(docsAvailable, hasDocs), NullLogger<PayAdviceBordxService>.Instance);

    private static PayAdviceBordxRequest Request(params (string Cas, int Version)[] picks) => new() { Items = picks.Select(p => new ListingPick(p.Cas, p.Version)).ToList() };

    [Fact]
    public void The_worksheet_number_is_needed_in_scan_mode_and_the_cash_type_is_h_or_m()
    {
        var validator = new PayAdviceFilterValidator();
        Assert.Equal("Please Enter Worksheet No", validator.FirstError(new PayAdviceFilter()));
        Assert.Null(validator.FirstError(new PayAdviceFilter { UseScan = false }));
        Assert.Null(validator.FirstError(new PayAdviceFilter { WksNoFrom = "MH1", CashType = "H - Hospital" }));
        Assert.NotNull(validator.FirstError(new PayAdviceFilter { WksNoFrom = "MH1", CashType = "X" }));
        Assert.Equal("Please select at least one record to be updated! ", new PayAdviceBordxRequestValidator().FirstError(new PayAdviceBordxRequest()));
    }

    [Fact]
    public void The_tick_rules_come_in_the_desktops_order()
    {
        Assert.Equal("Date of Discharge is Empty.", PayAdviceRules.SelectionProblem(Item("A") with { Dod = null }, true));
        Assert.Equal("Hospital cannot be empty!", PayAdviceRules.SelectionProblem(Item("A") with { Hospital = " " }, true));
        Assert.Equal("Final ICD Code cannot be empty!", PayAdviceRules.SelectionProblem(Item("A") with { IcdCode = "" }, true));
        Assert.Equal("Claimability is Not Decided!", PayAdviceRules.SelectionProblem(Item("A") with { Claimability = "N" }, true));
        Assert.Equal("Cost Centre is empty", PayAdviceRules.SelectionProblem(Item("A") with { CostCenter = null }, true));
        Assert.Equal("Group Category is empty", PayAdviceRules.SelectionProblem(Item("A") with { GroupCategory = "" }, true));
        Assert.Equal("Please ensure scanned documents are in system!", PayAdviceRules.SelectionProblem(Item("A"), false));
        Assert.Equal("Group Company cannot be empty!", PayAdviceRules.SelectionProblem(Item("A") with { InsuredType = "G" }, true));
        Assert.Null(PayAdviceRules.SelectionProblem(Item("A") with { InsuredType = "H", GroupCompany = "ACME" }, true));
        Assert.Null(PayAdviceRules.SelectionProblem(Item("A"), null));
    }

    [Fact]
    public void The_amount_is_the_payable_amount_of_whoever_is_paid_for_a_btbg_claim_plus_the_fee()
    {
        Assert.Equal(105m, PayAdviceRules.Amount(Item("A")));
        Assert.Equal(85m, PayAdviceRules.Amount(Item("A", btbg: "Y")));
        Assert.Equal(25m, PayAdviceRules.Amount(Item("A", cash: "M", btbg: "Y")));
    }

    [Fact]
    public async Task One_bordx_is_made_for_all_the_ticked_worksheets_with_their_total_and_count_in_one_transaction()
    {
        var repository = new FakeRepository();
        repository.Items["MH1-1"] = Item("MH1");
        repository.Items["MH2-3"] = Item("MH2", 3, cash: "M", approved: 50, fee: 0);
        var sessions = new FakeSessions();
        var result = await Service(repository, sessions).GenerateAsync(Request(("MH1", 1), ("MH2", 3)), "U1");
        Assert.Equal(["next MH", "apply MH1-1 NMH0000009", "apply MH2-3 NMH0000009", "save NMH0000009 155 2 M"], repository.Calls);
        Assert.True(sessions.Last.Committed);
        Assert.Equal(("NMH0000009", 155m, 2), (result.BordxNo, result.BordxAmount, result.Claims));
        Assert.Equal([105m, 50m], result.Lines.Select(l => l.BordxAmount));
        Assert.All(result.Lines, l => Assert.Equal("NMH0000009", l.BordxNo));
    }

    [Fact]
    public async Task A_worksheet_no_longer_in_the_list_or_breaking_a_rule_saves_nothing()
    {
        var repository = new FakeRepository();
        repository.Items["MH1-1"] = Item("MH1");
        var sessions = new FakeSessions();
        var missing = await Assert.ThrowsAsync<PayAdviceBordxException>(() => Service(repository, sessions).GenerateAsync(Request(("MH1", 1), ("MH9", 1)), "U1"));
        Assert.Equal("Worksheet MH9-1 is no longer waiting for a payment advice bordx. Search again.", missing.Message);
        Assert.False(sessions.Last.Committed);

        repository.Items["MH2-1"] = Item("MH2") with { IcdCode = "" };
        var rule = await Assert.ThrowsAsync<PayAdviceBordxException>(() => Service(repository, new FakeSessions()).GenerateAsync(Request(("MH2", 1)), "U1"));
        Assert.Equal("MH2-1: Final ICD Code cannot be empty!", rule.Message);
        var noDocs = await Assert.ThrowsAsync<PayAdviceBordxException>(() => Service(repository, new FakeSessions(), hasDocs: false).GenerateAsync(Request(("MH1", 1)), "U1"));
        Assert.Equal("MH1-1: Please ensure scanned documents are in system!", noDocs.Message);
        await Service(repository, new FakeSessions(), docsAvailable: false, hasDocs: false).GenerateAsync(Request(("MH1", 1)), "U1");
    }

    [Fact]
    public async Task Search_and_the_document_check_pass_through()
    {
        var repository = new FakeRepository();
        var service = Service(repository, new FakeSessions());
        var rows = await service.SearchAsync(new PayAdviceFilter { WksNoFrom = "MH1" });
        Assert.Equal(("MH1-2", "M"), (rows[0].WksNo, rows[0].Patient));
        var (available, cases) = service.Documents([" MH1 ", "MH1", ""]);
        Assert.True(available);
        Assert.Equal(["MH1"], cases.Keys);
    }
}

/// <summary>Payment Advice bordx against the database. The view holds no row in the development data, so a worksheet is made eligible in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class PayAdviceBordxDatabaseTests
{
    private static PayAdviceRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string ClaimChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, BordxRefNo, ClaimBordxStatus, CLMPayAdvice, CLMPending)), 0) + COUNT(*) FROM genisis.WS_Claim";

    private const string BordxChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxClaimAmt, BordxNoClaims, BordxStatus)), 0) + COUNT(*) FROM genisis.WS_Bordx";

    private const string SequenceChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(PAYCode, CLMBordxNo)), 0) + COUNT(*) FROM genisis.WS_BordxSEQ";

    /// <summary>Frees two MH worksheets flagged for a payment advice so that they are in the list.</summary>
    private static async Task<List<(string Case, int Version)>> FreeAsync(IDbSession s, int count) =>
        (await s.Connection.QueryAsync<(string Case, int Version)>(
            "SELECT TOP (@n) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c JOIN HISDB.dbo.VIEW_CAS_Patient p ON p.CASNumber = c.CASNumber WHERE p.PAYCode = N'MH' AND c.CLMMHBordx = 'Y' AND c.ClaimStatus = 'C' AND p.CASStatus = 'O' AND c.BordxRefNo <> 'VOID' ORDER BY c.CASNumber DESC",
            new { n = count }, s.Transaction)).ToList();

    [Fact]
    public async Task The_search_gives_the_flagged_worksheets_for_each_criterion_and_the_item_gives_its_amounts()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var free = await FreeAsync(write, 2);
        Assert.Equal(2, free.Count);
        foreach (var f in free)
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Claim SET ClaimBordxStatus = 0, BordxRefNo = NULL WHERE CASNumber = @c AND ClaimVersion = @v", new { c = f.Case, v = f.Version }, write.Transaction);

        var all = await Repository.SearchAsync(write, new PayAdviceFilter { UseScan = false });
        Assert.True(all.Count >= 2);
        Assert.Equal(await Count(write, "SELECT COUNT(*) FROM (SELECT DISTINCT CASNumber, ClaimVersion FROM genisis.WS_MasPaymentAdvice) x"), all.Select(r => (r.CaseNo, r.ClaimVersion)).Distinct().Count());
        Assert.All(all, r => Assert.Equal("MH", r.Payor));
        var one = all.First(r => r.CaseNo == free[0].Case.Trim());
        var scan = await Repository.SearchAsync(write, new PayAdviceFilter { WksNoFrom = one.CaseNo.ToLowerInvariant(), Version = one.ClaimVersion });
        Assert.All(scan, r => Assert.Equal((one.CaseNo, one.ClaimVersion), (r.CaseNo, r.ClaimVersion)));
        Assert.NotEmpty(scan);
        Assert.All(await Repository.SearchAsync(write, new PayAdviceFilter { UseScan = false, WksNoFrom = one.CaseNo, WksNoTo = one.CaseNo }), r => Assert.Equal(one.CaseNo, r.CaseNo));
        Assert.Empty(await Repository.SearchAsync(write, new PayAdviceFilter { UseScan = false, DoaFrom = new DateTime(2099, 1, 1) }));
        Assert.All(await Repository.SearchAsync(write, new PayAdviceFilter { UseScan = false, CashType = "H - Hospital" }), r => Assert.Equal("H", r.CashType));
        if (!string.IsNullOrWhiteSpace(one.Hospital)) Assert.All(await Repository.SearchAsync(write, new PayAdviceFilter { UseScan = false, Hospital = one.Hospital }), r => Assert.Equal(one.Hospital, r.Hospital));

        var item = await Repository.GetItemAsync(write, one.CaseNo, one.ClaimVersion);
        Assert.NotNull(item);
        Assert.Equal(one.CaseNo, item!.CaseNo);
        Assert.Equal(one.AppAmt ?? 0, item.TotalApproved);
        Assert.Null(await Repository.GetItemAsync(write, "ZZ-NO-CASE", 1));
    }

    [Fact]
    public async Task Generating_marks_the_worksheets_with_payment_advice_001_and_saves_the_bordx_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (claims, bordx, sequence) = (await Count(read, ClaimChecksum), await Count(read, BordxChecksum), await Count(read, SequenceChecksum));
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var free = await FreeAsync(write, 2);
            foreach (var f in free)
                await write.Connection.ExecuteAsync("UPDATE genisis.WS_Claim SET ClaimBordxStatus = 0, BordxRefNo = NULL, CLMPayAdvice = NULL WHERE CASNumber = @c AND ClaimVersion = @v", new { c = f.Case, v = f.Version }, write.Transaction);
            var number = await Repository.NextBordxNoAsync(write, "MH");
            Assert.EndsWith("MH", number[..^7]);
            foreach (var f in free) await Repository.ApplyAsync(write, f.Case, f.Version, number, "E2E");
            await Repository.SaveBordxAsync(write, number, 123.45m, 2, "H", "E2E");

            var rows = (await write.Connection.QueryAsync("SELECT RTRIM(BordxRefNo) AS BordxNo, ClaimBordxStatus AS Bordxed, RTRIM(CLMPayAdvice) AS PayAdvice, RTRIM(CLMPending) AS Pending FROM genisis.WS_Claim WHERE BordxRefNo = @n", new { n = number }, write.Transaction)).ToList();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal((number, true, "001", "N"), ((string)r.BordxNo, (bool)r.Bordxed, (string)r.PayAdvice, (string)r.Pending)));
            Assert.Empty(await Repository.SearchAsync(write, new PayAdviceFilter { UseScan = false, WksNoFrom = free[0].Case, WksNoTo = free[0].Case }));
            var row = await write.Connection.QuerySingleAsync("SELECT BordxClaimAmt AS Amount, BordxNoClaims AS Claims, RTRIM(BordxStatus) AS Status, RTRIM(BordxPAYCode) AS Payor FROM genisis.WS_Bordx WHERE BordxRefNo = @n", new { n = number }, write.Transaction);
            Assert.Equal((123.45m, 2, "C", "MH"), ((decimal)row.Amount, (int)row.Claims, (string)row.Status, (string)row.Payor));
        }

        Assert.Equal(claims, await Count(read, ClaimChecksum));
        Assert.Equal(bordx, await Count(read, BordxChecksum));
        Assert.Equal(sequence, await Count(read, SequenceChecksum));
    }
}
