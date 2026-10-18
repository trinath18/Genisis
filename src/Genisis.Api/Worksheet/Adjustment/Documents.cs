using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet.Enquiry;

namespace Genisis.Api.Worksheet.Adjustment;

/// <summary>A credit or debit note of a worksheet version: what the desktop's CreditNote / DebitNote Excel templates were filled with.</summary>
public record WorksheetNote
{
    /// <summary><c>Credit</c> (Medix owes the member), <c>Debit</c> (the member owes Medix) or <c>None</c> (nothing is due either way).</summary>
    public string Kind { get; set; } = NoteRules.None;

    /// <summary>Credit / debit note number; empty when none has been given yet (a credit note has the number it was given at registration).</summary>
    public string Number { get; set; } = "";

    /// <summary>Date the number was given.</summary>
    public DateTime? Date { get; set; }

    /// <summary>The amount, always positive: the credit due to the member or the debit due from the member.</summary>
    public decimal Amount { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Worksheet version.</summary>
    public int Version { get; set; }

    /// <summary>Patient name.</summary>
    public string PatientName { get; set; } = "";

    /// <summary>Member number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Member (principal) name.</summary>
    public string MemberName { get; set; } = "";

    /// <summary>Group company.</summary>
    public string GroupCompany { get; set; } = "";

    /// <summary>True when the plan is billed to the company: the debit note is then addressed to the group company (the desktop's DebitNoteCompany template).</summary>
    public bool CompanyPlan { get; set; }

    /// <summary>Hospital.</summary>
    public string Hospital { get; set; } = "";

    /// <summary>Admission date.</summary>
    public DateTime? Admitted { get; set; }

    /// <summary>Disability: the final diagnoses, one per line.</summary>
    public string Disability { get; set; } = "";

    /// <summary>The member's address, line 1.</summary>
    public string Address1 { get; set; } = "";

    /// <summary>The member's address, line 2.</summary>
    public string Address2 { get; set; } = "";

    /// <summary>The member's address, line 3.</summary>
    public string Address3 { get; set; } = "";

    /// <summary>Post code.</summary>
    public string PostCode { get; set; } = "";

    /// <summary>City.</summary>
    public string City { get; set; } = "";

    /// <summary>State.</summary>
    public string State { get; set; } = "";

    /// <summary>Credit / debit note remarks.</summary>
    public string Remarks { get; set; } = "";
}

/// <summary>The member's address and plan billing as read for the notes.</summary>
public record NoteMemberRow
{
    /// <summary>Address line 1.</summary>
    public string? Address1 { get; set; }

    /// <summary>Address line 2.</summary>
    public string? Address2 { get; set; }

    /// <summary>Address line 3.</summary>
    public string? Address3 { get; set; }

    /// <summary>Post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>The plan is billed to the company.</summary>
    public bool CompanyPlan { get; set; }
}

/// <summary>The result of giving a debit note its number.</summary>
public record DebitNoNumber
{
    /// <summary>0 ok, 1 the worksheet version does not exist.</summary>
    public int Result { get; set; }

    /// <summary>The number.</summary>
    public string? Number { get; set; }
}

/// <summary>One amount line of the Excess letter.</summary>
/// <param name="Label">What the amount is for.</param>
/// <param name="Amount">The amount the member has to pay.</param>
public record ExcessItem(string Label, decimal Amount);

/// <summary>The Excess letter of a case: what the desktop's EXCLetter Word templates were filled with.</summary>
public record ExcessLetter
{
    /// <summary>Which template the desktop used: <c>MAS</c>, <c>Etiqa</c> or <c>Medix</c>.</summary>
    public string Template { get; set; } = ExcessRules.Medix;

    /// <summary>Excess number.</summary>
    public string ExcessNo { get; set; } = "";

    /// <summary>Date the excess was recorded.</summary>
    public DateTime? ExcessDate { get; set; }

    /// <summary>Invoice number.</summary>
    public string InvoiceNo { get; set; } = "";

