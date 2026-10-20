using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.NonDisclosure;

/// <summary>A case's non-disclosure record as the screen shows it.</summary>
public record NonDisclosureView
{
    /// <summary>CAS.CASNumber.</summary>
    public string CaseNumber { get; set; } = "";

    /// <summary>False when CASOthersTwo has no row for the case yet.</summary>
    public bool HasRecord { get; set; }

    /// <summary>Pre-existing illness category (1 Illness, 2 Injury, 3 Hospitalisation, 4 Others).</summary>
    public string? PreCategory { get; set; }

    /// <summary>Pre-existing illness date.</summary>
    public DateTime? PreDate { get; set; }

    /// <summary>Free text when the pre-existing category is Others.</summary>
    public string? PreOthers { get; set; }

    /// <summary>Who informed the pre-existing illness.</summary>
    public string? PreInformedBy { get; set; }

    /// <summary>Underlying illness category (1 Diabetes, 2 Hypertension, 3 Others).</summary>
    public string? UndCategory { get; set; }

    /// <summary>Underlying illness date.</summary>
    public DateTime? UndDate { get; set; }

    /// <summary>Free text when the underlying category is Others.</summary>
    public string? UndOthers { get; set; }

    /// <summary>Who informed the underlying illness.</summary>
    public string? UndInformedBy { get; set; }
}

/// <summary>The values the Update button saves.</summary>
public class NonDisclosureRequest
{
    /// <summary>Pre-existing illness category.</summary>
    public string? PreCategory { get; set; }

    /// <summary>Pre-existing illness date.</summary>
    public DateTime? PreDate { get; set; }

    /// <summary>Pre-existing "Others" text.</summary>
    public string? PreOthers { get; set; }

    /// <summary>Who informed the pre-existing illness.</summary>
    public string? PreInformedBy { get; set; }

    /// <summary>Underlying illness category.</summary>
    public string? UndCategory { get; set; }

    /// <summary>Underlying illness date.</summary>
    public DateTime? UndDate { get; set; }

    /// <summary>Underlying "Others" text.</summary>
    public string? UndOthers { get; set; }

    /// <summary>Who informed the underlying illness.</summary>
    public string? UndInformedBy { get; set; }
}

/// <summary>The category lists of frmCASNonDisclosure.ComboListing and the rule that "Others" text only belongs to the Others category.</summary>
public static class NonDisclosureRules
{
    /// <summary>The "Others" category of the pre-existing list.</summary>
    public const string PreOthers = "4";

    /// <summary>The "Others" category of the underlying list.</summary>
    public const string UndOthers = "3";

    /// <summary>Valid pre-existing categories.</summary>
    public static readonly string[] PreCategories = ["1", "2", "3", PreOthers];

    /// <summary>Valid underlying categories.</summary>
    public static readonly string[] UndCategories = ["1", "2", UndOthers];

    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>The values as stored: trimmed, and "Others" text is dropped unless its category is Others.</summary>
    public static NonDisclosureRequest Normalize(NonDisclosureRequest r) => new()
    {
        PreCategory = Text(r.PreCategory), PreDate = r.PreDate, PreInformedBy = Text(r.PreInformedBy),
        PreOthers = Text(r.PreCategory) == PreOthers ? Text(r.PreOthers) : "",
        UndCategory = Text(r.UndCategory), UndDate = r.UndDate, UndInformedBy = Text(r.UndInformedBy),
        UndOthers = Text(r.UndCategory) == UndOthers ? Text(r.UndOthers) : "",
    };
}

/// <summary>Input checks of the Update button (categories in the lists, lengths of the CASOthersTwo columns).</summary>
public sealed class NonDisclosureRequestValidator : RequestValidator<NonDisclosureRequest>
{
    /// <summary>Declares the rules.</summary>
    public NonDisclosureRequestValidator()
    {
        RuleFor(r => r.PreCategory).Must(c => string.IsNullOrWhiteSpace(c) || NonDisclosureRules.PreCategories.Contains(c.Trim()))
            .WithMessage("Select a valid Pre-Existing Illness category.");
        RuleFor(r => r.UndCategory).Must(c => string.IsNullOrWhiteSpace(c) || NonDisclosureRules.UndCategories.Contains(c.Trim()))
            .WithMessage("Select a valid Underlying Illness category.");
        RuleFor(r => r).Must(r => (r.PreOthers ?? "").Trim().Length <= 50 && (r.UndOthers ?? "").Trim().Length <= 50)
            .WithMessage("Others cannot be longer than 50 characters.");
        RuleFor(r => r).Must(r => (r.PreInformedBy ?? "").Trim().Length <= 30 && (r.UndInformedBy ?? "").Trim().Length <= 30)
            .WithMessage("Informed by cannot be longer than 30 characters.");
    }
}

/// <summary>Reads and writes the non-disclosure columns of CASOthersTwo.</summary>
public interface INonDisclosureRepository
{
    /// <summary>The case's record, or null when the case does not exist.</summary>
    Task<NonDisclosureView?> GetAsync(IDbSession session, string cas);

    /// <summary>Updates (or inserts) the record; returns <see cref="StoredProcedures.NotFound"/> when the case does not exist.</summary>
    Task<int> SaveAsync(IDbSession session, string cas, NonDisclosureRequest values);
}

/// <inheritdoc />
public sealed class NonDisclosureRepository : INonDisclosureRepository
{
    /// <inheritdoc />
    public Task<NonDisclosureView?> GetAsync(IDbSession session, string cas) =>
        session.Connection.ProcFirstOrDefaultAsync<NonDisclosureView>("genisis.NonDisclosure_Get", new { cas }, session.Transaction);

    /// <inheritdoc />
    public Task<int> SaveAsync(IDbSession session, string cas, NonDisclosureRequest values) =>
        session.Connection.ProcSingleAsync<int>("genisis.NonDisclosure_Save", new
        {
            cas,
            preCategory = values.PreCategory, preDate = values.PreDate, preOthers = values.PreOthers, preInformedBy = values.PreInformedBy,
            undCategory = values.UndCategory, undDate = values.UndDate, undOthers = values.UndOthers, undInformedBy = values.UndInformedBy,
        }, session.Transaction);
}

/// <summary>Case > Non-Disclosure Info (legacy frmCASNonDisclosure).</summary>
public class NonDisclosureService(IDbSessions sessions, INonDisclosureRepository repository, ILogger<NonDisclosureService> log)
{
    private const string NoRecord = "No record found!";

    /// <summary>CasDetails. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task<NonDisclosureView> GetAsync(string cas)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetAsync(session, cas.Trim()) ?? throw new NotFoundException(NoRecord);
    }

    /// <summary>CmdUpdate_Click. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task SaveAsync(string cas, NonDisclosureRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        await SaveAsync(session, cas, request);
        await session.CommitAsync();
        log.LogInformation("Non-disclosure of case {CaseNumber} updated by {User}", cas, userCode);
    }

    /// <summary>Saves inside the caller's unit of work (nothing is committed here).</summary>
    public async Task SaveAsync(IDbSession session, string cas, NonDisclosureRequest request)
    {
        if (await repository.SaveAsync(session, cas.Trim(), NonDisclosureRules.Normalize(request)) != StoredProcedures.Ok)
            throw new NotFoundException(NoRecord);
    }
}
