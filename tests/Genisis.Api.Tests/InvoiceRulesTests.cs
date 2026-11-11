using Dapper;
using Genisis.Api.Invoice;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The arithmetic of the Invoice Processing form.</summary>
public class InvoiceRulesTests
{
    [Fact]
    public void A_sum_is_worked_out_from_the_breakdown_when_there_is_one()
    {
        var done = InvoiceRules.Complete(new InvoiceAmounts { PreHospitalDiagnosis = 1 }, new InvoiceBreakdown { PreXRay = 10, PreLab = 20.5m, PreOthers = null, SurgicalFee = 100, SurgicalWard = 30, AnaesthetistFees = 50 });
        Assert.Equal(30.5m, done.PreHospitalDiagnosis);
        Assert.Equal(130m, done.Surgical);
        Assert.Equal(50m, done.Anaesthetist);
    }

    [Fact]
    public void Without_a_breakdown_the_typed_amount_stands()
    {
        var done = InvoiceRules.Complete(new InvoiceAmounts { PreHospitalDiagnosis = 75, Surgical = 900, Anaesthetist = 120, Physician = 60, MedicalPreHospital = 15 }, new InvoiceBreakdown());
        Assert.Equal((75m, 900m, 120m, 60m, 15m), (done.PreHospitalDiagnosis, done.Surgical, done.Anaesthetist, done.Physician, done.MedicalPreHospital));
    }

    [Fact]
    public void A_breakdown_of_zeros_is_still_a_breakdown()
    {
        var done = InvoiceRules.Complete(new InvoiceAmounts { Surgical = 500 }, new InvoiceBreakdown { SurgicalFee = 0 });
        Assert.Equal(0m, done.Surgical);
    }

    [Fact]
    public void The_totals_with_tax_and_the_accident_and_tax_lines_are_added_from_their_parts()
    {
        var done = InvoiceRules.Complete(
            new InvoiceAmounts { Board = 180, NewBoardTax = 10.8m, Icu = 300, NewIcuTax = 18, Guardian = 40, NewLodgerTax = 2.4m, Emergency = 70, PostTreatment = 30, BoardTax = 1, IcuTax = 2, LodgerTax = 3 }, new InvoiceBreakdown());
        Assert.Equal((190.8m, 318m, 42.4m, 100m, 6m), (done.TotalBoard, done.TotalIcu, done.TotalGuardian, done.Accident, done.Tax));
    }

    [Fact]
    public void The_invoice_amount_counts_the_room_icu_and_guardian_lines_once_through_their_totals()
    {
        var amounts = InvoiceRules.Complete(new InvoiceAmounts { Board = 200, NewBoardTax = 12, Icu = 100, NewIcuTax = 6, Services = 50, Surgical = 400, Guardian = 30, NewLodgerTax = 0, Admin = 5 }, new InvoiceBreakdown());
        Assert.Equal(212m + 106m + 50m + 400m + 30m + 5m, InvoiceRules.Total(amounts));
    }

    [Fact]
    public void An_empty_invoice_is_zero() => Assert.Equal(0m, InvoiceRules.Total(new InvoiceAmounts()));
}

