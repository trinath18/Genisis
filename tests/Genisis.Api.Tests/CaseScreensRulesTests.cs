using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Case.Transfer;

namespace Genisis.Api.Tests;

/// <summary>Validators and pure rules of the nine Case screens that follow the desktop: messages, their order and the file layouts.</summary>
public class CaseScreensRulesTests
{
    [Fact]
    public void Access_positions_match_mdiMain()
    {
        Assert.Equal(7, AccessRight.CaseListing);
        Assert.Equal(18, AccessRight.ClaimAscii);
        Assert.Equal(18, AccessRight.CaseAscii);
        Assert.Equal(7, AccessRight.PanelHospitals);
        Assert.Equal(1, AccessRight.MmaCpt);
        Assert.All([AccessRight.CaseTransfer, AccessRight.CasePayClaim, AccessRight.CaseAuditReview, AccessRight.CaseNonDisclosure],
            p => Assert.Equal(AccessRight.CaseAdjustment, p));
    }

    // ---- Panel Hospitals ----

    [Fact]
    public void Panel_hospitals_needs_a_panel_code() =>
        Assert.Equal("Please Select Hospital Panel Code!", new PanelHospitalsQueryValidator().FirstError(new() { Code = "  " }));

    // ---- Non-Disclosure ----

    [Theory]
    [InlineData("5", null, "Select a valid Pre-Existing Illness category.")]
    [InlineData("4", "9", "Select a valid Underlying Illness category.")]
    public void NonDisclosure_categories_must_be_in_the_lists(string pre, string? und, string message) =>
        Assert.Equal(message, new NonDisclosureRequestValidator().FirstError(new() { PreCategory = pre, UndCategory = und }));

    [Fact]
    public void NonDisclosure_others_text_is_kept_only_for_the_others_category()
    {
        var validator = new NonDisclosureRequestValidator();
        var kept = NonDisclosureRules.Normalize(new() { PreCategory = "4", PreOthers = " cyst ", UndCategory = "3", UndOthers = "gout" });
        Assert.Equal(("cyst", "gout"), (kept.PreOthers, kept.UndOthers));
        var dropped = NonDisclosureRules.Normalize(new() { PreCategory = "1", PreOthers = "cyst", UndCategory = "2", UndOthers = "gout" });
        Assert.Equal(("", ""), (dropped.PreOthers, dropped.UndOthers));
        Assert.Null(validator.FirstError(new() { PreCategory = "4", PreOthers = new string('x', 50), PreInformedBy = new string('y', 30) }));
        Assert.NotNull(validator.FirstError(new() { PreOthers = new string('x', 51) }));
        Assert.NotNull(validator.FirstError(new() { UndInformedBy = new string('y', 31) }));
    }

    // ---- Pay & Claim ----

    [Fact]
    public void PayClaim_checks_follow_SavePayClaim()
    {
        var validator = new PayClaimRequestValidator();
        Assert.Equal("Please enter Pay&Claim Date", validator.FirstError(new() { Others = true }));
        Assert.Equal("Please enter Remarks", validator.FirstError(new() { PayClaimDate = DateTime.Today, Others = true, OthersRemark = " " }));
        Assert.NotNull(validator.FirstError(new() { PayClaimDate = DateTime.Today, Others = true, OthersRemark = new string('x', 81) }));
        Assert.Null(validator.FirstError(new() { PayClaimDate = DateTime.Today }));
        Assert.Null(validator.FirstError(new() { PayClaimDate = DateTime.Today, Others = true, OthersRemark = "x" }));
    }

    // ---- MMA / CPT ----

    private static MmaProcedureRequest Procedure() => new()
    {
        MainBodyPart = "Eye", OpcsCode = "C100", Narrative = "Cataract", PageNo = "12", Part = "A", PartACost = "100",
    };

    [Theory]
    [InlineData("body", "Please Enter Main Body Part")]
    [InlineData("code", "Please Enter OPCS Code")]
    [InlineData("narrative", "Please Enter OPCS Narrative")]
    [InlineData("page", "Please Enter Page No")]
    public void Mma_procedure_checks_follow_SaveProc(string missing, string message)
    {
        var r = Procedure();
        switch (missing)
        {
            case "body": r.MainBodyPart = ""; r.OpcsCode = null; break;
            case "code": r.OpcsCode = " "; r.Narrative = null; break;
            case "narrative": r.Narrative = ""; r.PageNo = null; break;
            case "page": r.PageNo = ""; break;
        }
        Assert.Equal(message, new MmaProcedureRequestValidator().FirstError(r));
    }

