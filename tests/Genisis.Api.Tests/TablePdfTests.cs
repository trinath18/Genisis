using System.Text;
using Genisis.Api.Common.Export;
using Genisis.Api.Controllers;

namespace Genisis.Api.Tests;

/// <summary>The Export button's PDF: the request checks, the column widths and the document itself.</summary>
public class TablePdfTests
{
    private static TablePdfRequest Request(int rows = 3) => new()
    {
        Title = "Worksheet Tracking",
        Subtitle = "Pending worksheets",
        Sections =
        [
            new()
            {
                Heading = "Benefits",
                Columns = [new() { Label = "Case No" }, new() { Label = "Member Name" }, new() { Label = "Amount", Numeric = true }],
                Rows = Enumerable.Range(1, rows).Select(i => new List<string?> { $"AB{i:00000000}", $"MEMBER NUMBER {i}", $"{i * 10:N2}" }).ToList(),
            },
        ],
    };

    [Fact]
    public void A_complete_request_has_no_problem() => Assert.Null(TablePdfRules.Problem(Request()));

    [Fact]
    public void The_checks_refuse_what_cannot_be_printed()
    {
        var noTitle = Request();
        noTitle.Title = " ";
        Assert.Equal("The export needs a title.", TablePdfRules.Problem(noTitle));

        var empty = Request();
        empty.Sections.Clear();
        Assert.Equal("There is nothing to export.", TablePdfRules.Problem(empty));

        var tooMany = Request(TablePdfRules.MaxRows + 1);
        Assert.StartsWith("An export can have at most 20,000 rows", TablePdfRules.Problem(tooMany));

        var wideRow = Request();
        wideRow.Sections[0].Rows.Add(["1", "2", "3", "4"]);
        Assert.Equal("A row has more cells than the list has columns, or a cell is too long.", TablePdfRules.Problem(wideRow));

        var longCell = Request();
        longCell.Sections[0].Rows[0][1] = new string('x', TablePdfRules.MaxCellLength + 1);
        Assert.Equal("A row has more cells than the list has columns, or a cell is too long.", TablePdfRules.Problem(longCell));

        var noColumns = Request();
        noColumns.Sections[0].Columns.Clear();
        noColumns.Sections[0].Rows.Clear();
        Assert.Equal("Each list needs at least one column.", TablePdfRules.Problem(noColumns));
    }

    [Fact]
    public void Columns_share_the_page_by_their_longest_text_with_a_floor_and_a_cap()
    {
        var section = new ExportSection
        {
            Columns = [new() { Label = "A" }, new() { Label = "Name" }, new() { Label = "Remarks" }],
            Rows = [["1", "TEN CHARS!", new string('r', 100)]],
        };
        var shares = TablePdfRules.ColumnShares(section);
        // 4 (floor), 10, 40 (cap) of 54.
        Assert.Equal([4 / 54.0, 10 / 54.0, 40 / 54.0], shares.Select(s => Math.Round(s, 6)).ToArray(), (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.Equal(1.0, shares.Sum(), 6);
    }

    [Fact]
    public void A_list_renders_as_a_pdf_over_several_pages_with_its_heading_repeated()
    {
        var pdf = TablePdf.Render(Request(400), "TESTER", new DateTime(2026, 11, 15, 9, 30, 0));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var text = Encoding.Latin1.GetString(pdf);
        // 400 rows at 8 pt need several landscape pages.
        var pages = System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page[^s]").Count;
        Assert.True(pages >= 5, $"{pages} pages");
    }

    [Fact]
    public void An_empty_list_still_renders_with_its_columns()
    {
        var pdf = TablePdf.Render(Request(0), "TESTER", DateTime.Now);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Theory]
    [InlineData("Worksheet Tracking", "worksheet-tracking")]
    [InlineData("  Worksheet AB0001 / version 2 ", "worksheet-ab0001-version-2")]
    [InlineData("///", "export")]
    public void The_file_name_comes_from_the_title(string title, string expected) => Assert.Equal(expected, ExportController.FileName(title));
}
