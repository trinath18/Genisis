using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Invoice;
using Genisis.Api.Worksheet.Adjustment;
using Genisis.Api.Worksheet.Engine;

namespace Genisis.Api.Worksheet.Calculation;

/// <summary>One invoice of the worksheet being registered (a line of <c>lstInvoice</c>).</summary>
public sealed class RegistrationInvoice
{
    /// <summary>The invoice's index.</summary>
    public int Index { get; set; }

    /// <summary>The invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>The invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>The invoice amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>The amount to pay on the invoice (what is payable to the hospital).</summary>
    public decimal PayAmount { get; set; }

    /// <summary>Who is paid: <c>H</c> hospital, <c>M</c> member.</summary>
    public string? PayTo { get; set; }

    /// <summary>The stay type.</summary>
    public string? StayType { get; set; }

    /// <summary>A worksheet has used the invoice already.</summary>
    public bool Used { get; set; }
}

/// <summary>The case with its member, patient, plan and hospital, and what the form copies for a member-paid worksheet.</summary>
public sealed class RegistrationHeader
{
    /// <summary>The case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>The member's number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>The member's name.</summary>
    public string? MemberName { get; set; }

    /// <summary>The patient's cover id (<c>00</c> is the member).</summary>
    public string? PatientId { get; set; }

    /// <summary>The patient's name.</summary>
    public string? PatientName { get; set; }

    /// <summary>The patient's plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>The payor's code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>The hospital's code.</summary>
    public string? HospitalCode { get; set; }

    /// <summary>The hospital's name.</summary>
    public string? HospitalName { get; set; }

    /// <summary>The claimability of the case (<c>AA</c>, <c>RR</c>...).</summary>
    public string? Claimability { get; set; }

    /// <summary>The admission date.</summary>
    public DateTime? AdmissionDate { get; set; }

    /// <summary>The discharge date.</summary>
    public DateTime? DischargeDate { get; set; }

    /// <summary>The case this one is linked to.</summary>
    public string? LinkCase { get; set; }

    /// <summary>The plan has a limit for each disability (<c>Y</c>).</summary>
    public string? PerDisabilityIndicator { get; set; }

    /// <summary>The member is a BTBG member (<c>Y</c>).</summary>
    public string? Btbg { get; set; }

    /// <summary>The member's address, first line.</summary>
    public string? Address1 { get; set; }

    /// <summary>The member's address, second line.</summary>
    public string? Address2 { get; set; }

    /// <summary>The member's address, third line.</summary>
    public string? Address3 { get; set; }

    /// <summary>The member's post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>The member's city.</summary>
    public string? City { get; set; }

    /// <summary>The member's state (its description).</summary>
    public string? State { get; set; }

    /// <summary>The member's bank.</summary>
    public string? Bank { get; set; }

    /// <summary>The member's account number.</summary>
    public string? AccountNo { get; set; }

    /// <summary>How many final diagnoses the case has.</summary>
    public int FinalDiagnosisCount { get; set; }
}

/// <summary>A worksheet status of the drop-down: two letters and its name.</summary>
/// <param name="Code">The two letters (<c>CLMWSStatus</c>).</param>
/// <param name="Name">The text the desktop shows after the letters.</param>
public record RegistrationStatus(string Code, string Name)
{
    /// <summary>The text of the drop-down and of <c>CLMWSStatus</c>'s source: <c>AA - Accepted</c>.</summary>
    public string Text => $"{Code} - {Name}";
}

/// <summary>What the Registration form shows for a case: the case, its invoices, the worksheet worked out for the next version and what the save will need.</summary>
public sealed class RegistrationForm
{
    /// <summary>The case.</summary>
    public RegistrationHeader Case { get; set; } = new();

    /// <summary>The version the save would register (the case's last worksheet plus one).</summary>
    public int Version { get; set; }

    /// <summary>The invoices of that version.</summary>
    public List<RegistrationInvoice> Invoices { get; set; } = [];

    /// <summary>Who is paid, by the first invoice: <c>H</c> hospital, <c>M</c> member; <c>null</c> without an invoice.</summary>
    public string? PayTo { get; set; }

