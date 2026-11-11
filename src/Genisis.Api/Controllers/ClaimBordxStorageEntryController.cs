using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Storage;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Storage > Claim files (legacy frmStorageEntry, "Storage Entry"): the writes. The cartons and the files are searched with the Storage Enquiry endpoints.</summary>
[Route("api/claim-bordx/storage-entry")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxStorageEntryController(StorageEntryService service) : ApiControllerBase
{
    /// <summary>SaveCarton / SaveRecByCarton: adds a carton, or changes the checked cartons.</summary>
    [HttpPost("cartons")]
    public async Task<IActionResult> SaveCartons(StorageCartonSaveRequest request)
    {
        await service.SaveCartonsAsync(request, UserCode);
        return Ok(new { message = request.Edit ? "Record Updated..." : "Record Saved..." });
    }

    /// <summary>cmdDelete_Click (Carton Details): deletes a carton that holds no files.</summary>
    [HttpDelete("cartons/{carton}")]
    public async Task<IActionResult> DeleteCarton(string carton)
    {
        await service.DeleteCartonAsync(carton, UserCode);
        return Ok(new { message = "Record has been deleted!" });
    }

    /// <summary>cmdSave_Click (Storage Details): puts the checked claims into a carton, or changes the one stored claim.</summary>
    [HttpPost("files")]
    public async Task<IActionResult> SaveFiles(StorageFileSaveRequest request)
    {
        await service.SaveFilesAsync(request, UserCode);
        return Ok(new { message = request.Edit ? "Record Updated..." : "Record Saved..." });
    }

    /// <summary>DeleteRecords (Remove): takes one claim file out of storage.</summary>
    [HttpDelete("files/{caseNo}/{version:int}")]
    public async Task<IActionResult> RemoveFile(string caseNo, int version)
    {
        await service.RemoveFileAsync(new StorageClaimRef(caseNo, version), UserCode);
        return Ok(new { message = "Record has been deleted!" });
    }
}
