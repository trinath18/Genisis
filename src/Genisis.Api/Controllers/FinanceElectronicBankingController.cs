using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Finance.ElectronicBanking;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Finance &gt; Electronic Banking (legacy frmFileExportBankESP): ONLINE payments and the bank payment file.</summary>
[Route("api/finance/electronic-banking")]
[RequireAccess(AccessRight.FinanceElectronicBanking)]
public class FinanceElectronicBankingController(ElectronicBankingService service) : ApiControllerBase
{
    /// <summary>Payee types.</summary>
    [HttpGet("lookups")]
    public ActionResult<object> Lookups() => Ok(new { payeeTypes = ElectronicBankingService.PayeeTypes });

    /// <summary>The ONLINE payments that match (account numbers masked).</summary>
    [HttpGet("search")]
    public async Task<ActionResult<BankPaymentSearchResult>> Search([FromQuery] BankPaymentFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>The bank file of the ticked payments, in the configured layout.</summary>
    [HttpPost("file")]
    public async Task<IActionResult> Download(BankFileRequest request)
    {
        var file = await service.FileAsync(request, UserCode);
        return File(file.Content, "text/csv", file.FileName);
    }
}
