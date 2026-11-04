using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;

namespace Genisis.Api.Payment.ContraAllocation;

/// <summary>The boxes of the Contra Allocation search. A blank box is no criterion.</summary>
public class ContraAllocationFilter
{
    /// <summary>Ref No from (the receipt number the contra row was written for).</summary>
    public string? RefFrom { get; set; }

    /// <summary>Ref No to.</summary>
    public string? RefTo { get; set; }

    /// <summary>0 all, 1 allocated, 2 not allocated (cboStatus).</summary>
    public int Status { get; set; }

    /// <summary>Use Scan: the one claim number scanned (exact).</summary>
    public string? ScanCase { get; set; }

    /// <summary>Case No from.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Case No to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Contra Case No from.</summary>
    public string? ContraFrom { get; set; }

    /// <summary>Contra Case No to.</summary>
    public string? ContraTo { get; set; }

    /// <summary>Allocation No from.</summary>
    public string? AllocationFrom { get; set; }

    /// <summary>Allocation No to.</summary>
    public string? AllocationTo { get; set; }

    /// <summary>Allocation date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Allocation date to.</summary>
    public DateTime? DateTo { get; set; }
}

/// <summary>cmdSearch_Click's checks.</summary>
public sealed class ContraAllocationFilterValidator : RequestValidator<ContraAllocationFilter>
{
    /// <summary>Declares the rules.</summary>
    public ContraAllocationFilterValidator()
    {
        RuleFor(f => f.Status).InclusiveBetween(0, 2).WithMessage("Select a valid status.");
        RuleFor(f => f).Must(f => new[] { f.RefFrom, f.RefTo, f.ScanCase, f.CaseFrom, f.CaseTo, f.ContraFrom, f.ContraTo, f.AllocationFrom, f.AllocationTo }.All(v => (v?.Length ?? 0) <= 15))
            .WithMessage("A number is too long (15 characters at most).");
    }
}

/// <summary>One contra row.</summary>
public record ContraAllocationRow
{
    /// <summary>CONIndex.</summary>
    public int Id { get; set; }

    /// <summary>The receipt number it was written for.</summary>
    public string? RefNo { get; set; }

    /// <summary>The claim's case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>The claim's version.</summary>
    public string? VersionNo { get; set; }

    /// <summary>The claim's amount.</summary>
    public decimal? CaseAmt { get; set; }

    /// <summary>The contra claim's case number.</summary>
    public string? ContraCaseNo { get; set; }

    /// <summary>The contra claim's version.</summary>
    public string? ContraVersionNo { get; set; }

    /// <summary>The contra claim's amount.</summary>
    public decimal? ContraCaseAmt { get; set; }

    /// <summary>The allocation number, once allocated.</summary>
    public string? AllocationNo { get; set; }

    /// <summary>The allocation date, once allocated.</summary>
    public DateTime? AllocationDate { get; set; }

    /// <summary>A when allocated.</summary>
    public string? ContraStatus { get; set; }
}

/// <summary>The rows found; <see cref="Truncated"/> when more than 5,000 matched.</summary>
public record ContraAllocationResult(List<ContraAllocationRow> Rows, bool Truncated);

/// <summary>The rows ticked to allocate.</summary>
public class ContraAllocationRequest
{
    /// <summary>CONIndex of each ticked row.</summary>
    public List<int>? Ids { get; set; }
}

/// <summary>cmdUpdate_Click's check: a row has to be ticked.</summary>
public sealed class ContraAllocationRequestValidator : RequestValidator<ContraAllocationRequest>
{
    /// <summary>Declares the rules.</summary>
    public ContraAllocationRequestValidator() =>
        RuleFor(r => r.Ids).Must(ids => ids is { Count: > 0 }).WithMessage("Please select the record");
}

/// <summary>What an allocation did: the number and how many rows got it.</summary>
public record ContraAllocationDone(string AllocationNo, int Count);

