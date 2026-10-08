using System.Text;

namespace Genisis.Api.Auth;

/// <summary>
/// Port of the VB6 frmLogin.Crypt function: each character code (Windows-1252) is shifted by position^2
/// and wrapped at 255. Stored values in USR.USRPassword are the uppercased result.
/// </summary>
public static class LegacyCrypt
{
    private static readonly char[] Cp1252High =
    {
        '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021', '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
        '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014', '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178',
    };

    public static string Encrypt(string plain)
    {
        var sb = new StringBuilder(plain.Length);
        for (var i = 0; i < plain.Length; i++)
        {
            var position = i + 1;
            var value = ToAnsi(plain[i]) + position * position;
            while (value > 255) value -= 255;
            sb.Append(FromAnsi(value));
        }
        return sb.ToString();
    }

    public static bool Matches(string plain, string? stored) =>
        stored is not null &&
        string.Equals(Encrypt(plain).ToUpperInvariant(), stored.Trim().ToUpperInvariant(), StringComparison.Ordinal);

    private static int ToAnsi(char c)
    {
        if (c < 0x80 || (c >= 0xA0 && c <= 0xFF)) return c;
        var idx = Array.IndexOf(Cp1252High, c);
        return idx >= 0 ? 0x80 + idx : '?';
    }

    private static char FromAnsi(int b) => b is >= 0x80 and <= 0x9F ? Cp1252High[b - 0x80] : (char)b;
}
