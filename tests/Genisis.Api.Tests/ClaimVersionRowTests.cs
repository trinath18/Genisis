using System.Globalization;
using Dapper;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The version row of Claim (SaveClaim) as pure rules.</summary>
public class ClaimVersionRowRulesTests
{
    private static readonly DateTime Today = new(2026, 10, 24, 14, 30, 0);

    private static WorksheetEvaluation Grid(string? monthlyLimit = null, string? homeLimit = null)
    {
        var lines = new List<WorksheetLine>
        {
            new(1, 1000m, "FULL", 800m, 800m, 0m),
            new(MonthlyOrHome(true), 100m, monthlyLimit, 100m, 100m, 0m),
            new(MonthlyOrHome(false), 50m, homeLimit, 50m, 50m, 0m),
        };
        return new WorksheetEvaluation(lines, new WorksheetTotals(1150m, 950m, 950m, 0m));
    }

    private static int MonthlyOrHome(bool monthly) => monthly ? ClaimVersionRow.MonthlySlot : ClaimVersionRow.HomeNursingSlot;

    private static WorksheetFinal Final(decimal approved = 950m, decimal excess = -20m, decimal payableToMember = 0m) =>
        new(approved + 20m, 0m, 0m, approved, approved, excess, payableToMember);

    private static ClaimVersionInput Input(Action<ClaimVersionInputBuilder>? change = null)
    {
        var b = new ClaimVersionInputBuilder();
        change?.Invoke(b);
        return b.Build();
    }

    private sealed class ClaimVersionInputBuilder
    {
        public string Status = "AA - Approve";
        public string? PayToCode = "H";
        public LimitKind Kind = LimitKind.Annual;
        public decimal Available = 50000m;
        public decimal PerDisability;
        public string? GpName = " Dr Lee ";
        public DateTime? LastDoc = new DateTime(2026, 10, 1, 9, 0, 0);
        public string? ExGratia;

        public ClaimVersionInput Build() => new()
        {
            CaseNo = " XI00002412 ",
            Version = 3,
            UserCode = "E2E",
            Today = Today,
            WorksheetStatus = Status,
            PayToCode = PayToCode,
            StayType = "I",
            PayTo = " ASSUNTA HOSPITAL ",
            Bank = "MAYBANK",
            AccountNo = "1234567",
            GpName = GpName,
            LastDocumentDate = LastDoc,
            ExGratia = ExGratia,
            Limit = Kind,
            AvailableLimit = Available,
            PerDisabilityLimit = PerDisability,
        };
    }

    private static Dictionary<string, object?> Row(ClaimVersionInput? input = null, WorksheetEvaluation? grid = null, WorksheetFinal? final = null) =>
        ClaimVersionRow.Build(input ?? Input(), grid ?? Grid(), final ?? Final());

    [Fact]
    public void The_row_has_every_parameter_of_the_procedure_and_no_other()
    {
        var row = Row();
        Assert.Equal(56, ClaimVersionRow.Parameters.Count);
        Assert.Equal(ClaimVersionRow.Parameters.OrderBy((n) => n, StringComparer.Ordinal), row.Keys.OrderBy((n) => n, StringComparer.Ordinal));
        Assert.Equal(ClaimVersionRow.Parameters.Count, ClaimVersionRow.Parameters.Distinct().Count());
    }

    [Fact]
    public void The_case_the_user_and_the_dates_are_those_of_the_save()
    {
        var row = Row();
        Assert.Equal(("XI00002412", 3, "E2E", "E2E"), (row["CasNumber"], row["claimversion"], row["ClaimOpenBy"], row["ClaimLastUpdateUser"]));
        Assert.Equal(new DateTime(2026, 10, 24), row["ClaimOpendate"]);
        Assert.Equal(new DateTime(2026, 10, 24), row["ClaimLastUpdateDate"]);
        Assert.Equal(new DateTime(2026, 10, 24), row["ClaimCloseDate"]);
        Assert.Equal(new DateTime(2026, 10, 1), row["CLMLastDocDate"]);
        Assert.Null(Row(Input((b) => b.LastDoc = null))["CLMLastDocDate"]);
    }

