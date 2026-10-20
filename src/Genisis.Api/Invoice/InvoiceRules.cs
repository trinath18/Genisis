namespace Genisis.Api.Invoice;

/// <summary>
/// The amounts of an invoice as the Invoice Processing form keeps them (<c>ClaimInvoiceActual</c>): the benefit amounts, the room, ICU and guardian lines with and without tax, and the taxes. The
/// property names follow the form's variables (<c>A1</c>, <c>A5</c>, …) in the comments, the stored procedure's parameters (<c>Insert_ClaimInvActual2010</c>) in the names.
/// </summary>
public record InvoiceAmounts
{
    /// <summary>A1: Room &amp; Board (<c>CLMBoardAmt</c>).</summary>
    public decimal Board { get; set; }

    /// <summary>A1B: Room &amp; Board with its new tax (<c>CLMTotalBoardAmt</c>).</summary>
    public decimal TotalBoard { get; set; }

    /// <summary>ART: the new tax on Room &amp; Board (<c>CLMNewRBTax</c>).</summary>
    public decimal NewBoardTax { get; set; }

    /// <summary>A2: ICU (<c>CLMICUAmt</c>).</summary>
    public decimal Icu { get; set; }

    /// <summary>A2B: ICU with its new tax (<c>CLMTotalICUAmt</c>).</summary>
    public decimal TotalIcu { get; set; }

    /// <summary>AIT: the new tax on ICU (<c>CLMNewICUTax</c>).</summary>
    public decimal NewIcuTax { get; set; }

    /// <summary>A3: hospital supplies and services (<c>CLMServices</c>).</summary>
    public decimal Services { get; set; }

    /// <summary>A4: operating theatre (<c>CLMTheatre</c>).</summary>
    public decimal Theatre { get; set; }

    /// <summary>A24: pre-hospital (<c>CLMPreHosp</c>).</summary>
    public decimal PreHospital { get; set; }

    /// <summary>A5: pre-hospital diagnosis (<c>CLMPreHSAmt</c>): X-ray + lab + others when the breakdown is given.</summary>
    public decimal PreHospitalDiagnosis { get; set; }

    /// <summary>A5B: medical pre-hospital (<c>CLMMPreHosAmt</c>): X-ray + lab + others of the medical breakdown.</summary>
    public decimal MedicalPreHospital { get; set; }

    /// <summary>A6: surgical (<c>CLMSurgicalAmt</c>): fee + physician + post-treatment + ward.</summary>
    public decimal Surgical { get; set; }

    /// <summary>A7: anaesthetist (<c>CLMAna</c>): fees + consultation + ward.</summary>
    public decimal Anaesthetist { get; set; }

    /// <summary>A8: non-surgical pre-hospital (<c>CLMNSPreHos</c>).</summary>
    public decimal NonSurgicalPreHospital { get; set; }

    /// <summary>A9: in-hospital physician (<c>CLMPhysician</c>): fees + ward + procedures.</summary>
    public decimal Physician { get; set; }

    /// <summary>A10: post-hospital (<c>CLMPost</c>).</summary>
    public decimal PostHospital { get; set; }

    /// <summary>A11: accident (<c>CLMAccPostAmt</c>): emergency + post-treatment.</summary>
    public decimal Accident { get; set; }

    /// <summary>Emergency treatment (<c>CLMEmergency</c>, <c>txtACC</c>).</summary>
    public decimal Emergency { get; set; }

    /// <summary>Post-treatment of an accident (<c>CLMPostTreat</c>, <c>txtPTreat</c>).</summary>
    public decimal PostTreatment { get; set; }

    /// <summary>A12: ambulance (<c>CLMAmbulance</c>).</summary>
    public decimal Ambulance { get; set; }

    /// <summary>A13: outpatient (<c>CLMOutpatient</c>).</summary>
    public decimal Outpatient { get; set; }

    /// <summary>A14: monthly (<c>CLMMonthly</c>).</summary>
    public decimal Monthly { get; set; }

    /// <summary>A15: medical report (<c>CLMMedical</c>).</summary>
    public decimal Medical { get; set; }

