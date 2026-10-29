using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.MemberDates;

/// <summary>The date ranges a member's effective date, expiry date and date of birth must fall in (the one row of <c>MBMValidDateRange</c>).</summary>
public record MemberDatesRow
{
    /// <summary>The table has a row ("Record not found" when it does not).</summary>
    public bool Found { get; set; }

    /// <summary>Effective date, from.</summary>
    public DateTime? EffectiveStart { get; set; }

    /// <summary>Effective date, to.</summary>
    public DateTime? EffectiveEnd { get; set; }

    /// <summary>Expiry date, from.</summary>
    public DateTime? ExpiryStart { get; set; }

    /// <summary>Expiry date, to.</summary>
    public DateTime? ExpiryEnd { get; set; }

    /// <summary>Date of birth, from.</summary>
    public DateTime? BirthStart { get; set; }

    /// <summary>Date of birth, to.</summary>
    public DateTime? BirthEnd { get; set; }

    /// <summary>User who last changed the row.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Day the row was last changed.</summary>
    public DateTime? UpdatedOn { get; set; }
}

/// <summary>The six dates to save (<c>cmdUpdate_Click</c>).</summary>
public class MemberDatesRequest
{
    /// <summary>Effective date, from.</summary>
    public DateTime? EffectiveStart { get; set; }

    /// <summary>Effective date, to.</summary>
    public DateTime? EffectiveEnd { get; set; }

    /// <summary>Expiry date, from.</summary>
    public DateTime? ExpiryStart { get; set; }

    /// <summary>Expiry date, to.</summary>
    public DateTime? ExpiryEnd { get; set; }

    /// <summary>Date of birth, from.</summary>
    public DateTime? BirthStart { get; set; }

    /// <summary>Date of birth, to.</summary>
    public DateTime? BirthEnd { get; set; }
}

/// <summary>cmdUpdate_Click's checks, in the desktop's order: each date has to be entered.</summary>
public sealed class MemberDatesRequestValidator : RequestValidator<MemberDatesRequest>
{
    /// <summary>Declares the rules.</summary>
    public MemberDatesRequestValidator()
    {
        RuleFor(r => r.EffectiveStart).NotNull().WithMessage("Please Enter Effective Start Date");
        RuleFor(r => r.EffectiveEnd).NotNull().WithMessage("Please Enter Effective End Date");
        RuleFor(r => r.ExpiryStart).NotNull().WithMessage("Please Enter Expiry Start Date");
        RuleFor(r => r.ExpiryEnd).NotNull().WithMessage("Please Enter Expiry End Date");
        RuleFor(r => r.BirthStart).NotNull().WithMessage("Please Enter DOB Start Date");
        RuleFor(r => r.BirthEnd).NotNull().WithMessage("Please Enter DOB End Date");
    }
}

/// <summary>Reads and saves the member date ranges.</summary>
public interface IMemberDatesRepository
{
    /// <summary>The row, or one with <see cref="MemberDatesRow.Found"/> false.</summary>
    Task<MemberDatesRow> GetAsync(IDbSession session);

    /// <summary>Updates the row, or adds it when the table is empty; returns 1 when it was added.</summary>
    Task<int> SaveAsync(IDbSession session, MemberDatesRequest request, string userCode, DateTime today);
}

/// <inheritdoc />
public sealed class MemberDatesRepository : IMemberDatesRepository
{
    /// <inheritdoc />
    public async Task<MemberDatesRow> GetAsync(IDbSession session) =>
        await session.Connection.ProcFirstOrDefaultAsync<MemberDatesRow>("genisis.MemberDates_Get", null, session.Transaction) ?? new MemberDatesRow();

    /// <inheritdoc />
    public async Task<int> SaveAsync(IDbSession session, MemberDatesRequest r, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.MemberDates_Save", new
        {
            effStart = r.EffectiveStart!.Value.Date, effEnd = r.EffectiveEnd!.Value.Date, expStart = r.ExpiryStart!.Value.Date, expEnd = r.ExpiryEnd!.Value.Date,
            birthStart = r.BirthStart!.Value.Date, birthEnd = r.BirthEnd!.Value.Date, userCode, today = today.Date,
        }, session.Transaction);
}

/// <summary>Maintenance > Member (legacy <c>frmMaintenanceMembers</c>, "Members Maintenance": the Valid Date Range tab).</summary>
public class MemberDatesService(IDbSessions sessions, IMemberDatesRepository repository, ILogger<MemberDatesService> log)
{
    /// <summary>LoadDate: the saved ranges.</summary>
    public async Task<MemberDatesRow> GetAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetAsync(session);
    }

    /// <summary>cmdUpdate_Click: saves the six dates.</summary>
    public async Task SaveAsync(MemberDatesRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        await repository.SaveAsync(session, request, userCode, DateTime.Today);
        await session.CommitAsync();
        log.LogInformation("Member valid date ranges saved by {User}", userCode);
    }
}
