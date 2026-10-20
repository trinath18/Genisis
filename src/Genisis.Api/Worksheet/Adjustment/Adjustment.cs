using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Adjustment;

/// <summary>The fields the Update button of Worksheet Adjustment saves: the payment details and the remarks of one worksheet version.</summary>
public record UpdatePaymentRequest
{
    /// <summary>Last document date; left as it is when not given.</summary>
    public DateTime? LastDocumentDate { get; set; }

    /// <summary>Pay To.</summary>
    public string? PayTo { get; set; }

    /// <summary>GP name.</summary>
    public string? GpName { get; set; }

    /// <summary>Bank name (one of the banks that have accounts).</summary>
    public string? Bank { get; set; }

    /// <summary>Bank account number.</summary>
    public string? AccountNo { get; set; }

    /// <summary>Payment mode: BY POST or BANK-IN.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>Attention.</summary>
    public string? Attention { get; set; }

    /// <summary>Member's address, line 1.</summary>
    public string? Address1 { get; set; }

    /// <summary>Member's address, line 2.</summary>
    public string? Address2 { get; set; }

    /// <summary>Member's address, line 3.</summary>
    public string? Address3 { get; set; }

    /// <summary>Post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>City.</summary>
    public string? City { get; set; }

    /// <summary>State.</summary>
    public string? State { get; set; }

    /// <summary>Note.</summary>
    public string? Note { get; set; }

    /// <summary>Worksheet remarks.</summary>
    public string? WorksheetRemarks { get; set; }

    /// <summary>Credit / debit note remarks.</summary>
    public string? CreditDebitNoteRemarks { get; set; }
}

/// <summary>The drop-down lists of the Adjustment screen.</summary>
/// <param name="Banks">Banks that have accounts.</param>
/// <param name="States">States.</param>
/// <param name="PaymentModes">Payment modes.</param>
public record AdjustmentLookups(List<string> Banks, List<string> States, List<string> PaymentModes);

/// <summary>A lookup value as read from the database.</summary>
public record NameRow
{
    /// <summary>The name.</summary>
    public string Name { get; set; } = "";
}

/// <summary>A worksheet's pay-to as read from the database.</summary>
public record PayToRow
{
    /// <summary>H paid to the hospital, M paid to the member.</summary>
    public string? PayTo { get; set; }
}

/// <summary>Rules of the Adjustment screen's Update button.</summary>
public static class AdjustmentRules
{
    /// <summary>The payment modes of cboPaymentMode.</summary>
    public static readonly string[] PaymentModes = ["BY POST", "BANK-IN"];

    /// <summary>Pay-to code of an invoice paid to the member (reimbursement).</summary>
    public const string PaidToMember = "M";

    /// <summary>Trims a text field the way the screen's checks read it; nothing becomes the empty string.</summary>
    public static string Clean(string? value) => value?.Trim() ?? "";

    /// <summary>
    /// cmdUpdate_Click's checks, in the desktop's order. They apply only to a worksheet paid to the member: the bank and the account number go together, and the
    /// member's address, post code, city and state are needed. Returns the first message, or <c>null</c> when the details can be saved.
    /// </summary>
    public static string? PaymentProblem(string? payTo, UpdatePaymentRequest r)
    {
        if (!string.Equals(payTo?.Trim(), PaidToMember, StringComparison.OrdinalIgnoreCase)) return null;
        var bank = Clean(r.Bank);
        var account = Clean(r.AccountNo);
        if (bank != "" && account == "") return "Please Enter Bank Account Number";
        if (bank == "" && account != "") return "Please Enter Bank Name";
        if (Clean(r.Address1) == "") return "Please Enter Member's Address";
        if (Clean(r.PostCode) == "") return "Please Enter Post Code";
        if (Clean(r.City) == "") return "Please Enter City";
        if (Clean(r.State) == "") return "Please Enter State";
        return null;
    }
}

/// <summary>Checks the shape of the Update request: the lengths the database columns hold and the payment mode list.</summary>
public class UpdatePaymentRequestValidator : AbstractValidator<UpdatePaymentRequest>
{
    /// <summary>Sets up the rules.</summary>
    public UpdatePaymentRequestValidator()
    {
        void Max(System.Linq.Expressions.Expression<Func<UpdatePaymentRequest, string?>> field, string name, int length) =>
            RuleFor(field).Must(v => Clean(v).Length <= length).WithMessage($"{name} can have at most {length} characters.");

        RuleFor(r => r.PaymentMode).Must(v => Clean(v) == "" || AdjustmentRules.PaymentModes.Contains(Clean(v))).WithMessage("Payment Mode must be BY POST or BANK-IN.");
        Max(r => r.PayTo, "Pay To", 50);
        Max(r => r.GpName, "GP", 50);
        Max(r => r.Bank, "Bank", 40);
        Max(r => r.AccountNo, "A/C No", 30);
        Max(r => r.Attention, "Attention", 60);
        Max(r => r.Address1, "Address 1", 50);
        Max(r => r.Address2, "Address 2", 50);
        Max(r => r.Address3, "Address 3", 50);
        Max(r => r.PostCode, "Post Code", 6);
        Max(r => r.City, "City", 50);
        Max(r => r.State, "State", 50);
        Max(r => r.Note, "Note", 100);
        Max(r => r.WorksheetRemarks, "Worksheet remarks", 200);
        Max(r => r.CreditDebitNoteRemarks, "Credit/Debit Note Remarks", 200);
    }

