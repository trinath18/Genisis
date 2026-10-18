using System.Globalization;
using Genisis.Api.Auth;
using Genisis.Api.Bordx;
using Genisis.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Membership &gt; Upload Bordx (legacy FrmNewMBMEndt10, menu position 4).</summary>
[ApiController]
[Route("api/membership/bordx")]
[RequireAccess(AccessRight.MembershipUploadBordx)]
public sealed class BordxController(IBordxService bordx) : ControllerBase
{
    private const string AsciiContentType = "text/plain";
    private const string TotalPrincipalsHeader = "X-Total-Principals";
    private const string TotalSupplementariesHeader = "X-Total-Supplementaries";

    [HttpGet("lookups")]
    public async Task<ActionResult<BordxLookups>> GetLookups(CancellationToken cancellationToken) => Ok(await bordx.GetLookupsAsync(cancellationToken));

    [HttpPost("verify")]
    [RequestSizeLimit(BordxRules.MaximumFileBytes)]
    public async Task<ActionResult<VerifyResult>> Verify(IFormFile? file, [FromForm] BordxOptions options, CancellationToken cancellationToken)
    {
        var (lines, settings, error) = await ReadAsync(file, options, upload: false, cancellationToken);
        if (error is not null) return this.Invalid(error);
        return Ok(await bordx.VerifyAsync(lines!, settings, file!.FileName, cancellationToken));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(BordxRules.MaximumFileBytes)]
    public async Task<ActionResult<UploadResult>> Upload(IFormFile? file, [FromForm] BordxOptions options, CancellationToken cancellationToken)
    {
        var (lines, settings, error) = await ReadAsync(file, options, upload: true, cancellationToken);
        if (error is not null) return this.Invalid(error);
        return Ok(await bordx.UploadAsync(lines!, settings, User.Identity?.Name ?? "", cancellationToken));
    }

    [HttpPost("ascii")]
    public async Task<IActionResult> ExportAscii(AsciiRequest request, CancellationToken cancellationToken)
    {
        if (BordxValidator.Validate(request) is { } error) return this.Invalid(error);
        var file = await bordx.ExportAsciiAsync(request, cancellationToken);
        if (file is null) return this.Invalid("No Record Available to Export To ASCII File");
        Response.Headers[TotalPrincipalsHeader] = file.Principals.ToString(CultureInfo.InvariantCulture);
        Response.Headers[TotalSupplementariesHeader] = file.Supplementaries.ToString(CultureInfo.InvariantCulture);
        Response.Headers.AccessControlExposeHeaders = $"{TotalPrincipalsHeader}, {TotalSupplementariesHeader}, Content-Disposition";
        return File(file.Content, AsciiContentType, file.FileName);
    }

    private static async Task<(List<BordxLine>? Lines, BordxSettings Settings, string? Error)> ReadAsync(IFormFile? file, BordxOptions options, bool upload,
        CancellationToken cancellationToken)
    {
        var settings = BordxSettings.From(options);
        if (file is null || file.Length == 0) return (null, settings, BordxValidator.FileRequired);
        await using var stream = file.OpenReadStream();
        var lines = await BordxFile.ReadAsync(stream, cancellationToken);
        if (lines is null) return (null, settings, BordxValidator.NotATextFile);
        return (lines, settings, BordxValidator.Validate(settings, lines.FirstOrDefault(), upload));
    }
}
