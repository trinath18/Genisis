using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Storage;

/// <summary>A carton (a row of <c>Carton</c>, a line of <c>Lstcrt</c>): where claim files are kept.</summary>
public record StorageCartonRow
{
    /// <summary>Carton number.</summary>
    public string CartonNo { get; set; } = "";

    /// <summary>Carton status: M Medix, S Store, R Return.</summary>
    public string? Status { get; set; }

    /// <summary>Date registered (the row's last update).</summary>
    public DateTime? RegisterDate { get; set; }

    /// <summary>Date sent.</summary>
    public DateTime? SendDate { get; set; }

    /// <summary>Date requested.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date delivered.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date returned.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Date destroyed.</summary>
    public DateTime? DestroyDate { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }
}

/// <summary>A claim file kept in a carton (a line of <c>Lststr</c>).</summary>
public record StorageFileRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Carton number.</summary>
    public string? CartonNo { get; set; }

    /// <summary>Carton status.</summary>
    public string? CartonStatus { get; set; }

    /// <summary>File status: I In, O Out.</summary>
    public string? FileStatus { get; set; }

    /// <summary>Bill amount (the worksheet's actual total).</summary>
    public decimal? BillAmount { get; set; }

    /// <summary>Date the carton was sent.</summary>
    public DateTime? SendDate { get; set; }

    /// <summary>Date the file was registered.</summary>
    public DateTime? RegisterDate { get; set; }

    /// <summary>Who requested the file.</summary>
    public string? RequestBy { get; set; }

    /// <summary>Date requested.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date delivered.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date returned.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Days from delivery to return.</summary>
    public int? Duration { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Bordx date of the claim.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Cash type of the claim.</summary>
    public string? CashType { get; set; }
}

/// <summary>One line of a carton's check sheet.</summary>
public record StorageCartonFile
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>File status.</summary>
    public string? FileStatus { get; set; }
}

/// <summary>The drop-down data: carton numbers and user names.</summary>
public record StorageLookups(List<string> Cartons, List<string> Users);

/// <summary>The criteria of the Carton Details tab: every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class StorageCartonFilter
{
    /// <summary>Carton number from.</summary>
    public string? CartonFrom { get; set; }

    /// <summary>Carton number to.</summary>
    public string? CartonTo { get; set; }

    /// <summary>Carton status: M, S or R.</summary>
    public string? Status { get; set; }

    /// <summary>Date sent from.</summary>
    public DateTime? SendFrom { get; set; }

    /// <summary>Date sent to.</summary>
    public DateTime? SendTo { get; set; }

    /// <summary>Date requested from.</summary>
    public DateTime? RequestFrom { get; set; }

    /// <summary>Date requested to.</summary>
    public DateTime? RequestTo { get; set; }

    /// <summary>Date delivered from.</summary>
    public DateTime? DeliveryFrom { get; set; }

    /// <summary>Date delivered to.</summary>
    public DateTime? DeliveryTo { get; set; }

    /// <summary>Date returned from.</summary>
    public DateTime? ReturnFrom { get; set; }

    /// <summary>Date returned to.</summary>
    public DateTime? ReturnTo { get; set; }

    /// <summary>Date destroyed from.</summary>
    public DateTime? DestroyFrom { get; set; }

    /// <summary>Date destroyed to.</summary>
    public DateTime? DestroyTo { get; set; }
}

/// <summary>The criteria of the Storage Details tab.</summary>
public class StorageFileFilter
{
    /// <summary>The Storage Entry list: also the claims that are not stored yet (the Enquiry lists only files in a carton).</summary>
    public bool IncludeUnstored { get; set; }

    /// <summary>Bordx date from (the Storage Entry list).</summary>
    public DateTime? BordxFrom { get; set; }

    /// <summary>Bordx date to (the Storage Entry list).</summary>
    public DateTime? BordxTo { get; set; }

    /// <summary>"Use Scan": one case number instead of a range.</summary>
    public bool UseScan { get; set; }

    /// <summary>Case number, or the start of the range.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>End of the case number range (range mode only).</summary>
    public string? CaseTo { get; set; }

    /// <summary>Carton number from.</summary>
    public string? CartonFrom { get; set; }

