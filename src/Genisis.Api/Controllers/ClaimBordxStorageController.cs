using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Storage;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Storage > Enquiry (legacy frmStorageEnq, "Storage Enquiry"): read only.</summary>
[Route("api/claim-bordx/storage")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxStorageController(StorageEnquiryService service) : ApiControllerBase
{
    /// <summary>ComboListing: the carton numbers and users for the drop-downs.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<StorageLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>CartonSearchRecord: the cartons that match.</summary>
    [HttpGet("cartons")]
    public async Task<ActionResult<SearchResult<StorageCartonRow>>> Cartons([FromQuery] StorageCartonFilter filter) => Ok(await service.CartonsAsync(filter));

    /// <summary>StorageSearchRecord: the claim files that match.</summary>
    [HttpGet("files")]
    public async Task<ActionResult<SearchResult<StorageFileRow>>> Files([FromQuery] StorageFileFilter filter) => Ok(await service.FilesAsync(filter));

    /// <summary>CmdListing_Click: the check sheet of a carton.</summary>
    [HttpGet("carton-files")]
    public async Task<ActionResult<List<StorageCartonFile>>> CartonFiles([FromQuery] string? carton) => Ok(await service.CartonFilesAsync(carton));
}
