using System.Text;
using Dapper;
using Genisis.Api.Bordx;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

public class BordxTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    /// <summary>Builds a fixed-width line: each value is written at its field's start column.</summary>
    private static string Line(params (BordxField Field, string Value)[] fields)
    {
        var width = fields.Max(f => f.Field.Start + f.Value.Length - 1);
        var chars = Enumerable.Repeat(' ', width).ToArray();
        foreach (var (field, value) in fields) value.CopyTo(0, chars, field.Start - 1, value.Length);
        return new string(chars);
    }

    private static string Header(string recordTypePayorDateStatus, string fileName) =>
        Line((HeaderLayout.RecordType, recordTypePayorDateStatus), (new BordxField(HeaderLayout.FileName.Start + 1, fileName.Length), fileName));

    private static List<BordxLine> File(params string[] lines) =>
        BordxFile.ReadAsync(new MemoryStream(BordxFile.Encoding.GetBytes(string.Join("\r\n", lines) + "\r\n")), default).GetAwaiter().GetResult()!;

    private static readonly string Policy = "WEBTEST" + DateTime.Now.ToString("HHmmssfff");
    private static readonly string Ic = "800101-14-" + DateTime.Now.ToString("ssfff")[..4];

    private static List<BordxLine> NewFile(string plan = "BT12") => File(
        Header("FXI01092021N", "TESTFILE"),
        Line((MemberLayout.DataType, "P"), (MemberLayout.PolicyNo, Policy), (MemberLayout.Salutation, "MR"), (MemberLayout.Name, "BORDX TEST PRINCIPAL"),
            (MemberLayout.Address1, "1 JALAN UJI"), (MemberLayout.City, "KUALA LUMPUR"), (MemberLayout.PostCode, "50000"), (MemberLayout.State, "WP KUALA LUMPUR"),
            (MemberLayout.IcNumber, Ic), (MemberLayout.DateOfBirth, "01011980"), (MemberLayout.Sex, "M"), (MemberLayout.Race, "M"),
            (MemberLayout.EffectiveDate, "01092021"), (MemberLayout.ExpiryDate, "31082022"), (MemberLayout.PlanCode, plan), (MemberLayout.InsuredType, "H"),
            (MemberLayout.GroupCompany, "WEB TEST COMPANY"), (MemberLayout.RenewalType, "N")),
        Line((MemberLayout.DataType, "W"), (MemberLayout.PolicyNo, Policy), (MemberLayout.Name, "BORDX TEST WIFE"), (MemberLayout.IcNumber, Ic + "W"),
            (MemberLayout.DateOfBirth, "02021982"), (MemberLayout.Sex, "F")),
        Line((MemberLayout.DataType, "T"), (TrailerLayout.TotalPrincipals, "1"), (TrailerLayout.TotalSupplementaries, "1")));

    private static BordxSettings Settings(string fileType, string payor = "XI", string health = "N", bool verifyIc = true, bool labelPrinted = false) =>
        BordxSettings.From(new BordxOptions { FileType = fileType, PayorCode = payor, HealthCode = health, VerifyIc = verifyIc, LabelPrinted = labelPrinted });

    private static DbConnections Connections(string main) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = main }).Build());

    private static BordxService Service(string main) =>
        new(Connections(main), new BordxAsciiExporter(Connections(main)), NullLogger<BordxService>.Instance);

    [Fact]
    public void Line_reads_fields_like_the_desktop_mid()
    {
        var line = new BordxLine(Line((MemberLayout.DataType, "P"), (MemberLayout.PolicyNo, "POL1"), (MemberLayout.DateOfBirth, "29022020"),
            (MemberLayout.EffectiveDate, "31022021"), (MemberLayout.PayorAmount1, "  -12.50")), 2);
        Assert.Equal("POL1", line.Text(MemberLayout.PolicyNo));
        Assert.Equal(new DateTime(2020, 2, 29), line.Date(MemberLayout.DateOfBirth));
        Assert.Null(line.Date(MemberLayout.EffectiveDate));
        Assert.Null(line.Date(MemberLayout.ClinicalExpiryDate));
        Assert.True(line.IsBlank(MemberLayout.ClinicalCardType));
        Assert.Equal(-12.50m, line.Amount(MemberLayout.PayorAmount1));
        Assert.Equal(0, line.Amount(MemberLayout.LoadingPremium));
        Assert.Equal(RecordTypes.Principal, line.RecordType);
    }

    [Fact]
    public async Task File_reader_rejects_binary_content()
    {
        Assert.Null(await BordxFile.ReadAsync(new MemoryStream([0x50, 0x4B, 0x00, 0x03]), default));
        Assert.Equal(2, (await BordxFile.ReadAsync(new MemoryStream(Encoding.Latin1.GetBytes("F\r\nT")), default))!.Count);
    }

    [Fact]
    public void Validator_checks_the_screen_options()
    {
        var header = new BordxLine("FXI01092021N", 1);
        var zurich = new BordxLine(Line((HeaderLayout.RecordType, "F"), (HeaderLayout.PayorFormat, BordxFormats.ZurichMarker)), 1);
        Assert.Equal("The file is empty.", BordxValidator.Validate(Settings("N"), null, upload: false));
        Assert.Equal("Please Select Bordx Type", BordxValidator.Validate(Settings(""), header, upload: false));
        Assert.Equal("Please Select New / Endt file", BordxValidator.Validate(Settings("X"), header, upload: true));
        Assert.Equal("Please Select Health Type", BordxValidator.Validate(Settings("N", health: ""), header, upload: true));
        Assert.Equal("Please Enter Payor Code", BordxValidator.Validate(Settings("N", payor: ""), header, upload: false));
        Assert.Null(BordxValidator.Validate(Settings("N", payor: ""), zurich, upload: false));
        Assert.Null(BordxValidator.Validate(Settings(" n ", health: "N - Non-Sihat"), header, upload: true));
        Assert.Equal("N", Settings("N", health: "N - Non-Sihat").HealthCode);

        var ascii = new AsciiRequest { HealthCode = "N", PayorCode = "XI", Version = "03", ExportOption = "D", BordxFrom = new DateTime(2021, 2, 1), BordxTo = new DateTime(2021, 1, 1) };
        Assert.Equal("Bordx To cannot be before Bordx From.", BordxValidator.Validate(ascii));
        ascii.ExportOption = "B";
        Assert.Equal("Please Enter Receipt Date or Batch No", BordxValidator.Validate(ascii));
        ascii.ExportOption = "D"; ascii.BordxTo = ascii.BordxFrom; ascii.Plan = "Medicalife999";
        Assert.Equal("Unknown plan filter.", BordxValidator.Validate(ascii));
        ascii.Plan = "Medicalife200";
        Assert.Null(BordxValidator.Validate(ascii));
    }

    [Fact]
    public void Ascii_pad_left_justifies_and_truncates()
    {
        Assert.Equal("AB   ", BordxAsciiExporter.Pad("AB", 5));
        Assert.Equal("ABC", BordxAsciiExporter.Pad("ABCDEF", 3));
        Assert.Equal("  ", BordxAsciiExporter.Pad(null, 2));
    }

    [Fact]
    public async Task Lookups_list_health_types_payors_and_plan_filters()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var lookups = await Service(MainDb).GetLookupsAsync(default);
        Assert.NotEmpty(lookups.HealthCodes);
        Assert.NotEmpty(lookups.Payors);
        Assert.Contains("Medicalife200", lookups.PlanFilters);
    }

    [Fact]
    public async Task Verify_reports_the_desktop_messages()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = Service(MainDb);
        var bad = File(
            "XAB01132021N",
            Line((MemberLayout.DataType, "P"), (MemberLayout.PolicyNo, Policy), (MemberLayout.DateOfBirth, "99999999"), (MemberLayout.EffectiveDate, "01092021"),
                (MemberLayout.ExpiryDate, "05092021"), (MemberLayout.PlanCode, "ZZZZ"), (MemberLayout.InsuredType, "H"), (MemberLayout.RenewalType, "Q")),
            "T");
        var result = await service.VerifyAsync(bad, Settings("N", verifyIc: false), "bad.txt", default);
        var messages = result.Errors.Select(e => e.Message).ToList();
        Assert.Contains("1st Record must be header with Data Type 'F'", messages);
        Assert.Contains("Invalid Insurer's Code AB", messages);
        Assert.Contains("Invalid Submission Date !", messages);
        Assert.Contains("Name Is Blank", messages);
        Assert.Contains("IC,BC or PP Is Blank", messages);
        Assert.Contains("Invalid Date format ~ Date Of Birth", messages);
        Assert.Contains("Sex Is Blank", messages);
        Assert.Contains("No such Plan Code - ' ZZZZ' !", messages);
        Assert.Contains("HIS Policy Coverage must be > 30 and < 365 days", messages);
        Assert.Contains("Address Is Blank", messages);
        Assert.Contains("No such Renewal Code - 'Q'", messages);
        Assert.Contains("Clinical Plan Is NULL !", messages);
        Assert.Equal(1, result.TotalPrincipal);

        var good = await service.VerifyAsync(NewFile(), Settings("N"), "good.txt", default);
        Assert.Equal(("01/09/2021", 1, 1), (good.SubmissionDate, good.TotalPrincipal, good.TotalSupplementary));
        Assert.Equal(["Clinical Plan Is NULL !"], good.Errors.Select(e => e.Message).Distinct());
    }

    [Fact]
    public async Task Upload_new_members_then_cancel_through_the_staging_tables()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        var result = await BordxService.UploadAsync(conn, tx, NewFile(), Settings("N", labelPrinted: true), "TEST", default);
        Assert.Equal((1, 1, 0), (result.NewMembers, result.Supplementaries, result.Cancelled));
        var number = Assert.Single(result.MembershipNumbers);
        Assert.StartsWith("XI", number);

        var member = await conn.QueryFirstAsync<(string Policy, string Status, string Plan, int Batch)>(
            "SELECT RTRIM(MBMPolicyNo), RTRIM(MBMStatus), RTRIM(PLNCode), MBMBatchNo FROM dbo.MBM WHERE MBMNumber = @number", new { number }, tx);
        Assert.Equal((Policy, "A", "BT12", result.BatchNo!.Value), member);
        Assert.Equal("BORDX TEST WIFE", await conn.ExecuteScalarAsync<string>(
            "SELECT RTRIM(MBMCName) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND MBMCCoverID = '01'", new { number }, tx));
        Assert.Equal("Y", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(LabelStatus) FROM dbo.MBMOthers WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM HISMaintenance.dbo.MBMCrossReference WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM UploadDb.dbo.MBM WHERE MBMNumber = @number", new { number }, tx));

        var cancel = File(
            Header("FXI15092021C", "CANCELFILE"),
            Line((MemberLayout.DataType, "P"), (MemberLayout.PolicyNo, Policy), (MemberLayout.EffectiveDate, "15092021"), (MemberLayout.ExpiryDate, "31082022"),
                (MemberLayout.PayorAmount1, "-50")),
            "T");
        var cancelled = await BordxService.UploadAsync(conn, tx, cancel, Settings("E"), "TEST", default);
        Assert.Equal(1, cancelled.Cancelled);
        Assert.Equal("C", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMStatus) FROM dbo.MBM WHERE MBMNumber = @number", new { number }, tx));
        var refund = await conn.QueryFirstAsync<(decimal Refund, string Cover)>(
            "SELECT MBMMCORefund, RTRIM(MBMCovID) FROM dbo.MBMRefund WHERE MBMNumber = @number", new { number }, tx);
        Assert.Equal((50m, "00"), refund);
        Assert.Equal("C", await conn.ExecuteScalarAsync<string>(
            "SELECT TOP 1 RTRIM(MBMAmendEdtType) FROM dbo.MBMAmendHistory WHERE MBMNumber = @number ORDER BY MBMAmendID DESC", new { number }, tx));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Upload_refuses_a_file_for_another_payor_and_imports_nothing()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var error = await Assert.ThrowsAsync<BordxException>(() => BordxService.UploadAsync(conn, tx, NewFile(), Settings("N", payor: "PW"), "TEST", default));
        Assert.Equal("Payor Selected Is Not Match The File Payor", error.Message);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Generate_ascii_writes_the_desktop_layout()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        var sample = await conn.QueryFirstOrDefaultAsync<(string Pay, DateTime Bordx)>(
            "SELECT TOP 1 RTRIM(PAYCode), MBMBordxDate FROM dbo.MBM WHERE HLTCode = 'N' AND MBMStatus <> 'D' AND MBMDataStatus <> 'D' AND MBMBordxDate IS NOT NULL ORDER BY MBMBordxDate DESC");
        var service = Service(MainDb);
        var file = await service.ExportAsciiAsync(new AsciiRequest
        {
            HealthCode = "N", PayorCode = sample.Pay, Version = "03", ExportOption = "D", BordxFrom = sample.Bordx, BordxTo = sample.Bordx,
        }, default);
        Assert.NotNull(file);
        var lines = Encoding.Latin1.GetString(file.Content).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(sample.Pay + sample.Bordx.ToString("yyMMdd"), file.FileName);
        Assert.Equal("F" + sample.Pay + sample.Bordx.ToString("ddMMyyyy"), lines[0]);
        Assert.StartsWith("T" + file.Principals.ToString().PadRight(AsciiLayout.TrailerCount), lines[^1]);
        Assert.Equal(1 + 2 * AsciiLayout.TrailerCount + AsciiLayout.TrailerFiller, lines[^1].Length);
        Assert.Equal(301, lines[1].Length);
        Assert.True(file.Principals > 0);

        Assert.Null(await service.ExportAsciiAsync(new AsciiRequest
        {
            HealthCode = "N", PayorCode = "Z9", Version = "03", ExportOption = "D", BordxFrom = sample.Bordx, BordxTo = sample.Bordx,
        }, default));
    }
}
