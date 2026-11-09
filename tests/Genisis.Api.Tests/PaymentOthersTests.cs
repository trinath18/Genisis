using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.Others;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Payment > Others: the validators and the service, against a fake repository.</summary>
public class PaymentOthersRulesTests
{
    private sealed class FakeRepository : IPaymentOthersRepository
    {
        public List<(string Pv, PaymentOthersRequest Request)> Inserted { get; } = [];

        public Task<List<PaymentOthersRow>> SearchAsync(IDbSession session, PaymentOthersFilter filter, int max) =>
            Task.FromResult(new List<PaymentOthersRow> { new() { PvNo = "CB1", Amount = 10m }, new() { PvNo = "CB2", Amount = 2.5m } });

        public Task<string?> NextPvAsync(IDbSession session, string prefix) => Task.FromResult<string?>(prefix == "CB" ? "CB00112243" : null);

        public Task<int> InsertAsync(IDbSession session, string pvNo, PaymentOthersRequest request, string userCode)
        {
            Inserted.Add((pvNo, request));
            return Task.FromResult(7);
        }
    }

    private static PaymentOthersRequest Valid() => new() { Prefix = "CB", PvDate = new DateTime(2021, 8, 6), PayTo = "LHDN", Amount = 120.5m, ChequeNo = "MBB026500" };

    [Theory]
    [InlineData("prefix", "PV Type is CB, CC or CG.")]
    [InlineData("date", "Please enter the PV Date.")]
    [InlineData("payTo", "Please enter the Pay To.")]
    [InlineData("amount", "Please enter the Amount.")]
    [InlineData("cheque", "Please enter the Cheque No.")]
    [InlineData("charges", "The bank charges cannot be negative.")]
    public void Save_needs_the_voucher_fields(string field, string message)
    {
        var r = Valid();
        switch (field)
        {
            case "prefix": r.Prefix = "XX"; break;
            case "date": r.PvDate = null; break;
            case "payTo": r.PayTo = " "; break;
            case "amount": r.Amount = 0; break;
            case "cheque": r.ChequeNo = ""; break;
            default: r.BankCharges = -1; break;
        }

        Assert.Equal(message, new PaymentOthersRequestValidator().FirstError(r));
        Assert.Null(new PaymentOthersRequestValidator().FirstError(Valid()));
    }

    [Fact]
    public async Task Save_numbers_the_voucher_and_commits()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var service = new PaymentOthersService(sessions, repository, NullLogger<PaymentOthersService>.Instance);
        Assert.Equal(new PaymentOthersDone(7, "CB00112243"), await service.CreateAsync(Valid(), "LISA"));
        Assert.True(sessions.Last.Committed);
        var cc = Valid();
        cc.Prefix = "CC";
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(cc, "LISA"));
        Assert.False(sessions.Last.Committed);
        var found = await service.SearchAsync(new PaymentOthersFilter());
        Assert.Equal((2, 12.5m), (found.Count, found.TotalAmount));
    }
}

/// <summary>Payment > Others against the database; the write is rolled back.</summary>
public class PaymentOthersDatabaseTests
{
    [Fact]
    public async Task A_voucher_is_written_with_its_period_and_found_again()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var repository = new PaymentOthersRepository();
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_PaymentOthers", transaction: read.Transaction);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var pv = (await repository.NextPvAsync(write, "CG"))!;
            var request = new PaymentOthersRequest { Prefix = "CG", PvDate = new DateTime(2021, 8, 6, 14, 0, 0), PayTo = "zz test payee", Amount = 99.99m, ChequeNo = "zz1", CaseNo = "st00000001", BankCharges = 1.5m };
            var id = await repository.InsertAsync(write, pv, request, "ZZTEST");
            var found = Assert.Single(await repository.SearchAsync(write, new PaymentOthersFilter { PayTo = "ZZ TEST", PvFrom = pv, PvTo = pv, DateFrom = new DateTime(2021, 8, 6), DateTo = new DateTime(2021, 8, 6) }, 10));
            Assert.Equal((id, pv, "ZZ TEST PAYEE", 99.99m, "ZZ1", new DateTime(2021, 8, 6), "ST00000001", 1.5m, "08", "2021", "A"),
                (found.Id, found.PvNo, found.PayTo, found.Amount, found.ChequeNo, found.ChequeDate, found.CaseNo, found.BankCharges, found.AccMonth, found.AccYear, found.Status));
            Assert.Equal(99.99m, await write.Connection.ExecuteScalarAsync<decimal>("SELECT PaidAmt FROM genisis.Pay_PaymentOthers WHERE PVOthIndex = @id", new { id }, write.Transaction));
            Assert.Empty(await repository.SearchAsync(write, new PaymentOthersFilter { PayTo = "ZZ TEST", CaseNo = "ST00000002" }, 10));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_PaymentOthers", transaction: read.Transaction));
    }
}
