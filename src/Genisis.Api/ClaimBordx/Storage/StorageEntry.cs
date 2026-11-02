using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Storage;

/// <summary>What the Carton Details tab saves: one new carton, or the status, dates and remarks of one or more existing cartons.</summary>
public class StorageCartonSaveRequest
{
    /// <summary>The carton to add, or the cartons to change (Edit: the checked ones).</summary>
    public List<string> Cartons { get; set; } = [];

    /// <summary>M Medix, S Store or R Return ("M - Medix" is accepted).</summary>
    public string? Status { get; set; }

    /// <summary>Date of send.</summary>
    public DateTime? SendDate { get; set; }

    /// <summary>Date of request.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date of delivery.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date of return.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Date of destroy.</summary>
    public DateTime? DestroyDate { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>True: change the existing cartons; false: add the one carton.</summary>
    public bool Edit { get; set; }
}

/// <summary>A claim file to put into a carton (a line of <c>Lststr</c>).</summary>
public record StorageClaimRef(string CaseNo, int Version);

/// <summary>What the Storage Details tab saves: the checked claims into a carton, or the one stored claim's row.</summary>
public class StorageFileSaveRequest
{
    /// <summary>The checked claims (Edit: the one stored claim).</summary>
    public List<StorageClaimRef> Files { get; set; } = [];

    /// <summary>Carton number.</summary>
    public string? Carton { get; set; }

    /// <summary>I In or O Out ("I - In" is accepted).</summary>
    public string? FileStatus { get; set; }

    /// <summary>Request by (a user name); without it the three dates are not kept.</summary>
    public string? RequestBy { get; set; }

    /// <summary>Date requested.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date delivered.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date returned.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>True: change the storage row of the one claim; false: store the claims.</summary>
    public bool Edit { get; set; }
}

/// <summary>SaveCarton's checks, in the desktop's order. The desktop compared the dates as text and with <c>IsDate</c> flags, so a request without a send date, a return without a delivery date or a destroy without a return date failed; the order of the dates is checked as well here.</summary>
public sealed class StorageCartonSaveRequestValidator : RequestValidator<StorageCartonSaveRequest>
{
    /// <summary>Declares the rules.</summary>
    public StorageCartonSaveRequestValidator()
    {
        RuleFor(r => r.Cartons).Must(c => c is { Count: > 0 } && c.All(x => !string.IsNullOrWhiteSpace(x))).WithMessage("Please Enter Carton Number");
        RuleFor(r => r).Must(r => r.Edit || r.Cartons.Count == 1).WithMessage("Add one carton at a time.");
        RuleFor(r => r.Cartons).Must(c => c.All(x => x.Trim().Length <= 20)).WithMessage("Carton Number can be at most 20 characters.");
        RuleFor(r => r.Status).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Please Enter Carton Status");
        RuleFor(r => r.Status).Must(s => "MSR".Contains(StorageDocumentRules.Letter(s))).WithMessage("Select M - Medix, S - Store or R - Return.");
        RuleFor(r => r).Must(r => r.RequestDate is null || (r.SendDate is not null && r.RequestDate.Value.Date >= r.SendDate.Value.Date)).WithMessage("Invalid Date ! Date of Request must greater than Date of Send ");
        RuleFor(r => r).Must(r => r.ReturnDate is null || (r.DeliveryDate is not null && r.ReturnDate.Value.Date >= r.DeliveryDate.Value.Date)).WithMessage("Invalid Date ! Date of Return must greater than Date of Delivery ");
        RuleFor(r => r).Must(r => r.DestroyDate is null || (r.ReturnDate is not null && r.DestroyDate.Value.Date >= r.ReturnDate.Value.Date)).WithMessage("Invalid Date ! Date of Destroy must greater than Date of Return ");
        RuleFor(r => r).Must(r => StorageDocumentRules.Letter(r.Status) != "R" || r.ReturnDate is not null).WithMessage("Date Return cannot be null when the status is 'R' !");
        RuleFor(r => r.Remarks).Must(m => (m ?? "").Trim().Length <= 400).WithMessage("Remarks can be at most 400 characters.");
    }
}

/// <summary>cmdAdd_Click and cmdSave_Click's checks of the Storage Details tab, in the desktop's order (the same as the Document screen's).</summary>
public sealed class StorageFileSaveRequestValidator : RequestValidator<StorageFileSaveRequest>
{
    /// <summary>Declares the rules.</summary>
    public StorageFileSaveRequestValidator()
    {
        RuleFor(r => r.Files).Must(f => f is { Count: > 0 } && f.All(x => !string.IsNullOrWhiteSpace(x.CaseNo) && x.Version > 0)).WithMessage("Please checked at least 1 Claim !");
        RuleFor(r => r).Must(r => !r.Edit || r.Files.Count == 1).WithMessage("Select the one claim to change.");
        RuleFor(r => r.Carton).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Carton Number");
        RuleFor(r => r.Carton).Must(c => c!.Trim().Length <= 20).WithMessage("Carton Number can be at most 20 characters.");
        RuleFor(r => r.FileStatus).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Please Enter File Status");
        RuleFor(r => r.FileStatus).Must(s => "IO".Contains(StorageDocumentRules.Letter(s))).WithMessage("Select I - In or O - Out.");
        RuleFor(r => r).Must(r => string.IsNullOrWhiteSpace(r.RequestBy) || r.RequestDate is not null).WithMessage("Please Enter Date Of Request");
        RuleFor(r => r).Must(r => string.IsNullOrWhiteSpace(r.RequestBy) || r.DeliveryDate is null || r.RequestDate is null || r.DeliveryDate.Value.Date >= r.RequestDate.Value.Date)
            .WithMessage("Invalid Date ! Date of Delivery must greater than Date of Request");
        RuleFor(r => r).Must(r => string.IsNullOrWhiteSpace(r.RequestBy) || r.ReturnDate is null || (r.DeliveryDate is not null && r.ReturnDate.Value.Date >= r.DeliveryDate.Value.Date))
            .WithMessage("Invalid Date ! Date of Return must greater than Date of Delivery");
        RuleFor(r => r.Remarks).Must(m => (m ?? "").Trim().Length <= 400).WithMessage("Remarks can be at most 400 characters.");
        RuleFor(r => r.RequestBy).Must(u => (u ?? "").Trim().Length <= 100).WithMessage("Request By can be at most 100 characters.");
    }
}

/// <summary>Writes the cartons and the storage of the claim files.</summary>
public interface IStorageEntryRepository
{
    /// <summary>Adds or changes one carton; returns 0 saved, 1 exists already (add), 2 no such carton (edit).</summary>
    Task<int> SaveCartonAsync(IDbSession session, string carton, StorageCartonSaveRequest request, string userCode, DateTime today);

