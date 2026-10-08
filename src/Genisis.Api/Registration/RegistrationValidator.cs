namespace Genisis.Api.Registration;

/// <summary>Required-field checks of the legacy registration Save button, in the same order.</summary>
public static class RegistrationValidator
{
    public static readonly string[] FamilyInsuredTypes = ["F", "H"];

    public static string? Validate(RegistrationRequest r)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);

        if (Blank(r.HealthCode)) return "Please enter Health Code.";
        if (Blank(r.PayorCode)) return "Please enter Payor Code.";
        if (r.BordxDate is null) return "Please enter a valid Bordx Date.";
        if (r.BatchNo is null or <= 0) return "Please enter Batch No.";
        if (r.Renewal is not ("N" or "R" or "T")) return "Renewal must be N, R or T.";
        if (r.Renewal != "N" && Blank(r.PreviousMembershipNo) && Blank(r.PreviousPolicyNo))
            return "Please enter Previous Membership No or Previous Policy No.";
        if (Blank(r.Name)) return "Please enter Membership Name.";
        if (Blank(r.IcBcPp)) return "Please enter IC/BC/PP.";
        if (Blank(r.Address1)) return "Please enter Address.";
        if (Blank(r.PostCode)) return "Please enter Post Code.";
        if (Blank(r.State)) return "Please enter State.";
        if (r.DateOfBirth is null) return "Please enter a valid Birthdate.";
        if (r.DateOfBirth > DateTime.Today) return "Birthdate cannot be in the future.";
        if (Blank(r.PlanCode)) return "Please enter Plan Code.";
        if (Blank(r.PolicyNo)) return "Please enter Policy No.";
        if (r.PayorEffectiveDate is null) return "Please enter Payor Effective Date.";
        if (r.PayorExpiryDate is null) return "Please enter Payor Expiry Date.";
        if (r.PayorExpiryDate < r.PayorEffectiveDate) return "Payor Expiry Date cannot be before Payor Effective Date.";
        if (Blank(r.InsuredType)) return "Please enter Insured Code.";
        if (r.TakeOver is not (null or "" or "N" or "Y" or "C")) return "Take Over must be N, Y or C.";
        if (r.Sex is not (null or "" or "M" or "F")) return "Sex must be M or F.";
        if ((TooLong(r.Name, 60, "Membership Name") ?? TooLong(r.IcBcPp, 30, "IC/BC/PP") ?? TooLong(r.PolicyNo, 25, "Policy No")
            ?? TooLong(r.Address1, 50, "Address line 1") ?? TooLong(r.Address2, 50, "Address line 2") ?? TooLong(r.Address3, 50, "Address line 3")
            ?? TooLong(r.City, 50, "City") ?? TooLong(r.PostCode, 6, "Post Code") ?? TooLong(r.State, 50, "State")) is { } tooLong)
            return tooLong;

        if (r.CoveredPersons.Count > 0 && !FamilyInsuredTypes.Contains(r.InsuredType!.Trim()))
            return "Covered persons can only be added for family insured types (F, H).";
        for (var i = 0; i < r.CoveredPersons.Count; i++)
        {
            var c = r.CoveredPersons[i];
            var row = $"Covered person {i + 1}: ";
            if (Blank(c.Name)) return row + "please enter Name.";
            if (Blank(c.Relationship)) return row + "please enter Relationship.";
            if (c.DateOfBirth is null) return row + "please enter a valid Birthdate.";
            if (c.Sex is not (null or "" or "M" or "F")) return row + "Sex must be M or F.";
            if ((TooLong(c.Name, 60, "Name") ?? TooLong(c.IcBcPp, 30, "IC/BC/PP")) is { } covTooLong) return row + covTooLong;
            if (c.EffectiveDate is { } eff && (eff < r.PayorEffectiveDate || eff > r.PayorExpiryDate))
                return row + "Effective Date must be within the payor period.";
            if (c.ExpiryDate is { } exp && (exp < (c.EffectiveDate ?? r.PayorEffectiveDate) || exp > r.PayorExpiryDate))
                return row + "Expiry Date must be within the payor period.";
        }
        return null;
    }

    private static string? TooLong(string? value, int max, string label) =>
        value is not null && value.Trim().Length > max ? $"{label} cannot be longer than {max} characters." : null;
}
