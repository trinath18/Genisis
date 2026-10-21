using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.ElectronicBanking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Genisis.Api.Tests;

/// <summary>Finance &gt; Electronic Banking: payment rules, the file layout and the service.</summary>
public class FinanceElectronicBankingTests
{
    private static BankPaymentSource Member(string pv = "CC00112650", string? account = "158412345678", string? bank = "MALAYAN BANKING BERHAD", int? digits = 12) => new()
    {
        PayeeType = "M", PvNo = pv, PvDate = new DateTime(2021, 8, 6), CreditRef = "09082021", Payee = "SANTANARAJ A/L LAZARUS", Bank = bank, Account = account,
        Amount = 296.5m, Cases = 1, AccountDigits = digits,
    };

    [Fact]
    public void A_payment_needs_a_bank_account_of_the_length_its_bank_uses()
    {
        Assert.Null(BankPaymentRules.Problem(Member()));
        Assert.Equal("No bank account on record.", BankPaymentRules.Problem(Member(account: null)));
        Assert.Equal("No bank account on record.", BankPaymentRules.Problem(Member(bank: " ")));
        Assert.Equal("Account No has 10 digits; MALAYAN BANKING BERHAD uses 12.", BankPaymentRules.Problem(Member(account: "1584123456")));
        Assert.Null(BankPaymentRules.Problem(Member(account: "1584123456", digits: null)));
        Assert.Null(BankPaymentRules.Problem(Member(account: "1234567890123456") with { Bank = "AGRO BANK", AccountDigits = 15, AccountDigitsTwo = 16 }));
    }

    [Fact]
    public void The_list_masks_the_account_and_reads_the_crediting_date_from_the_reference()
    {
        var row = BankPaymentRules.ToRow(Member());
        Assert.Equal(("M:CC00112650", "MEMBER", "****5678", new DateTime(2021, 8, 9)), (row.Key, row.PayeeType, row.AccountMasked, row.CreditDate));
        Assert.Equal(new DateTime(2021, 8, 6), BankPaymentRules.CreditDate(Member() with { CreditRef = "ONLINE" }));
    }

    [Fact]
    public void The_file_follows_the_configured_layout()
    {
        var text = Encoding.UTF8.GetString(BankPaymentRules.Write([Member(), Member("CC2") with { Payee = "TAN, AH KOW" }], new BankFileOptions()));
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("CreditDate,PvNo,PayeeType,Payee,Bank,Account,Amount,Cases", lines[0]);
        Assert.Equal("09/08/2021,CC00112650,MEMBER,SANTANARAJ A/L LAZARUS,MALAYAN BANKING BERHAD,158412345678,296.50,1", lines[1]);
        Assert.Contains("\"TAN, AH KOW\"", lines[2]);

        var custom = new BankFileOptions { Delimiter = "|", IncludeHeader = false, Columns = ["Account", "Amount", "PvNo"], DateFormat = "yyyyMMdd" };
        Assert.Equal("158412345678|296.50|CC00112650", Encoding.UTF8.GetString(BankPaymentRules.Write([Member()], custom)).Trim());
        Assert.Throws<InvalidOperationException>(() => BankPaymentRules.Write([Member()], new BankFileOptions { Columns = ["Nope"] }));
    }

    [Fact]
    public void A_search_needs_a_date_reference_or_pv_and_a_file_needs_a_ticked_payment()
    {
        Assert.Equal("Please enter the PV date, the credit reference or the PV no.", new BankPaymentFilterValidator().FirstError(new BankPaymentFilter()));
        Assert.Equal("Select a valid payee type.", new BankPaymentFilterValidator().FirstError(new BankPaymentFilter { CreditRef = "06082021", PayeeType = "X" }));
        Assert.Equal("Please select the record", new BankFileRequestValidator().FirstError(new BankFileRequest { CreditRef = "06082021" }));
    }

    private sealed class FakeRepository(List<BankPaymentSource> rows) : IElectronicBankingRepository
    {
        public Task<List<BankPaymentSource>> SearchAsync(IDbSession session, BankPaymentFilter filter) => Task.FromResult(rows);
    }

