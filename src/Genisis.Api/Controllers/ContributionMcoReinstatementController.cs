using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.McoReinstatement;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Contribution Billing > MCO Reinstatement (legacy frmMCOInvoiceReinstatement, "MCO Reinstatement Invoice").</summary>
[Route("api/contribution-billing/mco-reinstatement")]
[RequireAccess(AccessRight.ContributionInvoicing)]
public class ContributionMcoReinstatementController(McoReinstatementService service) : ApiControllerBase
{
    /// <summary>cmdMCOSearch_Click: the members of a batch or an invoice.</summary>
    [HttpGet]
    public async Task<ActionResult<McoSearchResult>> Search([FromQuery] McoFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>cmdMCOPrint_Click / cmdMCOPrintPreview_Click: Print gives the members an invoice number when they have none; both return the document to write.</summary>
    [HttpPost("print")]
    public async Task<ActionResult<McoDocument>> Print(McoPrintRequest request) => Ok(await service.PrintAsync(request, UserCode));
}