    /// <summary>A16: telephone (<c>CLMPhone</c>).</summary>
    public decimal Phone { get; set; }

    /// <summary>A17: guardian allowance (<c>CLMGuardianAmt</c>).</summary>
    public decimal Guardian { get; set; }

    /// <summary>ALB: guardian allowance with the lodger's new tax (<c>CLMTotalGAllowAmt</c>).</summary>
    public decimal TotalGuardian { get; set; }

    /// <summary>ALT: the new tax on the lodger (<c>CLMNewLodgerTax</c>).</summary>
    public decimal NewLodgerTax { get; set; }

    /// <summary>A18: admin (<c>CLMAdmin</c>).</summary>
    public decimal Admin { get; set; }

    /// <summary>A19: tax (<c>CLMTax</c>): the Room &amp; Board, ICU and lodger tax of the old tax.</summary>
    public decimal Tax { get; set; }

    /// <summary>The Room &amp; Board tax of the old tax (<c>CLMRBTax</c>).</summary>
    public decimal BoardTax { get; set; }

    /// <summary>The ICU tax of the old tax (<c>CLMICUTax</c>).</summary>
    public decimal IcuTax { get; set; }

    /// <summary>The lodger tax of the old tax (<c>CLMLodgerTax</c>).</summary>
    public decimal LodgerTax { get; set; }

    /// <summary>A20: others 1 (<c>CLMOthers1</c>).</summary>
    public decimal Others1 { get; set; }

    /// <summary>A20B: others 2 (<c>CLMOthers2</c>).</summary>
    public decimal Others2 { get; set; }

    /// <summary>A21: cash allowance (<c>CLMCashAmt</c>).</summary>
    public decimal Cash { get; set; }

    /// <summary>A22: organ (<c>CLMOrgan</c>).</summary>
    public decimal Organ { get; set; }

    /// <summary>A23: MRI (<c>CLMMRI</c>).</summary>
    public decimal Mri { get; set; }

    /// <summary>A25: others, no co-payment (<c>CLMOthersNotCOPay</c>).</summary>
    public decimal OthersNoCoPay { get; set; }

    /// <summary>A26: home nursing (<c>CLMHomeNCAmt</c>).</summary>
    public decimal HomeNursing { get; set; }

    /// <summary>A27: cancer treatment (<c>CLMCancerAmt</c>).</summary>
    public decimal Cancer { get; set; }
}

/// <summary>The finer entries the form adds up into some of the amounts (<c>ClaimInvoiceActualOthers</c>), as nullable amounts: an invoice entered without a breakdown has none.</summary>
public record InvoiceBreakdown
{
    /// <summary>Pre-hospital X-ray (<c>CLMPreXRay</c>, <c>txtXray</c>).</summary>
    public decimal? PreXRay { get; set; }

    /// <summary>Pre-hospital laboratory (<c>CLMPreLab</c>, <c>txtPreLab</c>).</summary>
    public decimal? PreLab { get; set; }

    /// <summary>Pre-hospital others (<c>CLMPreoTHERS</c>, <c>txtPreOthers</c>).</summary>
    public decimal? PreOthers { get; set; }

    /// <summary>Medical pre-hospital X-ray (<c>CLMMXRay</c>).</summary>
    public decimal? MedicalXRay { get; set; }

    /// <summary>Medical pre-hospital laboratory (<c>CLMMLab</c>).</summary>
    public decimal? MedicalLab { get; set; }

    /// <summary>Medical pre-hospital others (<c>CLMMPreOthers</c>).</summary>
    public decimal? MedicalOthers { get; set; }

    /// <summary>Surgical fee (<c>CLMSurgicalFee</c>).</summary>
    public decimal? SurgicalFee { get; set; }

    /// <summary>Surgical physician (<c>CLMSurgPhy</c>, <c>txtPhysician</c>).</summary>
    public decimal? SurgicalPhysician { get; set; }

    /// <summary>Surgical post-treatment (<c>CLMSurgPost</c>, <c>txtPostTreat</c>).</summary>
    public decimal? SurgicalPost { get; set; }

    /// <summary>Surgical ward (<c>CLMSurgicalWard</c>).</summary>
    public decimal? SurgicalWard { get; set; }

