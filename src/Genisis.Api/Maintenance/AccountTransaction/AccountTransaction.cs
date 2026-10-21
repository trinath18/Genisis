using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.AccountTransaction;

/// <summary>The criteria of the desktop's Criteria frame; every one is optional.</summary>
public class AccountTransactionQuery
{
    /// <summary>Body Part - MMA (contains).</summary>
    public string? BodyPart { get; set; }

    /// <summary>OPCS code (contains).</summary>
    public string? Opcs { get; set; }

    /// <summary>MMA description of the procedure (contains).</summary>
    public string? Procedure { get; set; }

    /// <summary>Surgeon's category as listed (contains).</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Anaesthetist's category as listed (contains).</summary>
    public string? AnaesthetistCategory { get; set; }

    /// <summary>MMA part (exact).</summary>
    public string? Part { get; set; }

    /// <summary>First page (the page text is page + part, compared as text).</summary>
    public string? PageFrom { get; set; }

    /// <summary>Last page.</summary>
    public string? PageTo { get; set; }

    /// <summary>CPT code (exact).</summary>
    public string? CptCode { get; set; }

    /// <summary>CPT description (contains).</summary>
    public string? CptDescription { get; set; }
}

/// <summary>Length limits of the criteria; the desktop's text boxes did not limit them.</summary>
public sealed class AccountTransactionQueryValidator : RequestValidator<AccountTransactionQuery>
{
    /// <summary>The longest accepted criterion.</summary>
    public const int MaxLength = 200;

    /// <summary>Declares the rules.</summary>
    public AccountTransactionQueryValidator()
    {
        Short(RuleFor(q => q.BodyPart), "Body Part");
        Short(RuleFor(q => q.Opcs), "OPCS");
        Short(RuleFor(q => q.Procedure), "MMA Desc");
        Short(RuleFor(q => q.SurgeonCategory), "Surg Cat");
        Short(RuleFor(q => q.AnaesthetistCategory), "Ane Cat");
        Short(RuleFor(q => q.Part), "MMA Part");
        Short(RuleFor(q => q.PageFrom), "Page");
        Short(RuleFor(q => q.PageTo), "Page To");
        Short(RuleFor(q => q.CptCode), "CPT Code");
        Short(RuleFor(q => q.CptDescription), "CPT Desc");
    }

    private static void Short(IRuleBuilderInitial<AccountTransactionQuery, string?> rule, string label) =>
        rule.Must(v => (v?.Trim().Length ?? 0) <= MaxLength).WithMessage($"{label} must be at most {MaxLength} characters.");
}

/// <summary>One row of the MMA Schedule of Fees list.</summary>
public record AccountTransactionRow
{
    /// <summary>Main body part code (MMA).</summary>
    public string? BodyPart { get; set; }

    /// <summary>OPCS code.</summary>
    public string? Opcs { get; set; }

    /// <summary>Procedure (OPCS narrative).</summary>
    public string? ProcedureDescription { get; set; }

    /// <summary>Surgeon's category without its first character, as the desktop listed it.</summary>
    public string? SurgeonCategory { get; set; }

    /// <summary>Anaesthetist's category without its first character.</summary>
    public string? AnaesthetistCategory { get; set; }

    /// <summary>MMA part (A, B, ...).</summary>
    public string? Part { get; set; }

    /// <summary>Page text (page + part).</summary>
    public string? Page { get; set; }

    /// <summary>Surgeon's fee (shown for parts other than A).</summary>
    public decimal? SurgeonFee { get; set; }

    /// <summary>Anaesthetist's fee (shown for parts other than A).</summary>
    public decimal? AnaesthetistFee { get; set; }

    /// <summary>Part A fee (shown for part A only).</summary>
    public string? PartACost { get; set; }

    /// <summary>CPT code.</summary>
    public string? CptCode { get; set; }

    /// <summary>CPT description.</summary>
    public string? CptDescription { get; set; }
}

/// <summary>The list and whether it was cut at <see cref="AccountTransactionService.Limit"/>.</summary>
public record AccountTransactionResult(List<AccountTransactionRow> Rows, bool Truncated);

/// <summary>Reads the MMA schedule of fees.</summary>
public interface IAccountTransactionRepository
{
    /// <summary>The rows matching the criteria, at most <paramref name="limit"/>.</summary>
    Task<List<AccountTransactionRow>> SearchAsync(IDbSession session, AccountTransactionQuery query, int limit);
}

/// <inheritdoc />
public sealed class AccountTransactionRepository : IAccountTransactionRepository
{
    /// <inheritdoc />
    public Task<List<AccountTransactionRow>> SearchAsync(IDbSession session, AccountTransactionQuery q, int limit) =>
        session.Connection.ProcQueryAsync<AccountTransactionRow>("genisis.AccountTransaction_Search", new
        {
            bodyPart = Text(q.BodyPart), opcs = Text(q.Opcs), procedure = Text(q.Procedure), surgeonCategory = Text(q.SurgeonCategory),
            anaesthetistCategory = Text(q.AnaesthetistCategory), part = Text(q.Part), pageFrom = Text(q.PageFrom), pageTo = Text(q.PageTo),
            cptCode = Text(q.CptCode), cptDescription = Text(q.CptDescription), limit,
        }, session.Transaction);

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Maintenance > Account Transaction (legacy frmMMASchedule, "MMA Schedule of Fees").</summary>
public class AccountTransactionService(IDbSessions sessions, IAccountTransactionRepository repository)
{
    /// <summary>The most rows listed; the schedule has a few thousand, so the whole schedule fits.</summary>
    public const int Limit = 5000;

    /// <summary>CmdSearch_Click / MMAListing. Fees follow the desktop: part A rows show only the part A fee, other parts the surgeon's and anaesthetist's fees.</summary>
    public async Task<AccountTransactionResult> SearchAsync(AccountTransactionQuery query)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, query, Limit + 1);
        var truncated = rows.Count > Limit;
        if (truncated) rows.RemoveAt(rows.Count - 1);
        return new AccountTransactionResult([.. rows.Select(ShowFees)], truncated);
    }

    /// <summary>The MMA part whose rows carry a single part A fee instead of surgeon's and anaesthetist's fees.</summary>
    public const string PartA = "A";

    /// <summary>MMAListing: part A shows only the part A fee; every other part only the surgeon's and anaesthetist's fees.</summary>
    public static AccountTransactionRow ShowFees(AccountTransactionRow row) =>
        string.Equals(row.Part?.Trim(), PartA, StringComparison.OrdinalIgnoreCase)
            ? row with { SurgeonFee = null, AnaesthetistFee = null }
            : row with { PartACost = null };
}