    [Fact]
    public void A_version_is_closed_not_bordxed_and_the_constants_are_those_of_the_desktop()
    {
        var row = Row();
        Assert.Equal(("C", "N", "N", "0", ""), (row["ClaimStatus"], row["CLMBordxWSStatus"], row["CLMBordxWSASCII"], row["ClaimBordxStatus"], row["BordxRefNo"]));
        Assert.Equal(("AA", "N", "M", "M"), (row["CLMWSStatus"], row["MBMTopupInd"], row["ExcessCollectedBy"], row["LimitCollectedBy"]));
        foreach (var zero in new[] { "CLMCompExcess", "CLMTotalCompApp", "CLMPreviousNewLimit", "CLMPaidBYMedix2M", "CLMPaidByMedix2H", "CLMMainTUAmt", "CLMPrevMajorMed", "CLMMajorMedFee", "CLMDeductibleAmt", "CLMExGratiaAmt", "CaseMgmtFees" })
            Assert.Equal(0m, row[zero]);
        Assert.Equal("", row["CLMTopMBMNumber"]);
    }

    [Fact]
    public void A_void_worksheet_is_bordxed_with_the_reference_VOID()
    {
        var row = Row(Input((b) => b.Status = "XR - Void"));
        Assert.Equal(("XR", "1", "VOID"), (row["CLMWSStatus"], row["ClaimBordxStatus"], row["BordxRefNo"]));
        Assert.Equal("XR", Row(Input((b) => b.Status = "xr"))["CLMWSStatus"]);
    }

    [Fact]
    public void The_cash_type_follows_the_pay_to_of_the_first_invoice_and_the_text_is_trimmed()
    {
        Assert.Equal("C", Row()["CLMCashType"]);
        Assert.Equal("R", Row(Input((b) => b.PayToCode = "M"))["CLMCashType"]);
        Assert.Equal("", Row(Input((b) => b.PayToCode = null))["CLMCashType"]);
        var row = Row();
        Assert.Equal(("ASSUNTA HOSPITAL", "MAYBANK", "1234567", "Dr Lee", "I"), (row["CLMPayto"], row["CLMBank"], row["CLMAccountNo"], row["CLMGPName"], row["CLMStayType"]));
        Assert.Equal("E", Row(Input((b) => b.ExGratia = "E - Ex-gratia"))["CLMExGratia"]);
        Assert.Equal("", Row()["CLMExGratia"]);
    }

    [Fact]
    public void The_totals_come_from_the_grid_and_the_summary()
    {
        var row = Row();
        Assert.Equal((1150m, 950m, 950m, -20m, 0m), (row["CLMTotalActual"], row["CLMTotalApproved"], row["CLMFinalApprove"], row["CLMExcess"], row["CLMReimburse"]));
        Assert.Equal(950m - 20m, row["CLMPayableByMedix2H"]);
        Assert.Equal(0m, row["CLMPayableByMedix2M"]);
    }

    [Fact]
    public void The_bordx_amount_is_the_final_approve_for_a_reimbursement_and_what_is_payable_for_a_cashless_worksheet()
    {
        var final = Final();
        Assert.Equal(final.FinalApprove, ClaimVersionRow.BordxAmount("R", final, 1m, 2m));
        Assert.Equal(final.FinalApprove, ClaimVersionRow.BordxAmount("r", final, 0m, 0m));
        Assert.Equal(3m, ClaimVersionRow.BordxAmount("C", final, 1m, 2m));
        Assert.Equal(3m, ClaimVersionRow.BordxAmount(null, final, 1m, 2m));
        Assert.Equal(930m, Row()["CLMBordxAmt"]);
        Assert.Equal(950m, Row(Input((b) => b.PayToCode = "M"))["CLMBordxAmt"]);
    }

    [Theory]
    [InlineData(true, "C", 30000, 47002.75, -17002.75)]
    [InlineData(true, "C", 268.56, 298.4, -29.84)]
    [InlineData(true, "C", 100, 100, 0)]
    [InlineData(true, "C", 120, 100, 0)]
    [InlineData(true, "R", 30000, 47002.75, 0)]
    [InlineData(false, "C", 30000, 47002.75, 0)]
    [InlineData(true, "C", 50, -40, 90)]
    public void The_BTBG_amount_is_what_the_member_nets_for_a_BTBG_members_cashless_worksheet_when_the_signs_differ(bool member, string cash, double finalApprove, double payableH, double expected)
    {
        var final = new WorksheetFinal((decimal)finalApprove, 0m, 0m, (decimal)finalApprove, (decimal)finalApprove, 0m, 0m);
        Assert.Equal((decimal)expected, ClaimVersionRow.BtbgAmount(member, cash, final, (decimal)payableH));
    }

