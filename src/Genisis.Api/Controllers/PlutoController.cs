using System.Globalization;
using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Pluto;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Upload Membership Excel (Pluto) and Generate Membership ASCII (Pluto) (legacy frmMembershipExcelPluto /
/// frmMembershipASCIIPluto). The desktop menu items have no access check of their own, so they use the Upload Bordx position:
/// they stage and export bordx data, and the payor list comes from the Upload Bordx lookups.
/// </summary>
[ApiController]
[Route("api/membership/pluto")]
[RequireAccess(AccessRight.MembershipUploadBordx)]
public sealed class PlutoController(IPlutoService pluto) : ControllerBase
{
    private const string AsciiContentType = "text/plain";
    private const string TotalPrincipalsHeader = "X-Total-Principals";
    private const string TotalSupplementariesHeader = "X-Total-Supplementaries";

    [HttpGet("payors")]
    public async Task<ActionResult<List<PlutoPayor>>> GetPayors(string? healthCode, CancellationToken cancellationToken)
    {
        var code = healthCode?.Trim().ToUpperInvariant() ?? "";
        if (!PlutoHealthCodes.All.Contains(code)) return this.Invalid(PlutoMessages.EnterHealthCode);
        return Ok(await pluto.GetPayorsAsync(code, cancellationToken));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(PlutoLimits.MaximumFileBytes)]
    public async Task<ActionResult<PlutoUploadResult>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return this.Invalid(PlutoMessages.FileRequired);
        await using var stream = file.OpenReadStream();
        var sheet = PlutoExcelReader.ReadSheet(stream);
        if (sheet is null) return this.Invalid(PlutoMessages.NotAnExcelFile);
        var read = PlutoExcelReader.Convert(sheet);
        if (read.Errors.Count > 0) return this.Invalid(string.Join(Environment.NewLine, read.Errors));
        if (read.Rows.Count == 0) return this.Invalid(PlutoMessages.NoRows);
        return Ok(await pluto.UploadAsync(read.Rows, cancellationToken));
    }

    [HttpPost("ascii")]
    public async Task<IActionResult> ExportAscii(PlutoAsciiRequest request, CancellationToken cancellationToken)
    {
        if (PlutoService.Validate(request) is { } error) return this.Invalid(error);
        var file = await pluto.ExportAsciiAsync(request, cancellationToken);
        if (file is null) return this.Invalid(PlutoMessages.NoRecord);
        Response.Headers[TotalPrincipalsHeader] = file.Principals.ToString(CultureInfo.InvariantCulture);
        Response.Headers[TotalSupplementariesHeader] = file.Supplementaries.ToString(CultureInfo.InvariantCulture);
        Response.Headers.AccessControlExposeHeaders = $"{TotalPrincipalsHeader}, {TotalSupplementariesHeader}, Content-Disposition";
        return File(file.Content, AsciiContentType, file.FileName);
    }
}
