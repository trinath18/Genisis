using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Receipt.Others;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Receipt &gt; Others: the validators and the service, against a fake repository.</summary>
public class ReceiptOthersRulesTests
{
    private sealed class FakeRepository : IReceiptOthersRepository
    {
        public List<ReceiptOthersRow> Rows { get; set; } = [];

        public int Changed { get; set; } = 1;

        public int Removed { get; set; } = 1;

        public List<string> Calls { get; } = [];

        public Task<List<ReceiptHospital>> HospitalsAsync(IDbSession session) => Task.FromResult(new List<ReceiptHospital> { new("H1", "Hospital 1") });

        public Task<List<ReceiptOthersRow>> SearchAsync(IDbSession session, ReceiptOthersFilter filter, int max)
        {
            Calls.Add($"search {max}");
            return Task.FromResult(Rows);
        }

        public Task<int> AddAsync(IDbSession session, ReceiptOthersRequest request, string userCode, DateTime today)
        {
            Calls.Add($"add {request.Hospital} {userCode} {today:yyyy-MM-dd}");
            return Task.FromResult(42);
        }

        public Task<int> UpdateAsync(IDbSession session, int id, ReceiptOthersRequest request, string userCode, DateTime today)
        {
            Calls.Add($"update {id} {request.ReceiptNo} {userCode}");
            return Task.FromResult(Changed);
        }

        public Task<int> DeleteAsync(IDbSession session, int id)
        {
            Calls.Add($"delete {id}");
            return Task.FromResult(Removed);
        }
    }

    private static ReceiptOthersService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<ReceiptOthersService>.Instance);

    private static ReceiptOthersRequest Full() => new() { Hospital = "H1", ReceiptNo = "R1", ReceiptDate = new DateTime(2026, 1, 2), Amount = 100m, ChequeNo = "C1", AccMonth = 1, AccYear = 2026 };

    [Fact]
    public void Save_asks_for_the_boxes_in_the_desktops_order_with_its_messages()
    {
        var validator = new ReceiptOthersRequestValidator();
        var request = new ReceiptOthersRequest();
        Assert.Equal("Please Enter Hospital Code", validator.FirstError(request));
        request.Hospital = "H1";
        Assert.Equal("Please Enter Receipt No", validator.FirstError(request));
        request.ReceiptNo = "R1";
        Assert.Equal("Please Enter Receipt Date", validator.FirstError(request));
        request.ReceiptDate = new DateTime(2026, 1, 2);
        Assert.Equal("Please Enter Receipt Amount", validator.FirstError(request));
        request.Amount = 10m;
        Assert.Equal("Please Enter Cheque No", validator.FirstError(request));
        request.ChequeNo = "C1";
        Assert.Null(validator.FirstError(request));
    }

    [Fact]
    public void Update_has_its_own_messages_and_asks_for_the_cheque_no_before_the_amount()
    {
        var validator = new ReceiptOthersUpdateRequestValidator();
        var request = new ReceiptOthersUpdateRequest();
        Assert.Equal("Please Enter Hospital Name!", validator.FirstError(request));
        request.Hospital = "H1";
        Assert.Equal("Please Enter Receipt No!", validator.FirstError(request));
        request.ReceiptNo = "R1";
        Assert.Equal("Please Enter Receipt Date", validator.FirstError(request));
        request.ReceiptDate = new DateTime(2026, 1, 2);
        Assert.Equal("Please Enter Cheque No!", validator.FirstError(request));
        request.ChequeNo = "C1";
        Assert.Equal("Please Enter Cheque Amt!", validator.FirstError(request));
        request.Amount = 0m;
        Assert.Null(validator.FirstError(request));
    }

    [Fact]
    public void Amounts_the_period_and_the_box_lengths_are_checked()
    {
        var validator = new ReceiptOthersRequestValidator();
        Assert.Equal("Receipt Amount cannot be negative.", validator.FirstError(Full().With(r => r.Amount = -1m)));
        Assert.Equal("Bank Charge cannot be negative.", validator.FirstError(Full().With(r => r.BankCharge = -1m)));
        Assert.Equal("A/C Period Mth must be 1 to 12.", validator.FirstError(Full().With(r => r.AccMonth = 13)));
        Assert.Equal("A/C Period Yr must be 1999 to 3000.", validator.FirstError(Full().With(r => r.AccYear = 1998)));
        Assert.Equal("Select a valid hospital.", validator.FirstError(Full().With(r => r.Hospital = new string('h', 11))));
        Assert.Equal("Receipt No is too long.", validator.FirstError(Full().With(r => r.ReceiptNo = new string('r', 31))));
        Assert.Equal("Cheque No is too long.", validator.FirstError(Full().With(r => r.ChequeNo = new string('c', 41))));
        Assert.Equal("Remarks is too long.", validator.FirstError(Full().With(r => r.Remarks = new string('x', 101))));
        var filter = new ReceiptOthersFilterValidator();
        Assert.Null(filter.FirstError(new ReceiptOthersFilter { MonthFrom = 1, MonthTo = 12, YearFrom = 1999, YearTo = 3000 }));
        Assert.Equal("A/C Period Mth must be 1 to 12.", filter.FirstError(new ReceiptOthersFilter { MonthTo = 0 }));
        Assert.Equal("A/C Period Yr must be 1999 to 3000.", filter.FirstError(new ReceiptOthersFilter { YearFrom = 3001 }));
    }

    [Fact]
    public async Task Search_totals_the_rows_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository();
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new ReceiptOthersFilter()))).Message);
        repository.Rows = [new ReceiptOthersRow { Id = 1, Amount = 10.5m }, new ReceiptOthersRow { Id = 2, Amount = null }, new ReceiptOthersRow { Id = 3, Amount = 2m }];
        var result = await Service(repository, new FakeSessions()).SearchAsync(new ReceiptOthersFilter());
        Assert.Equal((3, 12.5m, false), (result.Rows.Count, result.TotalAmount, result.Truncated));
        repository.Rows = Enumerable.Range(1, 5001).Select(i => new ReceiptOthersRow { Id = i, Amount = 1m }).ToList();
        var cut = await Service(repository, new FakeSessions()).SearchAsync(new ReceiptOthersFilter());
        Assert.Equal((5000, 5000m, true), (cut.Rows.Count, cut.TotalAmount, cut.Truncated));
    }

    [Fact]
    public async Task Add_update_and_delete_commit_with_the_user_and_a_missing_receipt_is_not_found()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var service = Service(repository, sessions);
        Assert.Equal(42, await service.AddAsync(Full(), "USER1"));
        Assert.True(sessions.Last.Committed);
        Assert.Equal($"add H1 USER1 {DateTime.Today:yyyy-MM-dd}", repository.Calls[0]);
        await service.UpdateAsync(7, Full(), "USER1");
        await service.DeleteAsync(7, "USER1");
        Assert.Equal(["update 7 R1 USER1", "delete 7"], repository.Calls.Skip(1));
        repository.Changed = 0;
        repository.Removed = 0;
        var update = new FakeSessions();
        Assert.Equal("Record not found.", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, update).UpdateAsync(7, Full(), "U"))).Message);
        Assert.False(update.Last.Committed);
        Assert.Equal("Record not found.", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).DeleteAsync(7, "U"))).Message);
    }
}

