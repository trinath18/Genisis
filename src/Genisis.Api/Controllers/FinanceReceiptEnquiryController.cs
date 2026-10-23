using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Finance.ReceiptEnquiry;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Finance &gt; Receipt Enquiry (legacy frmReceiptEnquiry; rebuilt from the receipt tables, read only).</summary>
[Route("api/finance/receipt-enquiry")]
[RequireAccess(AccessRight.FinanceReceiptEnquiry)]
public class FinanceReceiptEnquiryController(ReceiptEnquiryService service) : ApiControllerBase
{
    /// <summary>Receipt types, payors and the posted list.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<ReceiptEnquiryLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>The receipts that match the criteria, newest first.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<ReceiptSearchResult>> Search([FromQuery] ReceiptEnquiryFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>One receipt with its claim allocation lines, fee invoice and SUN postings.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReceiptDetail>> Get(int id) => Ok(await service.GetAsync(id));
}
