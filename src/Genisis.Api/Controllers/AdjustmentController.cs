using Genisis.Api.Adjustment;
using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Membership &gt; Adjustment (legacy frmNewMBMAdjustment, menu position 2).</summary>
[ApiController]
[Route("api/membership/adjustment")]
[RequireAccess(AccessRight.MembershipAdjustment)]
public sealed class AdjustmentController(IAdjustmentService adjustments, IAdjustmentQueryService queries) : ControllerBase
{
    public sealed record MembershipView(AdjustmentMember Member, List<AdjustmentCovered> Covered, ClinicalPlan? Clinic, PlanLimit? Limit, string? Blocked);

    private ObjectResult MembershipNotFound() => new(Problems.Create(HttpContext, StatusCodes.Status404NotFound, "Not found", AdjustmentMessages.NotFound))
    {
        StatusCode = StatusCodes.Status404NotFound,
    };

    [HttpGet("lookups")]
    public async Task<ActionResult<AdjustmentLookups>> GetLookups(CancellationToken cancellationToken) => Ok(await queries.GetLookupsAsync(cancellationToken));

    [HttpGet("plans")]
    public async Task<ActionResult<List<AdjustmentOption>>> GetPlans(string healthCode, string? payorCode, CancellationToken cancellationToken) =>
        Ok(await queries.GetPlansAsync(healthCode, payorCode, cancellationToken));

    /// <summary>Find a membership by number, name, IC or policy (desktop frmSearchMBM, opened from the Search button).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, string by = "number", CancellationToken cancellationToken = default)
    {
        var text = (q ?? "").Trim();
        var field = by.Trim().ToLowerInvariant();
        if (text.Length < AdjustmentLimits.SearchMinimumLength) return this.Invalid($"Enter at least {AdjustmentLimits.SearchMinimumLength} characters.");
        if (!AdjustmentSearchFields.All.Contains(field)) return this.Invalid("by must be one of: number, name, ic, policy.");
        return Ok(await queries.SearchAsync(text, field, cancellationToken));
    }

    [HttpGet("navigate")]
    public async Task<IActionResult> Navigate(string? from, string direction = "next", CancellationToken cancellationToken = default)
    {
        var previous = AdjustmentQueryService.IsPrevious(direction);
        var number = await queries.NavigateAsync(from ?? "", previous, cancellationToken);
        if (number is null)
            return new ObjectResult(Problems.Create(HttpContext, StatusCodes.Status404NotFound, "Not found", (previous ? "First" : "Last") + " Record Reached!"))
            {
                StatusCode = StatusCodes.Status404NotFound,
            };
        return Ok(new { membershipNo = number });
    }

    [HttpGet("{number}")]
    public async Task<ActionResult<MembershipView>> Get(string number, CancellationToken cancellationToken)
    {
        var membership = await adjustments.LoadAsync(number, cancellationToken);
        if (membership is null) return MembershipNotFound();
        return Ok(new MembershipView(membership.Member, membership.Covered, membership.Clinic, membership.Limit, AdjustmentValidator.Blocked(membership.Member)));
    }

    [HttpGet("{number}/fees")]
    public async Task<ActionResult<List<FeeRow>>> GetFees(string number, CancellationToken cancellationToken)
    {
        var membership = await adjustments.LoadAsync(number, cancellationToken);
        if (membership is null) return MembershipNotFound();
        return Ok(await adjustments.GetFeesAsync(membership, cancellationToken));
    }

    [HttpGet("{number}/history")]
    public async Task<IActionResult> GetHistory(string number, CancellationToken cancellationToken) =>
        Ok(await queries.GetAdjustmentHistoryAsync(number, cancellationToken));

    [HttpGet("{number}/member-history")]
    public async Task<IActionResult> GetMemberHistory(string number, CancellationToken cancellationToken)
    {
        var membership = await adjustments.LoadAsync(number, cancellationToken);
        if (membership is null) return MembershipNotFound();
        return Ok(await queries.GetMemberHistoryAsync(membership.Member, cancellationToken));
    }

    [HttpGet("last-batch")]
    public async Task<IActionResult> GetLastBatch(DateTime bordxDate, string? payorCode, CancellationToken cancellationToken) =>
        Ok(new { lastBatch = await queries.GetLastBatchAsync(bordxDate, payorCode, cancellationToken) });

    /// <summary>Another membership using the policy number (the desktop asks whether to accept it).</summary>
    [HttpGet("policy-check")]
    public async Task<IActionResult> CheckPolicy(string policyNo, string number, CancellationToken cancellationToken) =>
        Ok(new { duplicateOf = await queries.FindPolicyOwnerAsync(policyNo, number, cancellationToken) });

    [HttpPost("{number}/recalculate")]
    public async Task<ActionResult<Recalculation>> Recalculate(string number, RecalculateRequest request, CancellationToken cancellationToken)
    {
        var result = await adjustments.RecalculateAsync(number, request, cancellationToken);
        return result is null ? MembershipNotFound() : Ok(result);
    }

    [HttpPost("supplementary-expiry")]
    public async Task<ActionResult<SupplementaryExpiry>> GetSupplementaryExpiry(SupplementaryExpiryRequest request, CancellationToken cancellationToken) =>
        Ok(await adjustments.GetSupplementaryExpiryAsync(request, cancellationToken));

    [HttpPost("{number}")]
    public async Task<ActionResult<AdjustmentResult>> Save(string number, AdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (AdjustmentValidator.ValidateRequest(request) is { } error) return this.Invalid(error);
        var result = await adjustments.SaveAsync(number, request, User.Identity?.Name ?? "", cancellationToken);
        return result is null ? MembershipNotFound() : Ok(result);
    }
}
