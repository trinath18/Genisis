using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Invoice;
using Genisis.Api.Worksheet.Adjustment;
using Genisis.Api.Worksheet.Calculation;
using Genisis.Api.Worksheet.Engine;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Hands every service the same connection and transaction, and never commits: a whole flow can be run against the database and then rolled back when the helper is disposed.</summary>
internal sealed class SharedTransactionSessions : IDbSessions, IAsyncDisposable
{
    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;

    private SharedTransactionSessions(SqlConnection connection, SqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<SharedTransactionSessions> StartAsync()
    {
        var connection = TestDb.Connections().MainDb();
        await connection.OpenAsync();
        return new SharedTransactionSessions(connection, (SqlTransaction)await connection.BeginTransactionAsync());
    }

    public Task<IDbSession> OpenAsync() => Task.FromResult<IDbSession>(new Shared(_connection, _transaction));

    public Task<IDbSession> BeginAsync() => Task.FromResult<IDbSession>(new Shared(_connection, _transaction));

    public async ValueTask DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _connection.DisposeAsync();
    }

    private sealed class Shared(SqlConnection connection, SqlTransaction transaction) : IDbSession
    {
        public SqlConnection Connection => connection;

        public SqlTransaction? Transaction => transaction;

        public Task CommitAsync() => Task.CompletedTask;

        public Task RollbackAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>The rules of the Registration form that need no database.</summary>
public class WorksheetRegistrationRulesTests
{
    private static RegistrationHeader Header() => new() { CaseNo = "AB0001", MemberNo = "M1", MemberName = "A MEMBER", FinalDiagnosisCount = 1, Address1 = "1 ROAD", PostCode = "50000", City = "KL", State = "KUALA LUMPUR", Bank = "AFFIN BANK", AccountNo = "123456789012" };

    private static RegistrationInvoice Invoice(string payTo, decimal pay = 100m, int day = 5) => new() { Index = 1, InvoiceNo = "I1", InvoiceDate = new DateTime(2026, 10, day), Amount = pay, PayAmount = pay, PayTo = payTo, StayType = "I" };

    [Theory]
    [InlineData("Y", null, "limit for each disability")]
    [InlineData("N", "AB0002", "linked to another case")]
    public void A_plan_with_a_limit_for_each_disability_and_a_linked_case_are_refused(string perDisability, string? link, string words)
    {
        var header = Header();
        header.PerDisabilityIndicator = perDisability;
        header.LinkCase = link;
        Assert.Contains(words, RegistrationRules.Blocked(header));
    }

    [Fact]
    public void An_ordinary_case_is_not_refused() => Assert.Null(RegistrationRules.Blocked(Header()));

    [Fact]
    public void A_member_paid_worksheet_starts_with_the_members_details_and_a_hospital_paid_one_without_them()
    {
        var member = RegistrationRules.Defaults(Header(), "M", [Invoice("M", day: 3), Invoice("M", day: 9)]);
        Assert.Equal(("A MEMBER", "AFFIN BANK", "123456789012", "1 ROAD", "50000", "KL", "KUALA LUMPUR", new DateTime(2026, 10, 9)), (member.PayTo, member.Bank, member.AccountNo, member.Address1, member.PostCode, member.City, member.State, member.LastDocumentDate));
        var hospital = RegistrationRules.Defaults(Header(), "H", [Invoice("H")]);
        Assert.Equal(("", "", "", ""), (hospital.PayTo, hospital.Bank, hospital.AccountNo, hospital.Address1));
        Assert.Equal(new DateTime(2026, 10, 5), hospital.LastDocumentDate);
    }

    [Fact]
    public void The_status_is_found_by_its_two_letters_and_the_twelve_of_the_desktop_are_listed()
    {
        Assert.Equal(12, RegistrationRules.Statuses.Count);
        Assert.Equal("AA - Accepted", RegistrationRules.Status("aa")!.Text);
        Assert.Equal("PA - Pay&Claim/Accepted", RegistrationRules.Status(" PA ")!.Text);
        Assert.Null(RegistrationRules.Status("ZZ"));
        Assert.Null(RegistrationRules.Status(null));
    }

