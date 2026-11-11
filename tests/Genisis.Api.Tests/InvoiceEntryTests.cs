using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The checks, the arithmetic and the field list of Invoice Processing's save, without a database.</summary>
public class InvoiceEntryRulesTests
{
    private static readonly InvoiceCaseHeader Case = new() { CaseNo = "AB0001", MemberName = "A MEMBER", Discharged = new DateTime(2021, 1, 3), HospitalCode = "PANIN", PlanCode = "BV18", PayType = "C" };

    private static InvoiceForm Valid() => new()
    {
        CaseNo = "AB0001", InvoiceNo = "I1", InvoiceDate = new DateTime(2021, 1, 4), PayTo = "H", Payee = "PANIN", StayType = "I", ExcessAmount = 0, InitialAmount = 100,
    };

    [Fact]
    public void A_complete_invoice_passes_every_check() => Assert.Null(InvoiceChecks.First(Valid(), Case, "PANTAI HOSPITAL AMPANG"));

    [Theory]
    [InlineData("case", InvoiceMessages.NoCase)]
    [InlineData("discharge", InvoiceMessages.NoDischarge)]
    [InlineData("payto", InvoiceMessages.NoPayTo)]
    [InlineData("stay", InvoiceMessages.NoStayType)]
    [InlineData("payee", InvoiceMessages.NoPayee)]
    [InlineData("no", InvoiceMessages.NoInvoiceNo)]
    [InlineData("date", InvoiceMessages.NoInvoiceDate)]
    [InlineData("excess", InvoiceMessages.NoExcess)]
    [InlineData("initial", InvoiceMessages.NoInitialAmount)]
    public void Each_missing_entry_gets_the_desktops_message(string missing, string message)
    {
        var form = Valid();
        var header = Case;
        switch (missing)
        {
            case "case": header = Case with { MemberName = "" }; break;
            case "discharge": header = Case with { Discharged = null }; break;
            case "payto": form.PayTo = null; break;
            case "stay": form.StayType = " "; break;
            case "payee": form.Payee = ""; break;
            case "no": form.InvoiceNo = null; break;
            case "date": form.InvoiceDate = null; break;
            case "excess": form.ExcessAmount = null; break;
            case "initial": form.InitialAmount = null; break;
        }
        Assert.Equal(message, InvoiceChecks.First(form, header, null));
    }

    [Fact]
    public void A_paid_member_needs_a_receipt_date_then_a_receipt_mode_and_no_excess()
    {
        var form = Valid();
        form.PayTo = "M";
        form.Payee = "THE MEMBER";
        form.ExcessAmount = null;
        Assert.Equal(InvoiceMessages.NoReceiptDate, InvoiceChecks.First(form, Case, null));
        form.ReceiptDate = new DateTime(2021, 1, 5);
        Assert.Equal(InvoiceMessages.NoReceiptMode, InvoiceChecks.First(form, Case, null));
        form.ReceiptMode = "EF";
        Assert.Null(InvoiceChecks.First(form, Case, null));
    }

    [Fact]
    public void The_checks_run_in_the_desktops_order()
    {
        var form = new InvoiceForm { CaseNo = "AB0001", PayTo = "H" };
        var order = new List<string?>();
        order.Add(InvoiceChecks.First(form, Case with { Discharged = null }, null));
        order.Add(InvoiceChecks.First(form, Case, null));
        form.StayType = "I";
        order.Add(InvoiceChecks.First(form, Case, null));
        form.Payee = "PANIN";
        order.Add(InvoiceChecks.First(form, Case, null));
        form.InvoiceNo = "I1";
        order.Add(InvoiceChecks.First(form, Case, null));
        form.InvoiceDate = new DateTime(2021, 1, 4);
        order.Add(InvoiceChecks.First(form, Case, null));
        form.ExcessAmount = 0;
        order.Add(InvoiceChecks.First(form, Case, null));
        Assert.Equal([InvoiceMessages.NoDischarge, InvoiceMessages.NoStayType, InvoiceMessages.NoPayee, InvoiceMessages.NoInvoiceNo, InvoiceMessages.NoInvoiceDate, InvoiceMessages.NoExcess, InvoiceMessages.NoInitialAmount], order);
    }

    [Theory]
    [InlineData("dental clinic")]
    [InlineData("GENERAL PRACTITIONER")]
    [InlineData("Non-Panel Hospital")]
    public void A_generic_provider_needs_its_name_typed(string provider)
    {
        var form = Valid();
        Assert.Equal(InvoiceMessages.NoProviderName, InvoiceChecks.First(form, Case, provider));
        form.Texts["INVHospName"] = "DR LEE CLINIC";
        Assert.Null(InvoiceChecks.First(form, Case, provider));
    }

