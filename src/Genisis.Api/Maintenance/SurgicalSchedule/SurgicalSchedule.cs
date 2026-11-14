using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.SurgicalSchedule;

/// <summary>A payor of the drop-down (dbo.PAY).</summary>
public record SurgicalSchedulePayor
{
    /// <summary>Payor code.</summary>
    public string Code { get; set; } = "";

    /// <summary>Company name.</summary>
    public string Name { get; set; } = "";
}

/// <summary>A payor's plan (dbo.PLN).</summary>
public record SurgicalSchedulePlan
{
    /// <summary>Payor code.</summary>
    public string Payor { get; set; } = "";

    /// <summary>Plan code.</summary>
    public string PlanCode { get; set; } = "";

    /// <summary>Plan description.</summary>
    public string? Description { get; set; }
}

/// <summary>One row of the schedule (dbo.PLNSurgOperation).</summary>
public record SurgicalScheduleRow
{
    /// <summary>SURIndex.</summary>
    public int Index { get; set; }

    /// <summary>SurgMainCat.</summary>
    public string? MainCategory { get; set; }

    /// <summary>SurgSubCat.</summary>
    public string? SubCategory { get; set; }

    /// <summary>SurgPerc: the percentage of the surgical fee the plan pays for the category.</summary>
    public int? Percentage { get; set; }
}

/// <summary>A plan and its schedule.</summary>
public record SurgicalScheduleResult(SurgicalSchedulePlan Plan, List<SurgicalScheduleRow> Rows);

/// <summary>The payor and plan whose schedule is shown.</summary>
public class SurgicalScheduleQuery
{
    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Plan code.</summary>
    public string? Plan { get; set; }
}

/// <summary>A row to add or change.</summary>
public class SurgicalScheduleRequest : SurgicalScheduleQuery
{
    /// <summary>Surgical main category.</summary>
    public string? MainCategory { get; set; }

    /// <summary>Surgical sub category.</summary>
    public string? SubCategory { get; set; }

    /// <summary>Percentage (0 to 100).</summary>
    public int? Percentage { get; set; }
}

/// <summary>The search's checks: a payor, then a plan code.</summary>
public sealed class SurgicalScheduleQueryValidator : RequestValidator<SurgicalScheduleQuery>
{
    /// <summary>Declares the rules.</summary>
    public SurgicalScheduleQueryValidator()
    {
        RuleFor(q => q.Payor).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please Select Payor!");
        RuleFor(q => q.Plan).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please Enter Plan Code!");
    }
}

/// <summary>The save's checks: a payor and plan, a main category of at most 50 characters, a sub category of at most 200, and a percentage from 0 to 100.</summary>
public sealed class SurgicalScheduleRequestValidator : RequestValidator<SurgicalScheduleRequest>
{
    /// <summary>Declares the rules.</summary>
    public SurgicalScheduleRequestValidator()
    {
        RuleFor(q => q.Payor).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please Select Payor!");
        RuleFor(q => q.Plan).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please Enter Plan Code!");
        RuleFor(q => q.MainCategory).Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("Please Enter Surgical Main Category!");
        RuleFor(q => q.MainCategory).Must(m => (m ?? "").Trim().Length <= 50).WithMessage("Surgical Main Category can have at most 50 characters.");
        RuleFor(q => q.SubCategory).Must(s => (s ?? "").Trim().Length <= 200).WithMessage("Surgical Sub Category can have at most 200 characters.");
        RuleFor(q => q.Percentage).NotNull().WithMessage("Please Enter Percentage!");
        RuleFor(q => q.Percentage).InclusiveBetween(0, 100).When(q => q.Percentage is not null).WithMessage("Percentage must be from 0 to 100.");
    }
}

/// <summary>Reads and writes dbo.PLNSurgOperation.</summary>
public interface ISurgicalScheduleRepository
{
    /// <summary>The payors.</summary>
    Task<List<SurgicalSchedulePayor>> GetPayorsAsync(IDbSession session);

    /// <summary>The payor's plan, or null.</summary>
    Task<SurgicalSchedulePlan?> GetPlanAsync(IDbSession session, string payor, string plan);

    /// <summary>The plan's schedule.</summary>
    Task<List<SurgicalScheduleRow>> GetRowsAsync(IDbSession session, string payor, string plan);

