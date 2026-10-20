using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Deletion;
using Genisis.Api.LeafletCard;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Leaflet &amp; Card (legacy frmMainReport). The desktop menu item has no access check of its own; printing a
/// member's documents is part of Membership Enquiry, so it uses that position.
/// </summary>
[ApiController]
[Route("api/membership/leaflet-card")]
[RequireAccess(AccessRight.MembershipEnquiry)]
public sealed class LeafletCardController(ILeafletCardService documents, IDeletionService lists) : ControllerBase
{
    private const string PdfContentType = "application/pdf";

    /// <summary>Health types and payors (tables HLT and PAY, as in the desktop's combo lists).</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<DeletionLookups>> GetLookups(CancellationToken cancellationToken) => Ok(await lists.GetLookupsAsync(cancellationToken));

    [HttpPost("search")]
    public async Task<ActionResult<LeafletCardSearchResult>> Search(LeafletCardCriteria criteria, CancellationToken cancellationToken)
    {
        if (LeafletCardValidator.Validate(criteria) is { } error) return this.Invalid(error);
        return Ok(await documents.SearchAsync(criteria, cancellationToken));
    }

    [HttpPost("leaflets")]
    public async Task<IActionResult> Leaflets(LeafletCardCriteria criteria, CancellationToken cancellationToken)
    {
        if (LeafletCardValidator.Validate(criteria) is { } error) return this.Invalid(error);
        var leaflets = await documents.GetLeafletsAsync(criteria, cancellationToken);
        if (leaflets.Count == 0) return this.Invalid(LeafletCardMessages.NotFound);
        return File(LeafletCardPdf.Leaflets(leaflets), PdfContentType, "Leaflets.pdf");
    }

    [HttpPost("cards")]
    public async Task<IActionResult> Cards(LeafletCardCriteria criteria, CancellationToken cancellationToken)
    {
        if (LeafletCardValidator.Validate(criteria) is { } error) return this.Invalid(error);
        var export = await documents.GetCardsAsync(criteria, cancellationToken);
        if (export.Cards.Count == 0) return this.Invalid(LeafletCardMessages.NotFound);
        return File(LeafletCardPdf.Cards(export.PayorName, export.Cards), PdfContentType, $"{export.FileName}.pdf");
    }

    /// <summary>The card listing the desktop exported to Excel for the card printer.</summary>
    [HttpPost("card-export")]
    public async Task<ActionResult<CardExport>> CardExport(LeafletCardCriteria criteria, CancellationToken cancellationToken)
    {
        if (LeafletCardValidator.Validate(criteria) is { } error) return this.Invalid(error);
        var export = await documents.GetCardsAsync(criteria, cancellationToken);
        return export.Cards.Count == 0 ? this.Invalid(LeafletCardMessages.NotFound) : Ok(export);
    }
}
