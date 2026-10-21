using Genisis.Api.Common;

namespace Genisis.Api.Invoice;

/// <summary>An invoice as the Invoice Processing form holds it: what is typed, and what a saved invoice is loaded as.</summary>
public record InvoiceForm
{
    /// <summary>The case the invoice belongs to.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>The invoice's key when an invoice is changed; <c>null</c> when one is added.</summary>
    public int? Index { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Who is paid: <c>H</c> hospital, <c>M</c> member.</summary>
    public string? PayTo { get; set; }

    /// <summary>The hospital's code when the hospital is paid, the payee as typed when the member is.</summary>
    public string? Payee { get; set; }

    /// <summary>Stay type code.</summary>
    public string? StayType { get; set; }

    /// <summary>Room type.</summary>
    public string? RoomType { get; set; }

    /// <summary>Invoice receipt mode code.</summary>
    public string? ReceiptMode { get; set; }

    /// <summary>Invoice receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Follow-up date.</summary>
    public DateTime? FollowUpDate { get; set; }

    /// <summary>Excess amount.</summary>
    public decimal? ExcessAmount { get; set; }

    /// <summary>The initial invoice amount.</summary>
    public decimal? InitialAmount { get; set; }

    /// <summary>UHC number.</summary>
    public string? UhcNo { get; set; }

    /// <summary>The amounts, days and breakdown, by column name (see <see cref="InvoiceFields.Form"/>).</summary>
    public Dictionary<string, decimal?> Amounts { get; set; } = [];

    /// <summary>The text entries and the Y / N ticks, by column name.</summary>
    public Dictionary<string, string?> Texts { get; set; } = [];

    /// <summary>The remark kept for the case (one per case).</summary>
    public string? Remarks { get; set; }

    /// <summary>Whether the user has agreed to pay a hospital other than the case's.</summary>
    public bool ConfirmDifferentHospital { get; set; }
}

/// <summary>An invoice loaded to be changed, with its worked-out totals and why it may not be changed.</summary>
/// <param name="Form">The invoice as the form holds it.</param>
/// <param name="Calculation">The totals worked out from it.</param>
/// <param name="InWorksheet">Whether a worksheet has used it.</param>
/// <param name="Bordxed">Whether it has been bordxed.</param>
public record InvoiceFormData(InvoiceForm Form, InvoiceCalculation Calculation, bool InWorksheet, bool Bordxed);

/// <summary>The outcome of a save.</summary>
/// <param name="Index">The invoice's key; <c>null</c> when nothing was saved because a question has to be answered first.</param>
/// <param name="Confirm">The question to ask the user (answering yes sends the invoice again with the confirmation); <c>null</c> when none.</param>
/// <param name="Warning">A reminder to show after the save.</param>
/// <param name="Calculation">The totals that were saved.</param>
public record InvoiceSaveResult(int? Index, string? Confirm, string? Warning, InvoiceCalculation? Calculation);

/// <summary>A code and its name, for a drop-down.</summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public record InvoiceOption(string Code, string? Name);

/// <summary>The drop-down lists of the form.</summary>
/// <param name="StayTypes">Stay types.</param>
/// <param name="ReceiptModes">Invoice receipt modes.</param>
/// <param name="RoomTypes">Room types.</param>
/// <param name="Hospitals">Hospitals the invoice can be paid to.</param>
public record InvoiceLookups(List<InvoiceOption> StayTypes, List<InvoiceOption> ReceiptModes, List<string> RoomTypes, List<InvoiceOption> Hospitals);

/// <summary>The messages of the Invoice Processing form, as the desktop words them.</summary>
public static class InvoiceMessages
{
    /// <summary>No case, or a case without a member.</summary>
    public const string NoCase = "Please select a record before proceeding! ";

    /// <summary>The case has no discharge date.</summary>
    public const string NoDischarge = "Please Enter Date of Discharge! ";

    /// <summary>Nobody to be paid was chosen.</summary>
    public const string NoPayTo = "Please select who to be paid (Member / Hospital )! ";

    /// <summary>No stay type.</summary>
    public const string NoStayType = "Please select a Stay Type! ";

    /// <summary>No payee.</summary>
    public const string NoPayee = "Please key in the person/hospital to be paid! ";

    /// <summary>No invoice number.</summary>
    public const string NoInvoiceNo = "Please key in Invoice No.! ";

    /// <summary>No invoice date.</summary>
    public const string NoInvoiceDate = "Please key in Invoice date! ";

    /// <summary>The member is paid and no receipt date was given.</summary>
    public const string NoReceiptDate = "Please key in Invoice Receipt date! ";

    /// <summary>The member is paid and no receipt mode was given.</summary>
    public const string NoReceiptMode = "Please key in Invoice Receipt Mode! ";

    /// <summary>The hospital is paid and no excess amount was given.</summary>
    public const string NoExcess = "Please key in Excess Amount! ";

    /// <summary>No initial invoice amount.</summary>
    public const string NoInitialAmount = "Please key in Initial Invoice Amount! ";

    /// <summary>A generic provider was chosen and its name was not typed.</summary>
    public const string NoProviderName = "Please Enter Hospital Name or Clinic Name!";

