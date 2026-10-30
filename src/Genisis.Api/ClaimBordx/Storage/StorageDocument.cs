using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Storage;

/// <summary>A document of the accounts department with its storage row, if it has one (a line of <c>Lststr</c> of <c>frmStorageDoc</c>).</summary>
public record StorageDocumentRow
{
    /// <summary>Bordereaux, payment voucher or receipt number.</summary>
    public string DocumentNo { get; set; } = "";

    /// <summary>Carton it is stored in (empty when it is not stored).</summary>
    public string? CartonNo { get; set; }

    /// <summary>Document type of the storage row: B, P or R.</summary>
    public string? DocType { get; set; }

    /// <summary>File status: I In, O Out.</summary>
    public string? FileStatus { get; set; }

    /// <summary>Date the storage row was registered.</summary>
    public DateTime? RegisterDate { get; set; }

    /// <summary>Who requested it.</summary>
    public string? RequestBy { get; set; }

    /// <summary>Date requested.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date delivered.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date returned.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Department.</summary>
    public string? Department { get; set; }
}

/// <summary>The drop-down data of the Document screen: carton numbers, users and departments.</summary>
public record StorageDocumentLookups(List<string> Cartons, List<string> Users, List<string> Departments);

/// <summary>The criteria of the document search; the three dates are an exact day.</summary>
public class StorageDocumentFilter
{
    /// <summary>B bordereaux, P payment voucher, R receipt.</summary>
    public string? DocType { get; set; } = "B";

    /// <summary>Document number from.</summary>
    public string? DocFrom { get; set; }

    /// <summary>Document number to.</summary>
    public string? DocTo { get; set; }

    /// <summary>Carton number.</summary>
    public string? Carton { get; set; }

    /// <summary>I, O or A (all).</summary>
    public string? FileStatus { get; set; }

    /// <summary>Request by (a user name).</summary>
    public string? RequestBy { get; set; }

    /// <summary>Date requested.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date delivered.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date returned.</summary>
    public DateTime? ReturnDate { get; set; }
}

/// <summary>The search: the document type and the file status have to be among the drop-downs' choices.</summary>
public sealed class StorageDocumentFilterValidator : RequestValidator<StorageDocumentFilter>
{
    /// <summary>Declares the rules.</summary>
    public StorageDocumentFilterValidator()
    {
        RuleFor(f => f.DocType).Must(t => StorageDocumentRules.DocTypes.Contains(StorageDocumentRules.Letter(t))).WithMessage("Select a valid document type.");
        RuleFor(f => f.FileStatus).Must(s => string.IsNullOrWhiteSpace(s) || "AIO".Contains(StorageDocumentRules.Letter(s))).WithMessage("Select a valid file status.");
    }
}

/// <summary>Documents to put into a carton, or one stored document to change (<c>cmdSave_Click</c>).</summary>
public class StorageDocumentSaveRequest
{
    /// <summary>The checked documents (edit: the one that was clicked).</summary>
    public List<string> Documents { get; set; } = [];

    /// <summary>B, P or R.</summary>
    public string? DocType { get; set; }

    /// <summary>Carton number.</summary>
    public string? Carton { get; set; }

    /// <summary>I or O.</summary>
    public string? FileStatus { get; set; }

    /// <summary>Request by (a user name); without it the three dates are not kept.</summary>
    public string? RequestBy { get; set; }

    /// <summary>Department.</summary>
    public string? Department { get; set; }

    /// <summary>Date requested.</summary>
    public DateTime? RequestDate { get; set; }

    /// <summary>Date delivered.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Date returned.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>True: change the storage row of the one document; false: store the documents.</summary>
    public bool Edit { get; set; }
}

/// <summary>The rules of the Document screen.</summary>
public static class StorageDocumentRules
{
    /// <summary>The three document types.</summary>
    public static readonly string[] DocTypes = ["B", "P", "R"];

    /// <summary>The first letter, upper case ("B - Bordx" gives B).</summary>
    public static string Letter(string? text) => string.IsNullOrWhiteSpace(text) ? "" : text.Trim()[..1].ToUpperInvariant();