    /// <summary>The Room &amp; Board days of the stay, summed over the invoices.</summary>
    public decimal RoomDays { get; set; }

    /// <summary>The ICU days of the stay, summed over the invoices.</summary>
    public decimal IcuDays { get; set; }

    /// <summary>The 29 rows.</summary>
    public List<CalculationLine> Lines { get; set; } = [];

    /// <summary>The summary the engine calculated; <c>null</c> when the member has no plan to work it out from.</summary>
    public CalculationSummary? Summary { get; set; }

    /// <summary>The payable to the member of the worksheet (negative: the member owes).</summary>
    public decimal DueToMember { get; set; }

    /// <summary>Where the calculation cannot be trusted, in words for the user.</summary>
    public List<string> Notes { get; set; } = [];

    /// <summary>The status the case's claimability suggests (two letters), or empty.</summary>
    public string DefaultStatus { get; set; } = "";

    /// <summary>The payment details and remarks the form starts with (a member-paid worksheet copies the member's).</summary>
    public UpdatePaymentRequest Defaults { get; set; } = new();

    /// <summary>The worksheet statuses.</summary>
    public List<RegistrationStatus> Statuses { get; set; } = [];

    /// <summary>The banks that have accounts.</summary>
    public List<string> Banks { get; set; } = [];

    /// <summary>The states.</summary>
    public List<string> States { get; set; } = [];

    /// <summary>The payment modes.</summary>
    public List<string> PaymentModes { get; set; } = [];

    /// <summary>Why the worksheet cannot be saved yet (a kind the save does not do), or <c>null</c>.</summary>
    public string? Blocked { get; set; }
}

/// <summary>What the user sends with the save button.</summary>
public sealed class RegistrationRequest
{
    /// <summary>The worksheet status chosen (two letters).</summary>
    public string? WorksheetStatus { get; set; }

    /// <summary>The payment details and remarks.</summary>
    public UpdatePaymentRequest Payment { get; set; } = new();

    /// <summary>The user answered yes to "Are you sure?".</summary>
    public bool Confirmed { get; set; }
}

/// <summary>What the save did.</summary>
/// <param name="Saved">The worksheet was written.</param>
/// <param name="Confirm">The question to ask (answering yes sends the request again confirmed); <c>null</c> when none. Nothing was written.</param>
/// <param name="Version">The worksheet version written.</param>
/// <param name="ExcessNumber">The number of the excess letter the save made or moved on, when there is one.</param>
/// <param name="ExcessLetterAdded">The letter was added (the case had none).</param>
/// <param name="MemberBalance">What was done to the member's available limit.</param>
/// <param name="Notes">What the user should know (what the save did not do).</param>
public record RegistrationResult(bool Saved, string? Confirm, int? Version, string? ExcessNumber, bool ExcessLetterAdded, MemberBalanceResult? MemberBalance, List<string> Notes);

/// <summary>Reads what the Registration form shows and the save checks.</summary>
public interface IRegistrationReadRepository
{
    /// <summary>The case, or <c>null</c> when it does not exist.</summary>
    Task<RegistrationHeader?> HeaderAsync(IDbSession session, string caseNo);

    /// <summary>The invoices of a version.</summary>
    Task<List<RegistrationInvoice>> InvoicesAsync(IDbSession session, string caseNo, int version);

    /// <summary>The digits the accounts of a bank have; <c>null</c> when the bank is not listed.</summary>
    Task<int?> BankDigitsAsync(IDbSession session, string bank);

    /// <summary>What the case's versions before this one approved.</summary>
    Task<decimal> ApprovedBeforeAsync(IDbSession session, string caseNo, int version);