    private static string Clean(string? value) => AdjustmentRules.Clean(value);
}

/// <summary>Reads and writes what the Adjustment screen needs.</summary>
public interface IAdjustmentRepository
{
    /// <summary>The pay-to of the last invoice of a version, or <c>null</c> when it has none.</summary>
    Task<string?> PayToAsync(IDbSession session, string caseNo, int version);

    /// <summary>The banks that have accounts.</summary>
    Task<List<string>> BanksAsync(IDbSession session);

    /// <summary>The states.</summary>
    Task<List<string>> StatesAsync(IDbSession session);

    /// <summary>Saves the payment details and remarks of a version; returns <see cref="StoredProcedures.Ok"/> or <see cref="StoredProcedures.NotFound"/>.</summary>
    Task<int> UpdatePaymentAsync(IDbSession session, string caseNo, int version, UpdatePaymentRequest request);
}

/// <inheritdoc />
public sealed class AdjustmentRepository : IAdjustmentRepository
{
    /// <inheritdoc />
    public async Task<string?> PayToAsync(IDbSession session, string caseNo, int version) =>
        (await session.Connection.ProcQueryAsync<PayToRow>("genisis.WorksheetAdjustment_PayTo", new { cas = caseNo, version }, session.Transaction)).FirstOrDefault()?.PayTo;

    /// <inheritdoc />
    public async Task<List<string>> BanksAsync(IDbSession session) =>
        (await session.Connection.ProcQueryAsync<NameRow>("genisis.WorksheetAdjustment_Banks", null, session.Transaction)).Select(r => r.Name).ToList();

    /// <inheritdoc />
    public async Task<List<string>> StatesAsync(IDbSession session) =>
        (await session.Connection.ProcQueryAsync<NameRow>("genisis.WorksheetAdjustment_States", null, session.Transaction)).Select(r => r.Name).ToList();

    /// <inheritdoc />
    public Task<int> UpdatePaymentAsync(IDbSession session, string caseNo, int version, UpdatePaymentRequest request)
    {
        static string C(string? v) => AdjustmentRules.Clean(v);
        return session.Connection.ProcScalarAsync<int>("genisis.WorksheetAdjustment_UpdatePayment", new
        {
            cas = caseNo, version, lastDocDate = request.LastDocumentDate?.Date,
            payTo = C(request.PayTo), gpName = C(request.GpName), bank = C(request.Bank), accountNo = C(request.AccountNo),
            paymentMode = C(request.PaymentMode), attention = C(request.Attention),
            address1 = C(request.Address1), address2 = C(request.Address2), address3 = C(request.Address3),
            postCode = C(request.PostCode), city = C(request.City), state = C(request.State),
            note = C(request.Note), wsRemarks = C(request.WorksheetRemarks), cdRemarks = C(request.CreditDebitNoteRemarks),
        }, session.Transaction);
    }
}

/// <summary>Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013): the Update button. Viewing a worksheet is the Enquiry's work.</summary>
public class WorksheetAdjustmentService(IDbSessions sessions, IAdjustmentRepository repository, ILogger<WorksheetAdjustmentService> log)
{
    /// <summary>The drop-down lists: banks, states and payment modes.</summary>
    public async Task<AdjustmentLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return new AdjustmentLookups(await repository.BanksAsync(session), await repository.StatesAsync(session), [.. AdjustmentRules.PaymentModes]);
    }

    /// <summary>
    /// cmdUpdate_Click: saves the payment details and remarks of one version in a transaction. Throws <see cref="WorksheetException"/> with the desktop's message when a
    /// worksheet paid to the member lacks bank, account, address, post code, city or state, and <see cref="NotFoundException"/> when the version does not exist.
    /// </summary>
    public async Task UpdatePaymentAsync(string caseNo, int version, UpdatePaymentRequest request, string userCode)
    {
        var cas = caseNo.Trim();
        if (version < 1) throw new WorksheetException("Please select a version nombor!");
        await using var session = await sessions.BeginAsync();
        var problem = AdjustmentRules.PaymentProblem(await repository.PayToAsync(session, cas, version), request);
        if (problem is not null) throw new WorksheetException(problem);
        if (await repository.UpdatePaymentAsync(session, cas, version, request) == StoredProcedures.NotFound) throw new NotFoundException("Record not found!");
        await session.CommitAsync();
        log.LogInformation("Worksheet {Case} version {Version} payment details saved by {User}", cas, version, userCode);
    }
}
