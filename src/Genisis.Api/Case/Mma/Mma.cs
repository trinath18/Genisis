using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.Mma;

/// <summary>A row of the MMA procedure schedule (MMAProcedures).</summary>
public record MmaProcedure
{
    /// <summary>ProcIndex.</summary>
    public int Index { get; set; }

    /// <summary>Main body part.</summary>
    public string MainBodyPart { get; set; } = "";

    /// <summary>OPCS code.</summary>
    public string? OpcsCode { get; set; }

    /// <summary>OPCS narrative.</summary>
    public string? Narrative { get; set; }

    /// <summary>Surgeon's category.</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Anaesthetist's category.</summary>
    public string? AnaesthetistCategory { get; set; }

    /// <summary>Part (A, B or C).</summary>
    public string? Part { get; set; }

    /// <summary>Part A cost.</summary>
    public string? PartACost { get; set; }

    /// <summary>Page number.</summary>
    public string? PageNo { get; set; }
}

/// <summary>A row of the surgeon / anaesthetist fee look-up table (MMAFees).</summary>
public record MmaFee
{
    /// <summary>FeeIndex.</summary>
    public int Index { get; set; }

    /// <summary>Surgeon's category.</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Surgeon's fees.</summary>
    public decimal? SurgeonFees { get; set; }

    /// <summary>Anaesthetist's category.</summary>
    public string? AnaesthetistCategory { get; set; }

    /// <summary>Anaesthetist's fees.</summary>
    public decimal? AnaesthetistFees { get; set; }
}

/// <summary>The values of a procedure being added or changed.</summary>
public class MmaProcedureRequest
{
    /// <summary>Main body part.</summary>
    public string? MainBodyPart { get; set; }

    /// <summary>OPCS code.</summary>
    public string? OpcsCode { get; set; }

    /// <summary>OPCS narrative.</summary>
    public string? Narrative { get; set; }

    /// <summary>Surgeon's category.</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Anaesthetist's category.</summary>
    public string? AnaesthetistCategory { get; set; }

    /// <summary>Part (A, B or C).</summary>
    public string? Part { get; set; }

    /// <summary>Part A cost.</summary>
    public string? PartACost { get; set; }

    /// <summary>Page number.</summary>
    public string? PageNo { get; set; }
}

/// <summary>The values of a fee row being added or changed.</summary>
public class MmaFeeRequest
{
    /// <summary>Surgeon's category.</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Surgeon's fees.</summary>
    public decimal? SurgeonFees { get; set; }

    /// <summary>Anaesthetist's category.</summary>
    public string? AnaesthetistCategory { get; set; }

    /// <summary>Anaesthetist's fees.</summary>
    public decimal? AnaesthetistFees { get; set; }
}

/// <summary>The criteria of a procedure search (blank = not filtered).</summary>
public class MmaProcedureQuery
{
    /// <summary>Main body part contains.</summary>
    public string? MainBodyPart { get; set; }

    /// <summary>OPCS code contains.</summary>
    public string? OpcsCode { get; set; }
}

/// <summary>The criteria of a fee search (blank = not filtered).</summary>
public class MmaFeeQuery
{
    /// <summary>Surgeon's category contains.</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Anaesthetist's category contains.</summary>
    public string? AnaesthetistCategory { get; set; }
}

/// <summary>What a save returned: <c>Result</c> 0 saved / 1 row not found, and the row's index.</summary>
public record MmaSaveResult(int Result, int Index);

/// <summary>Search patterns, normalisation and limits shared by the MMA validators and service.</summary>
public static class MmaRules
{
    /// <summary>Most rows a search returns (the schedule has about 2,700).</summary>
    public const int MaxRows = 5000;

    /// <summary>Valid parts.</summary>
    public static readonly string[] Parts = ["A", "B", "C"];

    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>The LIKE pattern for a "contains" search, or null when the box is empty.</summary>
    public static string? Contains(string? value) => Text(value) is { Length: > 0 } s ? "%" + SqlText.EscapeLike(s) + "%" : null;

    /// <summary>The procedure as stored: trimmed, part upper-case.</summary>
    public static MmaProcedureRequest Normalize(MmaProcedureRequest r) => new()
    {
        MainBodyPart = Text(r.MainBodyPart), OpcsCode = Text(r.OpcsCode), Narrative = Text(r.Narrative), SurgeonCategory = Text(r.SurgeonCategory),
        AnaesthetistCategory = Text(r.AnaesthetistCategory), Part = Text(r.Part).ToUpperInvariant(), PartACost = Text(r.PartACost), PageNo = Text(r.PageNo),
    };
}

/// <summary>frmMMA.SaveProc's checks plus the MMAProcedures column lengths.</summary>
public sealed partial class MmaProcedureRequestValidator : RequestValidator<MmaProcedureRequest>
{
    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex Number();

    private static string Text(string? value) => (value ?? "").Trim();
    private static bool IsNumber(string? value) => Text(value).Length == 0 || Number().IsMatch(Text(value));

