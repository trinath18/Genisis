using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.Transfer;

/// <summary>The case being transferred and the member it is on now.</summary>
public record TransferCase
{
    /// <summary>CAS.CASNumber.</summary>
    public string CaseNumber { get; set; } = "";

    /// <summary>Current membership number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Current cover ID.</summary>
    public string CoverId { get; set; } = "";

    /// <summary>Case status.</summary>
    public string? CaseStatus { get; set; }
}

/// <summary>A member / patient as frmCaseTransferMBMNo's MBMInfo shows it.</summary>
public record TransferMember
{
    /// <summary>Membership number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Cover ID asked for.</summary>
    public string CoverId { get; set; } = "";

    /// <summary>Patient name (the principal for cover 00, otherwise the covered person).</summary>
    public string? PatientName { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>IC / passport number.</summary>
    public string? IcNo { get; set; }

    /// <summary>Policy status (A active, C cancelled, D deleted).</summary>
    public string? Status { get; set; }

    /// <summary>Policy effective date.</summary>
    public DateTime? EffDate { get; set; }

    /// <summary>Policy expiry date.</summary>
    public DateTime? ExpDate { get; set; }

    /// <summary>False when the member exists but has no such covered person.</summary>
    public bool CoverFound { get; set; }
}

/// <summary>What the Load button shows.</summary>
/// <param name="Case">The case and its current member / cover.</param>
/// <param name="Current">The current member's details.</param>
public record TransferView(TransferCase Case, TransferMember? Current);

/// <summary>The member and cover to transfer the case to.</summary>
public class TransferRequest
{
    /// <summary>Membership number to transfer to.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Cover ID to transfer to.</summary>
    public string? CoverId { get; set; }
}

/// <summary>The keys of the member to transfer to (looked up as soon as both are typed).</summary>
public class TransferMemberQuery
{
    /// <summary>Membership number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Cover ID.</summary>
    public string? CoverId { get; set; }
}

/// <summary>What a transfer reports.</summary>
/// <param name="Moved">The approved amount that moved between the members' limits.</param>
public record TransferResult(decimal Moved);

/// <summary>What the transfer procedure returned: a result code and the approved total that moved.</summary>
public record TransferApplied(int Result, decimal Moved);

/// <summary>cmdSave_Click's input checks, in the desktop's order.</summary>
public sealed class TransferRequestValidator : RequestValidator<TransferRequest>
{
    /// <summary>Declares the rules.</summary>
    public TransferRequestValidator()
    {
        RuleFor(r => r.MemberNo).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Please enter the Member No. to transfer to.");
        RuleFor(r => r.CoverId).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Please enter the Cover ID to transfer to.");
    }
}

/// <summary>DisplayTransMBMInfo needs both keys.</summary>
public sealed class TransferMemberQueryValidator : RequestValidator<TransferMemberQuery>
{
    /// <summary>Declares the rules.</summary>
    public TransferMemberQueryValidator() =>
        RuleFor(q => q).Must(q => !string.IsNullOrWhiteSpace(q.MemberNo) && !string.IsNullOrWhiteSpace(q.CoverId)).WithMessage("Enter the Member No. and the Cover ID.");
}

/// <summary>The checks cmdSave_Click makes on the member found for the target keys, after the input checks.</summary>
public static class TransferRules
{
    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>Returns the message to show, or null when the case may be moved to <paramref name="target"/>.</summary>
    public static string? Check(TransferMember? target, TransferCase current)
    {
        if (target is null || Text(target.PolicyNo).Length == 0) return "Member No not found.";
        if (Text(target.PatientName).Length == 0 && target.CoverFound) return "Patient not found.";
        if (Text(target.Status) == "D") return "Member status - 'DELETE'.";
        if (!target.CoverFound) return "Invalid Cover ID. Please try again.";
        if (Text(target.MemberNo) == Text(current.MemberNo) && Text(target.CoverId) == Text(current.CoverId)) return "The case is already on this member and cover.";
        return null;
    }
}

/// <summary>Reads the case and members and moves the case.</summary>
public interface ITransferRepository
{
    /// <summary>The case and its current member / cover, or null.</summary>
    Task<TransferCase?> GetCaseAsync(IDbSession session, string cas);

