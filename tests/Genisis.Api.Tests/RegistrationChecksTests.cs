using Genisis.Api.Worksheet.Calculation;

namespace Genisis.Api.Tests;

/// <summary>The checks before the Registration save, in the desktop's order and with its messages.</summary>
public class RegistrationChecksTests
{
    private static RegistrationEntry Valid() => new()
    {
        CaseNo = "AB0001", MemberNo = "ABHA0000001*01", InvoiceCount = 2, HasFinalDiagnosis = true, WorksheetStatus = "AA Approved", PayTo = "H", InvoicePayTotal = 500m, HospitalTotal = 500m,
    };

    private static RegistrationEntry ValidMember() => Valid() with { PayTo = "M", Bank = "MAYBANK", AccountNo = "1234567890", BankAccountDigits = 10, Address1 = "1 Jalan", PostCode = "50000", City = "KL", State = "WP" };

    [Fact]
    public void A_complete_worksheet_goes_on_to_are_you_sure() => Assert.Empty(RegistrationChecks.Problems(Valid()));

    [Fact]
    public void A_complete_member_paid_worksheet_goes_on_too() => Assert.True(RegistrationChecks.Passes(ValidMember()));

    [Theory]
    [InlineData("case", RegistrationMessages.NoCase)]
    [InlineData("member", RegistrationMessages.NoRecord)]
    [InlineData("invoice", RegistrationMessages.NoInvoice)]
    [InlineData("diagnosis", RegistrationMessages.NoFinalDiagnosis)]
    [InlineData("status", RegistrationMessages.NoStatus)]
    [InlineData("hospital", RegistrationMessages.HospitalAmount)]
    public void Each_missing_piece_gets_the_desktops_message(string missing, string message)
    {
        var entry = Valid();
        switch (missing)
        {
            case "case": entry.CaseNo = " "; break;
            case "member": entry.MemberNo = ""; break;
            case "invoice": entry.InvoiceCount = 0; break;
            case "diagnosis": entry.HasFinalDiagnosis = false; break;
            case "status": entry.WorksheetStatus = ""; break;
            case "hospital": entry.HospitalTotal = 499.99m; break;
        }
        Assert.Equal([message], RegistrationChecks.Problems(entry));
    }

    [Fact]
    public void Only_the_first_problem_is_reported_in_the_desktops_order()
    {
        var entry = new RegistrationEntry { PayTo = "H", InvoicePayTotal = 1, HospitalTotal = 2 };
        Assert.Equal([RegistrationMessages.NoCase], RegistrationChecks.Problems(entry));
        entry.CaseNo = "AB1";
        Assert.Equal([RegistrationMessages.NoRecord], RegistrationChecks.Problems(entry));
        entry.MemberNo = "M";
        Assert.Equal([RegistrationMessages.NoInvoice], RegistrationChecks.Problems(entry));
        entry.InvoiceCount = 1;
        Assert.Equal([RegistrationMessages.NoFinalDiagnosis], RegistrationChecks.Problems(entry));
        entry.HasFinalDiagnosis = true;
        Assert.Equal([RegistrationMessages.NoStatus], RegistrationChecks.Problems(entry));
        entry.WorksheetStatus = "AA";
        Assert.Equal([RegistrationMessages.HospitalAmount], RegistrationChecks.Problems(entry));
    }

    [Fact]
    public void The_hospital_amount_is_compared_to_the_cent_and_only_when_the_hospital_is_paid()
    {
        var entry = Valid() with { InvoicePayTotal = 100.004m, HospitalTotal = 100.00m };
        Assert.Empty(RegistrationChecks.Problems(entry));
        Assert.Empty(RegistrationChecks.Problems(ValidMember() with { InvoicePayTotal = 1m, HospitalTotal = 2m }));
    }

    [Theory]
    [InlineData("bank", RegistrationMessages.NoAccountNo)]
    [InlineData("account", RegistrationMessages.NoBank)]
    [InlineData("address", RegistrationMessages.NoAddress)]
    [InlineData("postcode", RegistrationMessages.NoPostCode)]
    [InlineData("city", RegistrationMessages.NoCity)]
    [InlineData("state", RegistrationMessages.NoState)]
    public void A_member_paid_worksheet_needs_the_bank_pair_and_the_address(string missing, string message)
    {
        var entry = ValidMember();
        switch (missing)
        {
            case "bank": entry.AccountNo = ""; break;
            case "account": entry.Bank = " "; break;
            case "address": entry.Address1 = ""; break;
            case "postcode": entry.PostCode = null; break;
            case "city": entry.City = " "; break;
            case "state": entry.State = ""; break;
        }
        Assert.Equal([message], RegistrationChecks.Problems(entry));
    }

    [Fact]
    public void The_address_checks_run_in_the_order_address_post_code_city_state()
    {
        var entry = ValidMember() with { Address1 = "", PostCode = "", City = "", State = "" };
        Assert.Equal([RegistrationMessages.NoAddress], RegistrationChecks.Problems(entry));
        entry.Address1 = "x";
        Assert.Equal([RegistrationMessages.NoPostCode], RegistrationChecks.Problems(entry));
        entry.PostCode = "x";
        Assert.Equal([RegistrationMessages.NoCity], RegistrationChecks.Problems(entry));
        entry.City = "x";
        Assert.Equal([RegistrationMessages.NoState], RegistrationChecks.Problems(entry));
    }

    [Fact]
    public void A_bank_account_with_the_wrong_number_of_digits_is_reported_first_and_alone_stops_the_save()
    {
        var wrong = ValidMember() with { AccountNo = "12345" };
        Assert.Equal([RegistrationMessages.BankAccount], RegistrationChecks.Problems(wrong));
        Assert.False(RegistrationChecks.Passes(wrong));
        // with something else wrong too, the desktop shows the account message at once and then the other one
        Assert.Equal([RegistrationMessages.BankAccount, RegistrationMessages.NoInvoice], RegistrationChecks.Problems(wrong with { InvoiceCount = 0 }));
        // a bank with no row, a hospital-paid worksheet or a missing half of the pair do not make the digit check
        Assert.Empty(RegistrationChecks.Problems(ValidMember() with { AccountNo = "12345", BankAccountDigits = null }));
        Assert.Empty(RegistrationChecks.Problems(Valid() with { Bank = "MAYBANK", AccountNo = "12345", BankAccountDigits = 10 }));
    }
}