    /// <summary>Declares the rules.</summary>
    public MmaProcedureRequestValidator()
    {
        RuleFor(r => r.MainBodyPart).Must(v => Text(v).Length > 0).WithMessage("Please Enter Main Body Part");
        RuleFor(r => r.OpcsCode).Must(v => Text(v).Length > 0).WithMessage("Please Enter OPCS Code");
        RuleFor(r => r.Narrative).Must(v => Text(v).Length > 0).WithMessage("Please Enter OPCS Narrative");
        RuleFor(r => r.PageNo).Must(v => Text(v).Length > 0).WithMessage("Please Enter Page No");
        RuleFor(r => r.Part).Must(v => Text(v).Length == 0 || MmaRules.Parts.Contains(Text(v).ToUpperInvariant())).WithMessage("Part must be A, B or C.");
        RuleFor(r => r.MainBodyPart).Must(v => Text(v).Length <= 100).WithMessage("Main Body Part cannot be longer than 100 characters.");
        RuleFor(r => r.OpcsCode).Must(v => Text(v).Length <= 10).WithMessage("OPCS Code cannot be longer than 10 characters.");
        RuleFor(r => r.Narrative).Must(v => Text(v).Length <= 400).WithMessage("OPCS Narrative cannot be longer than 400 characters.");
        RuleFor(r => r).Must(r => Text(r.SurgeonCategory).Length <= 30 && Text(r.AnaesthetistCategory).Length <= 30).WithMessage("Categories cannot be longer than 30 characters.");
        RuleFor(r => r.PartACost).Must(v => Text(v).Length <= 5 && IsNumber(v)).WithMessage("Part A Cost must be a number of up to 5 characters.");
        RuleFor(r => r.PageNo).Must(v => Text(v).Length <= 10 && IsNumber(v)).WithMessage("Page No must be a number of up to 10 characters.");
    }
}

/// <summary>frmMMA.SaveLook's checks plus the MMAFees column sizes.</summary>
public sealed class MmaFeeRequestValidator : RequestValidator<MmaFeeRequest>
{
    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>Declares the rules.</summary>
    public MmaFeeRequestValidator()
    {
        RuleFor(r => r).Must(r => Text(r.SurgeonCategory).Length > 0 || Text(r.AnaesthetistCategory).Length > 0)
            .WithMessage("Please enter a Surgeon's or Anaesthetist's category.");
        RuleFor(r => r.SurgeonCategory).Must(v => Text(v).Length <= 10).WithMessage("Surgeon's Category cannot be longer than 10 characters.");
        RuleFor(r => r.AnaesthetistCategory).Must(v => Text(v).Length <= 50).WithMessage("Anaesthetist's Category cannot be longer than 50 characters.");
        RuleFor(r => r).Must(r => r.SurgeonFees is null or >= 0 && r.AnaesthetistFees is null or >= 0).WithMessage("Fees cannot be negative.");
        RuleFor(r => r).Must(r => r.SurgeonFees is null or <= 99_999_999 && r.AnaesthetistFees is null or <= 99_999_999).WithMessage("Fees cannot be longer than 8 digits.");
    }
}

/// <summary>Reads and writes the MMA schedule and fee table.</summary>
public interface IMmaRepository
{
    /// <summary>Procedures matching the LIKE patterns (null = not filtered).</summary>
    Task<List<MmaProcedure>> SearchProceduresAsync(IDbSession session, string? mainBodyPartPattern, string? opcsCodePattern);

    /// <summary>Inserts (index null) or updates a procedure.</summary>
    Task<MmaSaveResult> SaveProcedureAsync(IDbSession session, int? index, MmaProcedureRequest values, string userCode);

    /// <summary>Deletes a procedure; false when it does not exist.</summary>
    Task<bool> DeleteProcedureAsync(IDbSession session, int index);

    /// <summary>Fee rows matching the LIKE patterns (null = not filtered).</summary>
    Task<List<MmaFee>> SearchFeesAsync(IDbSession session, string? surgeonPattern, string? anaesthetistPattern);

    /// <summary>Inserts (index null) or updates a fee row.</summary>
    Task<MmaSaveResult> SaveFeeAsync(IDbSession session, int? index, MmaFeeRequest values, string userCode);

    /// <summary>Deletes a fee row; false when it does not exist.</summary>
    Task<bool> DeleteFeeAsync(IDbSession session, int index);
}

/// <inheritdoc />
public sealed class MmaRepository : IMmaRepository
{
    /// <inheritdoc />
    public Task<List<MmaProcedure>> SearchProceduresAsync(IDbSession session, string? mainBodyPartPattern, string? opcsCodePattern) =>
        session.Connection.ProcQueryAsync<MmaProcedure>("genisis.Mma_ProcedureSearch",
            new { mainBodyPart = mainBodyPartPattern, opcsCode = opcsCodePattern, max = MmaRules.MaxRows }, session.Transaction);

