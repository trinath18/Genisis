using Genisis.Api.Common.Errors;

namespace Genisis.Api.Bordx;

/// <summary>Upload Bordx screen options sent with the file (multipart form fields).</summary>
public class BordxOptions
{
    /// <summary>N = new file, E = endorsement file (desktop cboFileType).</summary>
    public string? FileType { get; set; }
    public string? PayorCode { get; set; }
    public string? HealthCode { get; set; }
    public bool VerifyIc { get; set; } = true;
    public bool LabelPrinted { get; set; }
    public bool BackToBack { get; set; }
}

/// <summary>Options after validation: codes trimmed and upper-cased.</summary>
public sealed record BordxSettings(string FileType, string PayorCode, string HealthCode, bool VerifyIc, bool LabelPrinted, bool BackToBack)
{
    public static BordxSettings From(BordxOptions options)
    {
        static string Code(string? value) => (value ?? "").Trim().ToUpperInvariant();
        var health = Code(options.HealthCode);
        return new BordxSettings(Code(options.FileType), Code(options.PayorCode), health.Length > 0 ? health[..1] : "", options.VerifyIc,
            options.LabelPrinted, options.BackToBack);
    }
}

public sealed record BordxError(int Line, string PolicyNo, string Name, string Message, string FileName);

public sealed record VerifyResult(string? SubmissionDate, int TotalPrincipal, int TotalSupplementary, IReadOnlyList<BordxError> Errors);

public sealed record UploadResult(string Message, int? BatchNo, int NewMembers, int Supplementaries, int Cancelled, int Reinstated,
    IReadOnlyList<string> MembershipNumbers, IReadOnlyList<string> Warnings);

public sealed record BordxOption(string Code, string Name);

public sealed record BordxLookups(IReadOnlyList<BordxOption> HealthCodes, IReadOnlyList<BordxOption> Payors, IReadOnlyCollection<string> PlanFilters);

/// <summary>Generate ASCII request.</summary>
public class AsciiRequest
{
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    /// <summary>01 / 02 / 03 (Version 185 / 195 / 300). The desktop asks for it but writes the same layout for every version.</summary>
    public string? Version { get; set; }
    /// <summary>D = bordereaux date range, B = one bordereaux (receipt) date and batch.</summary>
    public string? ExportOption { get; set; }
    public DateTime? BordxFrom { get; set; }
    public DateTime? BordxTo { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public int? BatchNo { get; set; }
    /// <summary>Optional Medicalife plan filter for the date-range export (desktop cboPlan).</summary>
    public string? Plan { get; set; }
}

/// <summary>Export option of Generate ASCII.</summary>
public static class AsciiExportOptions
{
    public const string DateRange = "D";
    public const string Batch = "B";
}

public sealed record AsciiFile(string FileName, byte[] Content, int Principals, int Supplementaries);

/// <summary>A bordereaux rule stopped the upload (whole file rolled back).</summary>
public class BordxException(string message) : BusinessRuleException(message);
