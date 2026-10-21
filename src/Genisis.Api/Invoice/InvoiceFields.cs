using System.Text.Json.Serialization;

namespace Genisis.Api.Invoice;

/// <summary>What kind of entry a field of the invoice form is (sent as its name, so the page can tell a text from an amount).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InvoiceFieldKind
{
    /// <summary>An amount of money.</summary>
    Money,

    /// <summary>A count of days or months (may be fractional, as 7.5 days).</summary>
    Number,

    /// <summary>Free text.</summary>
    Text,

    /// <summary>A Y or N tick (stored as one character).</summary>
    Flag,
}

/// <summary>One field of the invoice form (a column of <c>ClaimInvoiceActual</c>, <c>ClaimInvoiceActualOthers</c> or <c>ClaimInvoiceNotCover</c>).</summary>
/// <param name="Key">The column name; the key of the field in a request and a response.</param>
/// <param name="Label">What the form calls it.</param>
/// <param name="Section">The group of the form it is in.</param>
/// <param name="Kind">Money, days, text or a tick.</param>
/// <param name="SumOf">For an amount the form works out: the columns it adds up; it is typed only when none of them has a value.</param>
/// <param name="Computed">Whether the server works the amount out whatever is sent (totals with tax, accident and tax lines).</param>
/// <param name="Frame">The surgical (<c>S</c>) or non-surgical (<c>N</c>) frame; the form enables the one that matches the case's modality.</param>
/// <param name="Tax">The tax era the field belongs to: <c>new</c> (plans 2 to 5) or <c>old</c>; the form enables one set.</param>
public record InvoiceField(string Key, string Label, string Section, InvoiceFieldKind Kind = InvoiceFieldKind.Money, string[]? SumOf = null, bool Computed = false, string? Frame = null, string? Tax = null);

/// <summary>
/// The columns and fields of the Invoice Processing form. The columns are the parameters of the legacy procedures <c>Insert_ClaimInvActual2010</c>, <c>Insert_ClaimInvoiceActOthers_New_2019</c> (the 2019 version, which the desktop whose data this is writes; the source snapshot calls the 41-parameter one without stents, balloons and implants) and
/// <c>Insert_ClaimInvoiceNotCov08</c> in the order the desktop calls them; <see cref="Form"/> is what the web form is drawn from and what a request is checked against.
/// </summary>
public static class InvoiceFields
{
    private const string Room = "Room & Board", Icu = "ICU", Hospital = "Hospital charges", Pre = "Pre-hospital", Surgery = "Surgical and anaesthetist", Physician = "Non-surgical (physician)",
        Post = "Post-hospital and accident", Other = "Other charges", Member = "Paid by the member", Supplies = "Hospital supplies detail", NotCovered = "Not covered";

    /// <summary>The columns of <c>ClaimInvoiceActual</c> (and of <c>ClaimInvoiceActualGST</c>) the form writes, without the invoice key.</summary>
    public static readonly string[] ActualColumns =
    [
        "CLMCHospSum", "CLMBoardAmt", "CLMGrossRB", "CLMTotalBoardAmt", "CLMICUAmt", "CLMTotalICUAmt", "CLMServices", "CLMTheatre", "CLMPreHosp", "CLMPreHSAmt", "CLMMPreHosAmt", "CLMSurgicalAmt", "CLMAna",
        "CLMNSPreHos", "CLMPhysician", "CLMPost", "CLMEmergency", "CLMPostTreat", "CLMAccPostAmt", "CLMAmbulance", "CLMOutpatient", "CLMMonthly", "CLMMedical", "CLMPhone", "CLMGuardianAmt",
        "CLMTotalGAllowAmt", "CLMAdmin", "CLMRBTax", "CLMICUTax", "CLMLodgerTax", "CLMTax", "CLMOthers1", "CLMMHospSum", "CLMOthersNotCOPay", "CLMOthersDescNoCOPay", "CLMOthers2", "CLMOther1Desc",
        "CLMOthers2Desc", "CLMCashAmt", "CLMOrgan", "CLMMRI", "CLMNewRBTax", "CLMNewICUTax", "CLMNewLodgerTax", "CLMHomeNCAmt", "CLMCancerAmt",
    ];

