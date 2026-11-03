using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Receipt.Member;

/// <summary>A debit note of a member and the receipt of one payment of it (a line of <c>lstReceiptMBM</c>).</summary>
public record ReceiptMemberRow(
    string? DebitNo, string? PaymentStatus, string? CaseNo, int ClaimVersion, decimal? PayableByMedix, decimal? ReceiptAmt, int? ReceiptIndex, string? ReceiptNo, DateTime? ReceiptDate, decimal? AccMonth, decimal? AccYear,
    decimal? BankCharge, string? ContraCaseNo, string? ContraVersionNo, decimal? ContraCaseAmt, decimal? GainLoss, decimal? Misc, decimal? Discount, decimal? AmtPaid, decimal? WriteOff, decimal? MedixReceipt, string? Remarks,
    string? MemberNo, string? MemberName, string? PayorCode);

/// <summary>The list, the received total and whether the user is "authorised".</summary>
public record ReceiptMemberSearchResult(List<ReceiptMemberRow> Rows, decimal ReceivedAmount, bool Authorised, bool Truncated);

/// <summary>The criteria of the search.</summary>
public class ReceiptMemberFilter
{
    /// <summary>Use Scan: one worksheet (case) number.</summary>
    public bool Scan { get; set; }

    /// <summary>Case No (from).</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Case No to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Mem No (any part).</summary>
    public string? MemberNo { get; set; }

    /// <summary>Mem Name (any part).</summary>
    public string? MemberName { get; set; }

    /// <summary>Payor (code or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Payment Status: 0 All, 1 Paid, 2 Unpaid, 3 Partial Paid, 4 Paid and Partial Paid, 5 Unpaid and Partial Paid.</summary>
    public int Status { get; set; }

    /// <summary>Recp No from.</summary>
    public string? RecFrom { get; set; }

    /// <summary>Recp No to.</summary>
    public string? RecTo { get; set; }

    /// <summary>Only the rows with no receipt number.</summary>
    public bool RecEmpty { get; set; }

    /// <summary>Recp Date from.</summary>
    public DateTime? RecDateFrom { get; set; }

    /// <summary>Recp Date to.</summary>
    public DateTime? RecDateTo { get; set; }

    /// <summary>DR No from.</summary>
    public string? DrFrom { get; set; }

    /// <summary>DR No to.</summary>
    public string? DrTo { get; set; }

    /// <summary>Only the rows with no DR number.</summary>
    public bool DrEmpty { get; set; }

    /// <summary>DR Amt from.</summary>
    public decimal? AmtFrom { get; set; }

    /// <summary>DR Amt to.</summary>
    public decimal? AmtTo { get; set; }

    /// <summary>Only the rows with no DR amount.</summary>
    public bool AmtEmpty { get; set; }

    /// <summary>A/C Period Mth from.</summary>
    public int? MthFrom { get; set; }

    /// <summary>A/C Period Mth to.</summary>
    public int? MthTo { get; set; }

    /// <summary>Only the rows with no period month.</summary>
    public bool MthEmpty { get; set; }

    /// <summary>A/C Period Yr from.</summary>
    public int? YrFrom { get; set; }

    /// <summary>A/C Period Yr to.</summary>
    public int? YrTo { get; set; }

    /// <summary>Only the rows with no period year.</summary>
    public bool YrEmpty { get; set; }
}

/// <summary>What is received for one ticked claim (the staged line).</summary>
public class ReceiptMemberItem
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public int Version { get; set; }

    /// <summary>F full or P partial.</summary>
    public string? Status { get; set; }

    /// <summary>Amount receiving.</summary>
    public decimal? PaymentAmt { get; set; }

    /// <summary>Bank Chrg.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Clm Gain/Loss.</summary>
    public decimal? GainLoss { get; set; }

    /// <summary>Misc. Diff.</summary>
    public decimal? Misc { get; set; }

    /// <summary>Discount.</summary>
    public decimal? Discount { get; set; }

    /// <summary>Amt Paid.</summary>
    public decimal? AmtPaid { get; set; }

    /// <summary>Write Off.</summary>
    public decimal? WriteOff { get; set; }

    /// <summary>Medix Rec.</summary>
    public decimal? MedixReceipt { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Contra Case.</summary>
    public string? ContraCase { get; set; }

    /// <summary>Contra version.</summary>
    public string? ContraVersion { get; set; }

    /// <summary>Contra Amt.</summary>
    public decimal? ContraAmt { get; set; }
}