/// <summary>The rules against the invoices the desktop saved.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class InvoiceRulesOracleTests(ITestOutputHelper output)
{
    private static readonly string[] Columns =
    [
        "INVIndex", "INVAmount", "CLMBoardAmt", "CLMNewRBTax", "CLMTotalBoardAmt", "CLMICUAmt", "CLMNewICUTax", "CLMTotalICUAmt", "CLMServices", "CLMTheatre", "CLMPreHosp", "CLMPreHSAmt", "CLMMPreHosAmt",
        "CLMSurgicalAmt", "CLMAna", "CLMNSPreHos", "CLMPhysician", "CLMPost", "CLMEmergency", "CLMPostTreat", "CLMAccPostAmt", "CLMAmbulance", "CLMOutpatient", "CLMMonthly", "CLMMedical", "CLMPhone",
        "CLMGuardianAmt", "CLMTotalGAllowAmt", "CLMNewLodgerTax", "CLMAdmin", "CLMRBTax", "CLMICUTax", "CLMLodgerTax", "CLMTax", "CLMOthers1", "CLMOthers2", "CLMCashAmt", "CLMOrgan", "CLMMRI",
        "CLMOthersNotCOPay", "CLMHomeNCAmt", "CLMCancerAmt", "CLMPreXRay", "CLMPreLab", "CLMPreoTHERS", "CLMMXRay", "CLMMLab", "CLMMPreOthers", "CLMSurgicalFee", "CLMSurgPhy", "CLMSurgPost",
        "CLMSurgicalWard", "CLMAnaeFees", "CLMAnaeConsultation", "CLMAnaeWard", "CLMInHosFees", "CLMInHosWard", "CLMProceduresFee",
    ];

    /// <summary>
    /// For about one invoice in two hundred that has its amounts saved: the sums the form works out from the breakdown equal the saved ones (all but 7 of 8,600 invoices that have a breakdown), and the
    /// invoice amount the form works out (<c>getTotal</c>) equals the saved one for 95 per cent of the invoices; the rest were changed after they were saved.
    /// </summary>
    [Fact]
    public async Task The_form_arithmetic_reproduces_the_sums_and_the_invoice_amount_the_desktop_saved()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = (await session.Connection.QueryAsync($"SELECT {string.Join(", ", Columns)} FROM genisis.WS_CASInvoice WHERE ABS(CHECKSUM(INVIndex)) % 200 = 0 AND CLMServices IS NOT NULL", transaction: session.Transaction, commandTimeout: 300))
            .Cast<IDictionary<string, object?>>().ToList();
        Assert.True(rows.Count >= 150, $"only {rows.Count} invoices in the sample");

        static decimal? N(IDictionary<string, object?> r, string c) => r[c] is null or DBNull ? null : Convert.ToDecimal(r[c]);
        static decimal V(IDictionary<string, object?> r, string c) => N(r, c) ?? 0m;
        int withParts = 0, sumsEqual = 0, totalTested = 0, totalEqual = 0;
        foreach (var r in rows)
        {
            var saved = new InvoiceAmounts
            {
                Board = V(r, "CLMBoardAmt"), NewBoardTax = V(r, "CLMNewRBTax"), TotalBoard = V(r, "CLMTotalBoardAmt"), Icu = V(r, "CLMICUAmt"), NewIcuTax = V(r, "CLMNewICUTax"), TotalIcu = V(r, "CLMTotalICUAmt"),
                Services = V(r, "CLMServices"), Theatre = V(r, "CLMTheatre"), PreHospital = V(r, "CLMPreHosp"), PreHospitalDiagnosis = V(r, "CLMPreHSAmt"), MedicalPreHospital = V(r, "CLMMPreHosAmt"),
                Surgical = V(r, "CLMSurgicalAmt"), Anaesthetist = V(r, "CLMAna"), NonSurgicalPreHospital = V(r, "CLMNSPreHos"), Physician = V(r, "CLMPhysician"), PostHospital = V(r, "CLMPost"),
                Emergency = V(r, "CLMEmergency"), PostTreatment = V(r, "CLMPostTreat"), Accident = V(r, "CLMAccPostAmt"), Ambulance = V(r, "CLMAmbulance"), Outpatient = V(r, "CLMOutpatient"),
                Monthly = V(r, "CLMMonthly"), Medical = V(r, "CLMMedical"), Phone = V(r, "CLMPhone"), Guardian = V(r, "CLMGuardianAmt"), TotalGuardian = V(r, "CLMTotalGAllowAmt"),
                NewLodgerTax = V(r, "CLMNewLodgerTax"), Admin = V(r, "CLMAdmin"), BoardTax = V(r, "CLMRBTax"), IcuTax = V(r, "CLMICUTax"), LodgerTax = V(r, "CLMLodgerTax"), Tax = V(r, "CLMTax"),
                Others1 = V(r, "CLMOthers1"), Others2 = V(r, "CLMOthers2"), Cash = V(r, "CLMCashAmt"), Organ = V(r, "CLMOrgan"), Mri = V(r, "CLMMRI"), OthersNoCoPay = V(r, "CLMOthersNotCOPay"),
                HomeNursing = V(r, "CLMHomeNCAmt"), Cancer = V(r, "CLMCancerAmt"),
            };
            var parts = new InvoiceBreakdown
            {
                PreXRay = N(r, "CLMPreXRay"), PreLab = N(r, "CLMPreLab"), PreOthers = N(r, "CLMPreoTHERS"), MedicalXRay = N(r, "CLMMXRay"), MedicalLab = N(r, "CLMMLab"), MedicalOthers = N(r, "CLMMPreOthers"),
                SurgicalFee = N(r, "CLMSurgicalFee"), SurgicalPhysician = N(r, "CLMSurgPhy"), SurgicalPost = N(r, "CLMSurgPost"), SurgicalWard = N(r, "CLMSurgicalWard"),
                AnaesthetistFees = N(r, "CLMAnaeFees"), AnaesthetistConsultation = N(r, "CLMAnaeConsultation"), AnaesthetistWard = N(r, "CLMAnaeWard"),
                PhysicianFees = N(r, "CLMInHosFees"), PhysicianWard = N(r, "CLMInHosWard"), ProceduresFee = N(r, "CLMProceduresFee"),
            };
            if (parts.PreXRay is not null && parts.SurgicalFee is not null && parts.AnaesthetistFees is not null && parts.PhysicianFees is not null)
            {
                withParts++;
                var done = InvoiceRules.Complete(saved, parts);
                bool Same(decimal a, decimal b) => Math.Abs(a - b) < 0.011m;
                if (Same(done.PreHospitalDiagnosis, saved.PreHospitalDiagnosis) && Same(done.MedicalPreHospital, saved.MedicalPreHospital) && Same(done.Surgical, saved.Surgical) && Same(done.Anaesthetist, saved.Anaesthetist)
                    && Same(done.Physician, saved.Physician) && Same(done.Accident, saved.Accident) && Same(done.Tax, saved.Tax) && Same(done.TotalBoard, saved.TotalBoard) && Same(done.TotalIcu, saved.TotalIcu)
                    && Same(done.TotalGuardian, saved.TotalGuardian)) sumsEqual++;
            }
            if (V(r, "INVAmount") != 0)
            {
                totalTested++;
                if (Math.Abs(InvoiceRules.Total(saved) - V(r, "INVAmount")) < 0.011m) totalEqual++;
            }
        }
        output.WriteLine($"{rows.Count} invoices: sums from the breakdown equal the saved ones for {sumsEqual} of {withParts} that have a breakdown; the invoice amount equals getTotal for {totalEqual} of {totalTested}");
        Assert.True(withParts >= 100, $"only {withParts} invoices with a breakdown");
        Assert.True(sumsEqual >= withParts * 0.98, $"sums: {sumsEqual} of {withParts}");
        Assert.True(totalTested >= 100 && totalEqual >= totalTested * 0.90, $"invoice amount: {totalEqual} of {totalTested}");
    }
}