    [Fact]
    public void Another_hospital_than_the_cases_has_to_be_confirmed()
    {
        var form = Valid();
        Assert.False(InvoiceChecks.NeedsHospitalConfirmation(form, Case));
        form.Payee = "OTHER";
        Assert.True(InvoiceChecks.NeedsHospitalConfirmation(form, Case));
        form.ConfirmDifferentHospital = true;
        Assert.False(InvoiceChecks.NeedsHospitalConfirmation(form, Case));
        form.PayTo = "M";
        form.ConfirmDifferentHospital = false;
        Assert.False(InvoiceChecks.NeedsHospitalConfirmation(form, Case));
    }

    [Fact]
    public void Fields_the_form_does_not_have_a_wrong_tick_and_long_texts_are_refused_and_credits_are_not()
    {
        var form = Valid();
        Assert.Null(InvoiceChecks.Fields(form));
        form.Amounts["NoSuchColumn"] = 1;
        Assert.Equal("Unknown field NoSuchColumn.", InvoiceChecks.Fields(form));
        form.Amounts.Clear();
        form.Amounts["CLMServices"] = -1;
        Assert.Null(InvoiceChecks.Fields(form));
        form.Amounts.Clear();
        form.Texts["CLMMedRptMBM"] = "X";
        Assert.Equal("CLMMedRptMBM must be Y or N.", InvoiceChecks.Fields(form));
        form.Texts.Clear();
        form.Texts["CLMOthers2Desc"] = new string('x', 101);
        Assert.Equal("CLMOthers2Desc is longer than 100 characters.", InvoiceChecks.Fields(form));
    }

    [Fact]
    public void The_hospital_pays_the_invoice_less_the_excess_and_the_member_nothing_through_the_hospital()
    {
        var amounts = new Dictionary<string, decimal?> { ["CLMServices"] = 500, ["CLMBoardAmt"] = 200 };
        Assert.Equal((700m, 650m), (InvoiceCalculator.Calculate(amounts, "H", 50, false).InvoiceAmount, InvoiceCalculator.Calculate(amounts, "H", 50, false).PayAmount));
        Assert.Equal((700m, 0m), (InvoiceCalculator.Calculate(amounts, "M", 50, false).InvoiceAmount, InvoiceCalculator.Calculate(amounts, "M", 50, false).PayAmount));
    }

    [Fact]
    public void The_hospital_sums_are_kept_only_for_the_plans_that_carry_them()
    {
        var amounts = new Dictionary<string, decimal?> { ["CLMPreXRay"] = 10, ["CLMPreHosp"] = 20, ["CLMSurgicalFee"] = 100, ["CLMAnaeFees"] = 30, ["CLMMXRay"] = 1, ["CLMNSPreHos"] = 2, ["CLMInHosFees"] = 3, ["CLMPost"] = 4 };
        var with = InvoiceCalculator.Calculate(amounts, "H", 0, true).Amounts;
        var without = InvoiceCalculator.Calculate(amounts, "H", 0, false).Amounts;
        Assert.Equal((160m, 10m), (with["CLMCHospSum"], with["CLMMHospSum"]));
        Assert.Equal((0m, 0m), (without["CLMCHospSum"], without["CLMMHospSum"]));
    }

    [Fact]
    public void Every_field_of_the_form_is_a_column_and_the_sums_name_columns_of_the_form()
    {
        var columns = InvoiceFields.ActualColumns.Concat(InvoiceFields.OthersColumns).Concat(InvoiceFields.NotCoverColumns).ToHashSet();
        Assert.All(InvoiceFields.Form, (f) => Assert.Contains(f.Key, columns));
        Assert.Equal(InvoiceFields.Form.Count, InvoiceFields.Form.Select((f) => f.Key + f.Tax).Distinct().Count());
        Assert.All(InvoiceFields.Form.Where((f) => f.SumOf is not null).SelectMany((f) => f.SumOf!), (k) => Assert.Contains(InvoiceFields.Form, (f) => f.Key == k));
        // the procedures take the invoice key and these: 47, 44 and 11 parameters
        Assert.Equal((46, 43, 10), (InvoiceFields.ActualColumns.Length, InvoiceFields.OthersColumns.Length, InvoiceFields.NotCoverColumns.Length));
    }
}
