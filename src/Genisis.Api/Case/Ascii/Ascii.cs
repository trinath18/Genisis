using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.Ascii;

/// <summary>The criteria of an ASCII export. <see cref="ClaimAsciiRequest"/> and <see cref="CaseAsciiRequest"/> add the screen-specific meaning.</summary>
public abstract class AsciiRequest
{
    /// <summary>The payor code (the first part of "AC - Company").</summary>
    public string? Payor { get; set; }

    /// <summary>Date range start.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Date range end (inclusive of the whole day).</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>N = new, R = reprint.</summary>
    public string? ExportStatus { get; set; }

    /// <summary>The status selector: see the derived requests.</summary>
    public string? WorksheetStatus { get; set; }
}

/// <summary>Claim ASCII (frmNewClaimAscii): accepted worksheets with a bordx, or rejected cases.</summary>
public class ClaimAsciiRequest : AsciiRequest
{
    /// <summary>"accepted" (default) or "rejected".</summary>
    public string? Variant { get; set; }

    /// <summary>True for the rejected-cases export (date range = case registration date, no status fields).</summary>
    public bool Rejected => string.Equals((Variant ?? "accepted").Trim(), "rejected", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Case ASCII (frmCaseASCII): registered cases (WorksheetStatus = Endt Status 0 new, 1 deleted, 2 amendment).</summary>
public class CaseAsciiRequest : AsciiRequest;

/// <summary>The generated file.</summary>
/// <param name="FileName">The server file's name; download it with the files endpoint.</param>
/// <param name="Records">Records written.</param>
/// <param name="BatchNo">The payor's batch number for the day.</param>
public record AsciiResult(string FileName, int Records, string BatchNo);

/// <summary>The payor code of a "AC - Company" drop-down text.</summary>
public static class AsciiPayor
{
    /// <summary>The code before " - ", trimmed.</summary>
    public static string Code(string? payor) => (payor ?? "").Split(" - ")[0].Trim();
}

/// <summary>Checks both exports share: a payor and a date range.</summary>
public abstract class AsciiRequestValidator<T> : RequestValidator<T> where T : AsciiRequest
{
    /// <summary>The caption of the date range (it differs by export).</summary>
    protected abstract string DateLabel(T request);

    /// <summary>Declares the payor and date rules; call from the derived constructor before its own rules.</summary>
    protected void CommonRules()
    {
        RuleFor(r => r.Payor).Must(p => AsciiPayor.Code(p).Length > 0).WithMessage("Please enter the Payor code.");
        RuleFor(r => r).Must(r => r.DateFrom is not null).WithMessage(r => $"Please enter {DateLabel(r)} from.");
        RuleFor(r => r).Must(r => r.DateTo is not null).WithMessage(r => $"Please enter {DateLabel(r)} to.");
        RuleFor(r => r).Must(r => r.DateFrom <= r.DateTo).WithMessage(r => $"{DateLabel(r)} from cannot be after {DateLabel(r)} to.");
    }
}

/// <summary>CmdGenerate_Click's checks of Claim ASCII.</summary>
public sealed class ClaimAsciiRequestValidator : AsciiRequestValidator<ClaimAsciiRequest>
{
    /// <inheritdoc />
    protected override string DateLabel(ClaimAsciiRequest request) => request.Rejected ? "Case Reg. Date" : "Bordx Date";

    /// <summary>Declares the rules.</summary>
    public ClaimAsciiRequestValidator()
    {
        CommonRules();
        RuleFor(r => r.ExportStatus).Must(s => (s ?? "").Trim() is "N" or "R").WithMessage("Please select the Export status.").When(r => !r.Rejected);
        RuleFor(r => r.WorksheetStatus).Must(s => (s ?? "").Trim() is "0" or "1").WithMessage("Please select the Bordx WS status.").When(r => !r.Rejected);
    }
}

/// <summary>CmdGenerate_Click's checks of Case ASCII.</summary>
public sealed class CaseAsciiRequestValidator : AsciiRequestValidator<CaseAsciiRequest>
{
    /// <inheritdoc />
    protected override string DateLabel(CaseAsciiRequest request) => "Case Reg. Date";

    /// <summary>Declares the rules.</summary>
    public CaseAsciiRequestValidator()
    {
        CommonRules();
        RuleFor(r => r.ExportStatus).Must(s => (s ?? "").Trim() is "N" or "R").WithMessage("Please select the Export status.");
        RuleFor(r => r.WorksheetStatus).Must(s => (s ?? "").Trim() is "0" or "1" or "2").WithMessage("Please select the Endt Status.");
    }
}

/// <summary>An ASCII export could not be completed (for example the output folder is not configured).</summary>
public class AsciiException(string message) : BusinessRuleException(message);

/// <summary>Reads the rows to export and flags what was exported.</summary>
public interface IAsciiRepository
{
    /// <summary>Accepted worksheets with a bordx dated in the range.</summary>
    Task<List<Dictionary<string, object?>>> GetAcceptedClaimsAsync(IDbSession session, string payor, DateTime from, DateTime to, bool reprint, string worksheetStatus);

    /// <summary>Repudiated cases registered in the range.</summary>
    Task<List<Dictionary<string, object?>>> GetRejectedClaimsAsync(IDbSession session, string payor, DateTime from, DateTime to);

    /// <summary>Registered cases of the payor in the range.</summary>
    Task<List<Dictionary<string, object?>>> GetCasesAsync(IDbSession session, string payor, DateTime from, DateTime to, bool reprint, string endorsement);

    /// <summary>The payor's next batch number for the day ("CLM" claim batches, "CAS" case batches), taken atomically.</summary>
    Task<string> NextBatchAsync(IDbSession session, string kind, string payor, DateTime date);

    /// <summary>Flags a worksheet version as exported.</summary>
    Task MarkClaimExportedAsync(IDbSession session, string cas, int version);

    /// <summary>Flags a case as exported.</summary>
    Task MarkCaseExportedAsync(IDbSession session, string cas);
}

/// <summary>Writes the payor files on the server and finds them again for download.</summary>
public interface IAsciiFileStore
{
    /// <summary>Writes a file into the claim or case folder and returns its name.</summary>
    Task<string> WriteAsync(string kind, string name, string text);

    /// <summary>The full path of an existing file, or null (only plain file names inside the folder are served).</summary>
    string? Find(string kind, string name);
}
