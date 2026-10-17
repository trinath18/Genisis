namespace Genisis.Api.Membership;

/// <summary>Settings section "Documents".</summary>
public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    /// <summary>Folder that holds one sub-folder per policy wording (desktop DocPath\PolicyWording).</summary>
    public string? PolicyWordingPath { get; set; }
}

public sealed record PolicyWordingFile(string Path, string ContentType, string FileName);

public interface IPolicyWordingStore
{
    /// <summary>Image / PDF files in the wording's folder; <c>FolderFound</c> is false when the folder is not configured or missing.</summary>
    (IReadOnlyList<string> Files, bool FolderFound) ListFiles(string wording);

    /// <returns>The file, or <c>null</c> when the name is not a plain allowed file name inside the wording's folder.</returns>
    PolicyWordingFile? Find(string wording, string fileName);
}

/// <summary>Policy Wording tab files (desktop addTheFiles). Only plain file names with an allowed extension are served from inside the configured folder.</summary>
public sealed class PolicyWordingStore(Microsoft.Extensions.Options.IOptions<DocumentOptions> options) : IPolicyWordingStore
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".tif"] = "image/tiff",
    };

    private string? Folder(string wording)
    {
        var root = options.Value.PolicyWordingPath;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(wording)) return null;
        if (wording.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || wording.Contains("..", StringComparison.Ordinal)) return null;
        return System.IO.Path.Combine(root, wording.Trim());
    }

    public (IReadOnlyList<string> Files, bool FolderFound) ListFiles(string wording)
    {
        var folder = Folder(wording);
        if (folder is null || !Directory.Exists(folder)) return ([], false);
        var files = Directory.EnumerateFiles(folder).Select(System.IO.Path.GetFileName).OfType<string>()
            .Where(f => ContentTypes.ContainsKey(System.IO.Path.GetExtension(f))).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return (files, true);
    }

    public PolicyWordingFile? Find(string wording, string fileName)
    {
        var folder = Folder(wording);
        if (folder is null || System.IO.Path.GetFileName(fileName) != fileName) return null;
        if (!ContentTypes.TryGetValue(System.IO.Path.GetExtension(fileName), out var contentType)) return null;
        var path = System.IO.Path.Combine(folder, fileName);
        return File.Exists(path) ? new PolicyWordingFile(path, contentType, fileName) : null;
    }
}