    /// <summary>Carton number to.</summary>
    public string? CartonTo { get; set; }

    /// <summary>File status: I or O.</summary>
    public string? FileStatus { get; set; }

    /// <summary>Request by (a user name).</summary>
    public string? RequestBy { get; set; }

    /// <summary>Date requested from.</summary>
    public DateTime? RequestFrom { get; set; }

    /// <summary>Date requested to.</summary>
    public DateTime? RequestTo { get; set; }

    /// <summary>Date delivered from.</summary>
    public DateTime? DeliveryFrom { get; set; }

    /// <summary>Date delivered to.</summary>
    public DateTime? DeliveryTo { get; set; }

    /// <summary>Date returned from.</summary>
    public DateTime? ReturnFrom { get; set; }

    /// <summary>Date returned to.</summary>
    public DateTime? ReturnTo { get; set; }

    /// <summary>Register date from.</summary>
    public DateTime? RegisterFrom { get; set; }

    /// <summary>Register date to.</summary>
    public DateTime? RegisterTo { get; set; }

    /// <summary>Date sent from.</summary>
    public DateTime? SentFrom { get; set; }

    /// <summary>Date sent to.</summary>
    public DateTime? SentTo { get; set; }

    /// <summary>Payor from (the first two letters of the case number).</summary>
    public string? PayorFrom { get; set; }

    /// <summary>Payor to.</summary>
    public string? PayorTo { get; set; }
}

/// <summary>The carton criteria: the status has to be one of the drop-down's three.</summary>
public sealed class StorageCartonFilterValidator : RequestValidator<StorageCartonFilter>
{
    /// <summary>Declares the rules.</summary>
    public StorageCartonFilterValidator() =>
        RuleFor(f => f.Status).Must(s => string.IsNullOrWhiteSpace(s) || s.Trim().ToUpperInvariant() is "M" or "S" or "R").WithMessage("Select a valid carton status.");
}

/// <summary>The storage criteria: a scan needs a case number (<c>cmdSearch_Click</c>); the file status is I or O.</summary>
public sealed class StorageFileFilterValidator : RequestValidator<StorageFileFilter>
{
    /// <summary>Declares the rules.</summary>
    public StorageFileFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.CaseFrom)).WithMessage("Please Enter Case No");
        RuleFor(f => f.FileStatus).Must(s => string.IsNullOrWhiteSpace(s) || s.Trim().ToUpperInvariant() is "I" or "O").WithMessage("Select a valid file status.");
    }
}

/// <summary>Reads the cartons and the files kept in them.</summary>
public interface IStorageEnquiryRepository
{
    /// <summary>The carton numbers and user names for the drop-downs.</summary>
    Task<StorageLookups> LookupsAsync(IDbSession session);

    /// <summary>The cartons that match (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<StorageCartonRow>> CartonsAsync(IDbSession session, StorageCartonFilter filter);

    /// <summary>The files that match (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<StorageFileRow>> FilesAsync(IDbSession session, StorageFileFilter filter);

    /// <summary>The check sheet of a carton.</summary>
    Task<List<StorageCartonFile>> CartonFilesAsync(IDbSession session, string carton);
}

/// <inheritdoc />
public sealed class StorageEnquiryRepository : IStorageEnquiryRepository
{
    private static string? Letter(string? value) => WorksheetLimits.Text(value) is { } t ? t[..1].ToUpperInvariant() : null;

    private static string? Upper(string? value) => WorksheetLimits.Text(value)?.ToUpperInvariant();

    /// <inheritdoc />
    public async Task<StorageLookups> LookupsAsync(IDbSession session) =>
        new((await session.Connection.ProcQueryAsync<string>("genisis.StorageEnquiry_Cartons", null, session.Transaction)).ToList(),
            (await session.Connection.ProcQueryAsync<string>("genisis.StorageEnquiry_Users", null, session.Transaction)).ToList());

