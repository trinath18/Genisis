namespace Genisis.Api.Worksheet.Calculation;

/// <summary>What the Registration form holds when its save button (<c>cmdApprove_Click</c>) is pressed, as far as the checks need it.</summary>
public record RegistrationEntry
{
    /// <summary>The case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>The member's number; empty when no record was selected.</summary>
    public string? MemberNo { get; set; }

    /// <summary>How many invoices are listed for the worksheet.</summary>
    public int InvoiceCount { get; set; }

    /// <summary>Whether the case has a final diagnosis (<c>DIAFinal</c>).</summary>
    public bool HasFinalDiagnosis { get; set; }

    /// <summary>The worksheet status chosen (two letters and a name); empty when none.</summary>
    public string? WorksheetStatus { get; set; }

    /// <summary>Who is paid: <c>H</c> hospital, <c>M</c> member.</summary>
    public string? PayTo { get; set; }

    /// <summary>What the invoices say is payable to the hospital (their amounts to pay added up).</summary>
    public decimal InvoicePayTotal { get; set; }

    /// <summary>The worksheet's total payable to the hospital.</summary>
    public decimal HospitalTotal { get; set; }

    /// <summary>The member's bank.</summary>
    public string? Bank { get; set; }

    /// <summary>The member's account number.</summary>
    public string? AccountNo { get; set; }

    /// <summary>The number of digits the bank's accounts have (<c>BankAccounts.AccountDigits</c>); <c>null</c> when the bank has no row.</summary>
    public int? BankAccountDigits { get; set; }

    /// <summary>The member's address, first line.</summary>
    public string? Address1 { get; set; }

    /// <summary>The post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>The city.</summary>
    public string? City { get; set; }

    /// <summary>The state.</summary>
    public string? State { get; set; }
}

/// <summary>The messages of the Registration save, as the desktop words them.</summary>
public static class RegistrationMessages
{
    /// <summary>No case number.</summary>
    public const string NoCase = "Please enter Case Number!";

    /// <summary>No record selected.</summary>
    public const string NoRecord = "Please select a record before proceeding!";

    /// <summary>No invoice.</summary>
    public const string NoInvoice = "Worksheet cannot be saved without any invoice!";

    /// <summary>No final diagnosis.</summary>
    public const string NoFinalDiagnosis = "Worksheet cannot be saved without Final Diagnosis!";

    /// <summary>No worksheet status.</summary>
    public const string NoStatus = "Please select worksheet status!";

    /// <summary>The hospital total is not what the invoices say.</summary>
    public const string HospitalAmount = "Please verify Payable to Hospital amount";

    /// <summary>An account number without a bank.</summary>
    public const string NoBank = "Please Enter Bank Name";

    /// <summary>A bank without an account number.</summary>
    public const string NoAccountNo = "Please Enter Bank Account Number";

    /// <summary>No address.</summary>
    public const string NoAddress = "Please Enter Member's Address";

    /// <summary>No post code.</summary>
    public const string NoPostCode = "Please Enter Post Code";

    /// <summary>No city.</summary>
    public const string NoCity = "Please Enter City";

    /// <summary>No state.</summary>
    public const string NoState = "Please Enter State";

    /// <summary>The account number has not the digits the bank's accounts have.</summary>
    public const string BankAccount = "Please check bank name or account number! ";

    /// <summary>The question asked once the checks pass.</summary>
    public const string AreYouSure = "Are you sure?";
}

/// <summary>
/// The checks the desktop makes before it saves a worksheet (<c>cmdApprove_Click</c> of <c>frmWorksheetRegGST2015</c>; Adjustment's is the same), in its order. The bank account's digit count is checked first
/// and its message shown at once, whatever else is wrong, but it only stops the save when nothing else does (the desktop leaves without a second message then).
/// </summary>
public static class RegistrationChecks
{
    /// <summary>The messages the desktop would show, in the order it shows them; empty when the save may go on to "Are you sure?".</summary>
    public static List<string> Problems(RegistrationEntry entry)
    {
        var messages = new List<string>();
        var member = string.Equals(entry.PayTo?.Trim(), "M", StringComparison.OrdinalIgnoreCase);
        var hospital = string.Equals(entry.PayTo?.Trim(), "H", StringComparison.OrdinalIgnoreCase);
        var bank = !string.IsNullOrWhiteSpace(entry.Bank);
        var account = !string.IsNullOrEmpty(entry.AccountNo);
        var accountWrong = member && bank && account && entry.BankAccountDigits is { } digits && digits != entry.AccountNo!.Length;
        if (accountWrong) messages.Add(RegistrationMessages.BankAccount);

        string? first =
            string.IsNullOrWhiteSpace(entry.CaseNo) ? RegistrationMessages.NoCase :
            string.IsNullOrWhiteSpace(entry.MemberNo) ? RegistrationMessages.NoRecord :
            entry.InvoiceCount == 0 ? RegistrationMessages.NoInvoice :
            !entry.HasFinalDiagnosis ? RegistrationMessages.NoFinalDiagnosis :
            string.IsNullOrEmpty(entry.WorksheetStatus) ? RegistrationMessages.NoStatus :
            hospital && Math.Round(entry.InvoicePayTotal, 2) != Math.Round(entry.HospitalTotal, 2) ? RegistrationMessages.HospitalAmount :
            member && bank && !account ? RegistrationMessages.NoAccountNo :
            member && !bank && account ? RegistrationMessages.NoBank :
            member && string.IsNullOrWhiteSpace(entry.Address1) ? RegistrationMessages.NoAddress :
            member && string.IsNullOrWhiteSpace(entry.PostCode) ? RegistrationMessages.NoPostCode :
            member && string.IsNullOrWhiteSpace(entry.City) ? RegistrationMessages.NoCity :
            member && string.IsNullOrWhiteSpace(entry.State) ? RegistrationMessages.NoState :
            null;
        if (first is not null) messages.Add(first);
        return messages;
    }

    /// <summary>Whether the checks let the save go on to "Are you sure?": nothing wrong, the bank account's digits included.</summary>
    public static bool Passes(RegistrationEntry entry) => Problems(entry).Count == 0;
}
