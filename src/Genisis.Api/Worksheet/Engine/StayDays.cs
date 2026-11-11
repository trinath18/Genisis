using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>The days of the stay a worksheet version's invoices charge for.</summary>
/// <param name="RoomDays">Room &amp; Board days (may be fractional).</param>
/// <param name="IcuDays">ICU days.</param>
/// <param name="PhysicianVisitDays">In-hospital physician visit days.</param>
/// <param name="KidneyMonths">Kidney dialysis months.</param>
/// <param name="CashDays">Cash allowance days.</param>
/// <param name="GuardianDays">Guardian (lodger) days.</param>
public record StayDays(decimal RoomDays, decimal IcuDays, decimal PhysicianVisitDays = 0m, decimal KidneyMonths = 0m, decimal CashDays = 0m, decimal GuardianDays = 0m);

/// <summary>Reads the days of the stay from a version's invoices.</summary>
public interface IStayDaysRepository
{
    /// <summary>The days summed over the version's invoices (zero when it has none).</summary>
    Task<StayDays> DaysAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class StayDaysRepository : IStayDaysRepository
{
    /// <inheritdoc />
    public async Task<StayDays> DaysAsync(IDbSession session, string caseNo, int version) =>
        await session.Connection.ProcFirstOrDefaultAsync<StayDays>("genisis.WorksheetEngine_StayDays", new { cas = caseNo, version }, session.Transaction) ?? new StayDays(0m, 0m);
}
