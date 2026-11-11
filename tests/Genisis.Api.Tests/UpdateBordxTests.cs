using Dapper;
using Genisis.Api.ClaimBordx.Update;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Update Bordx: the validator and the service, against a fake repository.</summary>
public class UpdateBordxRulesTests
{
    private sealed class FakeRepository : IUpdateBordxRepository
    {
        public Dictionary<string, (int Result, string? Payor, decimal Approved)> Applies { get; } = [];

        public int SaveResult { get; set; }

        public List<string> Calls { get; } = [];

        public Task<List<UpdateBordxRow>> SearchAsync(IDbSession session, UpdateBordxFilter filter) =>
            Task.FromResult(new List<UpdateBordxRow> { new() { CaseNo = "A1", TotalApproved = 10.5m }, new() { CaseNo = "A2", TotalApproved = null }, new() { CaseNo = "A3", TotalApproved = 4m } });

        public Task<(int Result, string? Payor, decimal Approved)> ApplyAsync(IDbSession session, UpdateBordxClaim claim, string bordxNo)
        {
            Calls.Add($"apply {claim.CaseNo}-{claim.Version} {bordxNo}");
            return Task.FromResult(Applies.GetValueOrDefault($"{claim.CaseNo}-{claim.Version}", (0, "AB", 100m)));
        }

        public Task<int> SaveBordxAsync(IDbSession session, UpdateBordxRequest request, string payor, string userCode)
        {
            Calls.Add($"save {payor}");
            return Task.FromResult(SaveResult);
        }

        public Task SaveMnrbReceiptAsync(IDbSession session, UpdateBordxRequest request, UpdateBordxClaim last, decimal checkedAmount, string userCode, DateTime today)
        {
            Calls.Add($"receipt {last.CaseNo}-{last.Version} {checkedAmount}");
            return Task.CompletedTask;
        }
    }