    private sealed class FixedClock : IFinanceClock
    {
        public DateTime Now => new(2021, 8, 9, 10, 30, 0);
    }

    private static ElectronicBankingService Service(params BankPaymentSource[] rows) =>
        new(new FakeSessions(), new FakeRepository([.. rows]), Options.Create(new BankFileOptions()), new FixedClock(), NullLogger<ElectronicBankingService>.Instance);

    [Fact]
    public async Task The_search_counts_what_is_ready_and_never_returns_a_full_account()
    {
        var hospital = Member("CB00112238", account: null, bank: null) with { PayeeType = "H", Amount = 12002.64m };
        var result = await Service(Member(), hospital).SearchAsync(new BankPaymentFilter { CreditRef = "06082021" });
        Assert.Equal((2, 12299.14m, 1), (result.Rows.Count, result.Total, result.Ready));
        Assert.DoesNotContain(result.Rows, r => r.AccountMasked == "158412345678");
        Assert.Equal("No bank account on record.", result.Rows[1].Problem);
    }

    [Fact]
    public async Task The_file_takes_the_ticked_payments_and_refuses_one_without_a_bank_account_or_gone()
    {
        var hospital = Member("CB00112238", account: null, bank: null) with { PayeeType = "H" };
        var file = await Service(Member(), hospital).FileAsync(new BankFileRequest { CreditRef = "06082021", Keys = ["M:CC00112650", "M:CC00112650"] }, "TESTER");
        Assert.Equal("ONLINE-PAYMENTS-20210809-1030.csv", file.FileName);
        Assert.Equal(2, Encoding.UTF8.GetString(file.Content).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);

        var noAccount = await Assert.ThrowsAsync<FinanceException>(() => Service(Member(), hospital).FileAsync(new BankFileRequest { CreditRef = "x", Keys = ["H:CB00112238"] }, "TESTER"));
        Assert.Equal("PV CB00112238: No bank account on record.", noAccount.Message);
        var gone = await Assert.ThrowsAsync<FinanceException>(() => Service(Member()).FileAsync(new BankFileRequest { CreditRef = "x", Keys = ["M:CC9"] }, "TESTER"));
        Assert.Equal("Payment CC9 is no longer in the list. Search again.", gone.Message);
    }
}

/// <summary>Finance &gt; Electronic Banking against the development databases (read only).</summary>
public class FinanceElectronicBankingDatabaseTests
{
    [Fact]
    public async Task Online_member_and_hospital_payments_are_read_one_row_per_pv()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await new ElectronicBankingRepository().SearchAsync(session, new BankPaymentFilter { PvDateFrom = new DateTime(2021, 8, 1), PvDateTo = new DateTime(2021, 8, 31) });
        Assert.All(rows, r => Assert.True(r.Amount > 0 && r.PayeeType is "M" or "H"));
        Assert.Equal(rows.Count, rows.Select(r => $"{r.PayeeType}:{r.PvNo}").Distinct().Count());
        var members = await new ElectronicBankingRepository().SearchAsync(session, new BankPaymentFilter { PayeeType = "M", PvDateFrom = new DateTime(2021, 8, 1) });
        Assert.All(members, r => Assert.Equal("M", r.PayeeType));
    }
}

/// <summary>Finance &gt; Electronic Banking over HTTP.</summary>
public class FinanceElectronicBankingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Every_endpoint_needs_menu_right_10_and_requests_are_checked()
    {
        using var otherRights = factory.ClientWith("111111111011111111111111111111");
        Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.GetAsync("/api/finance/electronic-banking/lookups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.PostAsJsonAsync("/api/finance/electronic-banking/file", new { })).StatusCode);
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.PostAsJsonAsync("/api/finance/electronic-banking/file", new { });
        Assert.Equal("Please select the record", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        var search = await client.GetAsync("/api/finance/electronic-banking/search");
        Assert.Equal(HttpStatusCode.BadRequest, search.StatusCode);
    }
}
