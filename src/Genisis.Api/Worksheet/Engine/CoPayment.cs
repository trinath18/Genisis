using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>The co-payment terms of the patient's plan (<c>PLN</c>).</summary>
public record PlanTerms
{
    /// <summary>Co-insurance percentage (PLNCoINSPerc): the share of the approved total the member pays.</summary>
    public decimal CoInsurancePercent { get; set; }

    /// <summary>Whether the co-insurance applies (PLNCoInsSign = Y).</summary>
    public string? CoInsuranceSign { get; set; }

    /// <summary>Room &amp; Board co-payment percentage (PLNCoPayPerc).</summary>
    public decimal RoomCoPayPercent { get; set; }

    /// <summary>Room &amp; Board co-payment indicator (CoPaymentInd).</summary>
    public string? CoPaymentInd { get; set; }
}

/// <summary>Rules of the co-payment, checked against the co-payment the desktop saved (<c>CLMCoPayRB</c>).</summary>
public static class CoPaymentRules
{
    /// <summary>
    /// The co-insurance: the plan's percentage of the approved total, to the cent, when the plan has a percentage and its sign is Y. Over the saved worksheets it gives the saved co-payment
    /// to the cent in 93 per cent of those that have one; the Room &amp; Board co-payment of the other plans is a separate rule that is not ported yet.
    /// </summary>
    public static decimal CoInsurance(decimal totalApproved, PlanTerms? terms) =>
        terms is { CoInsurancePercent: > 0 } && string.Equals(terms.CoInsuranceSign?.Trim(), "Y", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(totalApproved * terms.CoInsurancePercent / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;
}

/// <summary>Reads the plan's co-payment terms for a saved worksheet version.</summary>
public interface IPlanTermsRepository
{
    /// <summary>The terms of the patient's plan; <c>null</c> when the version has no member or its plan has no row.</summary>
    Task<PlanTerms?> TermsAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class PlanTermsRepository : IPlanTermsRepository
{
    /// <inheritdoc />
    public Task<PlanTerms?> TermsAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<PlanTerms>("genisis.WorksheetEngine_PlanTerms", new { cas = caseNo, version }, session.Transaction);
}

/// <summary>Worksheet calculation engine, step 5: the co-payment.</summary>
public class CoPaymentService(IDbSessions sessions, IPlanTermsRepository repository)
{
    /// <summary>The co-insurance of a saved version for an approved total.</summary>
    public async Task<decimal> CoInsuranceAsync(string caseNo, int version, decimal totalApproved)
    {
        await using var session = await sessions.OpenAsync();
        return CoPaymentRules.CoInsurance(totalApproved, await repository.TermsAsync(session, caseNo.Trim(), version));
    }
}