    /// <summary>Deletes a carton; returns 0 deleted, 1 no such carton, 2 files are stored in it.</summary>
    Task<int> DeleteCartonAsync(IDbSession session, string carton);

    /// <summary>Stores or changes one claim file; returns 0 saved, 1 stored already, 2 not stored, 3 no such carton, 4 no such claim.</summary>
    Task<int> SaveFileAsync(IDbSession session, StorageClaimRef claim, StorageFileSaveRequest request, string userCode, DateTime today);

    /// <summary>Takes one claim file out of storage; returns 0 removed, 1 it was not stored.</summary>
    Task<int> RemoveFileAsync(IDbSession session, StorageClaimRef claim);
}

/// <inheritdoc />
public sealed class StorageEntryRepository : IStorageEntryRepository
{
    private static string Upper(string? text) => (StorageDocumentRules.Text(text) ?? "").ToUpperInvariant();

    /// <inheritdoc />
    public async Task<int> SaveCartonAsync(IDbSession session, string carton, StorageCartonSaveRequest r, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.StorageEntry_CartonSave", new
        {
            carton, status = StorageDocumentRules.Letter(r.Status), sendDate = WorksheetLimits.Day(r.SendDate), requestDate = WorksheetLimits.Day(r.RequestDate), deliveryDate = WorksheetLimits.Day(r.DeliveryDate),
            returnDate = WorksheetLimits.Day(r.ReturnDate), destroyDate = WorksheetLimits.Day(r.DestroyDate), remarks = Upper(r.Remarks), edit = r.Edit, userCode, today = today.Date,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> DeleteCartonAsync(IDbSession session, string carton) => await session.Connection.ProcScalarAsync<int>("genisis.StorageEntry_CartonDelete", new { carton }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SaveFileAsync(IDbSession session, StorageClaimRef claim, StorageFileSaveRequest r, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.StorageEntry_FileSave", new
        {
            caseNo = claim.CaseNo.Trim().ToUpperInvariant(), version = claim.Version.ToString(), carton = Upper(r.Carton), fileStatus = StorageDocumentRules.Letter(r.FileStatus), requestBy = Upper(r.RequestBy),
            requestDate = WorksheetLimits.Day(r.RequestDate), deliveryDate = WorksheetLimits.Day(r.DeliveryDate), returnDate = WorksheetLimits.Day(r.ReturnDate), remarks = Upper(r.Remarks), edit = r.Edit,
            userCode, today = today.Date,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> RemoveFileAsync(IDbSession session, StorageClaimRef claim) =>
        await session.Connection.ProcScalarAsync<int>("genisis.StorageEntry_FileRemove", new { caseNo = claim.CaseNo.Trim().ToUpperInvariant(), version = claim.Version.ToString() }, session.Transaction);
}

/// <summary>Claim Bordx > Storage > Claim files (legacy <c>frmStorageEntry</c>, "Storage Entry").</summary>
public class StorageEntryService(IDbSessions sessions, IStorageEntryRepository repository, ILogger<StorageEntryService> log)
{
    private static string Carton(string text) => text.Trim().ToUpperInvariant();

    /// <summary>
    /// SaveCarton / SaveRecByCarton: adds the one carton (one that exists already is a <see cref="ConflictException"/>, "Record Duplicated! Please Change The Corresponding Field") or changes the
    /// status, dates and remarks of every carton given (one that does not exist is a <see cref="NotFoundException"/>), in one transaction.
    /// </summary>
    public async Task SaveCartonsAsync(StorageCartonSaveRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        foreach (var carton in request.Cartons.Select(Carton).Distinct())
            switch (await repository.SaveCartonAsync(session, carton, request, userCode, DateTime.Today))
            {
                case 1: throw new ConflictException("Record Duplicated! Please Change The Corresponding Field");
                case 2: throw new NotFoundException("Record not found");
            }

        await session.CommitAsync();
        log.LogInformation("{Count} carton(s) {Action} by {User}", request.Cartons.Count, request.Edit ? "changed" : "added", userCode);
    }

    /// <summary>cmdDelete_Click (Carton Details): a carton that is not there is a <see cref="NotFoundException"/>, one with files stored in it a <see cref="ConflictException"/>.</summary>
    public async Task DeleteCartonAsync(string carton, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        switch (await repository.DeleteCartonAsync(session, Carton(carton)))
        {
            case 1: throw new NotFoundException("Record not found");
            case 2: throw new ConflictException("This carton cannot be deleted because claim files are stored in it!");
        }

        await session.CommitAsync();
        log.LogInformation("Carton {Carton} deleted by {User}", carton, userCode);
    }

    /// <summary>
    /// SaveStore for each checked claim, in one transaction: a claim that is stored already (add) is a <see cref="ConflictException"/>, one that is not stored (edit) is a <see cref="NotFoundException"/>,
    /// a carton or claim that does not exist is a <see cref="BusinessRuleException"/>; nothing is saved then.
    /// </summary>
    public async Task SaveFilesAsync(StorageFileSaveRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        foreach (var claim in request.Files.DistinctBy(f => (f.CaseNo.Trim().ToUpperInvariant(), f.Version)))
            switch (await repository.SaveFileAsync(session, claim, request, userCode, DateTime.Today))
            {
                case 1: throw new ConflictException("Record Duplicated! Please Change The Corresponding Field");
                case 2: throw new NotFoundException("Record not found");
                case 3: throw new BusinessRuleException("Carton Number not found");
                case 4: throw new BusinessRuleException("Claim not found");
            }

        await session.CommitAsync();
        log.LogInformation("{Count} claim file(s) {Action} in carton {Carton} by {User}", request.Files.Count, request.Edit ? "changed" : "stored", request.Carton, userCode);
    }

    /// <summary>DeleteRecords (Remove): takes one claim file out of storage; one that is not stored is a <see cref="NotFoundException"/>.</summary>
    public async Task RemoveFileAsync(StorageClaimRef claim, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.RemoveFileAsync(session, claim) == 1) throw new NotFoundException("Record not found");
        await session.CommitAsync();
        log.LogInformation("Claim file {Case}-{Version} removed from storage by {User}", claim.CaseNo, claim.Version, userCode);
    }
}
