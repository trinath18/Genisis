using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace Genisis.Api.Pluto;

/// <summary>A worksheet row converted to the MBMExcellUploadShelly columns (keys are the column names).</summary>
public sealed record PlutoUploadRow(int RowNumber, IReadOnlyDictionary<string, object?> Values);

public sealed record PlutoReadResult(List<PlutoUploadRow> Rows, List<string> Errors);

/// <summary>Reads and converts the Upload Membership Excel (Pluto) worksheet.</summary>
public static class PlutoExcelReader
{
    /// <summary>Text dates as the desktop's Malaysian locale reads them (day first), plus ISO.</summary>
    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "yyyy-MM-dd", "d/M/yyyy H:mm:ss", "yyyy-MM-dd HH:mm:ss"];

    static PlutoExcelReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>The cell values of the first worksheet, row by row (row 1 first); <c>null</c> when the stream is not a workbook.</summary>
    public static List<object?[]>? ReadSheet(Stream stream)
    {
        try
        {
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var rows = new List<object?[]>();
            while (reader.Read() && rows.Count < PlutoExcelLayout.LastRow + 1)
            {
                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++) values[i] = reader.GetValue(i);
                rows.Add(values);
            }
            return rows;
        }
        catch (Exception e) when (e is ExcelDataReader.Exceptions.HeaderException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts rows from row 2 until the first empty column A, as the desktop loop does. Every row is checked first; the caller
    /// uploads nothing when there is an error (the desktop's insert failed on the first bad row and nothing was committed).
    /// </summary>
    public static PlutoReadResult Convert(IReadOnlyList<object?[]> sheet)
    {
        var rows = new List<PlutoUploadRow>();
        var errors = new List<string>();
        for (var index = PlutoExcelLayout.FirstDataRow - 1; index < sheet.Count; index++)
        {
            var rowNumber = index + 1;
            var cells = sheet[index];
            if (Text(Cell(cells, 1)) == "") return new PlutoReadResult(rows, errors);
            if (rowNumber > PlutoExcelLayout.LastRow)
            {
                errors.Add(PlutoMessages.TooManyRows);
                return new PlutoReadResult(rows, errors);
            }
            var values = new Dictionary<string, object?>();
            foreach (var column in PlutoExcelLayout.Columns)
            {
                var cell = Cell(cells, column.Excel);
                switch (column.Kind)
                {
                    case PlutoValueKind.Number:
                        if (Number(cell) is { } number) values[column.Name] = number;
                        else errors.Add(PlutoMessages.NotANumber(rowNumber, column));
                        break;
                    case PlutoValueKind.Date:
                        var text = Text(cell);
                        if (text == "") values[column.Name] = null;
                        else if (Date(cell) is { } date) values[column.Name] = date;
                        else errors.Add(PlutoMessages.NotADate(rowNumber, column));
                        break;
                    default:
                        var value = Text(cell);
                        if (value.Length > column.MaxLength) errors.Add(PlutoMessages.TooLong(rowNumber, column));
                        values[column.Name] = value;
                        break;
                }
            }
            rows.Add(new PlutoUploadRow(rowNumber, values));
            if (errors.Count >= PlutoLimits.MaximumErrors) break;
        }
        return new PlutoReadResult(rows, errors);
    }

    private static object? Cell(object?[] cells, int excelColumn) => excelColumn <= cells.Length ? cells[excelColumn - 1] : null;

    /// <summary>The text VB concatenation produced: numbers without a trailing ".0", dates as dd/MM/yyyy.</summary>
    public static string Text(object? cell) => cell switch
    {
        null or DBNull => "",
        double d => d.ToString("0.###############", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        bool b => b ? "True" : "False",
        _ => System.Convert.ToString(cell, CultureInfo.InvariantCulture)?.Trim() ?? "",
    };

    public static DateTime? Date(object? cell) => cell switch
    {
        DateTime date => date.Date,
        double serial when serial is > 0 and < 2_958_466 => DateTime.FromOADate(serial).Date,
        _ => DateTime.TryParseExact(Text(cell), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.Date : null,
    };

    public static decimal? Number(object? cell) => cell switch
    {
        double d => (decimal)d,
        _ => decimal.TryParse(Text(cell), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null,
    };
}
