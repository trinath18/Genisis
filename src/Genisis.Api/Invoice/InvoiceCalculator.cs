namespace Genisis.Api.Invoice;

/// <summary>An invoice worked out by the form's arithmetic.</summary>
/// <param name="Amounts">Every amount column with the sums, totals and hospital sums worked out.</param>
/// <param name="InvoiceAmount">The invoice amount (<c>getTotal</c>).</param>
/// <param name="PayAmount">The amount to pay: the invoice amount less the excess when the hospital is paid, zero when the member is.</param>
public record InvoiceCalculation(Dictionary<string, decimal?> Amounts, decimal InvoiceAmount, decimal PayAmount);

/// <summary>Works an invoice out from what was typed: the form's change handlers and <c>getTotal</c>, over the columns of the three amount tables.</summary>
public static class InvoiceCalculator
{
    private static decimal V(IReadOnlyDictionary<string, decimal?> c, string key) => c.TryGetValue(key, out var v) ? v ?? 0m : 0m;
    private static decimal? N(IReadOnlyDictionary<string, decimal?> c, string key) => c.TryGetValue(key, out var v) ? v : null;

    /// <summary>The typed columns as the amounts the rules work on.</summary>
    public static InvoiceAmounts ToAmounts(IReadOnlyDictionary<string, decimal?> c) => new()
    {
        Board = V(c, "CLMBoardAmt"), NewBoardTax = V(c, "CLMNewRBTax"), TotalBoard = V(c, "CLMTotalBoardAmt"), Icu = V(c, "CLMICUAmt"), NewIcuTax = V(c, "CLMNewICUTax"), TotalIcu = V(c, "CLMTotalICUAmt"),
        Services = V(c, "CLMServices"), Theatre = V(c, "CLMTheatre"), PreHospital = V(c, "CLMPreHosp"), PreHospitalDiagnosis = V(c, "CLMPreHSAmt"), MedicalPreHospital = V(c, "CLMMPreHosAmt"),
        Surgical = V(c, "CLMSurgicalAmt"), Anaesthetist = V(c, "CLMAna"), NonSurgicalPreHospital = V(c, "CLMNSPreHos"), Physician = V(c, "CLMPhysician"), PostHospital = V(c, "CLMPost"),
        Emergency = V(c, "CLMEmergency"), PostTreatment = V(c, "CLMPostTreat"), Accident = V(c, "CLMAccPostAmt"), Ambulance = V(c, "CLMAmbulance"), Outpatient = V(c, "CLMOutpatient"),
        Monthly = V(c, "CLMMonthly"), Medical = V(c, "CLMMedical"), Phone = V(c, "CLMPhone"), Guardian = V(c, "CLMGuardianAmt"), TotalGuardian = V(c, "CLMTotalGAllowAmt"), NewLodgerTax = V(c, "CLMNewLodgerTax"),
        Admin = V(c, "CLMAdmin"), BoardTax = V(c, "CLMRBTax"), IcuTax = V(c, "CLMICUTax"), LodgerTax = V(c, "CLMLodgerTax"), Tax = V(c, "CLMTax"), Others1 = V(c, "CLMOthers1"), Others2 = V(c, "CLMOthers2"),
        Cash = V(c, "CLMCashAmt"), Organ = V(c, "CLMOrgan"), Mri = V(c, "CLMMRI"), OthersNoCoPay = V(c, "CLMOthersNotCOPay"), HomeNursing = V(c, "CLMHomeNCAmt"), Cancer = V(c, "CLMCancerAmt"),
    };

    /// <summary>The typed columns as the breakdown the rules add up.</summary>
    public static InvoiceBreakdown ToBreakdown(IReadOnlyDictionary<string, decimal?> c) => new()
    {
        PreXRay = N(c, "CLMPreXRay"), PreLab = N(c, "CLMPreLab"), PreOthers = N(c, "CLMPreoTHERS"), MedicalXRay = N(c, "CLMMXRay"), MedicalLab = N(c, "CLMMLab"), MedicalOthers = N(c, "CLMMPreOthers"),
        SurgicalFee = N(c, "CLMSurgicalFee"), SurgicalPhysician = N(c, "CLMSurgPhy"), SurgicalPost = N(c, "CLMSurgPost"), SurgicalWard = N(c, "CLMSurgicalWard"),
        AnaesthetistFees = N(c, "CLMAnaeFees"), AnaesthetistConsultation = N(c, "CLMAnaeConsultation"), AnaesthetistWard = N(c, "CLMAnaeWard"),
        PhysicianFees = N(c, "CLMInHosFees"), PhysicianWard = N(c, "CLMInHosWard"), ProceduresFee = N(c, "CLMProceduresFee"),
    };

    /// <summary>
    /// Works the invoice out. A breakdown that has any value makes its sum; one without stays as typed. <c>CLMCHospSum</c> and <c>CLMMHospSum</c> (the form's hidden Text1 and Text2) are the hospital sums
    /// of plans 2 to 5 and new SM plans (pre-hospital diagnosis + pre-hospital + surgical + anaesthetist, and medical pre-hospital + non-surgical pre-hospital + physician + post-hospital) and zero for the rest.
    /// </summary>
    /// <param name="typed">What was typed, by column name; missing columns count as nothing.</param>
    /// <param name="payTo"><c>H</c> hospital or <c>M</c> member.</param>
    /// <param name="excess">The excess amount.</param>
    /// <param name="hospitalSums">Whether the plan is one that carries the hospital sums.</param>
    public static InvoiceCalculation Calculate(IReadOnlyDictionary<string, decimal?> typed, string payTo, decimal excess, bool hospitalSums)
    {
        var done = InvoiceRules.Complete(ToAmounts(typed), ToBreakdown(typed));
        var amounts = new Dictionary<string, decimal?>(typed)
        {
            ["CLMPreHSAmt"] = done.PreHospitalDiagnosis, ["CLMMPreHosAmt"] = done.MedicalPreHospital, ["CLMSurgicalAmt"] = done.Surgical, ["CLMAna"] = done.Anaesthetist, ["CLMPhysician"] = done.Physician,
            ["CLMAccPostAmt"] = done.Accident, ["CLMTax"] = done.Tax, ["CLMTotalBoardAmt"] = done.TotalBoard, ["CLMTotalICUAmt"] = done.TotalIcu, ["CLMTotalGAllowAmt"] = done.TotalGuardian,
            ["CLMCHospSum"] = hospitalSums ? done.PreHospitalDiagnosis + done.PreHospital + done.Surgical + done.Anaesthetist : 0m,
            ["CLMMHospSum"] = hospitalSums ? done.MedicalPreHospital + done.NonSurgicalPreHospital + done.Physician + done.PostHospital : 0m,
        };
        var total = Math.Round(InvoiceRules.Total(done), 2, MidpointRounding.AwayFromZero);
        return new InvoiceCalculation(amounts, total, payTo == "H" ? Math.Round(total - excess, 2, MidpointRounding.AwayFromZero) : 0m);
    }
}