    [Fact]
    public void The_desktops_checks_run_on_what_the_form_holds_in_their_order()
    {
        var grid = new WorksheetEvaluation([], new WorksheetTotals(0m, 0m, 100m, 0m));
        RegistrationRequest Request(string? status) => new() { WorksheetStatus = status };
        Assert.Equal([RegistrationMessages.NoInvoice], RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [], grid, Request("AA"), null)));
        var noDiagnosis = Header();
        noDiagnosis.FinalDiagnosisCount = 0;
        Assert.Equal([RegistrationMessages.NoFinalDiagnosis], RegistrationChecks.Problems(RegistrationRules.Entry(noDiagnosis, [Invoice("H")], grid, Request("AA"), null)));
        Assert.Equal([RegistrationMessages.NoStatus], RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [Invoice("H")], grid, Request(null), null)));
        Assert.Equal([RegistrationMessages.NoStatus], RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [Invoice("H")], grid, Request("ZZ"), null)));
        Assert.Equal([RegistrationMessages.HospitalAmount], RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [Invoice("H", pay: 90m)], grid, Request("AA"), null)));
        Assert.Empty(RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [Invoice("H")], grid, Request("AA"), null)));
    }

    [Fact]
    public void A_member_paid_worksheet_needs_the_address_and_the_banks_digits_come_first()
    {
        var grid = new WorksheetEvaluation([], new WorksheetTotals(0m, 0m, 0m, 0m));
        var request = new RegistrationRequest { WorksheetStatus = "AA", Payment = new Genisis.Api.Worksheet.Adjustment.UpdatePaymentRequest { Bank = "AFFIN BANK", AccountNo = "123" } };
        Assert.Equal([RegistrationMessages.BankAccount, RegistrationMessages.NoAddress], RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [Invoice("M")], grid, request, 12)));
        request.Payment.AccountNo = "123456789012";
        request.Payment.Address1 = "1 ROAD";
        request.Payment.PostCode = "50000";
        request.Payment.City = "KL";
        request.Payment.State = "KUALA LUMPUR";
        Assert.Empty(RegistrationChecks.Problems(RegistrationRules.Entry(Header(), [Invoice("M")], grid, request, 12)));
    }

    [Fact]
    public void The_days_a_plan_allows_are_read_from_its_benefit_rows()
    {
        var days = RegistrationRules.AllowedDays([new PlanBenefit { Code = "101", Days = "150" }, new PlanBenefit { Code = "102", Days = "60" }, new PlanBenefit { Code = "302", Days = null }, new PlanBenefit { Code = "404", Days = "1,200" }]);
        Assert.Equal(new WorksheetRows.LimitDays(150m, 60m, 0m, 1200m, 0m, 0m), days);
    }
}