    /// <summary>Invoice amount.</summary>
    public decimal InvoiceAmount { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Member (principal) name.</summary>
    public string PrincipalName { get; set; } = "";

    /// <summary>Patient name.</summary>
    public string PatientName { get; set; } = "";

    /// <summary>Patient I/C number.</summary>
    public string PatientIc { get; set; } = "";

    /// <summary>Plan code.</summary>
    public string PlanCode { get; set; } = "";

    /// <summary>Staff number.</summary>
    public string StaffNo { get; set; } = "";

    /// <summary>Cost center.</summary>
    public string CostCenter { get; set; } = "";

    /// <summary>Department.</summary>
    public string Department { get; set; } = "";

    /// <summary>Bordereaux number.</summary>
    public string BordxNo { get; set; } = "";

    /// <summary>Admission date.</summary>
    public DateTime? Admitted { get; set; }

    /// <summary>Discharge date.</summary>
    public DateTime? Discharged { get; set; }

    /// <summary>Hospital the letter is addressed to.</summary>
    public string Hospital { get; set; } = "";

    /// <summary>Hospital contact (attention).</summary>
    public string HospitalContact { get; set; } = "";

    /// <summary>Hospital fax number.</summary>
    public string HospitalFax { get; set; } = "";

    /// <summary>The amounts the member has to pay, for the items that have one.</summary>
    public List<ExcessItem> Items { get; set; } = [];

    /// <summary>Limit exceeded.</summary>
    public decimal LimitExceeded { get; set; }

    /// <summary>Co-payment (Room &amp; Board).</summary>
    public decimal CoPayment { get; set; }

    /// <summary>Excess undertaken (underwriting excess).</summary>
    public decimal UndertakenExcess { get; set; }

    /// <summary>Total excess: the items, the limit exceeded, the co-payment and the underwriting excess; zero is not shown.</summary>
    public decimal TotalExcess { get; set; }

    /// <summary>The description typed for the Others amount.</summary>
    public string OthersDescription { get; set; } = "";

    /// <summary>Date of issue (today).</summary>
    public DateTime IssuedOn { get; set; }
}

/// <summary>The excess record of a case as read from the database.</summary>
public record ExcessRow
{
    /// <summary>Excess number.</summary>
    public string? ExcessNo { get; set; }

    /// <summary>Date of the record.</summary>
    public DateTime? ExcessDate { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice amount.</summary>
    public decimal InvoiceAmount { get; set; }

    /// <summary>Room &amp; Board.</summary>
    public decimal RoomAndBoard { get; set; }

    /// <summary>Intensive care.</summary>
    public decimal Icu { get; set; }

    /// <summary>Hospital supplies and services.</summary>
    public decimal Services { get; set; }

    /// <summary>Operating theatre.</summary>
    public decimal Theatre { get; set; }

    /// <summary>Surgical fees.</summary>
    public decimal Surgical { get; set; }

    /// <summary>Anaesthetist's fee.</summary>
    public decimal Anaesthetist { get; set; }

    /// <summary>Physician's visit.</summary>
    public decimal Physician { get; set; }

    /// <summary>Ambulance.</summary>
    public decimal Ambulance { get; set; }

    /// <summary>Government hospitals income benefit.</summary>
    public decimal Tax { get; set; }

    /// <summary>Child's guardian allowance.</summary>
    public decimal Guardian { get; set; }

    /// <summary>Medical report.</summary>
    public decimal MedicalReport { get; set; }

    /// <summary>Admin / registration.</summary>
    public decimal Admin { get; set; }

    /// <summary>Service tax on Room &amp; Board.</summary>
    public decimal RoomAndBoardTax { get; set; }

    /// <summary>Service tax on ICU.</summary>
    public decimal IcuTax { get; set; }

    /// <summary>Service tax on the lodger.</summary>
    public decimal LodgerTax { get; set; }

    /// <summary>Others.</summary>
    public decimal Others { get; set; }

    /// <summary>Description of Others.</summary>
    public string? OthersDescription { get; set; }

    /// <summary>Limit exceeded.</summary>
    public decimal LimitExceeded { get; set; }

    /// <summary>Co-payment.</summary>
    public decimal CoPayment { get; set; }

    /// <summary>Excess undertaken.</summary>
    public decimal UndertakenExcess { get; set; }

    /// <summary>Hospital contact.</summary>
    public string? HospitalContact { get; set; }

    /// <summary>Hospital fax.</summary>
    public string? HospitalFax { get; set; }

    /// <summary>Staff number.</summary>
    public string? StaffNo { get; set; }

    /// <summary>Cost center.</summary>
    public string? CostCenter { get; set; }

    /// <summary>Department.</summary>
    public string? Department { get; set; }

    /// <summary>Patient I/C.</summary>
    public string? PatientIc { get; set; }

    /// <summary>The plan's excess letter type: 1 MAS, 2 Etiqa, otherwise Medix.</summary>
    public string? ExcessTemplate { get; set; }
}

/// <summary>Rules of the credit / debit note.</summary>
public static class NoteRules
{
    /// <summary>Medix owes the member.</summary>
    public const string Credit = "Credit";

