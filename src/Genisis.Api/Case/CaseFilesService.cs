using System.Text.RegularExpressions;

namespace Genisis.Api.Case;

public record CaseFile(string Name, long Size, DateTime Modified);

/// <summary>The case document folders under the desktop's DocPath (SVRSuper.ini): the Forms tab lists
/// (ScannedDoc, InternalDocuments, ScannedDoc\FinalLgs), the fax-document copy target and the policy wording folder.
/// Names are validated so a request can never leave the configured root.</summary>
public partial class CaseFilesService(IConfiguration configuration, ILogger<CaseFilesService> log)
{
    public const long MaxUploadBytes = 25 * 1024 * 1024;

    private static readonly Dictionary<string, string> Folders = new()
    {
        ["scanned"] = "ScannedDoc",
        ["internal"] = "InternalDocuments",
        ["final-lgs"] = Path.Combine("ScannedDoc", "FinalLgs"),
    };

    [GeneratedRegex("^[A-Za-z0-9*_-]{1,30}$")]
    private static partial Regex SafeKey();

    private string? Root => configuration["Documents:RootPath"] is { Length: > 0 } root && Directory.Exists(root) ? root : null;

    public bool Available => Root is not null;

    private string? CaseFolder(string folder, string cas) =>
        Root is { } root && Folders.TryGetValue(folder, out var sub) && SafeKey().IsMatch(cas) ? Path.Combine(root, sub, cas) : null;

    private string? WordingFolder(string wording) =>
        Root is { } root && SafeKey().IsMatch(wording) ? Path.Combine(root, "PolicyWording", wording) : null;

    private static List<CaseFile> List(string? dir) =>
        dir is not null && Directory.Exists(dir)
            ? new DirectoryInfo(dir).GetFiles().OrderBy(f => f.Name).Select(f => new CaseFile(f.Name, f.Length, f.LastWriteTime)).ToList()
            : [];

    private static string? FileIn(string? dir, string name) =>
        dir is not null && name == Path.GetFileName(name) && name.Length > 0 && File.Exists(Path.Combine(dir, name)) ? Path.Combine(dir, name) : null;

    public Dictionary<string, List<CaseFile>> ListCase(string cas) =>
        Folders.Keys.ToDictionary(k => k, k => List(CaseFolder(k, cas.Trim())));

    public string? CasePath(string folder, string cas, string name) => FileIn(CaseFolder(folder, cas.Trim()), name);

    public List<CaseFile> ListWording(string wording) => List(WordingFolder(wording.Trim()));

    public string? WordingPath(string wording, string name) => FileIn(WordingFolder(wording.Trim()), name);

    /// <summary>cmdDelGL_Click / cmdClearDoc_Click: delete one document of the case.</summary>
    public bool DeleteCaseFile(string folder, string cas, string name, string userCode)
    {
        var path = CasePath(folder, cas.Trim(), name);
        if (path is null) return false;
        File.Delete(path);
        log.LogInformation("Document {File} of case {CaseNumber} ({Folder}) deleted by {User}", name, cas, folder, userCode);
        return true;
    }

    /// <summary>CmdCopy_Click: store a document in the case's ScannedDoc folder (the desktop copied it from FaxDocuments).</summary>
    public async Task<CaseFile> SaveScannedAsync(string cas, IFormFile file, string userCode)
    {
        var dir = CaseFolder("scanned", cas.Trim()) ?? throw new CaseRegistrationException("The document folder is not available.");
        var name = Path.GetFileName(file.FileName);
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new CaseRegistrationException("Please check source file!");
        if (file.Length > MaxUploadBytes) throw new CaseRegistrationException("The file is larger than 25 MB.");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        await using (var stream = File.Create(path)) await file.CopyToAsync(stream);
        log.LogInformation("Document {File} stored for case {CaseNumber} by {User}", name, cas, userCode);
        var info = new FileInfo(path);
        return new(info.Name, info.Length, info.LastWriteTime);
    }
}