/// <summary>The receipt and the ticked claims (cmdSave_Click).</summary>
public class ReceiptMemberSaveRequest
{
    /// <summary>Receipt No.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt Date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Amount (the cheque amount).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque No.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Receipt remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>A/C Period Mth.</summary>
    public int? AccMonth { get; set; }

    /// <summary>A/C Period Year.</summary>
    public int? AccYear { get; set; }

    /// <summary>The ticked claims.</summary>
    public List<ReceiptMemberItem> Items { get; set; } = [];
}

/// <summary>What Edit then Update changes on one receipt row (CmdUpdate_Click).</summary>
public class ReceiptMemberUpdateRequest
{
    /// <summary>The receipt row (ReceiptIndex).</summary>
    public int? ReceiptIndex { get; set; }

    /// <summary>Receipt No.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt Date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Amount (cheque amount).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque No.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Receipt remarks.</summary>
    public string? ReceiptRemarks { get; set; }

    /// <summary>A/C Period Mth.</summary>
    public int? AccMonth { get; set; }

    /// <summary>A/C Period Year.</summary>
    public int? AccYear { get; set; }

    /// <summary>Bank Chrg.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Clm Gain/Loss.</summary>
    public decimal? GainLoss { get; set; }

    /// <summary>Misc. Diff.</summary>
    public decimal? Misc { get; set; }

    /// <summary>Discount.</summary>
    public decimal? Discount { get; set; }

    /// <summary>Amt Paid.</summary>
    public decimal? AmtPaid { get; set; }

    /// <summary>Write Off.</summary>
    public decimal? WriteOff { get; set; }

    /// <summary>Medix Rec.</summary>
    public decimal? MedixReceipt { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Contra Case.</summary>
    public string? ContraCase { get; set; }

    /// <summary>Contra version.</summary>
    public string? ContraVersion { get; set; }

    /// <summary>Contra Amt.</summary>
    public decimal? ContraAmt { get; set; }
}

/// <summary>One receipt row as it stands, for the entry boxes.</summary>
public record ReceiptMemberReceipt(string? ReceiptNo, DateTime? ReceiptDate, decimal? ReceiptAmt, string? ChequeNo, string? ReceiptRemarks, decimal? AccMonth, decimal? AccYear, string? CaseNo, int ClaimVersion,
    decimal? BankCharge, string? ContraCaseNo, string? ContraVersionNo, decimal? ContraCaseAmt, decimal? GainLoss, decimal? Misc, decimal? Discount, decimal? AmtPaid, decimal? WriteOff, decimal? MedixReceipt, string? Remarks);

/// <summary>How a claim stands in the view (the look-ups of ProcessCheckedItem).</summary>
public record ReceiptMemberClaim(string? CaseNo, int ClaimVersion, decimal? PayableByMedix, string? DebitNo, int? ReceiptIndex, string? ReceiptNo, decimal? ReceiptAmt, string? PaymentStatus);

/// <summary>A line of the official receipt.</summary>
public record ReceiptMemberOfficialLine(string? CaseNo, string? ClaimVersion, string? ReceiptNo, DateTime? ReceiptDate, string? HealthType, string? ChequeNo, decimal? ReceiptAmt, string? DebitNo, string? MemberName, string? PatientName);

/// <summary>The messages of the screen.</summary>
public static class ReceiptMemberMessages
{
    /// <summary>cmdSearch_Click, Use Scan.</summary>
    public const string EnterWorksheet = "Please Enter Worksheet No";

    /// <summary>cmdSave_Click.</summary>
    public const string EnterReceiptNo = "Please Enter Receipt No";

    /// <summary>cmdSave_Click.</summary>
    public const string EnterReceiptDate = "Please Enter Receipt Date";

    /// <summary>cmdSave_Click.</summary>
    public const string EnterAmount = "Please Enter Amount";

    /// <summary>cmdSave_Click.</summary>
    public const string EnterChequeNo = "Please Enter Cheque No";

    /// <summary>CmdAdd_Click.</summary>
    public const string InvalidContra = "Invalid Contra Case..., Please Keyin Contra Case No. or Contra Version ";

    /// <summary>CmdAdd_Click.</summary>
    public const string EnterContraAmount = "Please Keyin Contra Case Amount ";

    /// <summary>ProcessCheckedItem.</summary>
    public const string PaymentMade = "Payment has been made!";

    /// <summary>lstReceiptMBM_Click.</summary>
    public const string PaymentNotMade = "Payment hasn't been made!";

