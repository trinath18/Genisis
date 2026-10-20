using PdfSharp.Fonts;

namespace Genisis.Api.LeafletCard;

/// <summary>
/// PDFsharp font source for hosts other than Windows (Linux servers and CI runners), where PDFsharp cannot find fonts on its own.
/// Sans-serif families (Arial, the documents' font) map to DejaVu Sans / Liberation Sans, monospaced ones (Courier New, MigraDoc's
/// predefined error font) to DejaVu Sans Mono / Liberation Mono, read from the system font folders. Missing bold or italic files are
/// simulated.
/// </summary>
public sealed class PdfFontResolver : IFontResolver
{
    private const string SansFace = "Genisis-Sans";
    private const string SansBoldFace = "Genisis-Sans-Bold";
    private const string MonoFace = "Genisis-Mono";
    private const string MonoBoldFace = "Genisis-Mono-Bold";

    /// <summary>Folders searched, recursively, for TrueType files.</summary>
    public static readonly IReadOnlyList<string> DefaultFontFolders =
    [
        "/usr/share/fonts", "/usr/local/share/fonts", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"),
    ];

    private static readonly IReadOnlySet<string> MonospacedFamilies = new HashSet<string>(
        ["Courier New", "Courier", "Consolas", "Lucida Console", "Monospace", "DejaVu Sans Mono", "Liberation Mono"], StringComparer.OrdinalIgnoreCase);

    /// <summary>File names in order of preference for each face.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> Candidates = new Dictionary<string, string[]>
    {
        [SansFace] = ["DejaVuSans.ttf", "LiberationSans-Regular.ttf", "NotoSans-Regular.ttf", "FreeSans.ttf", "arial.ttf"],
        [SansBoldFace] = ["DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf", "NotoSans-Bold.ttf", "FreeSansBold.ttf", "arialbd.ttf"],
        [MonoFace] = ["DejaVuSansMono.ttf", "LiberationMono-Regular.ttf", "NotoSansMono-Regular.ttf", "FreeMono.ttf", "cour.ttf"],
        [MonoBoldFace] = ["DejaVuSansMono-Bold.ttf", "LiberationMono-Bold.ttf", "NotoSansMono-Bold.ttf", "FreeMonoBold.ttf", "courbd.ttf"],
    };

    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Indexes the TrueType files under <paramref name="fontFolders"/> (the system folders by default).</summary>
    public PdfFontResolver(IEnumerable<string>? fontFolders = null)
    {
        foreach (var folder in (fontFolders ?? DefaultFontFolders).Where(Directory.Exists))
            foreach (var file in Directory.EnumerateFiles(folder, "*.ttf", SearchOption.AllDirectories))
                _files.TryAdd(Path.GetFileName(file), file);
    }

    /// <summary>Whether a regular sans-serif and monospaced font were both found, which is what the documents need.</summary>
    public bool HasFonts => Find(SansFace) is not null && Find(MonoFace) is not null;

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var mono = MonospacedFamilies.Contains(familyName);
        var regular = mono ? MonoFace : SansFace;
        var bold = mono ? MonoBoldFace : SansBoldFace;
        if (isBold && Find(bold) is not null) return new FontResolverInfo(bold, false, isItalic);
        return Find(regular) is null ? null : new FontResolverInfo(regular, isBold, isItalic);
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName) => Find(faceName) is { } path ? File.ReadAllBytes(path) : null;

    private string? Find(string faceName) =>
        Candidates.TryGetValue(faceName, out var names) ? names.Select(n => _files.GetValueOrDefault(n)).FirstOrDefault(p => p is not null) : null;
}