    /// <summary>Trimmed text, or null when blank.</summary>
    public static string? Text(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>cmdAdd_Click and cmdSave_Click's checks, in the desktop's order. The desktop compared the dates as text, which also refused a return date with no delivery date.</summary>
public sealed class StorageDocumentSaveRequestValidator : RequestValidator<StorageDocumentSaveRequest>
{
    /// <summary>Declares the rules.</summary>
    public StorageDocumentSaveRequestValidator()
    {
        RuleFor(r => r.Documents).Must(d => d is { Count: > 0 } && d.All(x => !string.IsNullOrWhiteSpace(x))).WithMessage("Please checked at least 1 Claim !");
        RuleFor(r => r).Must(r => !r.Edit || r.Documents.Count == 1).WithMessage("Select the one document to change.");
        RuleFor(r => r.DocType).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Please Enter Document Type");
        RuleFor(r => r.DocType).Must(t => StorageDocumentRules.DocTypes.Contains(StorageDocumentRules.Letter(t))).WithMessage("Select a valid document type.");
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
        RuleFor(r => r.Department).Must(d => (d ?? "").Trim().Length <= 40).WithMessage("Department can be at most 40 characters.");
    }
}

/// <summary>Reads and writes the storage of the documents.</summary>
public interface IStorageDocumentRepository
{
    /// <summary>The departments (the cartons and users come from <see cref="IStorageEnquiryRepository"/>).</summary>
    Task<List<string>> DepartmentsAsync(IDbSession session);

    /// <summary>The documents that match (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<StorageDocumentRow>> SearchAsync(IDbSession session, StorageDocumentFilter filter);

    /// <summary>Stores or changes one document; returns 0 saved, 1 stored already, 2 not stored, 3 no such carton.</summary>
    Task<int> SaveAsync(IDbSession session, string documentNo, StorageDocumentSaveRequest request, string userCode, DateTime today);
}

/// <inheritdoc />
public sealed class StorageDocumentRepository : IStorageDocumentRepository
{
    /// <inheritdoc />
    public async Task<List<string>> DepartmentsAsync(IDbSession session) => (await session.Connection.ProcQueryAsync<string>("genisis.StorageDocument_Departments", null, session.Transaction)).ToList();

    /// <inheritdoc />
    public Task<List<StorageDocumentRow>> SearchAsync(IDbSession session, StorageDocumentFilter f) =>
        session.Connection.ProcQueryAsync<StorageDocumentRow>("genisis.StorageDocument_Search", new
        {
            docType = StorageDocumentRules.Letter(f.DocType), docFrom = StorageDocumentRules.Text(f.DocFrom)?.ToUpperInvariant(), docTo = StorageDocumentRules.Text(f.DocTo)?.ToUpperInvariant(),
            carton = StorageDocumentRules.Text(f.Carton)?.ToUpperInvariant(), fileStatus = StorageDocumentRules.Letter(f.FileStatus) is "I" or "O" ? StorageDocumentRules.Letter(f.FileStatus) : null,
            requestBy = StorageDocumentRules.Text(f.RequestBy), requestDate = WorksheetLimits.Day(f.RequestDate), deliveryDate = WorksheetLimits.Day(f.DeliveryDate),
            returnDate = WorksheetLimits.Day(f.ReturnDate), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SaveAsync(IDbSession session, string documentNo, StorageDocumentSaveRequest r, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.StorageDocument_Save", new
        {
            documentNo, carton = r.Carton!.Trim().ToUpperInvariant(), fileStatus = StorageDocumentRules.Letter(r.FileStatus), requestBy = (StorageDocumentRules.Text(r.RequestBy) ?? "").ToUpperInvariant(),
            docType = StorageDocumentRules.Letter(r.DocType), department = StorageDocumentRules.Text(r.Department), requestDate = WorksheetLimits.Day(r.RequestDate),
            deliveryDate = WorksheetLimits.Day(r.DeliveryDate), returnDate = WorksheetLimits.Day(r.ReturnDate), remarks = (StorageDocumentRules.Text(r.Remarks) ?? "").ToUpperInvariant(), edit = r.Edit,
            userCode, today = today.Date,
        }, session.Transaction);
}

/// <summary>Claim Bordx > Storage > Document (legacy <c>frmStorageDoc</c>, "Storage / Cartons").</summary>
public class StorageDocumentService(IDbSessions sessions, IStorageEnquiryRepository enquiry, IStorageDocumentRepository repository, ILogger<StorageDocumentService> log)
{
    /// <summary>ComboListing: the cartons, users and departments.</summary>
    public async Task<StorageDocumentLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        var lookups = await enquiry.LookupsAsync(session);
        return new StorageDocumentLookups(lookups.Cartons, lookups.Users, await repository.DepartmentsAsync(session));
    }

    /// <summary>StorageSearchRecord: the documents that match.</summary>
    public async Task<SearchResult<StorageDocumentRow>> SearchAsync(StorageDocumentFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return WorksheetLimits.Cap(await repository.SearchAsync(session, filter));
    }

    /// <summary>
    /// SaveStore for each checked document, in one transaction: a document that is stored already (add) is a <see cref="ConflictException"/> ("Record Duplicated! Please Change The Corresponding Field"),
    /// a document that is not stored (edit) is a <see cref="NotFoundException"/>, a carton that does not exist is a <see cref="BusinessRuleException"/>; nothing is saved then.
    /// </summary>
    public async Task SaveAsync(StorageDocumentSaveRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        foreach (var documentNo in request.Documents.Select(d => d.Trim().ToUpperInvariant()).Distinct())
            switch (await repository.SaveAsync(session, documentNo, request, userCode, DateTime.Today))
            {
                case 1: throw new ConflictException("Record Duplicated! Please Change The Corresponding Field");
                case 2: throw new NotFoundException("Record not found");
                case 3: throw new BusinessRuleException("Carton Number not found");
            }

        await session.CommitAsync();
        log.LogInformation("{Count} document(s) {Action} in carton {Carton} by {User}", request.Documents.Count, request.Edit ? "changed" : "stored", request.Carton, userCode);
    }
}