    private static UpdateBordxService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<UpdateBordxService>.Instance);

    private static UpdateBordxRequest Request(Action<UpdateBordxRequest>? tweak = null)
    {
        var r = new UpdateBordxRequest { Claims = [new("A1", 1), new("A2", 2)], BordxNo = " B1 ", BordxDate = new DateTime(2026, 10, 30) };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public void The_amount_choice_is_the_one_the_dropdown_offers()
    {
        var validator = new UpdateBordxFilterValidator();
        Assert.Null(validator.FirstError(new UpdateBordxFilter { Amount = "le" }));
        Assert.Null(validator.FirstError(new UpdateBordxFilter()));
        Assert.NotNull(validator.FirstError(new UpdateBordxFilter { Amount = "x" }));
    }

    [Fact]
    public void The_checks_come_in_the_desktops_order()
    {
        var validator = new UpdateBordxRequestValidator();
        Assert.Null(validator.FirstError(Request()));
        Assert.Equal("Please select at least one record to be updated! ", validator.FirstError(Request(r => r.Claims = [])));
        Assert.Equal("Please Enter Bordx No! ", validator.FirstError(Request(r => r.BordxNo = " ")));
        Assert.Equal("Please Enter Bordx Date! ", validator.FirstError(Request(r => r.BordxDate = null)));
        Assert.Equal("Please Enter Receipt No!", validator.FirstError(Request(r => r.ChequeAmount = 5m)));
        Assert.Null(validator.FirstError(Request(r => (r.ReceiptNo, r.ChequeAmount, r.ReceiptDate) = ("R1", 5m, new DateTime(2026, 10, 1)))));
        Assert.NotNull(validator.FirstError(Request(r => r.AcMonth = "13")));
        Assert.NotNull(validator.FirstError(Request(r => r.AcYear = "1900")));
        Assert.Null(validator.FirstError(Request(r => (r.AcMonth, r.AcYear) = ("7", "2026"))));
    }

    [Fact]
    public async Task Search_totals_the_approved_amounts()
    {
        var result = await Service(new FakeRepository(), new FakeSessions()).SearchAsync(new UpdateBordxFilter());
        Assert.Equal((3, 14.5m, false), (result.Rows.Count, result.TotalApproved, result.Truncated));
    }

    [Fact]
    public async Task Every_checked_worksheet_goes_into_the_bordx_which_is_then_saved_with_the_receipt_in_one_transaction()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var result = await Service(repository, sessions).UpdateAsync(Request(r => (r.ReceiptNo, r.Claims) = ("R1", [new("a1", 1), new("A1", 1), new("A2", 2)])), "U1");
        Assert.Equal(("B1", 2, 200m), (result.BordxNo, result.Claims, result.TotalApproved));
        Assert.Equal(["apply a1-1 B1", "apply A2-2 B1", "save AB", "receipt A2-2 200"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Without_a_receipt_number_no_receipt_is_written()
    {
        var repository = new FakeRepository();
        await Service(repository, new FakeSessions()).UpdateAsync(Request(), "U1");
        Assert.DoesNotContain(repository.Calls, c => c.StartsWith("receipt"));
    }

    [Fact]
    public async Task A_missing_or_bordxed_worksheet_or_mixed_payors_or_another_payors_bordx_saves_nothing()
    {
        var cases = new (Action<FakeRepository> Setup, Type Expected, string Message)[]
        {
            (r => r.Applies["A2-2"] = (1, null, 0m), typeof(NotFoundException), "Worksheet A2-2 not found"),
            (r => r.Applies["A2-2"] = (2, null, 0m), typeof(ConflictException), "Worksheet A2-2 is in a bordx already"),
            (r => r.Applies["A2-2"] = (3, null, 0m), typeof(BusinessRuleException), "Worksheet A2-2 is void"),
            (r => r.Applies["A2-2"] = (0, "CD", 5m), typeof(BusinessRuleException), "All the worksheets of a bordx must be of the same payor"),
            (r => r.SaveResult = 1, typeof(ConflictException), "Bordx B1 belongs to another payor"),
            (r => { r.Applies["A1-1"] = (0, null, 1m); r.Applies["A2-2"] = (0, null, 1m); }, typeof(BusinessRuleException), "The payor of the worksheets was not found"),
        };
        foreach (var (setup, expected, message) in cases)
        {
            var repository = new FakeRepository();
            setup(repository);
            var sessions = new FakeSessions();
            var failure = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(repository, sessions).UpdateAsync(Request(), "U1"));
            Assert.IsType(expected, failure);
            Assert.Equal(message, failure.Message);
            Assert.False(sessions.Last.Committed);
        }
    }
}

/// <summary>Update Bordx against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class UpdateBordxDatabaseTests
{
    private static UpdateBordxRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string Checksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, BordxRefNo, ClaimStatus, ClaimBordxStatus)), 0) + COUNT(*) FROM genisis.WS_Claim";

    private const string BordxChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYCode, BordxClaimAmt, BordxNoClaims, PAYToACPeriodMth, BordxPAYSentDate, BordxPAYToStatus)), 0) + COUNT(*) FROM genisis.WS_Bordx";

    private const string ReceiptChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(RecMNRBIndex, ReceiptNo, BordxNo, ReceiptAmt, ChequeNo)), 0) + COUNT(*) FROM genisis.Pay_ReceiptfrMNRB";

    [Fact]
    public async Task The_search_gives_the_pending_worksheets_for_each_criterion()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var unbordxed = await Repository.SearchAsync(session, new UpdateBordxFilter { Payor = "ta - x" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_PendingBordxAll WHERE PAYCode = 'TA' AND (BordxRefNo IS NULL OR BordxRefNo = '')"), unbordxed.Count);
        Assert.All(unbordxed, r => Assert.True(string.IsNullOrEmpty(r.BordxNo) && r.PayorCode == "TA"));
        var all = await Repository.SearchAsync(session, new UpdateBordxFilter { Payor = "TA", UnbordxedOnly = false });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_PendingBordxAll WHERE PAYCode = 'TA'"), all.Count);
        Assert.True(all.Count > unbordxed.Count);
        var small = await Repository.SearchAsync(session, new UpdateBordxFilter { Payor = "TA", Amount = "le" });
        var big = await Repository.SearchAsync(session, new UpdateBordxFilter { Payor = "TA", Amount = "gt" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_PendingBordxAll WHERE PAYCode = 'TA' AND (BordxRefNo IS NULL OR BordxRefNo = '') AND CLMTotalApproved <= 2000"), small.Count);
        Assert.Equal(unbordxed.Count, small.Count + big.Count + unbordxed.Count(r => r.TotalApproved is null));
        var one = unbordxed[0];
        Assert.All(await Repository.SearchAsync(session, new UpdateBordxFilter { CaseFrom = one.CaseNo.ToLowerInvariant(), CaseTo = one.CaseNo }), r => Assert.Equal(one.CaseNo, r.CaseNo));
        Assert.Empty(await Repository.SearchAsync(session, new UpdateBordxFilter { Payor = "TA", DoaFrom = new DateTime(2099, 1, 1) }));
        var voidRows = await Repository.SearchAsync(session, new UpdateBordxFilter { UnbordxedOnly = false, BordxFrom = "VOID", BordxTo = "VOID" });
        Assert.NotEmpty(voidRows);
        Assert.All(voidRows, r => Assert.Equal("VOID", r.BordxNo));
    }

    [Fact]
    public async Task Worksheets_go_into_a_new_bordx_which_is_then_updated_and_a_worksheet_already_bordxed_is_refused_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (claims, bordxes, receipts) = (await Count(read, Checksum), await Count(read, BordxChecksum), await Count(read, ReceiptChecksum));
        var pending = (await Repository.SearchAsync(read, new UpdateBordxFilter { Payor = "TA", UnbordxedOnly = true })).Where(r => r.TotalApproved > 0).Take(3).ToList();
        Assert.Equal(3, pending.Count);
        var voided = (await Repository.SearchAsync(read, new UpdateBordxFilter { Payor = "TA", UnbordxedOnly = false })).First(r => r.BordxNo == "VOID");
        var bordxed = await read.Connection.QueryFirstAsync<(string Case, int Version)>("SELECT TOP (1) RTRIM(CASNumber), ClaimVersion FROM genisis.WS_Claim WHERE BordxRefNo <> '' AND BordxRefNo <> 'VOID' ORDER BY CASNumber DESC", transaction: read.Transaction);
        var day = new DateTime(2026, 10, 30, 9, 0, 0);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var one = new UpdateBordxClaim(pending[0].CaseNo, pending[0].ClaimVersion);
            var two = new UpdateBordxClaim(pending[1].CaseNo, pending[1].ClaimVersion);
            var (r1, payor1, approved1) = await Repository.ApplyAsync(write, one, "ZZ-E2E-UPD1");
            Assert.Equal((0, "TA", pending[0].TotalApproved), (r1, payor1, approved1));
            Assert.Equal(0, (await Repository.ApplyAsync(write, two, "ZZ-E2E-UPD1")).Result);
            Assert.Equal(2, (await Repository.ApplyAsync(write, one, "ZZ-E2E-UPD2")).Result);
            Assert.Equal(2, (await Repository.ApplyAsync(write, new UpdateBordxClaim(bordxed.Case, bordxed.Version), "ZZ-E2E-UPD2")).Result);
            Assert.Equal(3, (await Repository.ApplyAsync(write, new UpdateBordxClaim(voided.CaseNo, voided.ClaimVersion), "ZZ-E2E-UPD2")).Result);
            Assert.Equal(1, (await Repository.ApplyAsync(write, new UpdateBordxClaim("ZZ-NO-CASE", 1), "ZZ-E2E-UPD2")).Result);

            var claim = await write.Connection.QuerySingleAsync("SELECT RTRIM(BordxRefNo) AS BordxNo, RTRIM(ClaimStatus) AS Status, ClaimBordxStatus AS Bordxed FROM genisis.WS_Claim WHERE CASNumber = @c AND ClaimVersion = @v", new { c = one.CaseNo, v = one.Version }, write.Transaction);
            Assert.Equal(("ZZ-E2E-UPD1", "C", true), ((string)claim.BordxNo, (string)claim.Status, (bool)claim.Bordxed));
            Assert.Equal("ZZ-E2E-UPD1", await write.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(INVBordxRefNo) FROM genisis.WS_ClaimInvoice WHERE CASNumber = @c AND INVWSversion = @v", new { c = one.CaseNo, v = one.Version.ToString() }, write.Transaction));

            var request = new UpdateBordxRequest { Claims = [one, two], BordxNo = "ZZ-E2E-UPD1", BordxDate = day, AcMonth = "7", AcYear = "2026", ToPayorDate = day.AddDays(1) };
            Assert.Equal(0, await Repository.SaveBordxAsync(write, request, "TA", "E2E"));
            var bordx = await write.Connection.QuerySingleAsync("SELECT RTRIM(BordxPAYCode) AS Payor, BordxClaimAmt AS Amount, BordxNoClaims AS Claims, BordxDate AS Day, RTRIM(BordxBy) AS UserCode, RTRIM(PAYToACPeriodMth) AS Mth, BordxPAYToStatus AS Sent, BordxPAYSentDate AS SentDate, RTRIM(BordxPAYDOCFRStatus) AS Back FROM genisis.WS_Bordx WHERE BordxRefNo = 'ZZ-E2E-UPD1'", transaction: write.Transaction);
            Assert.Equal(("TA", pending[0].TotalApproved + pending[1].TotalApproved, 2, day.Date, "E2E", "7", "1", day.AddDays(1).Date, "0"), ((string)bordx.Payor, (decimal)bordx.Amount, (int)bordx.Claims, (DateTime)bordx.Day, (string)bordx.UserCode, (string)bordx.Mth, ((string)bordx.Sent).Trim(), (DateTime)bordx.SentDate, ((string)bordx.Back).Trim()));

            // a third worksheet into the same bordx: the totals follow the worksheets in it now
            Assert.Equal(0, (await Repository.ApplyAsync(write, new UpdateBordxClaim(pending[2].CaseNo, pending[2].ClaimVersion), "ZZ-E2E-UPD1")).Result);
            Assert.Equal(0, await Repository.SaveBordxAsync(write, request, "TA", "E2E"));
            var again = await write.Connection.QuerySingleAsync("SELECT BordxClaimAmt AS Amount, BordxNoClaims AS Claims FROM genisis.WS_Bordx WHERE BordxRefNo = 'ZZ-E2E-UPD1'", transaction: write.Transaction);
            Assert.Equal((pending[0].TotalApproved + pending[1].TotalApproved + pending[2].TotalApproved, 3), ((decimal)again.Amount, (int)again.Claims));
            Assert.Equal(1, await Repository.SaveBordxAsync(write, request, "SE", "E2E"));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Bordx WHERE BordxRefNo = 'ZZ-E2E-UPD1'"));

            var receipt = new UpdateBordxRequest { Claims = [one], BordxNo = "ZZ-E2E-UPD1", ReceiptNo = "zz-r1", ReceiptDate = day, ChequeAmount = 50m, ChequeNo = "chq1" };
            await Repository.SaveMnrbReceiptAsync(write, receipt, two, 12.5m, "E2E", day);
            await Repository.SaveMnrbReceiptAsync(write, new UpdateBordxRequest { Claims = [one], BordxNo = "ZZ-E2E-UPD1", ReceiptNo = "zz-r1", ReceiptDate = day, ChequeAmount = 60m, ChequeNo = "chq1" }, one, 15m, "E2E", day);
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.Pay_ReceiptfrMNRB WHERE BordxNo = 'ZZ-E2E-UPD1'"));
            var row = await write.Connection.QuerySingleAsync("SELECT RTRIM(ReceiptNo) AS ReceiptNo, ReceiptAmt AS Amount, RTRIM(ChequeNo) AS ChequeNo, RTRIM(CASNumber) AS CaseNo, RecMNRBAmt AS Checked, RTRIM(MNRBLastUpdateUser) AS UserCode FROM genisis.Pay_ReceiptfrMNRB WHERE BordxNo = 'ZZ-E2E-UPD1'", transaction: write.Transaction);
            Assert.Equal(("ZZ-R1", 60m, "CHQ1", one.CaseNo, 15m, "E2E"), ((string)row.ReceiptNo, (decimal)row.Amount, (string)row.ChequeNo, (string)row.CaseNo, (decimal)row.Checked, (string)row.UserCode));
        }

        Assert.Equal(claims, await Count(read, Checksum));
        Assert.Equal(bordxes, await Count(read, BordxChecksum));
        Assert.Equal(receipts, await Count(read, ReceiptChecksum));
    }
}