    /// <summary>The member owes Medix.</summary>
    public const string Debit = "Debit";

    /// <summary>Nothing is due either way.</summary>
    public const string None = "None";

    /// <summary>
    /// The amount due to (positive) or from (negative) the member, from the figures saved with the worksheet: the payable to the member when there is one, otherwise the
    /// excess. The desktop worked this out live (totalFMem); the saved figures agree with the note numbers it gave in about 93 per cent of the versions that have one.
    /// </summary>
    public static decimal Due(EnquirySummary s) => s.PayableToMember != 0 ? s.PayableToMember : s.TotalExcess;

    /// <summary>cmdDebit / cmdCredit are enabled by the sign of the amount: a debit note when the member owes, a credit note when Medix owes.</summary>
    public static string KindOf(decimal due) => due < 0 ? Debit : due > 0 ? Credit : None;

    /// <summary>The final diagnoses, one per line, as the Disability box showed them.</summary>
    public static string Disability(IEnumerable<EnquiryDiagnosis> diagnoses) =>
        string.Join('\n', diagnoses.Where(d => d.Source == "Final").OrderBy(d => d.Slot).Select(d => d.Diagnosis?.Trim() ?? "").Where(t => t.Length > 0));
}

/// <summary>Rules of the Excess letter.</summary>
public static class ExcessRules
{
    /// <summary>The Medix template.</summary>
    public const string Medix = "Medix";

    /// <summary>The template of plans whose excess private type is 1 (the desktop's EXCLetterMAS).</summary>
    public const string Mas = "MAS";

    /// <summary>The template of plans whose excess private type is 2 (ExcessLetterEtiqa).</summary>
    public const string Etiqa = "Etiqa";

    /// <summary>Which template the plan's excess private type selects.</summary>
    public static string TemplateOf(string? excessPrivate) => excessPrivate?.Trim() switch { "1" => Mas, "2" => Etiqa, _ => Medix };

    /// <summary>The items that have an amount, in the letter's order.</summary>
    public static List<ExcessItem> Items(ExcessRow r) =>
        new List<ExcessItem>
        {
            new("Room & Board", r.RoomAndBoard), new("Intensive Care Unit", r.Icu), new("Hospital Supplies & Services", r.Services), new("Operating Theatre", r.Theatre),
            new("Surgical Fees", r.Surgical), new("Anaesthetist's Fee", r.Anaesthetist), new("Daily In-Hospital Physician's Visit", r.Physician), new("Ambulance Fees", r.Ambulance),
            new("Govt. Hospitals Income Benefit", r.Tax), new("Child's Guardian Allowance", r.Guardian), new("Medical Report", r.MedicalReport), new("Admin / Registration", r.Admin),
            new("Service Tax - R&B", r.RoomAndBoardTax), new("Service Tax - ICU", r.IcuTax), new("Service Tax - Lodger", r.LodgerTax),
            new(string.IsNullOrWhiteSpace(r.OthersDescription) ? "Others" : r.OthersDescription.Trim(), r.Others),
        }.Where(i => i.Amount != 0).ToList();

