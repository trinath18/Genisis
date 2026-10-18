namespace Genisis.Api.Mma;

/// <summary>Required-field and length checks of the Entity screen (frmMMA Save buttons).</summary>
public static class MmaValidator
{
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TooLong(string? value, int maximum, string label) =>
        Text(value)?.Length > maximum ? $"{label} cannot be longer than {maximum} characters." : null;

    public static string? Validate(MmaProcedureRequest request)
    {
        if (Text(request.BodyPart) is null) return "Please Enter Main Body Part";
        if (Text(request.Narrative) is null) return "Please Enter OPCS Narrative";
        if (Text(request.PageNo) is not { } page) return "Please Enter Page No";
        if (!page.All(char.IsAsciiDigit)) return "Page No must be a number.";
        if (Text(request.Part)?.ToUpperInvariant() is { } part && !MmaLimits.Parts.Contains(part)) return "Part must be A, B or C.";
        return TooLong(request.BodyPart, MmaLimits.BodyPartLength, "Main Body Part")
            ?? TooLong(request.Code, MmaLimits.CodeLength, "OPCS Code")
            ?? TooLong(request.Narrative, MmaLimits.NarrativeLength, "OPCS Narrative")
            ?? TooLong(request.SurgeonCategory, MmaLimits.ProcedureCategoryLength, "Surgeon's Category")
            ?? TooLong(request.AnaesthetistCategory, MmaLimits.ProcedureCategoryLength, "Anaesthetist's Category")
            ?? TooLong(request.PageNo, MmaLimits.PageNoLength, "Page No")
            ?? TooLong(request.PartACost, MmaLimits.PartACostLength, "Part A Cost");
    }

    /// <summary>The desktop saved even an empty row; the web needs at least one category.</summary>
    public static string? Validate(MmaFeeRequest request)
    {
        if (Text(request.SurgeonCategory) is null && Text(request.AnaesthetistCategory) is null) return "Please Enter Surgeon's or Anaesthetist's Category";
        if (request.SurgeonFee is < 0 or > MmaLimits.MaximumFee || request.AnaesthetistFee is < 0 or > MmaLimits.MaximumFee)
            return $"Fees must be from 0 to {MmaLimits.MaximumFee:N0}.";
        return TooLong(request.SurgeonCategory, MmaLimits.FeeSurgeonCategoryLength, "Surgeon's Category")
            ?? TooLong(request.AnaesthetistCategory, MmaLimits.FeeAnaesthetistCategoryLength, "Anaesthetist's Category");
    }

    /// <summary>Accounts direction text: next, prev or find.</summary>
    public static ScheduleDirection ParseDirection(string? direction) => direction?.Trim().ToLowerInvariant() switch
    {
        "prev" => ScheduleDirection.Previous,
        "find" => ScheduleDirection.Find,
        _ => ScheduleDirection.Next,
    };
}
