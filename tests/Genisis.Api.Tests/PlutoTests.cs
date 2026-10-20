using System.IO.Compression;
using System.Security;
using System.Text;
using Dapper;
using Genisis.Api.Pluto;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class PlutoTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    /// <summary>A worksheet row with every Pluto column; values by Excel column number.</summary>
    private static object?[] Row(params (int Column, object? Value)[] values)
    {
        var cells = new object?[30];
        cells[0] = 1d; cells[1] = "P"; cells[2] = "POL1"; cells[3] = "ALI BIN ABU"; cells[27] = "PL"; cells[28] = new DateTime(2026, 1, 5);
        foreach (var (column, value) in values) cells[column - 1] = value;
        return cells;
    }

    private static readonly object?[] Heading = Enumerable.Range(1, 30).Select(i => (object?)$"Col{i}").ToArray();

    [Fact]
    public void Rows_are_read_from_row_two_until_column_a_is_empty()
    {
        var read = PlutoExcelReader.Convert([Heading, Row(), Row((1, 2d), (2, "S"), (16, 800101015555d), (17, "15/06/1980"), (20, 45658d)),
            Row((1, null)), Row((1, 9d))]);

        Assert.Empty(read.Errors);
        Assert.Equal(2, read.Rows.Count);
        var second = read.Rows[1];
        Assert.Equal(3, second.RowNumber);
        Assert.Equal(2m, second.Values["No"]);
        Assert.Equal("800101015555", second.Values["MBMIcBcPp"]);
        Assert.Equal(new DateTime(1980, 6, 15), second.Values["MBMDOB"]);
        Assert.Equal(new DateTime(2025, 1, 1), second.Values["MBMPayorEffDate"]);
        Assert.Null(second.Values["MBMPayorExpDate"]);
        Assert.Equal("", second.Values["MBMAddress1"]);
        Assert.Equal(new DateTime(2026, 1, 5), second.Values["MBMBordxDate"]);
        Assert.Equal(PlutoExcelLayout.Columns.Count, second.Values.Count);
    }

    [Fact]
    public void Bad_cells_are_reported_by_row_and_column()
    {
        var read = PlutoExcelReader.Convert([Heading, Row((1, "x")), Row((14, "MF")), Row((17, "31/02/2020"))]);
        Assert.Equal(["Row 2: No is not a number.", "Row 3: MBMSex is longer than 1 characters.", "Row 4: MBMDOB is not a valid date."], read.Errors);
        Assert.Empty(PlutoExcelReader.Convert([Heading]).Rows);
    }

    [Fact]
    public void Ascii_file_follows_the_desktop_layout()
    {
        var principal = new Dictionary<string, object?>
        {
            ["RELCode"] = "P", ["MBMPolicyNo"] = "POL1", ["MBMName"] = "ALI", ["MBMDOB"] = new DateTime(1980, 6, 15), ["MBMRBAmt"] = 150.50m,
        };
        var supplementary = new Dictionary<string, object?> { ["RELCode"] = "S", ["MBMName"] = "SITI", ["MBMCLNExclusion"] = "END" };

        var file = PlutoAsciiExporter.Build("PL", new DateTime(2026, 1, 5), [principal, supplementary]);
        var lines = Encoding.Latin1.GetString(file.Content).Split("\r\n");

        Assert.Equal("PL05012026.txt", file.FileName);
        Assert.Equal((1, 1), (file.Principals, file.Supplementaries));
        Assert.Equal("FPL05012026", lines[0]);
        Assert.Equal("P", lines[1][..1]);
        Assert.Equal("POL1".PadRight(25), lines[1][1..26]);
        Assert.Equal("ALI".PadRight(60), lines[1][46..106]);
        // DOB follows the IC number: 1 + 25 + 15 + 5 + 60 + 90 + 20 + 5 + 15 + 36 + 20 = 292 characters before it.
        Assert.Equal("15061980", lines[1][292..300]);
        // MBMRBAmt is the last filled field of the principal, so the record ends with it.
        Assert.EndsWith(" 150.5", lines[1]);
        // The supplementary's last data field is filled, so only its padding and the two blank fields after it are trimmed.
        var width = PlutoAsciiLayout.Fields.Sum(f => f.Width) - (2500 - "END".Length) - 20 - 752;
        Assert.Equal(width, lines[2].Length);
        Assert.EndsWith("END", lines[2]);
        Assert.Equal("T" + "1".PadRight(10) + "1".PadRight(10) + new string(' ', 4778), lines[3]);
        Assert.Equal("", lines[4]);
    }

    [Fact]
    public void Ascii_request_messages_follow_the_form_order()
    {
        Assert.Equal(PlutoMessages.EnterHealthCode, PlutoService.Validate(new PlutoAsciiRequest()));
        Assert.Equal(PlutoMessages.EnterPayorCode, PlutoService.Validate(new PlutoAsciiRequest { HealthCode = "N" }));
        Assert.Equal(PlutoMessages.EnterBordxDate, PlutoService.Validate(new PlutoAsciiRequest { HealthCode = "N", PayorCode = "PL" }));
        Assert.Null(PlutoService.Validate(new PlutoAsciiRequest { HealthCode = "N", PayorCode = "PL", BordxDate = DateTime.Today }));
    }

    [Fact]
    public void A_real_xlsx_workbook_is_read()
    {
        using var stream = Xlsx([["No", "REL"], ["1", "P"], ["2", "S"]]);
        var sheet = PlutoExcelReader.ReadSheet(stream);
        Assert.NotNull(sheet);
        Assert.Equal(3, sheet.Count);
        Assert.Equal("S", PlutoExcelReader.Text(sheet[2][1]));
        Assert.Null(PlutoExcelReader.ReadSheet(new MemoryStream(Encoding.ASCII.GetBytes("not a workbook"))));
    }

    [Fact]
    public async Task Upload_and_ascii_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var bordx = new DateTime(1999, 12, 31);
        var first = await conn.ExecuteScalarAsync<decimal>("SELECT ISNULL(MAX(No), 0) + 1 FROM dbo.MBMExcellUploadShelly", transaction: tx);
        var read = PlutoExcelReader.Convert([Heading, Row((1, (double)first), (28, "ZZ"), (29, bordx)),
            Row((1, (double)first + 1), (2, "S"), (28, "ZZ"), (29, bordx), (17, "01/02/1990"))]);
        Assert.Empty(read.Errors);

        await PlutoService.InsertAsync(conn, tx, read.Rows, CancellationToken.None);

        var rows = (await conn.QueryAsync("genisis.Pluto_AsciiRows", new { bordxDate = bordx, pay = "ZZ" }, tx, commandType: System.Data.CommandType.StoredProcedure))
            .Select(r => (IDictionary<string, object?>)r).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateTime(1990, 2, 1), rows[1]["MBMDOB"]);
        var file = PlutoAsciiExporter.Build("ZZ", bordx, rows);
        Assert.Equal((1, 1, "ZZ31121999.txt"), (file.Principals, file.Supplementaries, file.FileName));

        var payors = await conn.QueryAsync<PlutoPayor>("genisis.Pluto_Payors", new { hlt = "N" }, tx, commandType: System.Data.CommandType.StoredProcedure);
        Assert.NotEmpty(payors);

        var again = await Assert.ThrowsAsync<PlutoException>(() => PlutoService.InsertAsync(conn, tx, read.Rows, CancellationToken.None));
        Assert.Equal(PlutoMessages.AlreadyUploaded(2, first), again.Message);
        await tx.RollbackAsync();
    }

    /// <summary>A minimal .xlsx with inline-string cells in the first sheet.</summary>
    private static MemoryStream Xlsx(string[][] rows)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }
            Add("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Add("_rels/.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add("xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            var data = new StringBuilder();
            for (var r = 0; r < rows.Length; r++)
            {
                data.Append($"<row r=\"{r + 1}\">");
                for (var c = 0; c < rows[r].Length; c++)
                    data.Append($"<c r=\"{(char)('A' + c)}{r + 1}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(rows[r][c])}</t></is></c>");
                data.Append("</row>");
            }
            Add("xl/worksheets/sheet1.xml", $"""<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{data}</sheetData></worksheet>""");
        }
        stream.Position = 0;
        return stream;
    }
}
