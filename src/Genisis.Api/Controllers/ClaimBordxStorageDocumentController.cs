using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Storage;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Storage > Document (legacy frmStorageDoc, "Storage / Cartons").</summary>
[Route("api/claim-bordx/storage-documents")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxStorageDocumentController(StorageDocumentService service) : ApiControllerBase
{
    /// <summary>ComboListing: the cartons, users and departments.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<StorageDocumentLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>StorageSearchRecord: the bordereaux, payment vouchers or receipts that match.</summary>
    [HttpGet]
    public async Task<ActionResult<SearchResult<StorageDocumentRow>>> Search([FromQuery] StorageDocumentFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>cmdSave_Click: stores the checked documents in a carton, or changes the one stored document.</summary>
    [HttpPost]
    public async Task<IActionResult> Save(StorageDocumentSaveRequest request)
    {
        await service.SaveAsync(request, UserCode);
        return Ok(new { message = request.Edit ? "Record Updated..." : "Record Saved..." });
    }
}
