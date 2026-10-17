using Genisis.Api.Data;

namespace Genisis.Api.Case.Lookups;

/// <summary>A payor of the payor drop-downs (Inpatient Case Listing, Claim ASCII, Case ASCII).</summary>
public record PayorOption
{
    /// <summary>PAY.PAYCode.</summary>
    public string Code { get; set; } = "";

    /// <summary>PAY.PAYCompanyName.</summary>
    public string? Name { get; set; }
}

/// <summary>Reads the payor list.</summary>
public interface IPayorRepository
{
    /// <summary>Every payor, by code.</summary>
    Task<List<PayorOption>> GetPayorsAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class PayorRepository : IPayorRepository
{
    /// <inheritdoc />
    public Task<List<PayorOption>> GetPayorsAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<PayorOption>("genisis.Case_Payors", null, session.Transaction);
}

/// <summary>The payor drop-down of the case screens (legacy SearchPayor / PayorComboListing).</summary>
public class PayorLookupService(IDbSessions sessions, IPayorRepository payors)
{
    /// <summary>Every payor, by code.</summary>
    public async Task<List<PayorOption>> GetPayorsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await payors.GetPayorsAsync(session);
    }
}
