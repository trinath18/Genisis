namespace Genisis.Api.Plans;

/// <summary>Required-field checks of the legacy Plan tab Save button, in the same order.</summary>
public static class PlanValidator
{
    public const int MaxPlans = 6;

    /// <summary>Client plan choices the desktop offers per payor (cboPLNPayor_Click).</summary>
    public static IReadOnlyList<string> ClientPlans(string? payorCode) => (payorCode ?? "").Trim().ToUpperInvariant() switch
    {
        var p when p.StartsWith("AX") => Enumerable.Range(1, 12).Select(i => i.ToString("000")).ToList(),
        var p when p.StartsWith("WM") || p.StartsWith("WK") || p.StartsWith("ET") || p.StartsWith("EC") =>
            [.. Enumerable.Range(1, 10).Select(i => $"PLAN{i}"), .. "ABCDEFGH".Select(c => $"PLAN{c}")],
        _ => [],
    };

    /// <param name="update">Editing one existing row: No of Plan Code is only used to generate new codes, so it is not checked.</param>
    public static string? Validate(PlanCreateRequest r, bool update = false)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        static bool YesNo(string? s) => s?.Trim().ToUpperInvariant() is "Y" or "N";
        var health = r.HealthCode?.Trim().ToUpperInvariant();
        var payor = r.PayorCode?.Trim().ToUpperInvariant() ?? "";

        if (Blank(health)) return "Please Enter Health Type";
        if (health == "N" && Blank(payor)) return "Please Enter Payor Code";
        if (!update && health == "N" && r.NumberOfPlans is null) return "Please Enter No of Plan Code";
        if (!YesNo(r.AnnualLimitInd)) return "Please Enter Annual Limit Ind.";
        if (!YesNo(r.LifetimeStatus)) return "Please Enter Life Limit Ind.";
        if (Blank(r.GroupCompany)) return "Please Enter Group Company";
        if (!YesNo(r.PremiumInd)) return "Please Enter Premium Ind.";
        if (!YesNo(r.McoInd)) return "Please Enter MCO Ind.";
        if (!YesNo(r.TopUpStatus)) return "Please Enter Topup Status";
        if (!YesNo(r.SpecialGracePeriod)) return "Please Enter Special GPeriod Status";
        if (r.SpecialGracePeriod!.Trim().ToUpperInvariant() == "Y" && r.SpecialGracePeriodDays is null)
            return "Please Enter Special GPeriod Days";
        if (!YesNo(r.CoPayment)) return "Please Enter Co-Payment Status";
        if (r.EffectiveDate is null) return "Please Enter Effective Date";
        if (health == "S" && Blank(r.Plans.FirstOrDefault()?.Code)) return "Please Enter Plan Code ";
        if (Blank(r.ProductCategory)) return "Please Select Product Category";
        if (!YesNo(r.Meal)) return "Please Enter Meals Status";
        if (!YesNo(r.Nursing)) return "Please Enter Nursing Charges Status";
        if (!YesNo(r.Tax)) return "Please Enter Tax Status";
        if (!YesNo(r.Mri)) return "Please Enter MRI Status";
        if (!YesNo(r.ManagementFee)) return "Please Enter Mgmt. Fee Status";
        if (!YesNo(r.DisIndicator)) return "Please Select a Dis. Indicator";
        if (r.StartAge is null) return "Please Enter Policy Start Age";
        if (r.EndAge is null) return "Please Enter Policy End Age";
        if (Blank(r.PolicyWording)) return "Please Enter Policy Category";
        if (payor.StartsWith("AX") && Blank(r.ClientPlan)) return "Please Select Client Plan";
        if (payor.StartsWith("AX") && Blank(r.ClientPolicyNo)) return "Please Enter Client Policy Number";

        if (health is not ("N" or "S")) return "Health Type must be N or S.";
        if (!update && health == "N" && r.NumberOfPlans is < 1 or > MaxPlans) return $"No of Plan Code must be 1 to {MaxPlans}.";
        if (r.Plans.Count > MaxPlans) return $"At most {MaxPlans} plans can be added at once.";
        if (r.SmPlan is not (null or "") && !YesNo(r.SmPlan)) return "SM Plan must be Y or N.";
        if (r.Sof is not (null or "") && !YesNo(r.Sof)) return "SOF must be Y or N.";
        if (r.SpecialGracePeriodDays is < 0 or > 999) return "Special GPeriod Days must be 0 to 999.";
        if (r.StartAge is < 0 or > 99) return "Policy Start Age must be 0 to 99.";
        if (r.EndAge is < 0 or > 999) return "Policy End Age must be 0 to 999.";
        if (r.CoPayPercent is < 0 or > 100) return "Co-Pay % must be 0 to 100.";
        if (!Blank(r.ClientPlan) && !ClientPlans(payor).Contains(r.ClientPlan!.Trim().ToUpperInvariant()))
            return "Client Plan is not valid for this payor.";
        foreach (var p in r.Plans)
            if ((TooLong(p.Code, 4, "Plan Code") ?? TooLong(p.Description, 200, "Plan Description")) is { } lineTooLong)
                return lineTooLong;
        return TooLong(r.GroupCompany, 200, "Group Company") ?? TooLong(r.PayorCode, 4, "Payor Code")
            ?? TooLong(r.ProductCategory, 2, "Product Category") ?? TooLong(r.PolicyWording, 500, "Policy Category")
            ?? TooLong(r.ClientPolicyNo, 500, "Client Policy Number");
    }

    private static string? TooLong(string? value, int max, string label) =>
        value is not null && value.Trim().Length > max ? $"{label} cannot be longer than {max} characters." : null;
}