    /// <summary>The total the letter shows: every item, the limit exceeded, the co-payment and the underwriting excess.</summary>
    public static decimal Total(ExcessRow r) => Items(r).Sum(i => i.Amount) + r.LimitExceeded + r.CoPayment + r.UndertakenExcess;
}

/// <summary>Reads and writes what the notes and the Excess letter need.</summary>
public interface IDocumentsRepository
{
    /// <summary>The member's address and plan billing; <c>null</c> when the worksheet's case has no member.</summary>
    Task<NoteMemberRow?> NoteMemberAsync(IDbSession session, string caseNo, int version);

    /// <summary>Gives a debit note its number when it has none; returns the number, or <c>null</c> when the version does not exist.</summary>
    Task<string?> AssignDebitNoAsync(IDbSession session, string caseNo, int version);

    /// <summary>The excess record of a case; <c>null</c> when it has none.</summary>
    Task<ExcessRow?> ExcessAsync(IDbSession session, string caseNo, int version);

    /// <summary>Marks the case as having had its Excess letter.</summary>
    Task MarkExcessLetterAsync(IDbSession session, string caseNo);
}

/// <inheritdoc />
public sealed class DocumentsRepository : IDocumentsRepository
{
    /// <inheritdoc />
    public Task<NoteMemberRow?> NoteMemberAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<NoteMemberRow>("genisis.WorksheetNote_Member", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public async Task<string?> AssignDebitNoAsync(IDbSession session, string caseNo, int version)
    {
        var r = await session.Connection.ProcFirstOrDefaultAsync<DebitNoNumber>("genisis.WorksheetNote_AssignDebitNo", new { cas = caseNo, version }, session.Transaction);
        return r is { Result: StoredProcedures.Ok } ? r.Number : null;
    }

    /// <inheritdoc />
    public Task<ExcessRow?> ExcessAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<ExcessRow>("genisis.WorksheetExcess_Get", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task MarkExcessLetterAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcScalarAsync<int>("genisis.WorksheetExcess_MarkIssued", new { cas = caseNo }, session.Transaction);
}

/// <summary>Worksheet > Worksheet Adjustment: the credit / debit note and the Excess letter (cmdCredit, cmdDebit, cmdExcLetter).</summary>
public class WorksheetDocumentsService(IDbSessions sessions, IEnquiryRepository enquiry, IDocumentsRepository repository, ILogger<WorksheetDocumentsService> log)
{
    private async Task<WorksheetNote> BuildNoteAsync(IDbSession session, string cas, int version)
    {
        var header = await enquiry.HeaderAsync(session, cas, version) ?? throw new NotFoundException("Record not found!");
        var summary = await enquiry.SummaryAsync(session, cas, version, version);
        var others = await enquiry.OthersAsync(session, cas, version);
        var member = await repository.NoteMemberAsync(session, cas, version);
        var due = NoteRules.Due(summary);
        static string T(string? s) => s?.Trim() ?? "";
        return new WorksheetNote
        {
            Kind = NoteRules.KindOf(due), Number = T(header.DebitCreditRefNo), Date = header.DebitCreditDate, Amount = Math.Abs(due),
            CaseNo = T(header.CaseNo), Version = version, PatientName = T(header.PatientName), MemberNo = T(header.MemberNo), MemberName = T(header.MemberName),
            GroupCompany = T(header.GroupCompany), CompanyPlan = member?.CompanyPlan == true, Hospital = T(header.Hospital), Admitted = header.Admitted,
            Disability = NoteRules.Disability(await enquiry.DiagnosesAsync(session, cas)),
            Address1 = T(member?.Address1), Address2 = T(member?.Address2), Address3 = T(member?.Address3), PostCode = T(member?.PostCode),
            City = T(others?.City), State = T(others?.State), Remarks = T(others?.CreditDebitRemarks),
        };
    }

    /// <summary>The credit / debit note of a version, as the desktop's buttons would offer it (the kind follows the sign of the amount due). Nothing is written.</summary>
    public async Task<WorksheetNote> NoteAsync(string caseNo, int version)
    {
        if (version < 1) throw new WorksheetException("Please select a version nombor!");
        await using var session = await sessions.OpenAsync();
        return await BuildNoteAsync(session, caseNo.Trim(), version);
    }

    /// <summary>
    /// cmdDebit_Click: the debit note of a version; a worksheet that has no debit note number gets the next one of its payor, saved with today's date. Only a worksheet
    /// that has an amount due from the member has a debit note.
    /// </summary>
    public async Task<WorksheetNote> DebitNoteAsync(string caseNo, int version, string userCode)
    {
        if (version < 1) throw new WorksheetException("Please select a version nombor!");
        var cas = caseNo.Trim();
        await using var session = await sessions.BeginAsync();
        var note = await BuildNoteAsync(session, cas, version);
        if (note.Kind != NoteRules.Debit) throw new WorksheetException("A debit note is for an amount due from the member; there is none on this worksheet.");
        if (note.Number == "")
        {
            var number = await repository.AssignDebitNoAsync(session, cas, version) ?? throw new NotFoundException("Record not found!");
            log.LogInformation("Debit note {Number} given to worksheet {Case} version {Version} by {User}", number, cas, version, userCode);
            note = await BuildNoteAsync(session, cas, version);
        }
        await session.CommitAsync();
        return note;
    }

    /// <summary>
    /// cmdExcLetter_Click: the Excess letter of a case, from its excess record, and the case is marked as having had it. A case with no excess record has no letter (the
    /// desktop opened an empty letter and marked the case anyway).
    /// </summary>
    public async Task<ExcessLetter> ExcessLetterAsync(string caseNo, int version, string userCode)
    {
        if (version < 1) throw new WorksheetException("Please select a version nombor!");
        var cas = caseNo.Trim();
        await using var session = await sessions.BeginAsync();
        var row = await repository.ExcessAsync(session, cas, version) ?? throw new NotFoundException("There is no excess record for this case.");
        var header = await enquiry.HeaderAsync(session, cas, version) ?? throw new NotFoundException("Record not found!");
        await repository.MarkExcessLetterAsync(session, cas);
        await session.CommitAsync();
        log.LogInformation("Excess letter of {Case} made by {User}", cas, userCode);
        static string T(string? s) => s?.Trim() ?? "";
        return new ExcessLetter
        {
            Template = ExcessRules.TemplateOf(row.ExcessTemplate), ExcessNo = T(row.ExcessNo), ExcessDate = row.ExcessDate, InvoiceNo = T(row.InvoiceNo), InvoiceAmount = row.InvoiceAmount,
            CaseNo = T(header.CaseNo), PrincipalName = T(header.MemberName), PatientName = T(header.PatientName), PatientIc = T(row.PatientIc), PlanCode = T(header.PlanCode),
            StaffNo = T(row.StaffNo), CostCenter = T(row.CostCenter), Department = T(row.Department), BordxNo = T(header.BordxRefNo), Admitted = header.Admitted, Discharged = header.Discharged,
            Hospital = T(header.Hospital), HospitalContact = T(row.HospitalContact), HospitalFax = T(row.HospitalFax),
            Items = ExcessRules.Items(row), LimitExceeded = row.LimitExceeded, CoPayment = row.CoPayment, UndertakenExcess = row.UndertakenExcess, TotalExcess = ExcessRules.Total(row),
            OthersDescription = T(row.OthersDescription), IssuedOn = DateTime.Today,
        };
    }
}