    /// <inheritdoc />
    public Task<List<StorageCartonRow>> CartonsAsync(IDbSession session, StorageCartonFilter f) =>
        session.Connection.ProcQueryAsync<StorageCartonRow>("genisis.StorageEnquiry_CartonSearch", new
        {
            cartonFrom = Upper(f.CartonFrom), cartonTo = Upper(f.CartonTo), status = Letter(f.Status),
            sendFrom = WorksheetLimits.Day(f.SendFrom), sendTo = WorksheetLimits.Day(f.SendTo), requestFrom = WorksheetLimits.Day(f.RequestFrom), requestTo = WorksheetLimits.Day(f.RequestTo),
            deliveryFrom = WorksheetLimits.Day(f.DeliveryFrom), deliveryTo = WorksheetLimits.Day(f.DeliveryTo), returnFrom = WorksheetLimits.Day(f.ReturnFrom), returnTo = WorksheetLimits.Day(f.ReturnTo),
            destroyFrom = WorksheetLimits.Day(f.DestroyFrom), destroyTo = WorksheetLimits.Day(f.DestroyTo), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<StorageFileRow>> FilesAsync(IDbSession session, StorageFileFilter f) =>
        session.Connection.ProcQueryAsync<StorageFileRow>("genisis.StorageEnquiry_StorageSearch", new
        {
            scan = f.UseScan, caseFrom = Upper(f.CaseFrom), caseTo = f.UseScan ? null : Upper(f.CaseTo), cartonFrom = Upper(f.CartonFrom), cartonTo = Upper(f.CartonTo), fileStatus = Letter(f.FileStatus),
            requestBy = WorksheetLimits.Text(f.RequestBy), requestFrom = WorksheetLimits.Day(f.RequestFrom), requestTo = WorksheetLimits.Day(f.RequestTo),
            deliveryFrom = WorksheetLimits.Day(f.DeliveryFrom), deliveryTo = WorksheetLimits.Day(f.DeliveryTo), returnFrom = WorksheetLimits.Day(f.ReturnFrom), returnTo = WorksheetLimits.Day(f.ReturnTo),
            registerFrom = WorksheetLimits.Day(f.RegisterFrom), registerTo = WorksheetLimits.Day(f.RegisterTo), sentFrom = WorksheetLimits.Day(f.SentFrom), sentTo = WorksheetLimits.Day(f.SentTo),
            payorFrom = Upper(f.PayorFrom) is { Length: > 1 } pf ? pf[..2] : Upper(f.PayorFrom), payorTo = Upper(f.PayorTo) is { Length: > 1 } pt ? pt[..2] : Upper(f.PayorTo), max = WorksheetLimits.FetchRows,
            includeUnstored = f.IncludeUnstored, bordxFrom = WorksheetLimits.Day(f.BordxFrom), bordxTo = WorksheetLimits.Day(f.BordxTo),
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<StorageCartonFile>> CartonFilesAsync(IDbSession session, string carton) =>
        session.Connection.ProcQueryAsync<StorageCartonFile>("genisis.StorageEnquiry_CartonFiles", new { carton = carton.Trim().ToUpperInvariant() }, session.Transaction);
}

/// <summary>Claim Bordx > Storage > Enquiry (legacy <c>frmStorageEnq</c>, "Storage Enquiry": the Carton Details and Storage Details tabs).</summary>
public class StorageEnquiryService(IDbSessions sessions, IStorageEnquiryRepository repository)
{
    /// <summary>ComboListing: the carton numbers and users.</summary>
    public async Task<StorageLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.LookupsAsync(session);
    }

    /// <summary>CartonSearchRecord: the cartons that match.</summary>
    public async Task<SearchResult<StorageCartonRow>> CartonsAsync(StorageCartonFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return WorksheetLimits.Cap(await repository.CartonsAsync(session, filter));
    }

    /// <summary>StorageSearchRecord: the files that match.</summary>
    public async Task<SearchResult<StorageFileRow>> FilesAsync(StorageFileFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return WorksheetLimits.Cap(await repository.FilesAsync(session, filter));
    }

    /// <summary>CmdListing_Click: the check sheet of a carton; a carton number is needed ("Please select Carton No. ").</summary>
    public async Task<List<StorageCartonFile>> CartonFilesAsync(string? carton)
    {
        if (string.IsNullOrWhiteSpace(carton)) throw new BusinessRuleException("Please select Carton No.");
        await using var session = await sessions.OpenAsync();
        return await repository.CartonFilesAsync(session, carton);
    }
}
