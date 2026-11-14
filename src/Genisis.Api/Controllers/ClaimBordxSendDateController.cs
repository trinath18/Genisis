using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.SendDate;
using Genisis.Api.Common;
using Genisis.Api.Common.Errors;
using Genisis.Api.Pluto;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Bor Delivery > Bordx Send Date (legacy frmUploadApproval): <c>kind</c> is <c>send</c>, <c>approval</c> or <c>return</c>; each takes an Excel file.</summary>
[Route("api/claim-bordx/send-date/" + BordxSendDateKinds.RoutePattern)]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxSendDateController(BordxSendDateService service) : ApiControllerBase
{
    /// <summary>The "Verify" buttons: every row of the file and what is wrong with it. Nothing is written.</summary>
    [HttpPost("verify")]
    [RequestSizeLimit(BordxSendDateLayout.MaximumFileBytes)]
    public async Task<ActionResult<BordxSendDateResult>> Verify(string kind, IFormFile? file)
    {
        if (Sheet(file) is not { } sheet) return this.Invalid(file is null || file.Length == 0 ? BordxSendDateMessages.FileRequired : BordxSendDateMessages.NotAnExcelFile);
        return Ok(await service.VerifyAsync(kind, sheet));
    }

    /// <summary>The upload buttons (Send Date, App Date, Return Date): checks the file again and writes it in one transaction.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(BordxSendDateLayout.MaximumFileBytes)]
    public async Task<ActionResult<BordxSendDateResult>> Upload(string kind, IFormFile? file)
    {
        if (Sheet(file) is not { } sheet) return this.Invalid(file is null || file.Length == 0 ? BordxSendDateMessages.FileRequired : BordxSendDateMessages.NotAnExcelFile);
        return Ok(await service.UploadAsync(kind, sheet, UserCode));
    }

    private static List<object?[]>? Sheet(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        using var stream = file.OpenReadStream();
        return PlutoExcelReader.ReadSheet(stream);
    }
}