    /// <summary>Adds (index null) or changes a row: the row's index, or -1 not found, -2 no such plan, -3 the category is there already.</summary>
    Task<int> SaveAsync(IDbSession session, int? index, string payor, string plan, string main, string? sub, int percentage);

    /// <summary>Deletes a row: a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> DeleteAsync(IDbSession session, int index, string payor, string plan);
}

/// <inheritdoc />
public sealed class SurgicalScheduleRepository : ISurgicalScheduleRepository
{
    /// <inheritdoc />
    public Task<List<SurgicalSchedulePayor>> GetPayorsAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<SurgicalSchedulePayor>("genisis.SurgSchedule_Payors", null, session.Transaction);

    /// <inheritdoc />
    public Task<SurgicalSchedulePlan?> GetPlanAsync(IDbSession session, string payor, string plan) =>
        session.Connection.ProcFirstOrDefaultAsync<SurgicalSchedulePlan>("genisis.SurgSchedule_Plan", new { pay = payor, plan }, session.Transaction);

    /// <inheritdoc />
    public Task<List<SurgicalScheduleRow>> GetRowsAsync(IDbSession session, string payor, string plan) =>
        session.Connection.ProcQueryAsync<SurgicalScheduleRow>("genisis.SurgSchedule_List", new { pay = payor, plan }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SaveAsync(IDbSession session, int? index, string payor, string plan, string main, string? sub, int percentage) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SurgSchedule_Save", new { index, pay = payor, plan, main, sub, percentage }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> DeleteAsync(IDbSession session, int index, string payor, string plan) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SurgSchedule_Delete", new { index, pay = payor, plan }, session.Transaction);
}

/// <summary>Maintenance > Surgical Schedule (rebuilt: the desktop has no form for it) on dbo.PLNSurgOperation.</summary>
public class SurgicalScheduleService(IDbSessions sessions, ISurgicalScheduleRepository repository, ILogger<SurgicalScheduleService> log)
{
    /// <summary>The payors of the drop-down.</summary>
    public async Task<List<SurgicalSchedulePayor>> GetPayorsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetPayorsAsync(session);
    }

    /// <summary>The plan and its schedule. Throws <see cref="NotFoundException"/> when the payor has no such plan.</summary>
    public async Task<SurgicalScheduleResult> GetAsync(SurgicalScheduleQuery query)
    {
        var (payor, plan) = Key(query);
        await using var session = await sessions.OpenAsync();
        var found = await repository.GetPlanAsync(session, payor, plan) ?? throw new NotFoundException($"Plan {plan} of payor {payor} was not found.");
        return new SurgicalScheduleResult(found, await repository.GetRowsAsync(session, payor, plan));
    }

    /// <summary>Adds a row (<paramref name="index"/> null) or changes one; returns the row's index.</summary>
    public async Task<int> SaveAsync(int? index, SurgicalScheduleRequest request, string userCode)
    {
        var (payor, plan) = Key(request);
        await using var session = await sessions.BeginAsync();
        var sub = string.IsNullOrWhiteSpace(request.SubCategory) ? null : request.SubCategory.Trim();
        var result = await repository.SaveAsync(session, index, payor, plan, request.MainCategory!.Trim(), sub, request.Percentage!.Value);
        if (result == -1) throw new NotFoundException("The row was not found. Search again.");
        if (result == -2) throw new NotFoundException($"Plan {plan} of payor {payor} was not found.");
        if (result == -3) throw new BusinessRuleException("The plan already has this Surgical Main Category and Sub Category.");
        await session.CommitAsync();
        log.LogInformation("Surgical schedule {Payor}/{Plan}: row {Index} {Action} by {User}", payor, plan, result, index is null ? "added" : "changed", userCode);
        return result;
    }

    /// <summary>Deletes a row of the plan.</summary>
    public async Task DeleteAsync(int index, SurgicalScheduleQuery query, string userCode)
    {
        var (payor, plan) = Key(query);
        await using var session = await sessions.BeginAsync();
        if (await repository.DeleteAsync(session, index, payor, plan) == StoredProcedures.NotFound) throw new NotFoundException("The row was not found. Search again.");
        await session.CommitAsync();
        log.LogInformation("Surgical schedule {Payor}/{Plan}: row {Index} deleted by {User}", payor, plan, index, userCode);
    }

    private static (string Payor, string Plan) Key(SurgicalScheduleQuery query) => (query.Payor!.Trim().ToUpperInvariant(), query.Plan!.Trim().ToUpperInvariant());
}
