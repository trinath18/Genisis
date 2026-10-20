using Genisis.Api.LeafletCard;

namespace Genisis.Api.Tests;

public class PdfFontResolverTests
{
    /// <summary>The system fonts: C:\Windows\Fonts on Windows (Arial / Courier New), the Linux font folders elsewhere (DejaVu).</summary>
    private static PdfFontResolver SystemFonts() => OperatingSystem.IsWindows()
        ? new PdfFontResolver([Environment.GetFolderPath(Environment.SpecialFolder.Fonts)])
        : new PdfFontResolver();

    [Fact]
    public void Sans_and_monospaced_families_resolve_to_system_fonts()
    {
        var resolver = SystemFonts();
        Assert.True(resolver.HasFonts, "No sans-serif and monospaced TrueType fonts found (on Linux install fonts-dejavu-core).");

        var arial = resolver.ResolveTypeface("Arial", false, false)!;
        var courier = resolver.ResolveTypeface("Courier New", false, false)!;
        Assert.NotEqual(arial.FaceName, courier.FaceName);
        Assert.NotEmpty(resolver.GetFont(arial.FaceName)!);
        Assert.NotEmpty(resolver.GetFont(courier.FaceName)!);

        var bold = resolver.ResolveTypeface("Arial", true, false)!;
        Assert.True(bold.FaceName != arial.FaceName || bold.MustSimulateBold);
    }

    [Fact]
    public void Without_fonts_nothing_resolves()
    {
        var resolver = new PdfFontResolver([Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))]);
        Assert.False(resolver.HasFonts);
        Assert.Null(resolver.ResolveTypeface("Arial", false, false));
        Assert.Null(resolver.GetFont("Genisis-Sans"));
    }
}