    [Fact]
    public void Mma_procedure_fields_respect_the_column_lengths()
    {
        var validator = new MmaProcedureRequestValidator();
        Assert.Null(validator.FirstError(Procedure()));
        var r = Procedure();
        r.Narrative = new string('n', 401);
        Assert.NotNull(validator.FirstError(r));
        r = Procedure();
        r.Part = "D";
        Assert.Equal("Part must be A, B or C.", validator.FirstError(r));
        r = Procedure();
        r.PartACost = "123456";
        Assert.NotNull(validator.FirstError(r));
        r = Procedure();
        r.PageNo = "1x";
        Assert.NotNull(validator.FirstError(r));
        Assert.Equal("%a[[]b[%]c[_]%", MmaRules.Contains(" a[b%c_ "));
        Assert.Null(MmaRules.Contains("  "));
        Assert.Equal("B", MmaRules.Normalize(new() { Part = " b " }).Part);
    }

    [Fact]
    public void Mma_fee_needs_a_category_and_a_sane_amount()
    {
        var validator = new MmaFeeRequestValidator();
        Assert.NotNull(validator.FirstError(new()));
        Assert.Null(validator.FirstError(new() { SurgeonCategory = "SC1" }));
        Assert.Null(validator.FirstError(new() { AnaesthetistCategory = "AC1", AnaesthetistFees = 10.5m }));
        Assert.NotNull(validator.FirstError(new() { SurgeonCategory = "SC1", SurgeonFees = -1 }));
        Assert.NotNull(validator.FirstError(new() { SurgeonCategory = "SC12345678901" }));
        Assert.NotNull(validator.FirstError(new() { SurgeonCategory = "SC1", AnaesthetistFees = 100_000_000 }));
    }

    // ---- Audit review ----

    [Fact]
    public void Audit_review_checks_follow_cmdUpdate1()
    {
        var validator = new AuditReviewRequestValidator();
        Assert.Equal("Please Enter Discount Type!", validator.FirstError(new()));
        Assert.Null(validator.FirstError(new() { DiscountType = "B", Status = "O", VisitTime = "14:05", InvoiceAmount = 10 }));
        Assert.Equal("Invalid Time Format!", validator.FirstError(new() { DiscountType = "A", VisitTime = "25:00" }));
        Assert.Equal("Select a valid Discount Type.", validator.FirstError(new() { DiscountType = "Z" }));
        Assert.Equal("Select a valid Audit Review Status.", validator.FirstError(new() { DiscountType = "A", Status = "X" }));
        Assert.NotNull(validator.FirstError(new() { DiscountType = "A", Query = new string('q', 501) }));
        Assert.NotNull(validator.FirstError(new() { DiscountType = "A", DiscountReceived = -1 }));
    }

    // ---- Case transfer ----

    [Fact]
    public void Transfer_input_checks_follow_cmdSave_order()
    {
        var validator = new TransferRequestValidator();
        Assert.Equal("Please enter the Member No. to transfer to.", validator.FirstError(new() { CoverId = "00" }));
        Assert.Equal("Please enter the Cover ID to transfer to.", validator.FirstError(new() { MemberNo = "M2" }));
        Assert.Null(validator.FirstError(new() { MemberNo = "M2", CoverId = "00" }));
    }

    [Fact]
    public void Transfer_target_checks_follow_cmdSave_order()
    {
        var current = new TransferCase { CaseNumber = "AC1", MemberNo = "M1", CoverId = "00" };
        static TransferMember Target(Action<TransferMember>? edit = null)
        {
            var m = new TransferMember { MemberNo = "M2", CoverId = "00", PatientName = "ALI", PolicyNo = "P", Status = "A", CoverFound = true };
            edit?.Invoke(m);
            return m;
        }
        Assert.Equal("Member No not found.", TransferRules.Check(null, current));
        Assert.Equal("Member No not found.", TransferRules.Check(Target(m => m.PolicyNo = ""), current));
        Assert.Equal("Patient not found.", TransferRules.Check(Target(m => m.PatientName = ""), current));
        Assert.Equal("Member status - 'DELETE'.", TransferRules.Check(Target(m => m.Status = "D"), current));
        Assert.Equal("Invalid Cover ID. Please try again.", TransferRules.Check(Target(m => { m.CoverFound = false; m.PatientName = null; }), current));
        Assert.Equal("The case is already on this member and cover.", TransferRules.Check(Target(m => m.MemberNo = "M1"), current));
        Assert.Null(TransferRules.Check(Target(), current));
    }

    // ---- Inpatient case listing ----

    private static ListingItem Item() => new()
    {
        CaseNo = "AC00000001", ClaimVersion = 1, Dod = DateTime.Today, Hospital = "H", Claimability = "AA", HltType = "N", InsuredType = "I",
        CashType = "H", TotalApproved = 100, Payable2H = 80, Payable2M = 60, CaseMgmtFees = 5,
    };