    /// <summary>Whether the version has a worksheet row already.</summary>
    Task<bool> RegisteredAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class RegistrationReadRepository : IRegistrationReadRepository
{
    /// <inheritdoc />
    public Task<RegistrationHeader?> HeaderAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcFirstOrDefaultAsync<RegistrationHeader>("genisis.WorksheetReg_Header", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public Task<List<RegistrationInvoice>> InvoicesAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcQueryAsync<RegistrationInvoice>("genisis.WorksheetReg_Invoices", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task<int?> BankDigitsAsync(IDbSession session, string bank) =>
        session.Connection.ProcScalarAsync<int?>("genisis.WorksheetReg_BankDigits", new { bank }, session.Transaction);

    /// <inheritdoc />
    public Task<decimal> ApprovedBeforeAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcScalarAsync<decimal>("genisis.WorksheetReg_ApprovedBefore", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> RegisteredAsync(IDbSession session, string caseNo, int version) =>
        await session.Connection.ProcScalarAsync<int>("genisis.WorksheetReg_Registered", new { cas = caseNo, version }, session.Transaction) == 1;
}

/// <summary>Rules of the Registration form that need no database.</summary>
public static class RegistrationRules
{
    /// <summary>The worksheet statuses of <c>cboWSStatus</c>, in the desktop's order.</summary>
    public static readonly IReadOnlyList<RegistrationStatus> Statuses =
    [
        new("AA", "Accepted"), new("RR", "Repudiated"), new("CC", "Cancelled"), new("SA", "Special Approval"), new("PP", "Pay & Claim"), new("NN", "Not Decided"),
        new("NA", "Not Decided/Accepted"), new("NR", "Not Decided/Repudiated"), new("PA", "Pay&Claim/Accepted"), new("PR", "Pay&Claim/Rejected"), new("AR", "Accepted/Rejected"), new("RA", "Rejected/Accepted"),
    ];

    /// <summary>The note shown when the worksheet leaves a member balance and the save issues no debit or credit note number.</summary>
    public const string NoteNotNumbered = "No debit or credit note number was issued: the web does not number those notes yet.";

    /// <summary>The status of the drop-down for two letters, or <c>null</c> when they are none of the list.</summary>
    public static RegistrationStatus? Status(string? code) => Statuses.FirstOrDefault((s) => string.Equals(s.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Why the save does not do a worksheet of this case yet: a plan with a limit for each disability or a linked case (their previous limits come from other worksheets); <c>null</c> when it can.</summary>
    public static string? Blocked(RegistrationHeader header) =>
        string.Equals(header.PerDisabilityIndicator?.Trim(), "Y", StringComparison.OrdinalIgnoreCase) ? "A plan with a limit for each disability cannot be registered here yet." :
        !string.IsNullOrWhiteSpace(header.LinkCase) ? "A case linked to another case cannot be registered here yet." : null;

    /// <summary>The payment details the form starts with: a member-paid worksheet copies the member's name, address, bank and account (the "Get Address" box); the last document is the latest invoice.</summary>
    public static UpdatePaymentRequest Defaults(RegistrationHeader header, string? payTo, IReadOnlyList<RegistrationInvoice> invoices) => new()
    {
        LastDocumentDate = invoices.Count == 0 ? null : invoices.Max((i) => i.InvoiceDate),
        PayTo = payTo == "M" ? header.MemberName : "",
        Bank = payTo == "M" ? header.Bank : "",
        AccountNo = payTo == "M" ? header.AccountNo : "",
        Address1 = payTo == "M" ? header.Address1 : "",
        Address2 = payTo == "M" ? header.Address2 : "",
        Address3 = payTo == "M" ? header.Address3 : "",
        PostCode = payTo == "M" ? header.PostCode : "",
        City = payTo == "M" ? header.City : "",
        State = payTo == "M" ? header.State : "",
    };

    /// <summary>The entry the desktop's checks read when the save button is pressed.</summary>
    public static RegistrationEntry Entry(RegistrationHeader header, IReadOnlyList<RegistrationInvoice> invoices, WorksheetEvaluation grid, RegistrationRequest request, int? bankDigits) => new()
    {
        CaseNo = header.CaseNo,
        MemberNo = header.MemberNo,
        InvoiceCount = invoices.Count,
        HasFinalDiagnosis = header.FinalDiagnosisCount > 0,
        WorksheetStatus = Status(request.WorksheetStatus)?.Code,
        PayTo = invoices.FirstOrDefault()?.PayTo,
        InvoicePayTotal = invoices.Sum((i) => i.PayAmount),
        HospitalTotal = grid.Totals.ToHospital,
        Bank = request.Payment.Bank,
        AccountNo = request.Payment.AccountNo,
        BankAccountDigits = bankDigits,
        Address1 = request.Payment.Address1,
        PostCode = request.Payment.PostCode,
        City = request.Payment.City,
        State = request.Payment.State,
    };

    /// <summary>The days the plan allows for each benefit that has a day limit (room and board, ICU, physician visits, kidney months, cash and guardian days); none for a benefit without one.</summary>
    public static WorksheetRows.LimitDays AllowedDays(IEnumerable<PlanBenefit> benefits)
    {
        decimal Days(string code) => decimal.TryParse(benefits.FirstOrDefault((b) => b.Code.Trim() == code)?.Days?.Replace(",", ""), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
        return new WorksheetRows.LimitDays(Days("101"), Days("102"), Days("302"), Days("404"), Days("601"), Days("701"));
    }
}

/// <summary>Everything the rows of a worksheet version are built from.</summary>
/// <param name="Header">The case.</param>
/// <param name="Invoices">The invoices of the version.</param>
/// <param name="Calculation">The worksheet worked out.</param>
/// <param name="Benefits">The plan's benefit rows.</param>
/// <param name="Stay">The days of the stay.</param>
/// <param name="Status">The worksheet status chosen.</param>
/// <param name="Payment">The payment details and remarks.</param>
/// <param name="Version">The version to write.</param>
/// <param name="UserCode">Who saves.</param>
/// <param name="Today">The date of the save.</param>
public sealed record RegistrationBuildInput(
    RegistrationHeader Header, IReadOnlyList<RegistrationInvoice> Invoices, WorksheetCalculation Calculation, IReadOnlyList<PlanBenefit> Benefits, StayDays Stay,
    RegistrationStatus Status, UpdatePaymentRequest Payment, int Version, string UserCode, DateTime Today);

/// <summary>Builds the rows the save writes from what the form holds: the same rows the rolled-back tests rebuilt from saved worksheets.</summary>
public static class RegistrationBuilder
{
    /// <summary>The rows of the version.</summary>
    public static WorksheetVersionRows Build(RegistrationBuildInput i)
    {
        var header = i.Header;
        var cas = header.CaseNo.Trim();
        var calc = i.Calculation;
        var grid = calc.Grid;
        var first = i.Invoices.FirstOrDefault();
        var flat = WorksheetRows.Flat(header.PlanCode?.Trim() is { Length: > 0 } plan ? plan[..1] : null, header.PayorCode?.Trim() is { Length: > 1 } payor ? payor[..2] : header.PayorCode);
        var days = PlanLimitRules.LimitDays([.. i.Benefits], i.Stay);
        var btbg = string.Equals(header.Btbg?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
        var payment = i.Payment;

        var claim = ClaimVersionRow.Build(new ClaimVersionInput
        {
            CaseNo = cas,
            Version = i.Version,
            UserCode = i.UserCode,
            Today = i.Today,
            WorksheetStatus = i.Status.Text,
            PayToCode = first?.PayTo,
            StayType = first?.StayType,
            PayTo = payment.PayTo?.Trim() ?? "",
            Bank = payment.Bank?.Trim() ?? "",
            AccountNo = payment.AccountNo?.Trim() ?? "",
            GpName = payment.GpName?.Trim() ?? "",
            LastDocumentDate = payment.LastDocumentDate ?? i.Invoices.Max((x) => x.InvoiceDate),
            MemberBtbg = btbg,
            Limit = LimitKind.Annual,
            AvailableLimit = calc.PreviousBalance,
        }, grid, calc.Final);

        var others = ClaimOthersRow.Build(new ClaimOthersInput
        {
            CaseNo = cas,
            Version = i.Version,
            WorksheetRemarks = payment.WorksheetRemarks?.Trim(),
            NoteRemarks = payment.CreditDebitNoteRemarks?.Trim(),
            Note = payment.Note?.Trim(),
            Address1 = payment.Address1?.Trim(),
            Address2 = payment.Address2?.Trim(),
            Address3 = payment.Address3?.Trim(),
            PostCode = payment.PostCode?.Trim(),
            City = payment.City?.Trim(),
            State = payment.State?.Trim(),
            PaymentMode = payment.PaymentMode?.Trim(),
            Attention = payment.Attention?.Trim(),
        });

        var hospitalInvoice = i.Invoices.FirstOrDefault((x) => string.Equals(x.PayTo?.Trim(), "H", StringComparison.OrdinalIgnoreCase));
        var letter = ExcessLetterRow.Build(new ExcessLetterInput
        {
            CaseNo = cas,
            UserCode = i.UserCode,
            Today = i.Today,
            InvoiceNo = hospitalInvoice?.InvoiceNo,
            HospitalInvoiceAmount = hospitalInvoice?.Amount,
            MemberBtbg = btbg,
        }, grid, calc.Final);

        return new WorksheetVersionRows(
            claim, others, WorksheetRows.Limits(grid, flat, days),
            WorksheetRows.Build(grid, flat, WorksheetRows.Kind.Actual), WorksheetRows.ActualGst(flat),
            WorksheetRows.Build(grid, flat, WorksheetRows.Kind.Approved), WorksheetRows.Build(grid, flat, WorksheetRows.Kind.Hospital), WorksheetRows.Build(grid, flat, WorksheetRows.Kind.Member),
            PayMbmStatusRow.Build(grid, flat), letter,
            new SameLimitInput(SameLimitRow.Limits(grid, flat), RegistrationRules.AllowedDays(i.Benefits), days));
    }
}

/// <summary>Checks the shape of the registration request: the payment details' lengths and payment mode (the checks of the desktop come later, in its order).</summary>
public class RegistrationRequestValidator : AbstractValidator<RegistrationRequest>
{
    /// <summary>Sets up the rules.</summary>
    public RegistrationRequestValidator() => RuleFor(r => r.Payment).NotNull().SetValidator(new UpdatePaymentRequestValidator());
}

/// <summary>
/// Worksheet > Worksheet Registration (legacy <c>frmWorksheetRegGST2015</c>): the form for the next worksheet of a case, and its save (<c>cmdApprove_Click</c>): the checks in the desktop's order, "Are you sure?",
/// then every row of the version in one transaction. The save does not number debit and credit notes yet, and refuses what it cannot do: a plan with a limit for each disability, a linked case, a top-up member,
/// a lifetime plan, a take-over case, a kind D limit.
/// </summary>
public class RegistrationService(
    IDbSessions sessions, IRegistrationReadRepository read, IInvoiceEntryRepository invoices, IPlanBenefitRepository benefits, IStayDaysRepository stay, IWorksheetCalculator calculator,
    ICalculationRepository calculation, IAdjustmentRepository adjustment, IRegistrationRepository writer, TimeProvider time)
{
    private const string NotFound = "Record not found!";

    private sealed record Loaded(RegistrationHeader Header, int Version, List<RegistrationInvoice> Invoices, WorksheetCalculation? Calculation, List<PlanBenefit> Benefits, StayDays Stay);

    private async Task<Loaded> LoadAsync(IDbSession session, string cas)
    {
        var header = await read.HeaderAsync(session, cas) ?? throw new NotFoundException(NotFound);
        var version = await invoices.NextVersionAsync(session, cas);
        var lines = await read.InvoicesAsync(session, cas, version);
        var calc = await calculator.CalculateAsync(cas, version, await read.ApprovedBeforeAsync(session, cas, version));
        return new Loaded(header, version, lines, calc, await benefits.BenefitsAsync(session, cas, version), await stay.DaysAsync(session, cas, version));
    }

    /// <summary>The form for the next worksheet of a case. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task<RegistrationForm> PrepareAsync(string caseNo)
    {
        var cas = caseNo.Trim();
        await using var session = await sessions.OpenAsync();
        var loaded = await LoadAsync(session, cas);
        var payTo = loaded.Invoices.FirstOrDefault()?.PayTo?.Trim();
        var form = new RegistrationForm
        {
            Case = loaded.Header, Version = loaded.Version, Invoices = loaded.Invoices, PayTo = payTo,
            DefaultStatus = RegistrationRules.Status(loaded.Header.Claimability)?.Code ?? "",
            Defaults = RegistrationRules.Defaults(loaded.Header, payTo, loaded.Invoices),
            Statuses = [.. RegistrationRules.Statuses],
            Banks = await adjustment.BanksAsync(session), States = await adjustment.StatesAsync(session), PaymentModes = [.. AdjustmentRules.PaymentModes],
            Blocked = RegistrationRules.Blocked(loaded.Header),
        };
        if (loaded.Calculation is not { } calc) return form;

        var captions = (await calculation.CaptionsAsync(session)).ToDictionary((c) => c.Slot);
        form.RoomDays = calc.RoomDays;
        form.IcuDays = calc.IcuDays;
        form.Lines = CalculationRules.Lines(calc.Grid, captions, new Dictionary<int, string?>());
        form.Summary = new CalculationSummary(calc.Final.SumApproved, calc.Final.CoPayment, calc.Final.LimitExceeded, calc.Final.TotalApproved, calc.PreviousBalance);
        form.DueToMember = calc.Grid.Totals.ToMember;
        form.Notes = CalculationRules.Notes(form.Lines, calc.PlanHasRows, calc.ConventionSlots);
        if (form.Blocked is null && loaded.Invoices.Count > 0 && RegistrationRules.Status(loaded.Header.Claimability) is { } status)
        {
            var rows = RegistrationBuilder.Build(new RegistrationBuildInput(loaded.Header, loaded.Invoices, calc, loaded.Benefits, loaded.Stay, status, form.Defaults, loaded.Version, "", time.GetLocalNow().DateTime));
            if (await writer.CheckMemberBalanceAsync(session, rows) is { Unsupported: true } balance) form.Blocked = $"This worksheet cannot be registered here yet: {balance.Reason}.";
        }
        return form;
    }

    /// <summary>
    /// The save of the Register button. The checks of the desktop run first and the first problem is thrown as a <see cref="BusinessRuleException"/> (the bank account's digit message too, when it comes with another).
    /// Without <c>Confirmed</c> the question "Are you sure?" comes back and nothing is written; with it the whole version is written in one transaction.
    /// </summary>
    public async Task<RegistrationResult> SaveAsync(string caseNo, RegistrationRequest request, string userCode)
    {
        var cas = caseNo.Trim();
        Loaded loaded;
        int? digits = null;
        await using (var session = await sessions.OpenAsync())
        {
            loaded = await LoadAsync(session, cas);
            if (!string.IsNullOrWhiteSpace(request.Payment.Bank) && !string.IsNullOrWhiteSpace(request.Payment.AccountNo)) digits = await read.BankDigitsAsync(session, request.Payment.Bank.Trim());
        }
        var grid = loaded.Calculation?.Grid ?? new WorksheetEvaluation([], new WorksheetTotals(0m, 0m, 0m, 0m));
        var problems = RegistrationChecks.Problems(RegistrationRules.Entry(loaded.Header, loaded.Invoices, grid, request, digits));
        if (problems.Count > 0) throw new BusinessRuleException(string.Join("\n", problems));
        if (RegistrationRules.Blocked(loaded.Header) is { } blocked) throw new BusinessRuleException(blocked);
        var calc = loaded.Calculation ?? throw new NotFoundException(NotFound);
        var status = RegistrationRules.Status(request.WorksheetStatus)!;
        if (!request.Confirmed) return new RegistrationResult(false, RegistrationMessages.AreYouSure, null, null, false, null, []);

        var rows = RegistrationBuilder.Build(new RegistrationBuildInput(loaded.Header, loaded.Invoices, calc, loaded.Benefits, loaded.Stay, status, request.Payment, loaded.Version, userCode, time.GetLocalNow().DateTime));
        await using var write = await sessions.BeginAsync();
        if (await read.RegisteredAsync(write, cas, loaded.Version)) throw new ConflictException("This worksheet has already been registered.");
        if (await writer.CheckMemberBalanceAsync(write, rows) is { Unsupported: true } balance) throw new BusinessRuleException($"This worksheet cannot be registered here yet: {balance.Reason}.");
        var result = await writer.WriteVersionAsync(write, rows);
        await write.CommitAsync();
        List<string> notes = calc.Grid.Totals.ToMember != 0m ? [RegistrationRules.NoteNotNumbered] : [];
        return new RegistrationResult(true, null, loaded.Version, result.ExcessNumber, result.ExcessLetterAdded, result.MemberBalance, notes);
    }
}