/// <summary>Reads and allocates the contra rows.</summary>
public interface IContraAllocationRepository
{
    /// <summary>The rows that match, at most <paramref name="max"/>.</summary>
    Task<List<ContraAllocationRow>> SearchAsync(IDbSession session, ContraAllocationFilter filter, int max);

    /// <summary>The next allocation number (the counter is locked until the transaction ends).</summary>
    Task<string> NextNumberAsync(IDbSession session);

    /// <summary>Allocates a row: 0 done, 1 no such row, 2 allocated already, 3 the row has no claim.</summary>
    Task<int> AllocateAsync(IDbSession session, int id, string allocationNo, DateTime today);
}

/// <inheritdoc />
public sealed class ContraAllocationRepository : IContraAllocationRepository
{
    /// <inheritdoc />
    public Task<List<ContraAllocationRow>> SearchAsync(IDbSession session, ContraAllocationFilter f, int max) =>
        session.Connection.ProcQueryAsync<ContraAllocationRow>("genisis.PaymentContra_Search", new
        {
            refFrom = FinanceLimits.Text(f.RefFrom), refTo = FinanceLimits.Text(f.RefTo), status = f.Status, scanCase = FinanceLimits.Text(f.ScanCase), caseFrom = FinanceLimits.Text(f.CaseFrom),
            caseTo = FinanceLimits.Text(f.CaseTo), contraFrom = FinanceLimits.Text(f.ContraFrom), contraTo = FinanceLimits.Text(f.ContraTo), allocFrom = FinanceLimits.Text(f.AllocationFrom),
            allocTo = FinanceLimits.Text(f.AllocationTo), dateFrom = FinanceLimits.Day(f.DateFrom), dateTo = FinanceLimits.Day(f.DateTo), max,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextNumberAsync(IDbSession session) =>
        (await session.Connection.ProcScalarAsync<string>("genisis.PaymentContra_NextNumber", null, session.Transaction))!;

    /// <inheritdoc />
    public Task<int> AllocateAsync(IDbSession session, int id, string allocationNo, DateTime today) =>
        session.Connection.ProcScalarAsync<int>("genisis.PaymentContra_Allocate", new { id, allocationNo, today }, session.Transaction);
}

/// <summary>Payment > Contra Allocation (legacy <c>frmContraAllocation</c>).</summary>
public class ContraAllocationService(IDbSessions sessions, IContraAllocationRepository repository, ILogger<ContraAllocationService> log)
{
    /// <summary>cmdSearch_Click: the contra rows that match; nothing found is a <see cref="NotFoundException"/> ("No record found!").</summary>
    public async Task<ContraAllocationResult> SearchAsync(ContraAllocationFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        if (rows.Count == 0) throw new NotFoundException("No record found!");
        return new ContraAllocationResult(rows.Take(FinanceLimits.MaxRows).ToList(), rows.Count > FinanceLimits.MaxRows);
    }

    /// <summary>cmdUpdate_Click: gives the rows ticked one allocation number, today's date and the status A, all or nothing.</summary>
    public async Task<ContraAllocationDone> AllocateAsync(ContraAllocationRequest request, string userCode)
    {
        var ids = request.Ids!.Distinct().ToList();
        await using var session = await sessions.BeginAsync();
        var number = await repository.NextNumberAsync(session);
        foreach (var id in ids)
        {
            switch (await repository.AllocateAsync(session, id, number, DateTime.Today))
            {
                case 1: throw new NotFoundException("Record not found.");
                case 2: throw new BusinessRuleException("Record has been allocated!");
                case 3: throw new BusinessRuleException("The record has no claim to allocate.");
            }
        }

        await session.CommitAsync();
        log.LogInformation("Contra allocation {AllocationNo}: {Count} row(s) by {User}", number, ids.Count, userCode);
        return new ContraAllocationDone(number, ids.Count);
    }
}