    [Fact]
    public void Listing_selection_rules_follow_ItemCheck()
    {
        Assert.Null(ListingRules.SelectionProblem(Item(), true));
        Assert.Equal("Date of Discharge is Empty.", ListingRules.SelectionProblem(Item() with { Dod = null }, true));
        Assert.Equal("Hospital cannot be empty!", ListingRules.SelectionProblem(Item() with { Hospital = " " }, true));
        Assert.Equal("Claimability is Not Decided!", ListingRules.SelectionProblem(Item() with { Claimability = "N" }, true));
        Assert.Equal("Please ensure scanned documents are in system!", ListingRules.SelectionProblem(Item(), false));
        Assert.Null(ListingRules.SelectionProblem(Item(), null));
    }

    [Theory]
    [InlineData("G")]
    [InlineData("H")]
    public void Listing_group_worksheets_need_a_group_company_and_no_documents(string insured)
    {
        var item = Item() with { InsuredType = insured };
        Assert.True(ListingRules.IsGroup(item));
        Assert.Equal("Group Company cannot be empty!", ListingRules.SelectionProblem(item, true));
        Assert.Null(ListingRules.SelectionProblem(item with { GroupCompany = "ACME" }, false));
    }

    [Fact]
    public void Listing_amount_depends_on_btbg_and_who_is_paid()
    {
        Assert.Equal(105, ListingRules.Amount(Item()));
        Assert.Equal(85, ListingRules.Amount(Item() with { Btbg = "Y", CashType = "H" }));
        Assert.Equal(65, ListingRules.Amount(Item() with { Btbg = "Y", CashType = "M" }));
        Assert.Equal(105, ListingRules.Amount(Item() with { Btbg = "N", CashType = "M" }));
    }

    [Fact]
    public void Listing_search_and_bordx_checks()
    {
        var search = new ListingFilterValidator();
        Assert.Equal("Please Enter Worksheet No", search.FirstError(new()));
        Assert.Null(search.FirstError(new() { WksNoFrom = "AC1" }));
        Assert.Null(search.FirstError(new() { UseScan = false }));
        Assert.True(ListingRules.UsesIbView(new() { WksNoFrom = "IB0001" }));
        Assert.True(ListingRules.UsesIbView(new() { Payor = "IB - Some payor" }));
        Assert.False(ListingRules.UsesIbView(new() { WksNoFrom = "AC0001", Payor = "AC" }));

        var bordx = new BordxRequestValidator();
        Assert.Equal("Please select at least one record to be updated!", bordx.FirstError(new() { Option = "Single Claim" }));
        var items = new List<ListingPick> { new("AC1", 1) };
        Assert.Equal("Please select Bordx option", bordx.FirstError(new() { Items = items }));
        Assert.Equal("Please select Bordx option", bordx.FirstError(new() { Items = items, Option = "Whatever" }));
        Assert.Null(bordx.FirstError(new() { Items = items, Option = "Multiple Claims" }));

        var row = new ListingRow { CaseNo = "AC1", ClaimVersion = 2, CoverId = "00", MemberName = "PRINCIPAL", CoveredName = "CHILD" };
        Assert.Equal(("AC1-2", "PRINCIPAL"), (row.WksNo, row.Patient));
        Assert.Equal("CHILD", (row with { CoverId = "01" }).Patient);
    }

    // ---- ASCII exports ----

    [Fact]
    public void Claim_ascii_checks_follow_CmdGenerate_and_use_the_variant_wording()
    {
        var validator = new ClaimAsciiRequestValidator();
        var ok = new ClaimAsciiRequest { Payor = "AC - AXA", DateFrom = new(2021, 1, 1), DateTo = new(2021, 1, 31), ExportStatus = "N", WorksheetStatus = "0" };
        Assert.Null(validator.FirstError(ok));
        Assert.Equal("Please enter the Payor code.", validator.FirstError(new()));
        Assert.Equal("Please enter Bordx Date from.", validator.FirstError(new() { Payor = "AC" }));
        Assert.Equal("Please enter Bordx Date to.", validator.FirstError(new() { Payor = "AC", DateFrom = new(2021, 1, 1) }));
        Assert.Equal("Please enter Case Reg. Date from.", validator.FirstError(new() { Payor = "AC", Variant = "rejected" }));
        Assert.Equal("Bordx Date from cannot be after Bordx Date to.", validator.FirstError(new() { Payor = "AC", DateFrom = new(2021, 2, 1), DateTo = new(2021, 1, 1) }));
        Assert.Null(validator.FirstError(new() { Payor = "AC", DateFrom = new(2021, 1, 1), DateTo = new(2021, 1, 2), Variant = "rejected" }));
        Assert.Equal("Please select the Export status.", validator.FirstError(new() { Payor = "AC", DateFrom = ok.DateFrom, DateTo = ok.DateTo }));
        Assert.Equal("Please select the Bordx WS status.", validator.FirstError(new() { Payor = "AC", DateFrom = ok.DateFrom, DateTo = ok.DateTo, ExportStatus = "R" }));
        Assert.NotNull(validator.FirstError(new() { Payor = "AC", DateFrom = ok.DateFrom, DateTo = ok.DateTo, ExportStatus = "R", WorksheetStatus = "2" }));
    }