    /// <inheritdoc />
    public Task<MmaSaveResult> SaveProcedureAsync(IDbSession session, int? index, MmaProcedureRequest values, string userCode) =>
        session.Connection.ProcSingleAsync<MmaSaveResult>("genisis.Mma_ProcedureSave", new
        {
            index, mainBodyPart = values.MainBodyPart, opcsCode = values.OpcsCode, narrative = values.Narrative, surgeonCategory = values.SurgeonCategory,
            anaesthetistCategory = values.AnaesthetistCategory, part = values.Part, partACost = values.PartACost, pageNo = values.PageNo, userCode,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> DeleteProcedureAsync(IDbSession session, int index) =>
        await session.Connection.ProcSingleAsync<int>("genisis.Mma_ProcedureDelete", new { index }, session.Transaction) == StoredProcedures.Ok;

    /// <inheritdoc />
    public Task<List<MmaFee>> SearchFeesAsync(IDbSession session, string? surgeonPattern, string? anaesthetistPattern) =>
        session.Connection.ProcQueryAsync<MmaFee>("genisis.Mma_FeeSearch",
            new { surgeonCategory = surgeonPattern, anaesthetistCategory = anaesthetistPattern, max = MmaRules.MaxRows }, session.Transaction);

    /// <inheritdoc />
    public Task<MmaSaveResult> SaveFeeAsync(IDbSession session, int? index, MmaFeeRequest values, string userCode) =>
        session.Connection.ProcSingleAsync<MmaSaveResult>("genisis.Mma_FeeSave", new
        {
            index, surgeonCategory = (values.SurgeonCategory ?? "").Trim(), surgeonFees = values.SurgeonFees,
            anaesthetistCategory = (values.AnaesthetistCategory ?? "").Trim(), anaesthetistFees = values.AnaesthetistFees, userCode,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> DeleteFeeAsync(IDbSession session, int index) =>
        await session.Connection.ProcSingleAsync<int>("genisis.Mma_FeeDelete", new { index }, session.Transaction) == StoredProcedures.Ok;
}

/// <summary>Case > MMA/CPT (legacy frmMMA).</summary>
public class MmaService(IDbSessions sessions, IMmaRepository repository, ILogger<MmaService> log)
{
    private const string NoRecord = "Record not found";

    /// <summary>ProcListing / SearchProc.</summary>
    public async Task<List<MmaProcedure>> SearchProceduresAsync(MmaProcedureQuery query)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchProceduresAsync(session, MmaRules.Contains(query.MainBodyPart), MmaRules.Contains(query.OpcsCode));
    }

    /// <summary>LookListing / SearchLook.</summary>
    public async Task<List<MmaFee>> SearchFeesAsync(MmaFeeQuery query)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchFeesAsync(session, MmaRules.Contains(query.SurgeonCategory), MmaRules.Contains(query.AnaesthetistCategory));
    }

    /// <summary>SaveProc: adds (index null) or changes a procedure and returns its index. Throws <see cref="NotFoundException"/> when the row to change is gone.</summary>
    public async Task<int> SaveProcedureAsync(int? index, MmaProcedureRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var saved = await SaveProcedureAsync(session, index, request, userCode);
        await session.CommitAsync();
        log.LogInformation("MMA procedure {Index} {Action} by {User}", saved, index is null ? "added" : "updated", userCode);
        return saved;
    }

    /// <summary>Saves inside the caller's unit of work (nothing is committed here).</summary>
    public async Task<int> SaveProcedureAsync(IDbSession session, int? index, MmaProcedureRequest request, string userCode)
    {
        var saved = await repository.SaveProcedureAsync(session, index, MmaRules.Normalize(request), userCode);
        return saved.Result == StoredProcedures.Ok ? saved.Index : throw new NotFoundException(NoRecord);
    }

    /// <summary>SaveLook: adds (index null) or changes a fee row and returns its index. Throws <see cref="NotFoundException"/> when the row to change is gone.</summary>
    public async Task<int> SaveFeeAsync(int? index, MmaFeeRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var saved = await SaveFeeAsync(session, index, request, userCode);
        await session.CommitAsync();
        log.LogInformation("MMA fee {Index} {Action} by {User}", saved, index is null ? "added" : "updated", userCode);
        return saved;
    }

    /// <summary>Saves inside the caller's unit of work (nothing is committed here).</summary>
    public async Task<int> SaveFeeAsync(IDbSession session, int? index, MmaFeeRequest request, string userCode)
    {
        var saved = await repository.SaveFeeAsync(session, index, request, userCode);
        return saved.Result == StoredProcedures.Ok ? saved.Index : throw new NotFoundException(NoRecord);
    }

    /// <summary>DeleteProc. Throws <see cref="NotFoundException"/> when the row is gone.</summary>
    public async Task DeleteProcedureAsync(int index, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (!await repository.DeleteProcedureAsync(session, index)) throw new NotFoundException(NoRecord);
        await session.CommitAsync();
        log.LogInformation("MMA procedure {Index} deleted by {User}", index, userCode);
    }

    /// <summary>DeleteLook. Throws <see cref="NotFoundException"/> when the row is gone.</summary>
    public async Task DeleteFeeAsync(int index, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (!await repository.DeleteFeeAsync(session, index)) throw new NotFoundException(NoRecord);
        await session.CommitAsync();
        log.LogInformation("MMA fee {Index} deleted by {User}", index, userCode);
    }
}