    /// <summary>CmdUpdate_Click.</summary>
    public const string SelectRecord = "Please select the record";
}

/// <summary>cmdSearch_Click's check: Use Scan needs a worksheet number; the payment status is one of the six choices.</summary>
public sealed class ReceiptMemberFilterValidator : RequestValidator<ReceiptMemberFilter>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptMemberFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.Scan || !string.IsNullOrWhiteSpace(f.CaseFrom)).WithMessage(ReceiptMemberMessages.EnterWorksheet);
        RuleFor(f => f.Status).InclusiveBetween(0, 5).WithMessage("Select a Payment Status.");
    }
}

/// <summary>The checks of one ticked claim, those of CmdAdd_Click and the status the tick gives.</summary>
public sealed class ReceiptMemberItemValidator : RequestValidator<ReceiptMemberItem>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptMemberItemValidator()
    {
        RuleFor(i => i.CaseNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Select the claims to be received.");
        RuleFor(i => i.Status).Must(s => s is "F" or "P").WithMessage("The payment of a claim is Full or Partial.");
        RuleFor(i => i).Must(i => i.PaymentAmt is > 0).WithMessage("Please enter the amount receiving for the claim.");
        RuleFor(i => i).Must(i => string.IsNullOrWhiteSpace(i.ContraCase) == string.IsNullOrWhiteSpace(i.ContraVersion)).WithMessage(ReceiptMemberMessages.InvalidContra);
        RuleFor(i => i).Must(i => string.IsNullOrWhiteSpace(i.ContraCase) || i.ContraAmt is not null).WithMessage(ReceiptMemberMessages.EnterContraAmount);
    }
}