/// <summary>Worksheet Registration against the database: an invoice is entered for a real case, the form is prepared and the worksheet saved, all in one transaction that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetRegistrationDatabaseTests(ITestOutputHelper output)
{
    private static InvoiceForm InvoiceFor(string cas, string hospital) => new()
    {
        CaseNo = cas, InvoiceNo = "ZZ-" + Guid.NewGuid().ToString("N")[..10], InvoiceDate = new DateTime(2026, 10, 5), PayTo = "H", Payee = hospital, StayType = "I", RoomType = "", ReceiptMode = "", ExcessAmount = 0, InitialAmount = 100,
        Amounts = new() { ["CLMBoardDay"] = 2, ["CLMRB"] = 100, ["CLMBoardAmt"] = 200, ["CLMServices"] = 40.5m, ["CLMPreXRay"] = 10 },
    };

    private static RegistrationService ServiceOn(IDbSessions sessions) => new(
        sessions, new RegistrationReadRepository(), new InvoiceEntryRepository(), new PlanBenefitRepository(), new StayDaysRepository(),
        new WorksheetCalculator(sessions, new WorksheetEngineRepository(), new PlanBenefitRepository(), new PlanTermsRepository(), new BalanceLimitRepository(), new StayDaysRepository()),
        new CalculationRepository(), new AdjustmentRepository(), new RegistrationRepository(), TimeProvider.System);

    private static async Task<(string Cas, string Hospital)> ACaseTheSaveCanDoAsync(IDbSession session)
    {
        var pool = (await session.Connection.QueryAsync<(string Cas, string Hospital)>(
            """
            SELECT TOP (150) RTRIM(c.CASNumber), RTRIM(c.CASHOSCode) FROM dbo.CAS c JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber JOIN dbo.PLN p ON p.PLNCode = m.PLNCode
            WHERE c.CASNumber NOT LIKE 'ZZ%' AND c.CASDischargeDate IS NOT NULL AND c.CASHOSCode IS NOT NULL AND RTRIM(c.CASClaimability) = 'AA' AND ISNULL(RTRIM(p.PLNDisIndicator), 'N') <> 'Y' AND ISNULL(RTRIM(c.LINKCASNo), '') = ''
              AND EXISTS (SELECT 1 FROM dbo.DIAFinal d WHERE d.CASNumber = c.CASNumber) AND EXISTS (SELECT 1 FROM dbo.HOS h WHERE h.HOSCode = c.CASHOSCode) AND c.CASDateAdmitted >= '2020-07-01'
              AND NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber AND ISNULL(i.INVWSed, 0) = 0)
            ORDER BY ABS(CHECKSUM(c.CASNumber))
            """, transaction: session.Transaction)).ToList();
        foreach (var (cas, hospital) in pool)
            if (await new RegistrationRepository().CheckMemberBalanceAsync(session, new WorksheetVersionRows(new() { ["CasNumber"] = cas, ["claimversion"] = 9999, ["CLMFinalApprove"] = 100m }, [], [], [], [], [], [], [], [])) is { Unsupported: false, Reason: null })
                return (cas, hospital);
        throw new InvalidOperationException("no case the save can do in the pool");
    }

    [Fact]
    public async Task A_new_worksheet_is_worked_out_checked_confirmed_and_written_in_one_transaction()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var sessions = await SharedTransactionSessions.StartAsync();
        await using var session = await sessions.OpenAsync();
        var (cas, hospital) = await ACaseTheSaveCanDoAsync(session);
        var invoice = await new InvoiceEntryService(sessions, new InvoiceRepository(), new InvoiceEntryRepository(), TimeProvider.System).SaveAsync(session, InvoiceFor(cas, hospital), "ZZTEST");
        Assert.NotNull(invoice.Index);
        var service = ServiceOn(sessions);

        var form = await service.PrepareAsync(cas);
        Assert.Equal(cas, form.Case.CaseNo);
        Assert.Contains(form.Invoices, (i) => i.Index == invoice.Index && i.PayTo == "H");
        Assert.Equal("H", form.PayTo);
        Assert.Equal(29, form.Lines.Count);
        Assert.NotNull(form.Summary);
        Assert.Null(form.Blocked);
        Assert.Equal(12, form.Statuses.Count);
        Assert.Equal("AA", form.DefaultStatus);
        Assert.Equal(new DateTime(2026, 10, 5), form.Defaults.LastDocumentDate);
        Assert.True(form.Banks.Count > 0 && form.States.Count > 0);
        output.WriteLine($"{cas} version {form.Version}: actual {form.Lines.Sum((l) => l.Actual)}, approved {form.Lines.Sum((l) => l.Approved)}, to hospital {form.Lines.Sum((l) => l.ToHospital)}, to member {form.DueToMember}, balance before {form.Summary!.PreviousBalance}");
        var claimRows = () => session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @v", new { cas, v = form.Version }, session.Transaction);
        Assert.Equal(0, await claimRows());

        var request = new RegistrationRequest { WorksheetStatus = form.DefaultStatus, Payment = form.Defaults };
        var noStatus = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(cas, new RegistrationRequest { Payment = form.Defaults }, "ZZTEST"));
        Assert.Equal(RegistrationMessages.NoStatus, noStatus.Message);

        var asked = await service.SaveAsync(cas, request, "ZZTEST");
        Assert.False(asked.Saved);
        Assert.Equal(RegistrationMessages.AreYouSure, asked.Confirm);
        Assert.Equal(0, await claimRows());

        request.Confirmed = true;
        var saved = await service.SaveAsync(cas, request, "ZZTEST");
        Assert.True(saved.Saved);
        Assert.Equal(form.Version, saved.Version);
        Assert.Equal(1, await claimRows());
        Assert.NotNull(saved.MemberBalance);
        Assert.True(saved.MemberBalance!.Applied);

        var claim = (IDictionary<string, object?>)await session.Connection.QueryFirstAsync("SELECT * FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @v", new { cas, v = form.Version }, session.Transaction);
        Assert.Equal("AA", Convert.ToString(claim["CLMWSStatus"])!.Trim());
        Assert.Equal("ZZTEST", Convert.ToString(claim["ClaimOpenBy"])!.Trim());
        Assert.Equal(form.Summary.TotalApproved, SavedVersion.Number(claim["CLMTotalApproved"]));
        Assert.Equal(form.Summary.PreviousBalance, SavedVersion.Number(claim["CLMPreviousBalance"]));
        foreach (var table in new[] { "ClaimActual", "ClaimApprove", "ClaimHos", "ClaimMember", "ClaimLimit", "ClaimOthers" })
            Assert.Equal(1, await session.Connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM genisis.WS_{table} WHERE CASNumber = @cas AND {(table == "ClaimOthers" ? "ClaimVersion" : "CLMVersion")} = @v", new { cas, v = form.Version }, session.Transaction));
        Assert.True(await session.Connection.ExecuteScalarAsync<bool>("SELECT CAST(INVWSed AS bit) FROM genisis.WS_ClaimInvoice WHERE INVIndex = @i", new { i = invoice.Index }, session.Transaction));
        Assert.Equal(saved.MemberBalance.After, await session.Connection.ExecuteScalarAsync<decimal?>(
            "SELECT m.MBMAvailableLimit FROM dbo.CAS c JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber WHERE c.CASNumber = @cas AND RTRIM(c.MBMPatientCovID) = '00' AND @record = 'MBM'", new { cas, record = saved.MemberBalance.Record }, session.Transaction)
            ?? saved.MemberBalance.After);

        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(cas, request, "ZZTEST"));
        Assert.Equal(RegistrationMessages.NoInvoice, again.Message);
    }

    [Fact]
    public async Task A_case_that_does_not_exist_is_not_found_and_a_rolled_back_flow_leaves_nothing()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        string cas, hospital;
        int version;
        await using (var sessions = await SharedTransactionSessions.StartAsync())
        {
            await using var session = await sessions.OpenAsync();
            (cas, hospital) = await ACaseTheSaveCanDoAsync(session);
            await Assert.ThrowsAsync<NotFoundException>(() => ServiceOn(sessions).PrepareAsync("NO SUCH CASE"));
            await new InvoiceEntryService(sessions, new InvoiceRepository(), new InvoiceEntryRepository(), TimeProvider.System).SaveAsync(session, InvoiceFor(cas, hospital), "ZZTEST");
            var form = await ServiceOn(sessions).PrepareAsync(cas);
            version = form.Version;
            Assert.True((await ServiceOn(sessions).SaveAsync(cas, new RegistrationRequest { WorksheetStatus = form.DefaultStatus, Payment = form.Defaults, Confirmed = true }, "ZZTEST")).Saved);
        }
        await using var read = await TestDb.Sessions().OpenAsync();
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version", new { cas, version }, read.Transaction));
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVNo LIKE 'ZZ-%'", new { cas }, read.Transaction));
    }
}