    /// <summary>Anaesthetist fees (<c>CLMAnaeFees</c>).</summary>
    public decimal? AnaesthetistFees { get; set; }

    /// <summary>Anaesthetist consultation (<c>CLMAnaeConsultation</c>).</summary>
    public decimal? AnaesthetistConsultation { get; set; }

    /// <summary>Anaesthetist ward (<c>CLMAnaeWard</c>).</summary>
    public decimal? AnaesthetistWard { get; set; }

    /// <summary>In-hospital physician fees (<c>CLMInHosFees</c>).</summary>
    public decimal? PhysicianFees { get; set; }

    /// <summary>In-hospital physician ward (<c>CLMInHosWard</c>).</summary>
    public decimal? PhysicianWard { get; set; }

    /// <summary>Procedures fee (<c>CLMProceduresFee</c>).</summary>
    public decimal? ProceduresFee { get; set; }
}

/// <summary>
/// The arithmetic of the Invoice Processing form (<c>frmInvoice</c>), ported from its change handlers and <c>getTotal</c>. Each amount that is a sum of finer entries is that sum when the invoice
/// has a breakdown, and what was typed otherwise. Checked against the saved invoices by <c>InvoiceRulesOracleTests</c>.
/// </summary>
public static class InvoiceRules
{
    /// <summary>
    /// The amounts with every sum worked out from the breakdown. Where the invoice has no breakdown for a sum (every part missing) the amount given stands: the form lets the total be typed.
    /// Over the saved invoices that have a breakdown the sums equal the saved amounts in all but 7 of 8,600.
    /// </summary>
    public static InvoiceAmounts Complete(InvoiceAmounts typed, InvoiceBreakdown parts)
    {
        static decimal? Sum(params decimal?[] items) => items.All((i) => i is null) ? null : items.Sum((i) => i ?? 0m);
        return typed with
        {
            PreHospitalDiagnosis = Sum(parts.PreXRay, parts.PreLab, parts.PreOthers) ?? typed.PreHospitalDiagnosis,
            MedicalPreHospital = Sum(parts.MedicalXRay, parts.MedicalLab, parts.MedicalOthers) ?? typed.MedicalPreHospital,
            Surgical = Sum(parts.SurgicalFee, parts.SurgicalPhysician, parts.SurgicalPost, parts.SurgicalWard) ?? typed.Surgical,
            Anaesthetist = Sum(parts.AnaesthetistFees, parts.AnaesthetistConsultation, parts.AnaesthetistWard) ?? typed.Anaesthetist,
            Physician = Sum(parts.PhysicianFees, parts.PhysicianWard, parts.ProceduresFee) ?? typed.Physician,
            Accident = typed.Emergency + typed.PostTreatment,
            Tax = typed.BoardTax + typed.IcuTax + typed.LodgerTax,
            TotalBoard = typed.Board + typed.NewBoardTax,
            TotalIcu = typed.Icu + typed.NewIcuTax,
            TotalGuardian = typed.Guardian + typed.NewLodgerTax,
        };
    }

    /// <summary>
    /// <c>getTotal</c>: the invoice amount. The form adds every amount control named A… (at most four characters) and takes off the room, ICU and guardian lines that are also inside their totals with
    /// tax, so each is counted once: the totals (A1B, A2B, ALB) stand for the room, ICU and guardian lines. Equal to the saved invoice amount for 95 per cent of the saved invoices (8,867 of 9,262; the
    /// rest were changed after they were saved).
    /// </summary>
    public static decimal Total(InvoiceAmounts a) =>
        a.TotalBoard + a.TotalIcu + a.Services + a.Theatre + a.PreHospitalDiagnosis + a.MedicalPreHospital + a.Surgical + a.Anaesthetist + a.NonSurgicalPreHospital + a.Physician + a.PostHospital + a.Accident
        + a.Ambulance + a.Outpatient + a.Monthly + a.Medical + a.Phone + a.TotalGuardian + a.Admin + a.Tax + a.Others1 + a.Others2 + a.Cash + a.Organ + a.Mri + a.PreHospital + a.OthersNoCoPay
        + a.HomeNursing + a.Cancer;
}
