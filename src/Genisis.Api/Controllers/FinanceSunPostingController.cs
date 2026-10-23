using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Finance.SunPosting;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Finance &gt; SUN Posting (legacy frmPostingInvoiceMBM, "Member Invoice Posting").</summary>
[Route("api/finance/sun-posting")]
[RequireAccess(AccessRight.FinanceSunPosting)]
public class FinanceSunPostingController(SunPostingService service) : ApiControllerBase
{
    private const string CsvContentType = "text/csv";

    /// <summary>ComboListing: health types, payors, accounting periods, the fixed lists and whether posting is open.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<SunPostingLookups>> Lookups() => Ok(await service.LookupsAsync(UserCode));

    /// <summary>&amp;Search (mode "all"), &amp;SearchCR ("credit"), &amp;SearchDN ("debit"): the journal lines of the matching invoices.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SunPostingSearchResult>> Search([FromQuery] SunPostingFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>&amp;Post: posts the ticked invoices under a new batch number.</summary>
    [HttpPost("post")]
    public async Task<ActionResult<SunPostResult>> Post(SunPostRequest request) => Ok(await service.PostAsync(request, UserCode));

    /// <summary>The journal file of a posted batch (CSV, the desktop's Print to File after posting).</summary>
    [HttpGet("journal")]
    public async Task<IActionResult> Journal([FromQuery] SunJournalQuery query)
    {
        var file = await service.JournalFileAsync(query);
        return File(file.Content, CsvContentType, file.FileName);
    }
}
