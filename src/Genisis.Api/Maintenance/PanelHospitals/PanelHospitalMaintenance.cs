using FluentValidation;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.PanelHospitals;

/// <summary>A hospital that can be added to a panel (<c>SearchHOS2</c>).</summary>
public record AvailableHospital
{
    /// <summary>HOS.HOSCode.</summary>
    public string Code { get; set; } = "";

    /// <summary>Hospital name.</summary>
    public string? Name { get; set; }

    /// <summary>Telephone number.</summary>
    public string? Tel { get; set; }

    /// <summary>Fax number.</summary>
    public string? Fax { get; set; }
}

/// <summary>The panel code whose hospitals are listed.</summary>
public class PanelHospitalMaintenanceQuery
{
    /// <summary>The hospital panel code.</summary>
    public string? Code { get; set; }
}

/// <summary>Search and Add: a panel code must be chosen.</summary>
public sealed class PanelHospitalMaintenanceQueryValidator : RequestValidator<PanelHospitalMaintenanceQuery>
{
    /// <summary>Declares the rules.</summary>
    public PanelHospitalMaintenanceQueryValidator() =>
        RuleFor(q => q.Code).Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Please Select Hospital Panel Code!");
}

/// <summary>What Update changes: the hospitals to put in the panel (Add) and the ones to take out (Edit).</summary>
public class PanelHospitalMaintenanceRequest
{
    /// <summary>The hospital panel code.</summary>
    public string? Code { get; set; }

    /// <summary>Hospital codes ticked in the Add list.</summary>
    public List<string>? Add { get; set; }

    /// <summary>Hospital codes unticked in the Edit list.</summary>
    public List<string>? Remove { get; set; }
}

/// <summary>cmdUpdate_Click's checks: a panel code, and a hospital cannot be added and removed in one update.</summary>
public sealed class PanelHospitalMaintenanceRequestValidator : RequestValidator<PanelHospitalMaintenanceRequest>
{
    /// <summary>Declares the rules.</summary>
    public PanelHospitalMaintenanceRequestValidator()
    {
        RuleFor(r => r.Code).Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Please Select Hospital Panel Code!");
        RuleFor(r => r).Must(r => (r.Add ?? []).Intersect(r.Remove ?? [], StringComparer.OrdinalIgnoreCase).Any() is false).WithMessage("A hospital cannot be added and removed in the same update.");
        RuleFor(r => r).Must(r => (r.Add ?? []).Concat(r.Remove ?? []).All(h => !string.IsNullOrWhiteSpace(h) && h.Trim().Length <= 10)).WithMessage("Select valid hospitals.");
    }
}

/// <summary>What an update did.</summary>
/// <param name="Added">Hospitals put in the panel (or stamped again).</param>
/// <param name="Removed">Hospitals taken out.</param>
public record PanelHospitalMaintenanceResult(int Added, int Removed);

/// <summary>Reads and changes the hospitals of a panel.</summary>
public interface IPanelHospitalMaintenanceRepository
{
    /// <summary>The hospitals not yet in the panel.</summary>
    Task<List<AvailableHospital>> GetAvailableAsync(IDbSession session, string code);

    /// <summary>Puts a hospital in the panel: 0 added, 1 no such hospital, 2 no such panel code, 3 it was in already.</summary>
    Task<int> AddAsync(IDbSession session, string code, string hospital, string userCode, DateTime today);

    /// <summary>Takes a hospital out of the panel; the number of rows removed.</summary>
    Task<int> RemoveAsync(IDbSession session, string code, string hospital);
}

/// <inheritdoc />
public sealed class PanelHospitalMaintenanceRepository : IPanelHospitalMaintenanceRepository
{
    /// <inheritdoc />
    public Task<List<AvailableHospital>> GetAvailableAsync(IDbSession session, string code) =>
        session.Connection.ProcQueryAsync<AvailableHospital>("genisis.PanelHospitalMaint_Available", new { code }, session.Transaction);

    /// <inheritdoc />
    public Task<int> AddAsync(IDbSession session, string code, string hospital, string userCode, DateTime today) =>
        session.Connection.ProcScalarAsync<int>("genisis.PanelHospitalMaint_Add", new { code, hospital, userCode, today }, session.Transaction);

    /// <inheritdoc />
    public Task<int> RemoveAsync(IDbSession session, string code, string hospital) =>
        session.Connection.ProcScalarAsync<int>("genisis.PanelHospitalMaint_Remove", new { code, hospital }, session.Transaction);
}

/// <summary>Maintenance > Panel Hospital Maintenance (legacy <c>FrmMaintenancePanelHos</c>).</summary>
public class PanelHospitalMaintenanceService(IDbSessions sessions, IPanelHospitalsRepository panels, IPanelHospitalMaintenanceRepository repository, ILogger<PanelHospitalMaintenanceService> log)
{
    /// <summary>GetPanelHosCode: the hospital panel codes.</summary>
    public async Task<List<PanelCode>> GetCodesAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await panels.GetCodesAsync(session);
    }

    /// <summary>PanelListing: the hospitals of a panel (Search and Edit).</summary>
    public async Task<List<PanelHospital>> GetHospitalsAsync(string code)
    {
        await using var session = await sessions.OpenAsync();
        return await panels.GetHospitalsAsync(session, code.Trim());
    }

    /// <summary>HOSListing: the hospitals that can be added (Add).</summary>
    public async Task<List<AvailableHospital>> GetAvailableAsync(string code)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetAvailableAsync(session, code.Trim());
    }

    /// <summary>cmdUpdate_Click: adds the hospitals ticked and removes the ones unticked, all or nothing.</summary>
    public async Task<PanelHospitalMaintenanceResult> UpdateAsync(PanelHospitalMaintenanceRequest request, string userCode)
    {
        var code = request.Code!.Trim();
        await using var session = await sessions.BeginAsync();
        var added = 0;
        foreach (var hospital in (request.Add ?? []).Select(h => h.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var result = await repository.AddAsync(session, code, hospital, userCode, DateTime.Today);
            if (result == 2) throw new NotFoundException("Hospital Panel Code not found.");
            if (result == 1) throw new NotFoundException($"Hospital {hospital} not found.");
            added++;
        }

        var removed = 0;
        foreach (var hospital in (request.Remove ?? []).Select(h => h.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            removed += await repository.RemoveAsync(session, code, hospital);
        await session.CommitAsync();
        log.LogInformation("Panel {Panel}: {Added} hospital(s) added, {Removed} removed by {User}", code, added, removed, userCode);
        return new PanelHospitalMaintenanceResult(added, removed);
    }
}
