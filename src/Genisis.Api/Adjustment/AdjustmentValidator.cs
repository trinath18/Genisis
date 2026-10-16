using Genisis.Api.Membership;

namespace Genisis.Api.Adjustment;

/// <summary>Acceptable date ranges from the maintenance settings (genisis.Adjustment_ValidDates); a missing bound means no check.</summary>
public sealed record AdjustmentDateRanges(DateTime? DobStart, DateTime? DobEnd, DateTime? EffectiveStart, DateTime? EffectiveEnd,
    DateTime? ExpiryStart, DateTime? ExpiryEnd)
{
    public static readonly AdjustmentDateRanges Unbounded = new(null, null, null, null, null, null);

    private static bool Within(DateTime date, DateTime? from, DateTime? to) =>
        from is null || to is null || (date.Date >= from.Value.Date && date.Date <= to.Value.Date);

    public bool DateOfBirthAllowed(DateTime date) => Within(date, DobStart, DobEnd);
    public bool EffectiveDateAllowed(DateTime date) => Within(date, EffectiveStart, EffectiveEnd);
    public bool ExpiryDateAllowed(DateTime date) => Within(date, ExpiryStart, ExpiryEnd);
}

/// <summary>
/// Save checks of the Adjustment screen: the desktop CmdSave_Click checks in its order, plus the per-type checks from
/// cboEndorseStatus_LostFocus and the supplementary buttons. Pure: the caller supplies the loaded membership and date ranges.
/// </summary>
public static class AdjustmentValidator
{
    private static string Code(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>Request shape, checked before anything is loaded.</summary>
    public static string? ValidateRequest(AdjustmentRequest request)
    {
        var type = Code(request.EndorsementType);
        if (type == "") return "Please Choose An Endorsement Type";
        if (!EndorsementTypes.All.ContainsKey(type)) return "Unknown endorsement type.";
        return null;
    }

    /// <summary>Why the desktop refuses to edit this membership, or <c>null</c> when it can be edited.</summary>
    public static string? Blocked(AdjustmentMember member) => Code(member.Status) switch
    {
        MemberStatus.Deleted => "Membership has been deleted. No further changes are allowed!",
        MemberStatus.Cancelled => "Membership has been cancelled. No further changes are allowed!",
        MemberStatus.Suspended => "Membership has been Suspended. No further changes are allowed!",
        _ => null,
    };

    public static string? Validate(LoadedMembership current, AdjustmentRequest request, AdjustmentDateRanges dates)
    {
        if (ValidateRequest(request) is { } requestError) return requestError;
        var type = Code(request.EndorsementType);
        if (Blocked(current.Member) is { } blocked) return blocked;

        var stored = current.Member;
        var hasPlan = Code(stored.PlanCode) != "" || Code(stored.InsuredType) != "";
        var familyPlan = InsuredTypes.IsFamily(stored.InsuredType) || InsuredTypes.IsFamily(current.Clinic?.InsuredType);
        if (type is EndorsementTypes.AddSupplementary or EndorsementTypes.DeleteSupplementary && !familyPlan) return "This Plan does not have supplementary";
        if (type == EndorsementTypes.PlanChange && Code(stored.InsuredType) == "" && Code(stored.PlanCode) == "") return "No Plan to Change !";

        var member = EndorsementTypes.EditsPrincipal(type) ? request.Member : stored;
        if (ValidatePrincipal(member, request) is { } principalError) return principalError;
        if (!hasPlan && EndorsementTypes.EndsMembership(type) && Code(stored.Status) == MemberStatus.Active)
            return "This is Clinical Member, pls use Clinical Adjustment !";

        if (EndorsementTypes.EditsPrincipal(type) && ValidatePrincipalChange(type, stored, member, request, dates) is { } changeError) return changeError;
        return type switch
        {
            EndorsementTypes.AddSupplementary => ValidateAddedSupplementaries(request, dates),
            EndorsementTypes.DeleteSupplementary => ValidateChosen(current, request, CoveredActions.Remove),
            EndorsementTypes.CancelSupplementary => ValidateChosen(current, request, CoveredActions.Cancel),
            _ => null,
        };
    }

    private static string? ValidatePrincipal(AdjustmentMember member, AdjustmentRequest request)
    {
        var hasHisPlan = Code(member.PlanCode) != "";
        if (hasHisPlan && Blank(member.PolicyNo)) return "Please Enter Policy No!";
        if (request.EndtBordxDate is null) return "Please Enter Endt. Bordx Date!";
        if (request.EndtEffectiveDate is null) return "Please Enter Endt. Effective Date!";
        if (member.DateOfBirth is null) return "Please Enter Birth Date!";
        if (hasHisPlan && member.PayorEffectiveDate is null) return "Please Enter Payor Eff. Date!";
        if (hasHisPlan && member.PayorExpiryDate is null) return "Please Enter Payor Exp. Date!";
        if (member.BordxDate is null) return "Please Enter Bordx Date!";
        if (Blank(member.Address1)) return "Please Enter Address!";
        if (Blank(member.PostCode)) return "Please Enter Post Code!";
        if (Blank(member.State)) return "Please Enter State!";
        if (Blank(member.IcBcPp)) return "Please Enter IC Number!";
        if (Blank(member.Sex)) return "Please Enter Sex!";
        if (!Blank(member.NewPlanCode) && member.NewPolicyEffectiveDate is null) return "Please Enter New Policy Eff Date";
        if (Blank(member.NewPlanCode) && member.NewPolicyEffectiveDate is not null) return "Please Enter New Plan Code";
        return null;
    }

    private static string? ValidatePrincipalChange(string type, AdjustmentMember stored, AdjustmentMember member, AdjustmentRequest request,
        AdjustmentDateRanges dates)
    {
        if (member.PayorEffectiveDate != stored.PayorEffectiveDate && member.PayorEffectiveDate is { } effective && !dates.EffectiveDateAllowed(effective))
            return "Policy Effective Date is out of acceptable range! ";
        if (member.PayorExpiryDate != stored.PayorExpiryDate && member.PayorExpiryDate is { } expiry && !dates.ExpiryDateAllowed(expiry))
            return "Policy Expiry Date is out of acceptable range! ";
        if (type == EndorsementTypes.Amendment && Code(member.PlanCode) != Code(stored.PlanCode) && Code(stored.PlanCode) == "") return "No Plan to Change !";
        if (Code(member.InsuredType) != Code(stored.InsuredType) || Code(member.HealthCode) != Code(stored.HealthCode)
            || Code(member.PayorCode) != Code(stored.PayorCode))
            return "Health type, payor and insured type cannot be changed here.";
        foreach (var person in request.Covered.Where(p => !CoveredActions.IsChange(p.Action)))
        {
            if (Blank(person.Name)) return "Please Enter Covered Person Name";
            if (Blank(person.IcBcPp)) return "Please Enter Covered Person Identification";
            if (Blank(person.Relationship)) return "Please Enter Covered Person Relationship";
        }
        return null;
    }

    private static string? ValidateAddedSupplementaries(AdjustmentRequest request, AdjustmentDateRanges dates)
    {
        var added = request.Covered.Where(p => CoveredActions.Is(p.Action, CoveredActions.Add)).ToList();
        if (added.Count == 0) return "Please add at least one supplementary.";
        foreach (var person in added)
        {
            if (Blank(person.Name)) return "Please Enter Covered Person Name!";
            if (Blank(person.IcBcPp)) return "Please Enter Covered Person Identification!";
            if (Blank(person.Relationship)) return "Please Enter Covered Person Relationship!";
            if (person.DateOfBirth is not { } dateOfBirth) return "Please Enter Covered Person Birth Date!";
            if (Blank(person.Sex)) return "Please Enter Covered Person Sex!";
            if (!dates.DateOfBirthAllowed(dateOfBirth)) return $"{person.Name!.Trim()}: Birthdate is out of acceptable range! ";
        }
        return null;
    }

    private static string? ValidateChosen(LoadedMembership current, AdjustmentRequest request, string action)
    {
        const string ChooseRecord = "Please Choose a Record";
        var existing = current.Covered.Select(p => AdjustmentRules.CoverId(p.CoverId)).ToHashSet();
        var chosen = request.Covered.Where(p => CoveredActions.Is(p.Action, action)).ToList();
        if (chosen.Count == 0 || chosen.Any(p => !existing.Contains(AdjustmentRules.CoverId(p.CoverId)))) return ChooseRecord;
        return null;
    }
}
