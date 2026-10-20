using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.PanelHospitals;

/// <summary>A hospital panel code (HosPanelCode).</summary>
public record PanelCode
{
    /// <summary>The panel code; it already includes the panel name.</summary>
    public string Code { get; set; } = "";

    /// <summary>HosPanelDesc.</summary>
    public string? Description { get; set; }
}

/// <summary>One row of the Panel Hospitals list.</summary>
public record PanelHospital
{
    /// <summary>HOS.HOSCode.</summary>
    public string Code { get; set; } = "";

    /// <summary>Hospital name.</summary>
    public string? Name { get; set; }

    /// <summary>Telephone number.</summary>
    public string? Tel { get; set; }

    /// <summary>Fax number.</summary>
    public string? Fax { get; set; }

    /// <summary>Address line 1.</summary>
    public string? Add1 { get; set; }

    /// <summary>Address line 2.</summary>
    public string? Add2 { get; set; }

    /// <summary>Address line 3.</summary>
    public string? Add3 { get; set; }

    /// <summary>State.</summary>
    public string? State { get; set; }

    /// <summary>Town.</summary>
    public string? Town { get; set; }
}

/// <summary>The criteria of the Panel Hospitals search.</summary>
public class PanelHospitalsQuery
{
    /// <summary>The hospital panel code.</summary>
    public string? Code { get; set; }
}

/// <summary>frmPanelHospital's check: a panel code must be chosen.</summary>
public sealed class PanelHospitalsQueryValidator : RequestValidator<PanelHospitalsQuery>
{
    /// <summary>Declares the rules.</summary>
    public PanelHospitalsQueryValidator() =>
        RuleFor(q => q.Code).Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Please Select Hospital Panel Code!");
}

/// <summary>Reads panel codes and the hospitals of a panel.</summary>
public interface IPanelHospitalsRepository
{
    /// <summary>The panel codes.</summary>
    Task<List<PanelCode>> GetCodesAsync(IDbSession session);

    /// <summary>The hospitals of one panel code.</summary>
    Task<List<PanelHospital>> GetHospitalsAsync(IDbSession session, string code);
}

/// <inheritdoc />
public sealed class PanelHospitalsRepository : IPanelHospitalsRepository
{
    /// <inheritdoc />
    public Task<List<PanelCode>> GetCodesAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<PanelCode>("genisis.PanelHospitals_Codes", null, session.Transaction);

    /// <inheritdoc />
    public Task<List<PanelHospital>> GetHospitalsAsync(IDbSession session, string code) =>
        session.Connection.ProcQueryAsync<PanelHospital>("genisis.PanelHospitals_List", new { code }, session.Transaction);
}

/// <summary>Case > Panel Hospitals (legacy frmPanelHospital).</summary>
public class PanelHospitalsService(IDbSessions sessions, IPanelHospitalsRepository repository)
{
    /// <summary>GetPanelHosCode: the hospital panel codes.</summary>
    public async Task<List<PanelCode>> GetCodesAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetCodesAsync(session);
    }

    /// <summary>PanelListing: the hospitals of a panel code (empty when the panel has none).</summary>
    public async Task<List<PanelHospital>> GetHospitalsAsync(string code)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetHospitalsAsync(session, code.Trim());
    }
}
