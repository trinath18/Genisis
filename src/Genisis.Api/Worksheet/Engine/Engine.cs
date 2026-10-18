using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>The amount of one worksheet grid row (slot), added up from the invoices of a version.</summary>
public record SlotAmount
{
    /// <summary>Grid row, 1 to 29: 1 Room &amp; Board ... 25 Emergency Sickness Treatment, 26 Others - No Co-Pay, 27 to 29 the service taxes.</summary>
    public int Slot { get; set; }

    /// <summary>The actual amount: every invoice of the version.</summary>
    public decimal Actual { get; set; }

    /// <summary>The part of <see cref="Actual"/> on invoices paid to the hospital (cashless), the desktop's H1..H29.</summary>
    public decimal ToHospital { get; set; }
}

/// <summary>Reads the inputs of the worksheet calculation.</summary>
public interface IWorksheetEngineRepository
{
    /// <summary>The 29 slots of a version with the amounts added up from its invoices (zero for a slot without an invoice amount).</summary>
    Task<List<SlotAmount>> ActualAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class WorksheetEngineRepository : IWorksheetEngineRepository
{
    /// <inheritdoc />
    public Task<List<SlotAmount>> ActualAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcQueryAsync<SlotAmount>("genisis.WorksheetEngine_Actual", new { cas = caseNo, version }, session.Transaction);
}

/// <summary>
/// The worksheet calculation the desktop's Registration and Adjustment forms run (getDatatoWS / getFinalTotal). It is built up in steps, each checked against the
/// worksheets the desktop saved: step 1 is the actual amount of every row, added up from the invoices of the version.
/// </summary>
public class WorksheetEngineService(IDbSessions sessions, IWorksheetEngineRepository repository)
{
    /// <summary>The actual amounts of a version, row by row, as the Actual column of the worksheet grid shows them.</summary>
    public async Task<List<SlotAmount>> ActualAsync(string caseNo, int version)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.ActualAsync(session, caseNo.Trim(), version);
    }
}