    /// <summary>The member / patient for a member number and cover ID, or null when the member does not exist.</summary>
    Task<TransferMember?> GetMemberAsync(IDbSession session, string memberNo, string coverId);

    /// <summary>Moves the case and its approved limits; <c>Result</c> 0 done, 1 case not found, 2 target not found, 3 same member, 4 previous member missing.</summary>
    Task<TransferApplied> ApplyAsync(IDbSession session, string cas, string memberNo, string coverId, string userCode);
}

/// <inheritdoc />
public sealed class TransferRepository : ITransferRepository
{
    /// <inheritdoc />
    public Task<TransferCase?> GetCaseAsync(IDbSession session, string cas) =>
        session.Connection.ProcFirstOrDefaultAsync<TransferCase>("genisis.CaseTransfer_Case", new { cas }, session.Transaction);

    /// <inheritdoc />
    public Task<TransferMember?> GetMemberAsync(IDbSession session, string memberNo, string coverId) =>
        session.Connection.ProcFirstOrDefaultAsync<TransferMember>("genisis.CaseTransfer_Member", new { mbm = memberNo, cover = coverId }, session.Transaction);

    /// <inheritdoc />
    public Task<TransferApplied> ApplyAsync(IDbSession session, string cas, string memberNo, string coverId, string userCode) =>
        session.Connection.ProcSingleAsync<TransferApplied>("genisis.CaseTransfer_Apply", new { cas, newMbm = memberNo, newCov = coverId, userCode }, session.Transaction);
}

/// <summary>Case > Case Transfer (legacy frmCaseTransferMBMNo).</summary>
public class TransferService(IDbSessions sessions, ITransferRepository repository, ILogger<TransferService> log)
{
    private const string NoRecord = "No record found!";

    /// <summary>CmdGo_Click: the case and the member it is on now. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task<TransferView> LoadAsync(string cas)
    {
        await using var session = await sessions.OpenAsync();
        var header = await repository.GetCaseAsync(session, cas.Trim()) ?? throw new NotFoundException(NoRecord);
        return new(header, await repository.GetMemberAsync(session, header.MemberNo, header.CoverId));
    }

    /// <summary>DisplayTransMBMInfo: the member to transfer to. Throws <see cref="NotFoundException"/> when the member does not exist.</summary>
    public async Task<TransferMember> LookupAsync(string memberNo, string coverId)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetMemberAsync(session, memberNo.Trim(), coverId.Trim()) ?? throw new NotFoundException("Member not found.");
    }

    /// <summary>cmdSave_Click: checks the target, then moves the case and its approved limits in one transaction.
    /// Throws <see cref="NotFoundException"/> or <see cref="BusinessRuleException"/> with the message to show.</summary>
    public async Task<TransferResult> TransferAsync(string cas, TransferRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var result = await TransferAsync(session, cas, request, userCode);
        await session.CommitAsync();
        log.LogInformation("Case {CaseNumber} transferred to {Member}-{Cover} by {User}; approved amount moved {Moved}",
            cas, request.MemberNo, request.CoverId, userCode, result.Moved);
        return result;
    }

    /// <summary>Transfers inside the caller's unit of work (nothing is committed here).</summary>
    public async Task<TransferResult> TransferAsync(IDbSession session, string cas, TransferRequest request, string userCode)
    {
        cas = cas.Trim();
        var current = await repository.GetCaseAsync(session, cas) ?? throw new NotFoundException(NoRecord);
        var memberNo = (request.MemberNo ?? "").Trim();
        var coverId = (request.CoverId ?? "").Trim();
        var target = await repository.GetMemberAsync(session, memberNo, coverId);
        if (TransferRules.Check(target, current) is { } problem) throw new BusinessRuleException(problem);

        var applied = await repository.ApplyAsync(session, cas, memberNo, coverId, userCode);
        return applied.Result switch
        {
            StoredProcedures.Ok => new TransferResult(applied.Moved),
            StoredProcedures.NotFound => throw new NotFoundException(NoRecord),
            2 => throw new BusinessRuleException("Member No not found."),
            3 => throw new BusinessRuleException("The case is already on this member and cover."),
            _ => throw new BusinessRuleException("The case's current member is not on the member table, so its limit cannot be given back. Contact IT."),
        };
    }
}
