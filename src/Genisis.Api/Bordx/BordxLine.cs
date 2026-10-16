using System.Globalization;
using System.Text;

namespace Genisis.Api.Bordx;

/// <summary>One fixed-width bordereaux line. Fields are read by <see cref="BordxField"/> (see <see cref="MemberLayout"/>); text past the end is blank.</summary>
public sealed class BordxLine(string text, int number)
{
    private const string DateFormat = "ddMMyyyy";

    public string Content { get; } = text;
    public int Number { get; } = number;

    public char RecordType => Content.Length > 0 ? Content[0] : ' ';

    /// <summary>The field as written, padding included.</summary>
    public string Raw(BordxField field) =>
        field.Start - 1 >= Content.Length ? "" : Content.Substring(field.Start - 1, Math.Min(field.Length, Content.Length - field.Start + 1));

    /// <summary>The field trimmed (the desktop's Trim(Mid(...))).</summary>
    public string Text(BordxField field) => Raw(field).Trim();

    public bool IsBlank(BordxField field) => Text(field).Length == 0;

    /// <summary>ddMMyyyy date, or <c>null</c> when blank or invalid (the desktop builds "dd/mm/yyyy" and tests IsDate).</summary>
    public DateTime? Date(BordxField field) =>
        DateTime.TryParseExact(Raw(field), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    /// <summary>The number in the field, or 0 when it is not numeric (IsNumeric(Trim(Mid(...))) ? value : 0).</summary>
    public decimal Amount(BordxField field) =>
        decimal.TryParse(Text(field), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

/// <summary>Reads an uploaded bordereaux file.</summary>
public static class BordxFile
{
    /// <summary>Bordereaux files are single-byte ANSI; Latin-1 keeps one character per byte so the column positions hold.</summary>
    public static readonly Encoding Encoding = Encoding.Latin1;

    private const char NullCharacter = '\0';

    /// <returns>The lines, or <c>null</c> when the content is not a text file.</returns>
    public static async Task<List<BordxLine>?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding);
        var lines = new List<BordxLine>();
        while (await reader.ReadLineAsync(cancellationToken) is { } text)
        {
            if (text.Contains(NullCharacter)) return null;
            lines.Add(new BordxLine(text, lines.Count + 1));
        }
        return lines;
    }
}