    /// <summary>The hospital is not the case's: the question to answer before saving.</summary>
    public const string DifferentHospital = "Different Hospital. Do you want to Proceed? ";

    /// <summary>The case already has an invoice with this number.</summary>
    public const string DuplicateInvoice = "Current Invoice No has been used before! PLease re-verify the invoice.";

    /// <summary>The invoice has been bordxed and cannot be changed.</summary>
    public const string Bordxed = "Invoice has been bordx!";

    /// <summary>A worksheet has used the invoice and it cannot be removed.</summary>
    public const string UsedInWorksheet = "Selected Invoice cannot be deleted!\r\nIt has been used in Worksheet!";

    /// <summary>A worksheet has used the invoice: the desktop lets it be changed and then saves the worksheet's limits too; that is not migrated.</summary>
    public const string ChangeInWorksheet = "This invoice has been used in a worksheet. Changing it is not available yet: the desktop also saves the worksheet's limits then.";

    /// <summary>No case was chosen to add an invoice to.</summary>
    public const string SelectCaseFirst = "Please select the case number first!";
}

/// <summary>The checks the desktop's <c>SaveClaim</c> makes, in its order, as a function of what was typed and the case.</summary>
public static class InvoiceChecks
{
    /// <summary>The provider names that stand for a kind of provider; the name of the actual provider has to be typed beside them.</summary>
    public static readonly string[] GenericProviders =
        ["DENTAL CLINIC", "DIALYSIS CENTRE", "FOREIGN HOSPITAL", "GENERAL PRACTITIONER", "GOVERNMENT HOSPITAL", "NON-PANEL HOSPITAL", "SPECIALIST CLINIC"];

    /// <summary>The first check that fails, or <c>null</c>. The check that the case already has the invoice number needs the database and is made after these.</summary>
    /// <param name="form">What was typed.</param>
    /// <param name="header">The case; <c>null</c> when it is not there.</param>
    /// <param name="payeeName">The name of the payee hospital when the hospital is paid.</param>
    public static string? First(InvoiceForm form, InvoiceCaseHeader? header, string? payeeName)
    {
        var hospital = form.PayTo == "H";
        var texts = form.Texts;
        if (header is null || string.IsNullOrWhiteSpace(form.CaseNo) || string.IsNullOrWhiteSpace(header.MemberName)) return InvoiceMessages.NoCase;
        if (header.Discharged is null) return InvoiceMessages.NoDischarge;
        if (form.PayTo is not ("H" or "M")) return InvoiceMessages.NoPayTo;
        if (string.IsNullOrWhiteSpace(form.StayType)) return InvoiceMessages.NoStayType;
        if (string.IsNullOrWhiteSpace(form.Payee)) return InvoiceMessages.NoPayee;
        if (string.IsNullOrWhiteSpace(form.InvoiceNo)) return InvoiceMessages.NoInvoiceNo;
        if (form.InvoiceDate is null) return InvoiceMessages.NoInvoiceDate;
        if (!hospital && form.ReceiptDate is null) return InvoiceMessages.NoReceiptDate;
        if (!hospital && string.IsNullOrWhiteSpace(form.ReceiptMode)) return InvoiceMessages.NoReceiptMode;
        if (hospital && form.ExcessAmount is null) return InvoiceMessages.NoExcess;
        if (form.InitialAmount is null) return InvoiceMessages.NoInitialAmount;
        if (hospital && payeeName is not null && GenericProviders.Contains(payeeName.Trim().ToUpperInvariant()) && string.IsNullOrWhiteSpace(texts.GetValueOrDefault("INVHospName"))) return InvoiceMessages.NoProviderName;
        return null;
    }

    /// <summary>Whether the user has to be asked: the hospital to be paid is not the case's hospital and has not been confirmed.</summary>
    public static bool NeedsHospitalConfirmation(InvoiceForm form, InvoiceCaseHeader header) =>
        form.PayTo == "H" && !form.ConfirmDifferentHospital && !string.Equals((form.Payee ?? "").Trim(), (header.HospitalCode ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The first problem with the amounts and texts sent: a column the form does not have, a tick that is not Y or N, a text longer than its column. Negative amounts are allowed: saved invoices have credits.</summary>
    public static string? Fields(InvoiceForm form)
    {
        foreach (var (key, value) in form.Amounts)
        {
            if (!InvoiceFields.AmountColumns.Contains(key)) return $"Unknown field {key}.";
        }
        foreach (var (key, value) in form.Texts)
        {
            if (InvoiceFields.FlagColumns.Contains(key)) { if (value is not (null or "Y" or "N")) return $"{key} must be Y or N."; continue; }
            if (!InvoiceFields.TextColumns.Contains(key)) return $"Unknown field {key}.";
            if (value is not null && value.Length > InvoiceFields.TextLength[key]) return $"{key} is longer than {InvoiceFields.TextLength[key]} characters.";
        }
        if ((form.InvoiceNo?.Length ?? 0) > 50 || (form.Payee?.Length ?? 0) > 50 || (form.UhcNo?.Length ?? 0) > 10 || (form.Remarks?.Length ?? 0) > 500) return "A value is longer than its field.";
        return null;
    }
}