    /// <summary>The columns of <c>ClaimInvoiceActualOthers</c> the form writes, without the invoice key.</summary>
    public static readonly string[] OthersColumns =
    [
        "CLMBoardDay", "CLMRB", "CLMICUDay", "CLMICU", "CLMPreXRay", "CLMPreLab", "CLMPreoTHERS", "CLMMXRay", "CLMMLab", "CLMMPreOthers", "CLMSurgicalFee", "CLMSurgPhy", "CLMSurgPost",
        "CLMINHospHyVisitDay", "CLMKidneyMnth", "CLMLodger", "CLMLodgerday", "CLMCashDay", "CLMCash", "CLMSurgicalWard", "CLMAnaeFees", "CLMAnaeConsultation", "CLMAnaeWard", "CLMInHosFees",
        "CLMInHosWard", "CLMProceduresFee", "CLMMedRptMBM", "CLMTelChargesMBM", "CLMGuaAllowMBM", "CLMAdminRegMBM", "CLMOthersMBM", "CLMPharmacyHSS", "CLMMedSuppliesHSS", "CLMNursingCareHSS",
        "CLMNursingSvrHSS", "CLMLabHSS", "CLMRadiologiHSS", "CLMOTSuppHSS", "CLMEquipHSS", "CLMMiscHSS", "CLMStentsHSS", "CLMBalloonHSS", "CLMImplantsHSS",
    ];

    /// <summary>The columns of <c>ClaimInvoiceNotCover</c> the form writes, without the invoice key.</summary>
    public static readonly string[] NotCoverColumns = ["PreH60", "PreGP", "PreMedic", "PreMR", "PreSpec", "PostHosp", "PostNotSame", "Others", "ReimOtherRemarks", "INVHospName"];

    /// <summary>The columns that hold text.</summary>
    public static readonly HashSet<string> TextColumns = ["CLMOthersDescNoCOPay", "CLMOther1Desc", "CLMOthers2Desc", "ReimOtherRemarks", "INVHospName"];

    /// <summary>The columns that hold a Y or N tick.</summary>
    public static readonly HashSet<string> FlagColumns = ["CLMMedRptMBM", "CLMTelChargesMBM", "CLMGuaAllowMBM", "CLMAdminRegMBM", "CLMOthersMBM"];

    /// <summary>The longest text each text column holds.</summary>
    public static readonly IReadOnlyDictionary<string, int> TextLength = new Dictionary<string, int>
    {
        ["CLMOthersDescNoCOPay"] = 50, ["CLMOther1Desc"] = 1000, ["CLMOthers2Desc"] = 100, ["ReimOtherRemarks"] = 4000, ["INVHospName"] = 100,
    };