    [Fact]
    public void The_previous_balance_and_the_balance_limit_follow_the_limit_of_the_plan()
    {
        Assert.Equal((50000m, 50000m), ClaimVersionRow.Balances(LimitKind.Annual, 50000m, 10000m, 950m));
        Assert.Equal((10000m, 10000m), ClaimVersionRow.Balances(LimitKind.PerDisability, 50000m, 10000m, 950m));
        Assert.Equal((950m, 950m), ClaimVersionRow.Balances(LimitKind.None, 50000m, 10000m, 950m));
        var annual = Row();
        Assert.Equal((50000m, 50000m, 0m), (annual["CLMPreviousBalance"], annual["CLMBalanceLimit"], annual["CLMPrevPerDisLmt"]));
        var none = Row(Input((b) => b.Kind = LimitKind.None));
        Assert.Equal((950m, 950m), (none["CLMPreviousBalance"], none["CLMBalanceLimit"]));
        var perDisability = Row(Input((b) => { b.Kind = LimitKind.PerDisability; b.PerDisability = 7500m; }));
        Assert.Equal((7500m, 7500m, 7500m), (perDisability["CLMPreviousBalance"], perDisability["CLMBalanceLimit"], perDisability["CLMPrevPerDisLmt"]));
    }

    [Theory]
    [InlineData("50,000.00", 50000)]
    [InlineData("1000", 1000)]
    [InlineData("FULL", 0)]
    [InlineData("-", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData(" 250.50 ", 250.5)]
    public void A_limit_text_is_a_number_only_when_it_is_one(string? text, double expected) => Assert.Equal((decimal)expected, ClaimVersionRow.NumericLimit(text));

    [Fact]
    public void The_previous_cancer_and_home_nursing_limits_are_the_limits_of_the_monthly_and_home_nursing_rows()
    {
        var row = Row(grid: Grid("30,000.00", "FULL"));
        Assert.Equal((30000m, 0m), (row["CLMPrevCancerLmt"], row["CLMPrevHomeNCLmt"]));
        var other = Row(grid: Grid(null, "5000"));
        Assert.Equal((0m, 5000m), (other["CLMPrevCancerLmt"], other["CLMPrevHomeNCLmt"]));
    }
}

/// <summary>The version row against the database: its parameters are those of the procedure, and a saved version rebuilds.</summary>
public class ClaimVersionRowReferenceTests(ITestOutputHelper output)
{
    /// <summary>A saved number as the desktop wrote it: money, or the grid's text (a dash for nothing, brackets for a negative, thousands separators).</summary>
    private static decimal Number(object? value)
    {
        if (value is decimal money) return money;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text) || text == "-") return 0m;
        var negative = text.StartsWith('(') && text.EndsWith(')');
        text = text.Trim('(', ')').Replace(",", "");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? (negative ? -n : n) : 0m;
    }

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";

    [Fact]
    public async Task The_parameters_are_those_of_Insert_WS_Claim_2012_in_its_order()
    {
        var worksheetDb = TestDb.WorkSheet;
        if (worksheetDb is null) return;
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(worksheetDb);
        await connection.OpenAsync();
        var parameters = (await connection.QueryAsync<string>("SELECT REPLACE(name, '@', '') FROM sys.parameters WHERE object_id = OBJECT_ID('dbo.Insert_WS_Claim_2012') ORDER BY parameter_id")).ToList();
        Assert.Equal(parameters, ClaimVersionRow.Parameters);
    }

    /// <summary>
    /// Each saved closed version of a sample (about one in twenty of those saved since July 2020) is read back into a grid (the four tables' numbers and the saved limit texts), the inputs the desktop reads from
    /// the screen are taken from the saved row, and every column <see cref="ClaimVersionRow.Build"/> writes is compared with the saved one (not the four that later steps change: the bordx status and reference and
    /// the last update). The columns that follow a rule (the totals, the payable amounts, the bordx amount, the BTBG amount, the previous cancer and home nursing limits and the constants) are the test.
    /// </summary>
    [Fact]
    public async Task A_saved_closed_version_is_rebuilt_column_by_column()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, string? Family, string? Payor, string? Btbg)>(
            """
            SELECT RTRIM(a.CASNumber), a.CLMVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2), RTRIM(o.MBMBTBG)
            FROM genisis.WS_ClaimActual a
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = a.CASNumber AND v.ClaimVersion = a.CLMVersion
            JOIN genisis.WS_Claim w ON w.CASNumber = a.CASNumber AND w.ClaimVersion = a.CLMVersion
            LEFT JOIN dbo.MBMOthersTwo o ON o.MBMNumber = v.MBMNumber
            WHERE a.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND w.ClaimOpenDate >= '2020-07-01' AND w.ClaimStatus = 'C' AND w.CLMPreviousBalance IS NOT NULL AND ABS(CHECKSUM(a.CASNumber, a.CLMVersion)) % 20 = 0
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimApprove p WHERE p.CASNumber = a.CASNumber AND p.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimHos h WHERE h.CASNumber = a.CASNumber AND h.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimMember m WHERE m.CASNumber = a.CASNumber AND m.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimLimit l WHERE l.CASNumber = a.CASNumber AND l.CLMVersion = a.CLMVersion)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(sample.Count >= 300, $"only {sample.Count} versions in the sample");

        var skipped = new HashSet<string>(["ClaimBordxStatus", "BordxRefNo", "ClaimLastUpdateUser", "ClaimLastUpdateDate"], StringComparer.OrdinalIgnoreCase);
        var savedName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["claimversion"] = "ClaimVersion", ["CaseMgmtFees"] = "CLMCaseMgmtFees" };
        var same = ClaimVersionRow.Parameters.Where((p) => !skipped.Contains(p)).ToDictionary((p) => p, (_) => 0);
        var miss = new Dictionary<string, List<string>>();
        var balanceMisses = new Dictionary<string, int>();
        int done = 0, rowsSame = 0;
        foreach (var (cas, version, family, payor, btbg) in sample)
        {
            var flat = WorksheetRows.Flat(family, payor);
            var saved = new Dictionary<string, IDictionary<string, object?>>();
            foreach (var table in new[] { "ClaimActual", "ClaimApprove", "ClaimHos", "ClaimMember", "ClaimLimit" })
                if (await session.Connection.QueryFirstOrDefaultAsync($"SELECT * FROM genisis.WS_{table} WHERE CASNumber = @cas AND CLMVersion = @version", new { cas, version }, session.Transaction) is IDictionary<string, object?> row) saved[table] = row;
            if (saved.Count < 5 || await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version", new { cas, version }, session.Transaction) is not IDictionary<string, object?> raw) continue;
            var claim = new Dictionary<string, object?>(raw, StringComparer.OrdinalIgnoreCase);
            done++;
            var lines = WorksheetRows.Columns(flat).Select((c) => new WorksheetLine(c.Slot, Number(saved["ClaimActual"][c.Column]), Text(saved["ClaimLimit"][c.Column]) is { Length: > 0 } limit && limit != "-" ? limit : null,
                Number(saved["ClaimApprove"][c.Column]), Number(saved["ClaimHos"][c.Column]), Number(saved["ClaimMember"][c.Column]))).ToList();
            var grid = new WorksheetEvaluation(lines, new WorksheetTotals(lines.Sum((l) => l.Actual), lines.Sum((l) => l.Approved), lines.Sum((l) => l.ToHospital), lines.Sum((l) => l.ToMember)));
            var final = WorksheetRules.Final(grid, Number(claim["CLMCoPayRB"]), Number(claim["CLMPreviousBalance"]));
            var perDisability = Number(claim["CLMPrevPerDisLmt"]);
            var input = new ClaimVersionInput
            {
                CaseNo = cas,
                Version = version,
                UserCode = Text(claim["ClaimOpenBy"]),
                Today = Convert.ToDateTime(claim["ClaimOpendate"], CultureInfo.InvariantCulture),
                WorksheetStatus = Text(claim["CLMWSStatus"]),
                PayToCode = Text(claim["CLMCashType"]) == "C" ? "H" : Text(claim["CLMCashType"]) == "R" ? "M" : null,
                StayType = Text(claim["CLMStayType"]),
                PayTo = Text(claim["CLMPayto"]),
                Bank = Text(claim["CLMBank"]),
                AccountNo = Text(claim["CLMAccountNo"]),
                GpName = Text(claim["CLMGPName"]),
                LastDocumentDate = claim["CLMLastDocDate"] as DateTime?,
                ExcessCollectedBy = Text(claim["ExcessCollectedBy"]) is { Length: 1 } e ? e[0] : 'M',
                LimitCollectedBy = Text(claim["LimitCollectedBy"]) is { Length: 1 } l ? l[0] : 'M',
                ExGratia = Text(claim["CLMExGratia"]),
                ExGratiaAmount = Number(claim["CLMExGratiaAmt"]),
                CaseMgmtFees = Number(claim["CLMCaseMgmtFees"]),
                MemberBtbg = btbg == "Y",
                Limit = perDisability != 0 ? LimitKind.PerDisability : LimitKind.Annual,
                AvailableLimit = Number(claim["CLMPreviousBalance"]),
                PerDisabilityLimit = perDisability,
                LifetimeBalance = Number(claim["CLMLifetimeBal"]),
                TakeOverLimit = Number(claim["CLMPrevTOLmt"]),
                LinkCase = Text(claim["CLMLinkCase"]),
                LinkVersion = claim["CLMLinkVersion"] is null ? null : Convert.ToInt32(claim["CLMLinkVersion"], CultureInfo.InvariantCulture),
            };
            var built = ClaimVersionRow.Build(input, grid, final);
            var allSame = true;
            foreach (var parameter in same.Keys.ToList())
            {
                var column = savedName.GetValueOrDefault(parameter, parameter);
                var saved1 = claim[column];
                var value = built[parameter];
                var equal = value switch
                {
                    decimal d => Math.Abs(d - Number(saved1)) < 0.011m,
                    DateTime dt => saved1 is DateTime s ? dt.Date == s.Date : saved1 is null && false,
                    int i => i == (int)Number(saved1),
                    _ => Text(value) == Text(saved1),
                };
                if (value is null) equal = saved1 is null;
                if (parameter == "CLMLastDocDate" && value is null) equal = saved1 is null;
                if (equal) same[parameter]++;
                else
                {
                    allSame = false;
                    if (parameter == "CLMBalanceLimit")
                    {
                        var difference = Number(saved1) - Number(claim["CLMPreviousBalance"]);
                        var kind = Math.Abs(difference - final.FinalApprove) < 0.011m ? "previous + final approve" : Math.Abs(difference + final.FinalApprove) < 0.011m ? "previous - final approve" : "something else";
                        balanceMisses[kind] = balanceMisses.GetValueOrDefault(kind) + 1;
                    }
                    if ((miss.TryGetValue(parameter, out var list) ? list : miss[parameter] = []).Count < 3) miss[parameter].Add($"{cas} v{version}: saved {Text(saved1)} built {Text(value)}");
                }
            }
            if (allSame) rowsSame++;
        }
        Assert.True(done >= 300, $"only {done} versions could be read");
        foreach (var (parameter, n) in same.OrderBy((p) => p.Value)) output.WriteLine($"{parameter}: {n} of {done} equal ({100.0 * n / done:F2}%)");
        foreach (var (parameter, list) in miss) foreach (var line in list) output.WriteLine($"  {parameter} {line}");
        output.WriteLine($"versions with every compared column equal: {rowsSame} of {done}");
        output.WriteLine("the balance limit misses by relation to the previous balance: " + string.Join(", ", balanceMisses.Select((m) => $"{m.Key} {m.Value}")));
        foreach (var (parameter, n) in same) Assert.True(n >= done * 0.92, $"{parameter}: {n} of {done}");
        Assert.True(rowsSame >= done * 0.80, $"{rowsSame} of {done} versions rebuild with every column equal");
    }
}
