using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Membership &gt; Tracking (legacy FrmNewMBMTracking, menu position 14).</summary>
[ApiController]
[Route("api/membership/tracking")]
[RequireAccess(AccessRight.MembershipTracking)]
public sealed class TrackingController(ITrackingService tracking) : ControllerBase
{
    private const string CsvContentType = "text/csv";

    /// <summary>Report types / selections and the filter fields, with their drop-down options.</summary>
    [HttpGet("meta")]
    public async Task<ActionResult<TrackingMeta>> GetMeta(CancellationToken cancellationToken) =>
        Ok(await tracking.GetMetaAsync(cancellationToken));

    /// <summary>Runs a report; returns at most <see cref="TrackingLimits.ScreenRows"/> rows with totals over every row.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<TrackingResult>> Run(TrackingRequest request, CancellationToken cancellationToken)
    {
        if (TrackingValidator.Validate(request, out var query) is { } error) return this.Invalid(error);
        return Ok(await tracking.RunAsync(query!, TrackingLimits.ScreenRows, cancellationToken));
    }

    /// <summary>Every row of a report as CSV (the desktop's Print to File).</summary>
    [HttpPost("export")]
    public async Task<IActionResult> Export(TrackingRequest request, CancellationToken cancellationToken)
    {
        if (TrackingValidator.Validate(request, out var query) is { } error) return this.Invalid(error);
        var export = await tracking.ExportAsync(query!, cancellationToken);
        return File(export.Content, CsvContentType, export.FileName);
    }
}