internal static class ReceiptOthersRequestExtensions
{
    /// <summary>The request with one box changed.</summary>
    public static ReceiptOthersRequest With(this ReceiptOthersRequest request, Action<ReceiptOthersRequest> change)
    {
        change(request);
        return request;
    }
}

/// <summary>Receipt &gt; Others against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class ReceiptOthersDatabaseTests
{
    private static ReceiptOthersRepository Repository => new();

    private const string Checksum = "SELECT CHECKSUM_AGG(CHECKSUM(ReceiptIndex, ReceiptNo, ReceiptDate, ReceiptAmt, HOSBankCharge, ReceiptChequeNo, ReceiptLastUpdateDate, ReceiptLastUpdateUser, ReceiptRemarks, RECMBMAccPeriodMth, RECMBMAccPeriodYr, HOSName)) FROM genisis.Pay_ReceiptfrHOS";

    private static ReceiptOthersRequest Request(string no) => new()
    {
        Hospital = "ZZTST", ReceiptNo = no, ReceiptDate = new DateTime(2026, 3, 4), Amount = 1234.5m, BankCharge = 12.34m, ChequeNo = "CHQ-" + no, AccMonth = 3, AccYear = 2026, Remarks = "e2e",
    };

    [Fact]
    public async Task A_receipt_is_added_read_back_changed_and_deleted_with_the_user_and_day_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction);
        Assert.NotEmpty(await Repository.HospitalsAsync(read));

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var id = await Repository.AddAsync(write, Request("ZZR1"), "ZZTEST", new DateTime(2026, 10, 29, 9, 0, 0));
            var added = (await Repository.SearchAsync(write, new ReceiptOthersFilter { NoFrom = "ZZR1", NoTo = "ZZR1" }, 10)).Single();
            Assert.Equal((id, "ZZTST", null, "ZZR1", new DateTime(2026, 3, 4), 1234.5m, 12.34m, "CHQ-ZZR1", "e2e", 3, 2026, "ZZTEST", new DateTime(2026, 10, 29)),
                (added.Id, added.HospitalCode, added.HospitalName, added.ReceiptNo, added.ReceiptDate, added.Amount, added.BankCharge, added.ChequeNo, added.Remarks, added.AccMonth, added.AccYear, added.UpdatedBy, added.UpdatedOn));

            Assert.Equal(1, await Repository.UpdateAsync(write, id, Request("ZZR2").With(r => { r.Amount = 99m; r.BankCharge = null; r.AccMonth = null; }), "ZZTWO", new DateTime(2026, 10, 30)));
            var changed = (await Repository.SearchAsync(write, new ReceiptOthersFilter { NoFrom = "ZZR2", NoTo = "ZZR2" }, 10)).Single();
            Assert.Equal((id, "ZZR2", 99m, null, null, "ZZTWO", new DateTime(2026, 10, 30)), (changed.Id, changed.ReceiptNo, changed.Amount, changed.BankCharge, changed.AccMonth, changed.UpdatedBy, changed.UpdatedOn));
            Assert.Equal(0, await Repository.UpdateAsync(write, int.MaxValue, Request("ZZR3"), "ZZTWO", DateTime.Today));

            Assert.Equal(1, await Repository.DeleteAsync(write, id));
            Assert.Equal(0, await Repository.DeleteAsync(write, id));
            Assert.Empty(await Repository.SearchAsync(write, new ReceiptOthersFilter { NoFrom = "ZZR2", NoTo = "ZZR2" }, 10));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction));
    }

    [Fact]
    public async Task The_search_filters_by_hospital_receipt_no_date_and_period_and_lists_the_receipts_without_a_value()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var a = await Repository.AddAsync(write, Request("ZZA"), "ZZTEST", DateTime.Today);
        var b = await Repository.AddAsync(write, Request("ZZB").With(r => { r.ReceiptDate = new DateTime(2026, 4, 5); r.AccMonth = 4; r.AccYear = 2027; r.Hospital = "ZZTS2"; }), "ZZTEST", DateTime.Today);
        var c = await Repository.AddAsync(write, Request("ZZC").With(r => { r.AccMonth = null; r.AccYear = null; }), "ZZTEST", DateTime.Today);
        await write.Connection.ExecuteAsync("UPDATE genisis.Pay_ReceiptfrHOS SET ReceiptNo = '' WHERE ReceiptIndex = @c; UPDATE genisis.Pay_ReceiptfrHOS SET ReceiptDate = NULL WHERE ReceiptIndex = @c", new { c }, write.Transaction);
        async Task<List<int>> Ids(ReceiptOthersFilter f) => (await Repository.SearchAsync(write, f, 100000)).Where(r => new[] { a, b, c }.Contains(r.Id)).Select(r => r.Id).Order().ToList();

        Assert.Equal([a, c], await Ids(new ReceiptOthersFilter { Hospital = "ZZTST" }));
        Assert.Equal([b], await Ids(new ReceiptOthersFilter { Hospital = "ZZTS2" }));
        Assert.Equal([a, b], await Ids(new ReceiptOthersFilter { NoFrom = "ZZA", NoTo = "ZZB" }));
        Assert.Equal([a, b], await Ids(new ReceiptOthersFilter { NoFrom = "ZZA" }));
        Assert.Equal([c], await Ids(new ReceiptOthersFilter { BlankNo = true }));
        Assert.Equal([b], await Ids(new ReceiptOthersFilter { DateFrom = new DateTime(2026, 4, 1), DateTo = new DateTime(2026, 4, 30) }));
        Assert.Equal([c], await Ids(new ReceiptOthersFilter { BlankDate = true }));
        Assert.Equal([a], await Ids(new ReceiptOthersFilter { MonthFrom = 3, MonthTo = 3, YearFrom = 2026, YearTo = 2026 }));
        Assert.Equal([c], await Ids(new ReceiptOthersFilter { BlankMonth = true }));
        Assert.Equal([c], await Ids(new ReceiptOthersFilter { BlankYear = true }));
        Assert.Equal([b], await Ids(new ReceiptOthersFilter { YearFrom = 2027 }));
    }

    [Fact]
    public async Task The_service_adds_in_a_transaction_that_is_not_kept_when_the_session_is_rolled_back()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new ReceiptOthersService(TestDb.Sessions(), Repository, NullLogger<ReceiptOthersService>.Instance);
        await using var read = await TestDb.Sessions().OpenAsync();
        var result = await service.SearchAsync(new ReceiptOthersFilter());
        Assert.Equal(result.Rows.Sum(r => r.Amount ?? 0m), result.TotalAmount);
        Assert.Contains(result.Rows, r => r.HospitalCode == "OTHER");
    }
}
