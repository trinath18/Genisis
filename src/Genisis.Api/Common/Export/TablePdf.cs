using System.Globalization;
using Genisis.Api.LeafletCard;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Genisis.Api.Common.Export;

/// <summary>One column of an exported list: its heading and whether its cells are amounts (right-aligned).</summary>
public sealed class ExportColumn
{
    public string Label { get; set; } = "";
    public bool Numeric { get; set; }
}

/// <summary>One list of an export, with an optional heading above it (a worksheet's benefits, then its summary).</summary>
public sealed class ExportSection
{
    public string? Heading { get; set; }
    public List<ExportColumn> Columns { get; set; } = [];
    public List<List<string?>> Rows { get; set; } = [];
}

/// <summary>A list the page already shows, to be printed: the screens' Export button.</summary>
public sealed class TablePdfRequest
{
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public List<ExportSection> Sections { get; set; } = [];
}

/// <summary>The checks on a print request, so one request cannot ask the server for an unbounded document.</summary>
public static class TablePdfRules
{
    public const int MaxRows = 20_000;
    public const int MaxColumns = 40;
    public const int MaxSections = 10;
    public const int MaxCellLength = 2_000;
    public const int MaxTitleLength = 200;

    public static string? Problem(TablePdfRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return "The export needs a title.";
        if (request.Title.Length > MaxTitleLength || request.Subtitle?.Length > MaxTitleLength) return $"The title can be at most {MaxTitleLength} characters.";
        if (request.Sections.Count == 0) return "There is nothing to export.";
        if (request.Sections.Count > MaxSections) return $"An export can have at most {MaxSections} lists.";
        if (request.Sections.Sum(s => s.Rows.Count) > MaxRows) return $"An export can have at most {MaxRows:N0} rows; narrow the search first.";
        foreach (var section in request.Sections)
        {
            if (section.Columns.Count == 0) return "Each list needs at least one column.";
            if (section.Columns.Count > MaxColumns) return $"A list can have at most {MaxColumns} columns.";
            if (section.Heading?.Length > MaxTitleLength) return $"A heading can be at most {MaxTitleLength} characters.";
            if (section.Columns.Any(c => c.Label.Length > MaxCellLength)
                || section.Rows.Any(r => r.Count > section.Columns.Count || r.Any(c => c?.Length > MaxCellLength)))
                return "A row has more cells than the list has columns, or a cell is too long.";
        }
        return null;
    }

    /// <summary>
    /// The share of the page width each column gets: the longest text in it (heading or cell, at most 40 characters), with a floor so a
    /// short column stays readable.
    /// </summary>
    public static double[] ColumnShares(ExportSection section)
    {
        const int Cap = 40, Floor = 4;
        var widths = section.Columns.Select((c, i) =>
        {
            var longest = Math.Max(c.Label.Length, section.Rows.Select(r => i < r.Count ? r[i]?.Length ?? 0 : 0).DefaultIfEmpty(0).Max());
            return (double)Math.Clamp(longest, Floor, Cap);
        }).ToArray();
        var total = widths.Sum();
        return widths.Select(w => w / total).ToArray();
    }
}

/// <summary>Renders an exported list as a landscape A4 PDF: the title, who printed it and when, then each list as a table whose
/// heading row repeats on every page, and the page number in the footer.</summary>
public static class TablePdf
{
    private const string FontName = "Arial";

    static TablePdf()
    {
        // The same font source as the leaflet and card PDFs (PDFsharp needs one before the first document is rendered).
        try
        {
            if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            else GlobalFontSettings.FontResolver ??= new PdfFontResolver();
        }
        catch (InvalidOperationException)
        {
            // Already set by the first PDF rendered in this process.
        }
    }

    public static byte[] Render(TablePdfRequest request, string printedBy, DateTime printedAt)
    {
        var document = new Document();
        document.Info.Title = request.Title;
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontName;
        normal.Font.Size = 8;

        var section = document.AddSection();
        var page = section.PageSetup;
        page.PageFormat = PageFormat.A4;
        page.Orientation = Orientation.Landscape;
        page.TopMargin = page.BottomMargin = Unit.FromCentimeter(1.5);
        page.LeftMargin = page.RightMargin = Unit.FromCentimeter(1.2);
        // A4 landscape: 29.7 cm wide less the margins.
        var usable = Unit.FromCentimeter(29.7 - 2 * 1.2);

        var title = section.AddParagraph(request.Title);
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        if (!string.IsNullOrWhiteSpace(request.Subtitle)) section.AddParagraph(request.Subtitle).Format.Font.Size = 10;
        var printed = section.AddParagraph($"Printed by {printedBy} on {printedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
        printed.Format.Font.Color = Colors.Gray;
        printed.Format.SpaceAfter = Unit.FromPoint(8);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Size = 7;
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        foreach (var list in request.Sections)
        {
            if (!string.IsNullOrWhiteSpace(list.Heading))
            {
                var heading = section.AddParagraph(list.Heading);
                heading.Format.Font.Size = 10;
                heading.Format.Font.Bold = true;
                heading.Format.SpaceBefore = Unit.FromPoint(6);
                heading.Format.SpaceAfter = Unit.FromPoint(4);
                heading.Format.KeepWithNext = true;
            }
            AddTable(section, list, usable);
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void AddTable(Section section, ExportSection list, Unit usable)
    {
        var table = section.AddTable();
        table.Borders.Color = Colors.LightGray;
        table.Borders.Width = 0.25;
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);
        table.LeftPadding = table.RightPadding = Unit.FromPoint(3);
        var shares = TablePdfRules.ColumnShares(list);
        for (var i = 0; i < list.Columns.Count; i++)
        {
            var column = table.AddColumn(Unit.FromPoint(usable.Point * shares[i]));
            column.Format.Alignment = list.Columns[i].Numeric ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        }

        var head = table.AddRow();
        head.HeadingFormat = true;
        head.Format.Font.Bold = true;
        head.Shading.Color = new Color(0xE8, 0xEE, 0xF6);
        for (var i = 0; i < list.Columns.Count; i++) head.Cells[i].AddParagraph(list.Columns[i].Label);

        var shade = new Color(0xF7, 0xF9, 0xFC);
        for (var r = 0; r < list.Rows.Count; r++)
        {
            var row = table.AddRow();
            if (r % 2 == 1) row.Shading.Color = shade;
            var cells = list.Rows[r];
            for (var i = 0; i < list.Columns.Count; i++) row.Cells[i].AddParagraph(i < cells.Count ? cells[i] ?? "" : "");
        }
        if (list.Rows.Count == 0)
        {
            var empty = table.AddRow();
            empty.Cells[0].MergeRight = list.Columns.Count - 1;
            empty.Cells[0].AddParagraph("No records.");
        }
    }
}
