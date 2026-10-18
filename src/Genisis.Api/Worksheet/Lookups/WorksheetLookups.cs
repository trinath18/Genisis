using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Lookups;

/// <summary>An insured type of the Ins Type drop-downs (INS).</summary>
public record InsuredTypeOption
{
    /// <summary>INS.INSCode.</summary>
    public string Code { get; set; } = "";

    /// <summary>INS.INSDescription.</summary>
    public string? Name { get; set; }
}

/// <summary>Reads the lists the worksheet screens offer.</summary>
public interface IWorksheetLookupRepository
{
    /// <summary>Every insured type, by code.</summary>
    Task<List<InsuredTypeOption>> GetInsuredTypesAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class WorksheetLookupRepository : IWorksheetLookupRepository
{
    /// <inheritdoc />
    public Task<List<InsuredTypeOption>> GetInsuredTypesAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<InsuredTypeOption>("genisis.Worksheet_InsuredTypes", null, session.Transaction);
}

/// <summary>The drop-downs of the worksheet screens (legacy ComboListing / SearchINS).</summary>
public class WorksheetLookupService(IDbSessions sessions, IWorksheetLookupRepository repository)
{
    /// <summary>Every insured type, by code.</summary>
    public async Task<List<InsuredTypeOption>> GetInsuredTypesAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetInsuredTypesAsync(session);
    }
}