    [Fact]
    public void Case_ascii_checks_follow_CmdGenerate()
    {
        var validator = new CaseAsciiRequestValidator();
        var ok = new CaseAsciiRequest { Payor = "AC", DateFrom = new(2021, 1, 1), DateTo = new(2021, 1, 31), ExportStatus = "R", WorksheetStatus = "2" };
        Assert.Null(validator.FirstError(ok));
        Assert.Equal("Please enter Case Reg. Date from.", validator.FirstError(new() { Payor = "AC" }));
        ok.WorksheetStatus = null;
        Assert.Equal("Please select the Endt Status.", validator.FirstError(ok));
        Assert.Equal("AC", AsciiPayor.Code("AC - AXA AFFIN"));
        Assert.Equal("AC", AsciiPayor.Code(" AC "));
    }

    private static Dictionary<string, object?> ClaimRow() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["BordxRefNo"] = "NAX0010119", ["BordxDate"] = new DateTime(2021, 8, 6), ["PAYCode"] = "AX", ["CASNumber"] = "AX00000001", ["ClaimVersion"] = 2,
        ["MBMNumber"] = "AXHA1*01", ["MBMPatientCovID"] = "00", ["MBMPolicyNo"] = "POL1", ["MBMName"] = "TEST MEMBER",
        ["MBMPayorEffDate"] = new DateTime(2021, 1, 2), ["MBMPayorExpDate"] = new DateTime(2022, 1, 1), ["MBMIcBcPp"] = "800101015555",
        ["PatientName"] = "TEST PATIENT", ["ICNo"] = null, ["CLMTotalActual"] = 1234567.5m, ["CLMTotalApproved"] = 100m,
        ["CASClaimability"] = "AA", ["CASAdmissionReason"] = "FEVER", ["CASDateAdmitted"] = new DateTime(2021, 7, 1), ["CASReserveAmount"] = 1600.5m,
        ["CASREPCode1"] = "R01", ["CASLGAmt"] = 2500m, ["CASASCIIAmendStatus"] = null,
    };

    [Fact]
    public void Ascii_records_have_the_desktop_widths()
    {
        var accepted = AsciiFormat.AcceptedRecord(ClaimRow());
        Assert.Equal(5000, accepted.Length);
        Assert.EndsWith("\r", accepted);
        Assert.StartsWith("NAX0010119" + new string(' ', 5) + "06082021AX" + "AX00000001" + new string(' ', 5) + "AX00000001-2" + new string(' ', 3)
            + "AXHA1*01" + new string(' ', 7) + "00POL1", accepted);
        Assert.Equal(5001, AsciiFormat.RejectedRecord(ClaimRow()).Length);
        Assert.StartsWith("N" + new string(' ', 23) + "AXAX00000001", AsciiFormat.RejectedRecord(ClaimRow()));
        Assert.Equal(4975, AsciiFormat.CaseRecord(ClaimRow(), new(2026, 10, 17), "00007").Length);

        // 20-wide IC field even when the IC is missing: the amounts that follow stay at the same offset as a record with an IC.
        var withIc = ClaimRow();
        withIc["ICNo"] = "900101015555";
        Assert.Equal(AsciiFormat.AcceptedRecord(withIc).IndexOf("1,234,567.50", StringComparison.Ordinal), accepted.IndexOf("1,234,567.50", StringComparison.Ordinal));
        Assert.Contains("1,234,567.50" + "100.00      ", accepted);
    }

    [Fact]
    public void Ascii_case_record_and_file_framing()
    {
        var record = AsciiFormat.CaseRecord(ClaimRow(), new(2026, 10, 17), "00007");
        Assert.StartsWith("NAXAX00000001", record);
        Assert.Contains("1600.5  2500    17102026" + "00007", record);
        Assert.Equal("FMX0000117102026", AsciiFormat.ClaimHeader("MX", "00001", new(2026, 10, 17)));
        Assert.Equal(175, AsciiFormat.ClaimTrailer(3).Length);
        Assert.StartsWith("T3         ", AsciiFormat.ClaimTrailer(3));
        Assert.Equal("AX261017(00002)", AsciiFormat.FileName("AX", new(2026, 10, 17), "00002"));
        Assert.Equal("a\r\nb\r\n", AsciiFormat.Join(["a", "b"]));
        Assert.Equal("ab ", AsciiFormat.L("ab", 3));
        Assert.Equal("abc", AsciiFormat.L("abcdef", 3));
        Assert.Equal("   ", AsciiFormat.L(null, 3));
    }
}