    /// <summary>The fields of the form, in the order and groups it shows them.</summary>
    public static readonly IReadOnlyList<InvoiceField> Form =
    [
        new("CLMBoardDay", "Days", Room, InvoiceFieldKind.Number), new("CLMRB", "Rate per day", Room), new("CLMBoardAmt", "Room & Board", Room),
        new("CLMNewRBTax", "Tax", Room, Tax: "new"), new("CLMRBTax", "Tax", Room, Tax: "old"), new("CLMTotalBoardAmt", "Room & Board with tax", Room, Computed: true),
        new("CLMICUDay", "Days", Icu, InvoiceFieldKind.Number), new("CLMICU", "Rate per day", Icu), new("CLMICUAmt", "ICU", Icu),
        new("CLMNewICUTax", "Tax", Icu, Tax: "new"), new("CLMICUTax", "Tax", Icu, Tax: "old"), new("CLMTotalICUAmt", "ICU with tax", Icu, Computed: true),
        new("CLMServices", "Hospital supplies and services", Hospital), new("CLMTheatre", "Operating theatre", Hospital), new("CLMMRI", "MRI", Hospital), new("CLMPreHosp", "Pre-hospital", Hospital),
        new("CLMPreXRay", "Diagnosis: X-ray", Pre), new("CLMPreLab", "Diagnosis: laboratory", Pre), new("CLMPreoTHERS", "Diagnosis: others", Pre),
        new("CLMPreHSAmt", "Pre-hospital diagnosis", Pre, SumOf: ["CLMPreXRay", "CLMPreLab", "CLMPreoTHERS"]),
        new("CLMMXRay", "Medical: X-ray", Pre), new("CLMMLab", "Medical: laboratory", Pre), new("CLMMPreOthers", "Medical: others", Pre),
        new("CLMMPreHosAmt", "Medical pre-hospital", Pre, SumOf: ["CLMMXRay", "CLMMLab", "CLMMPreOthers"]), new("CLMNSPreHos", "Non-surgical pre-hospital", Pre),
        new("CLMSurgicalFee", "Surgical fee", Surgery, Frame: "S"), new("CLMSurgPhy", "Physician", Surgery, Frame: "S"), new("CLMSurgPost", "Post-treatment", Surgery, Frame: "S"),
        new("CLMSurgicalWard", "Ward", Surgery, Frame: "S"),
        new("CLMSurgicalAmt", "Surgical", Surgery, SumOf: ["CLMSurgicalFee", "CLMSurgPhy", "CLMSurgPost", "CLMSurgicalWard"], Frame: "S"),
        new("CLMAnaeFees", "Anaesthetist fees", Surgery, Frame: "S"), new("CLMAnaeConsultation", "Anaesthetist consultation", Surgery, Frame: "S"), new("CLMAnaeWard", "Anaesthetist ward", Surgery, Frame: "S"),
        new("CLMAna", "Anaesthetist", Surgery, SumOf: ["CLMAnaeFees", "CLMAnaeConsultation", "CLMAnaeWard"], Frame: "S"),
        new("CLMInHosFees", "Physician fees", Physician, Frame: "N"), new("CLMInHosWard", "Physician ward", Physician, Frame: "N"), new("CLMProceduresFee", "Procedures", Physician, Frame: "N"),
        new("CLMPhysician", "Physician", Physician, SumOf: ["CLMInHosFees", "CLMInHosWard", "CLMProceduresFee"], Frame: "N"),
        new("CLMINHospHyVisitDay", "Visit days", Physician, InvoiceFieldKind.Number, Frame: "N"),
        new("CLMPost", "Post-hospital", Post), new("CLMEmergency", "Accident: emergency treatment", Post), new("CLMPostTreat", "Accident: post-treatment", Post),
        new("CLMAccPostAmt", "Accident", Post, Computed: true), new("CLMAmbulance", "Ambulance", Post),
        new("CLMOutpatient", "Outpatient", Other), new("CLMMonthly", "Monthly (kidney)", Other), new("CLMKidneyMnth", "Months", Other, InvoiceFieldKind.Number), new("CLMOrgan", "Organ", Other),
        new("CLMCancerAmt", "Cancer treatment", Other), new("CLMHomeNCAmt", "Home nursing", Other), new("CLMMedical", "Medical report", Other), new("CLMPhone", "Telephone", Other),
        new("CLMGuardianAmt", "Guardian allowance", Other), new("CLMLodgerday", "Lodger days", Other, InvoiceFieldKind.Number), new("CLMLodger", "Lodger rate per day", Other),
        new("CLMNewLodgerTax", "Lodger tax", Other, Tax: "new"), new("CLMLodgerTax", "Lodger tax", Other, Tax: "old"),
        new("CLMTotalGAllowAmt", "Guardian allowance with tax", Other, Computed: true), new("CLMAdmin", "Admin", Other), new("CLMTax", "Tax", Other, Computed: true, Tax: "old"),
        new("CLMOthers1", "Others 1", Other), new("CLMOther1Desc", "Others 1: description", Other, InvoiceFieldKind.Text),
        new("CLMOthers2", "Others 2", Other), new("CLMOthers2Desc", "Others 2: description", Other, InvoiceFieldKind.Text),
        new("CLMCashDay", "Cash allowance days", Other, InvoiceFieldKind.Number), new("CLMCash", "Cash allowance rate per day", Other), new("CLMCashAmt", "Cash allowance", Other),
        new("CLMOthersNotCOPay", "Others, no co-payment", Other), new("CLMOthersDescNoCOPay", "Others, no co-payment: description", Other, InvoiceFieldKind.Text),
        new("CLMMedRptMBM", "Medical report", Member, InvoiceFieldKind.Flag), new("CLMTelChargesMBM", "Telephone", Member, InvoiceFieldKind.Flag),
        new("CLMGuaAllowMBM", "Guardian allowance", Member, InvoiceFieldKind.Flag), new("CLMAdminRegMBM", "Admin", Member, InvoiceFieldKind.Flag), new("CLMOthersMBM", "Others", Member, InvoiceFieldKind.Flag),
        new("CLMPharmacyHSS", "Pharmacy", Supplies), new("CLMMedSuppliesHSS", "Medical supplies", Supplies), new("CLMNursingCareHSS", "Nursing care", Supplies),
        new("CLMNursingSvrHSS", "Nursing services", Supplies), new("CLMLabHSS", "Laboratory", Supplies), new("CLMRadiologiHSS", "Radiology", Supplies),
        new("CLMOTSuppHSS", "Operating theatre supplies", Supplies), new("CLMEquipHSS", "Equipment", Supplies), new("CLMMiscHSS", "Miscellaneous", Supplies),
        new("CLMStentsHSS", "Stents", Supplies), new("CLMBalloonHSS", "Balloons", Supplies), new("CLMImplantsHSS", "Implants", Supplies),
        new("PreH60", "Pre-hospital over 60 days", NotCovered), new("PreGP", "Pre-hospital general practitioner", NotCovered), new("PreMedic", "Pre-hospital medication", NotCovered),
        new("PreMR", "Pre-hospital medical report", NotCovered), new("PreSpec", "Pre-hospital specialist", NotCovered), new("PostHosp", "Post-hospital", NotCovered),
        new("PostNotSame", "Post-hospital, not the same condition", NotCovered), new("Others", "Others", NotCovered),
        new("ReimOtherRemarks", "Others: remarks on what is not covered", NotCovered, InvoiceFieldKind.Text),
    ];

    /// <summary>Every column of the three tables by name in any capitals, to the spelling the procedures use (the tables spell five of them with other capitals).</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyOf = ActualColumns.Concat(OthersColumns).Concat(NotCoverColumns).ToDictionary((c) => c, (c) => c, StringComparer.OrdinalIgnoreCase);

    /// <summary>The columns that carry an amount (the money and day fields, not the text and the ticks), from the three tables.</summary>
    public static readonly IReadOnlySet<string> AmountColumns = ActualColumns.Concat(OthersColumns).Concat(NotCoverColumns).Where((c) => !TextColumns.Contains(c) && !FlagColumns.Contains(c)).ToHashSet();

    /// <summary>The columns of the GST row that are the invoice's own and not GST amounts: the desktop passes them through, every GST amount is zero (GST has been zero-rated since 2018).</summary>
    public static readonly IReadOnlySet<string> GstPassThrough = new HashSet<string>
    {
        "CLMEmergency", "CLMPostTreat", "CLMRBTax", "CLMICUTax", "CLMLodgerTax", "CLMOthers2", "CLMOthersDescNoCOPay", "CLMOther1Desc", "CLMOthers2Desc", "CLMNewRBTax", "CLMNewICUTax", "CLMNewLodgerTax",
    };
}