/// <summary>cmdSave_Click's checks, in the desktop's order.</summary>
public sealed class ReceiptMemberSaveRequestValidator : RequestValidator<ReceiptMemberSaveRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptMemberSaveRequestValidator()
    {
        RuleFor(r => r.ReceiptNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(ReceiptMemberMessages.EnterReceiptNo);
        RuleFor(r => r.ReceiptDate).Must(d => d is not null).WithMessage(ReceiptMemberMessages.EnterReceiptDate);
        RuleFor(r => r.Amount).Must(a => a is not null).WithMessage(ReceiptMemberMessages.EnterAmount);
        RuleFor(r => r.ChequeNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage(ReceiptMemberMessages.EnterChequeNo);
        RuleFor(r => r.Items).Must(i => i.Count > 0).WithMessage("Please tick the claims to be received.");
        RuleForEach(r => r.Items).SetValidator(new ReceiptMemberItemValidator());
    }
}

/// <summary>CmdUpdate_Click's checks, in the desktop's order.</summary>
public sealed class ReceiptMemberUpdateRequestValidator : RequestValidator<ReceiptMemberUpdateRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptMemberUpdateRequestValidator()
    {
        RuleFor(r => r.ReceiptIndex).Must(i => i is not null).WithMessage(ReceiptMemberMessages.SelectRecord);
        RuleFor(r => r.ReceiptNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Please Enter Receipt No!");
        RuleFor(r => r.ReceiptDate).Must(d => d is not null).WithMessage(ReceiptMemberMessages.EnterReceiptDate);
        RuleFor(r => r.ChequeNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Cheque No!");
        RuleFor(r => r.Amount).Must(a => a is not null).WithMessage("Please Enter Cheque Amt!");
        RuleFor(r => r).Must(i => string.IsNullOrWhiteSpace(i.ContraCase) == string.IsNullOrWhiteSpace(i.ContraVersion)).WithMessage(ReceiptMemberMessages.InvalidContra);
        RuleFor(r => r).Must(i => string.IsNullOrWhiteSpace(i.ContraCase) || i.ContraAmt is not null).WithMessage(ReceiptMemberMessages.EnterContraAmount);
    }
}

/// <summary>Reads the debit notes and writes the receipts.</summary>
public interface IReceiptMemberRepository
{
    /// <summary>The debit notes that match (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<ReceiptMemberRow>> SearchAsync(IDbSession session, ReceiptMemberFilter filter);

    /// <summary>Whether the user is authorised (USR.AuthorisedStatus is not N).</summary>
    Task<bool> AuthorisedAsync(IDbSession session, string userCode);

    /// <summary>The rows of a claim (of one receipt number when given).</summary>
    Task<List<ReceiptMemberClaim>> ClaimAsync(IDbSession session, string caseNo, int version, string? receiptNo);

    /// <summary>Writes the receipt of one claim (and its contra allocation).</summary>
    Task<int> SaveAsync(IDbSession session, ReceiptMemberSaveRequest receipt, ReceiptMemberItem item, string userCode, bool authorised);

    /// <summary>One receipt row (null when there is none).</summary>
    Task<ReceiptMemberReceipt?> RowAsync(IDbSession session, int receiptIndex);

    /// <summary>Changes one receipt row and its contra allocation (false when the row is gone).</summary>
    Task<bool> UpdateAsync(IDbSession session, ReceiptMemberUpdateRequest request, string userCode);

    /// <summary>The lines of an official receipt.</summary>
    Task<List<ReceiptMemberOfficialLine>> OfficialReceiptAsync(IDbSession session, string receiptNo);
}

/// <inheritdoc />
public sealed class ReceiptMemberRepository : IReceiptMemberRepository
{
    private static string? Text(string? value) => WorksheetLimits.Text(value);

    /// <inheritdoc />
    public Task<List<ReceiptMemberRow>> SearchAsync(IDbSession session, ReceiptMemberFilter f) =>
        session.Connection.ProcQueryAsync<ReceiptMemberRow>("genisis.ReceiptMember_Search", new
        {
            caseFrom = Text(f.CaseFrom)?.ToUpperInvariant(), caseTo = (f.Scan ? Text(f.CaseFrom) : Text(f.CaseTo))?.ToUpperInvariant(), memberNo = Text(f.MemberNo), memberName = Text(f.MemberName),
            payor = Text(f.Payor)?.Split('-')[0].Trim(), status = f.Status, recFrom = Text(f.RecFrom), recTo = Text(f.RecTo), recEmpty = f.RecEmpty, recDateFrom = WorksheetLimits.Day(f.RecDateFrom),
            recDateTo = WorksheetLimits.Day(f.RecDateTo), drFrom = Text(f.DrFrom), drTo = Text(f.DrTo), drEmpty = f.DrEmpty, amtFrom = f.AmtFrom, amtTo = f.AmtTo, amtEmpty = f.AmtEmpty, mthFrom = f.MthFrom, mthTo = f.MthTo,
            mthEmpty = f.MthEmpty, yrFrom = f.YrFrom, yrTo = f.YrTo, yrEmpty = f.YrEmpty, max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> AuthorisedAsync(IDbSession session, string userCode) => await session.Connection.ProcScalarAsync<bool>("genisis.ReceiptMember_UserAuthorised", new { userCode }, session.Transaction);

    /// <inheritdoc />
    public Task<List<ReceiptMemberClaim>> ClaimAsync(IDbSession session, string caseNo, int version, string? receiptNo) =>
        session.Connection.ProcQueryAsync<ReceiptMemberClaim>("genisis.ReceiptMember_Receipt", new { cas = caseNo, version, receiptNo }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SaveAsync(IDbSession session, ReceiptMemberSaveRequest r, ReceiptMemberItem i, string userCode, bool authorised) =>
        await session.Connection.ProcScalarAsync<int>("genisis.ReceiptMember_Save", new
        {
            cas = i.CaseNo!.Trim().ToUpperInvariant(), version = i.Version, status = i.Status, paymentAmt = i.PaymentAmt, receiptNo = r.ReceiptNo!.Trim(), receiptDate = r.ReceiptDate!.Value.Date, chequeNo = r.ChequeNo!.Trim(),
            receiptRemarks = Text(r.Remarks), accMonth = r.AccMonth, accYear = r.AccYear, bankCharge = i.BankCharge ?? 0m, gainLoss = i.GainLoss ?? 0m, misc = i.Misc ?? 0m, discount = i.Discount ?? 0m, amtPaid = i.AmtPaid ?? 0m,
            writeOff = i.WriteOff ?? 0m, medixReceipt = i.MedixReceipt ?? 0m, remarks = Text(i.Remarks), contraCase = Text(i.ContraCase), contraVersion = Text(i.ContraVersion), contraAmt = i.ContraAmt, userCode, authorised,
            today = DateTime.Today,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<ReceiptMemberReceipt?> RowAsync(IDbSession session, int receiptIndex) =>
        session.Connection.ProcFirstOrDefaultAsync<ReceiptMemberReceipt>("genisis.ReceiptMember_Row", new { receiptIndex }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(IDbSession session, ReceiptMemberUpdateRequest r, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.ReceiptMember_Update", new
        {
            receiptIndex = r.ReceiptIndex, receiptNo = r.ReceiptNo!.Trim(), receiptDate = r.ReceiptDate!.Value.Date, receiptAmt = r.Amount, chequeNo = r.ChequeNo!.Trim(), receiptRemarks = Text(r.ReceiptRemarks), accMonth = r.AccMonth,
            accYear = r.AccYear, bankCharge = r.BankCharge, gainLoss = r.GainLoss, misc = r.Misc, discount = r.Discount, amtPaid = r.AmtPaid, writeOff = r.WriteOff, medixReceipt = r.MedixReceipt, remarks = Text(r.Remarks),
            contraCase = Text(r.ContraCase), contraVersion = Text(r.ContraVersion), contraAmt = r.ContraAmt, userCode, today = DateTime.Today,
        }, session.Transaction) == 1;

    /// <inheritdoc />
    public Task<List<ReceiptMemberOfficialLine>> OfficialReceiptAsync(IDbSession session, string receiptNo) =>
        session.Connection.ProcQueryAsync<ReceiptMemberOfficialLine>("genisis.ReceiptMember_OfficialReceipt", new { receiptNo }, session.Transaction);
}

/// <summary>Receipt > From Member (legacy <c>FrmReceiptMember</c>, "Receipt From Member").</summary>
public class ReceiptMemberService(IDbSessions sessions, IReceiptMemberRepository repository, ILogger<ReceiptMemberService> log)
{
    /// <summary>
    /// cmdSearch_Click: the debit notes of members who owe money with the receipt of each payment. Nothing found is a <see cref="NotFoundException"/> ("No record found!").
    /// <see cref="ReceiptMemberSearchResult.Authorised"/> tells the screen which ticking rule the user has.
    /// </summary>
    public async Task<ReceiptMemberSearchResult> SearchAsync(ReceiptMemberFilter filter, string userCode)
    {
        await using var session = await sessions.OpenAsync();
        var capped = WorksheetLimits.Cap(await repository.SearchAsync(session, filter));
        if (capped.Rows.Count == 0) throw new NotFoundException("No record found!");
        return new ReceiptMemberSearchResult(capped.Rows, capped.Rows.Sum(r => r.ReceiptAmt ?? 0m), await repository.AuthorisedAsync(session, userCode), capped.Truncated);
    }

    /// <summary>
    /// cmdSave_Click, UpdateRecord and UpdateContra for each ticked claim, in one transaction. A user who is not authorised cannot receive a claim that is paid in full already ("Payment has been made!"); the
    /// claim must be a debit note of a member (it is in the list the screen shows).
    /// </summary>
    public async Task<int> SaveAsync(ReceiptMemberSaveRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var authorised = await repository.AuthorisedAsync(session, userCode);
        foreach (var item in request.Items)
        {
            var rows = await repository.ClaimAsync(session, item.CaseNo!.Trim().ToUpperInvariant(), item.Version, null);
            var head = rows.FirstOrDefault() ?? throw new NotFoundException($"Claim {item.CaseNo}-{item.Version} not found");
            if ((head.DebitNo ?? "").StartsWith('D') is false || head.PayableByMedix > 0) throw new BusinessRuleException($"Claim {item.CaseNo}-{item.Version} is not a debit note of a member.");
            if (!authorised && rows.Any(r => r.PaymentStatus == "F")) throw new ConflictException(ReceiptMemberMessages.PaymentMade);
            await repository.SaveAsync(session, request, item, userCode, authorised);
        }

        await session.CommitAsync();
        log.LogInformation("Receipt {Receipt} of member debit notes saved for {Count} claim(s) by {User}", request.ReceiptNo, request.Items.Count, userCode);
        return request.Items.Count;
    }

    /// <summary>lstReceiptMBM_Click (Edit): one receipt row for the entry boxes; a row with no payment is "Payment hasn't been made!".</summary>
    public async Task<ReceiptMemberReceipt> ReceiptAsync(int receiptIndex)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.RowAsync(session, receiptIndex) ?? throw new NotFoundException(ReceiptMemberMessages.PaymentNotMade);
    }

    /// <summary>CmdUpdate_Click: changes one receipt row and its contra allocation in one transaction.</summary>
    public async Task UpdateAsync(ReceiptMemberUpdateRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (!await repository.UpdateAsync(session, request, userCode)) throw new NotFoundException(ReceiptMemberMessages.PaymentNotMade);
        await session.CommitAsync();
        log.LogInformation("Receipt row {Index} of member debit notes updated by {User}", request.ReceiptIndex, userCode);
    }

    /// <summary>cmdPrintPV_Click: the lines of the official receipt of a receipt number.</summary>
    public async Task<List<ReceiptMemberOfficialLine>> OfficialReceiptAsync(string? receiptNo)
    {
        if (string.IsNullOrWhiteSpace(receiptNo)) throw new BusinessRuleException("Please Enter or Generate Receipt No");
        await using var session = await sessions.OpenAsync();
        var lines = await repository.OfficialReceiptAsync(session, receiptNo.Trim());
        return lines.Count == 0 ? throw new NotFoundException("No record found!") : lines;
    }
}
