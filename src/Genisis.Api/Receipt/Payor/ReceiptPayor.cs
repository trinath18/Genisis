using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Receipt.Payor;

/// <summary>A bordx and what the payor has paid for it (a line of <c>lstBordx</c>).</summary>
public sealed class PayorBordxRow
{
    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>The cheque amount of the receipt.</summary>
    public decimal? ReceiptAmt { get; set; }

    /// <summary>Accounting period month.</summary>
    public decimal? AccMonth { get; set; }

    /// <summary>Accounting period year.</summary>
    public decimal? AccYear { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Payment voucher date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Payment voucher number.</summary>
    public string? PvNo { get; set; }

    /// <summary>The check amount: what the receipt covers of the bordx.</summary>
    public decimal? CheckAmt { get; set; }

    /// <summary>Bordx status: F fully received, P partly.</summary>
    public string? BordxStatus { get; set; }

    /// <summary>Receipt remark.</summary>
    public string? Remark { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Receipt number prefix (RCInd).</summary>
    public string? ReceiptPrefix { get; set; }

    /// <summary>The amount of the bordx.</summary>
    public decimal? BordxAmount { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>The sum of the check amounts received for the bordx.</summary>
    public decimal? Received { get; set; }
}

/// <summary>A claim and what the payor has paid for it (a line of <c>lstWorksheet</c>).</summary>
public sealed class PayorWorksheetRow
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>The amount of the claim (approved).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>The cheque amount of the receipt.</summary>
    public decimal? ReceiptAmt { get; set; }

    /// <summary>Accounting period month.</summary>
    public decimal? AccMonth { get; set; }

    /// <summary>Accounting period year.</summary>
    public decimal? AccYear { get; set; }

    /// <summary>The check amount: what this receipt row covers of the claim.</summary>
    public decimal? CheckAmt { get; set; }

    /// <summary>Claim status: F fully received, P partly.</summary>
    public string? ClaimStatus { get; set; }

    /// <summary>Bank charge.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Contra case number.</summary>
    public string? ContraCaseNo { get; set; }

    /// <summary>Contra version.</summary>
    public string? ContraVersionNo { get; set; }

    /// <summary>Contra amount.</summary>
    public decimal? ContraCaseAmt { get; set; }

    /// <summary>Gain or loss.</summary>
    public decimal? GainLoss { get; set; }

    /// <summary>Misc. difference.</summary>
    public decimal? Misc { get; set; }

    /// <summary>Discount.</summary>
    public decimal? Discount { get; set; }

    /// <summary>Write off.</summary>
    public decimal? WriteOff { get; set; }

    /// <summary>Medix receipt.</summary>
    public decimal? MedixReceipt { get; set; }

    /// <summary>Remarks of the claim's receipt row.</summary>
    public string? PayRemarks { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Receipt remark.</summary>
    public string? Remark { get; set; }

    /// <summary>The receipt row (RecPAYIndex).</summary>
    public int? ReceiptIndex { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>The date the claim was closed.</summary>
    public DateTime? ClaimCloseDate { get; set; }

    /// <summary>Receipt number prefix (RCInd).</summary>
    public string? ReceiptPrefix { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Cover id of the patient.</summary>
    public string? CoverId { get; set; }

    /// <summary>Patient name.</summary>
    public string? PatientName { get; set; }

    /// <summary>The sum of the check amounts received for the claim.</summary>
    public decimal? Received { get; set; }

    /// <summary>Payment voucher number.</summary>
    public string? PvNo { get; set; }

    /// <summary>Payment voucher date.</summary>
    public DateTime? PvDate { get; set; }
}

/// <summary>The list, its totals and whether the user is authorised (USR.AuthorisedStatus other than N).</summary>
public record PayorSearchResult(List<PayorBordxRow> Bordx, List<PayorWorksheetRow> Worksheets, decimal TotalAmount, decimal TotalReceived, bool Authorised, bool Truncated);

/// <summary>The criteria of the search.</summary>
public class ReceiptPayorFilter
{
    /// <summary>Receipt Type: bordx (By Bordx) or worksheet (By Worksheet).</summary>
    public string? Mode { get; set; } = "bordx";

    /// <summary>Use Scan: one bordx (By Bordx) or one worksheet (By Worksheet).</summary>
    public bool Scan { get; set; }

    /// <summary>Payor (code or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Bordx No from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx No to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>WS No from.</summary>
    public string? WsFrom { get; set; }

    /// <summary>WS No to.</summary>
    public string? WsTo { get; set; }

    /// <summary>WS status: 1 received, 2 not received (By Worksheet).</summary>
    public int WsStatus { get; set; }

    /// <summary>Payment Status: 0 All, 1 Received, 2 Not received, 3 Partial, 4 Received and Partial, 5 Not received and Partial.</summary>
    public int Status { get; set; }

    /// <summary>Recp No from.</summary>
    public string? RecFrom { get; set; }

    /// <summary>Recp No to.</summary>
    public string? RecTo { get; set; }

    /// <summary>Recp Date from.</summary>
    public DateTime? RecDateFrom { get; set; }

    /// <summary>Recp Date to.</summary>
    public DateTime? RecDateTo { get; set; }

    /// <summary>A/C Period Mth from.</summary>
    public int? MthFrom { get; set; }

    /// <summary>A/C Period Mth to.</summary>
    public int? MthTo { get; set; }

    /// <summary>A/C Period Yr from.</summary>
    public int? YrFrom { get; set; }

    /// <summary>A/C Period Yr to.</summary>
    public int? YrTo { get; set; }

    /// <summary>Bordx Date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx Date to.</summary>
    public DateTime? BordxDateTo { get; set; }

    /// <summary>WS Date from (the date the claim was closed).</summary>
    public DateTime? CloseFrom { get; set; }

    /// <summary>WS Date to.</summary>
    public DateTime? CloseTo { get; set; }

    /// <summary>Group Company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Policy Number.</summary>
    public string? Policy { get; set; }
}

/// <summary>What is received for one ticked claim (By Worksheet).</summary>
public class ReceiptPayorItem
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public int Version { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>F full or P partial.</summary>
    public string? Status { get; set; }

    /// <summary>Paying: the amount received for the claim.</summary>
    public decimal? PaymentAmt { get; set; }

    /// <summary>Bank charge.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Gain or loss.</summary>
    public decimal? GainLoss { get; set; }

    /// <summary>Misc. difference.</summary>
    public decimal? Misc { get; set; }

    /// <summary>Discount.</summary>
    public decimal? Discount { get; set; }

    /// <summary>Write off.</summary>
    public decimal? WriteOff { get; set; }

    /// <summary>Medix receipt.</summary>
    public decimal? MedixReceipt { get; set; }

    /// <summary>Remarks of the claim.</summary>
    public string? PayRemarks { get; set; }

    /// <summary>Contra case.</summary>
    public string? ContraCase { get; set; }

    /// <summary>Contra version.</summary>
    public string? ContraVersion { get; set; }

    /// <summary>Contra amount.</summary>
    public decimal? ContraAmt { get; set; }

    /// <summary>The receipt row to change (Update); none for Save.</summary>
    public int? ReceiptIndex { get; set; }
}

/// <summary>The receipt header the boxes hold, and the ticked bordx or claims (cmdSave_Click and CmdUpdate_Click).</summary>
public class ReceiptPayorSaveRequest
{
    /// <summary>Receipt Type: bordx or worksheet.</summary>
    public string? Mode { get; set; } = "bordx";

    /// <summary>Rec Amt (the cheque amount).</summary>
    public decimal? ReceiptAmt { get; set; }

    /// <summary>Cheque No.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Receipt Date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>A/C Mth.</summary>
    public int? AccMonth { get; set; }

    /// <summary>A/C Year.</summary>
    public int? AccYear { get; set; }

    /// <summary>Pay PV No.</summary>
    public string? PvNo { get; set; }

    /// <summary>Pay Chq Date (the PV date).</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Remark: needed when the receipt amount differs from the checked amount.</summary>
    public string? Remark { get; set; }

    /// <summary>Checked Amt: what the ticked lines add up to.</summary>
    public decimal CheckedAmount { get; set; }

    /// <summary>The receipt number (Update).</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>The ticked bordx (By Bordx).</summary>
    public List<string> BordxNos { get; set; } = [];

    /// <summary>The ticked claims (By Worksheet).</summary>
    public List<ReceiptPayorItem> Items { get; set; } = [];
}

/// <summary>The receipt being changed (Edit then Update): the same boxes as Save, with the receipt number.</summary>
public class ReceiptPayorUpdateRequest : ReceiptPayorSaveRequest;

/// <summary>What Remove asks for.</summary>
public class ReceiptPayorDeleteRequest
{
    /// <summary>Receipt Type: bordx or worksheet.</summary>
    public string? Mode { get; set; } = "bordx";

    /// <summary>The bordx (By Bordx).</summary>
    public string? BordxNo { get; set; }

    /// <summary>The case number (By Worksheet).</summary>
    public string? CaseNo { get; set; }

    /// <summary>The claim version (By Worksheet).</summary>
    public int? Version { get; set; }
}

/// <summary>A line of the official receipt.</summary>
public record ReceiptPayorOfficialLine(string? ReceiptNo, DateTime? ReceiptDate, string? BordxNo, decimal? ReceiptAmt, string? ChequeNo, string? PayorName);

/// <summary>The messages of the screen.</summary>
public static class ReceiptPayorMessages
{
    /// <summary>cmdSearch_Click.</summary>
    public const string EnterWorksheet = "Please Enter Worksheet No";

    /// <summary>cmdSearch_Click.</summary>
    public const string EnterBordx = "Please Enter Bordx No";

    /// <summary>cmdSave_Click.</summary>
    public const string SelectOne = "Please select at least one record to be updated! ";

    /// <summary>lstBordx_ItemCheck.</summary>
    public const string Paid = "Record has been paid !";

    /// <summary>lstBordx_ItemCheck.</summary>
    public const string PartialPaid = "Record has been Partial Paid ! Please Pay by Receipt Type - Worksheet !";
}

/// <summary>cmdSearch_Click's checks.</summary>
public sealed class ReceiptPayorFilterValidator : RequestValidator<ReceiptPayorFilter>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptPayorFilterValidator()
    {
        RuleFor(f => f.Mode).Must(m => m is "bordx" or "worksheet").WithMessage("Select Receipt Type By Bordx or By Worksheet.");
        RuleFor(f => f.Status).InclusiveBetween(0, 5).WithMessage("Select a Payment Status.");
        RuleFor(f => f).Must(f => !(f.Scan && f.Mode == "worksheet") || !string.IsNullOrWhiteSpace(f.WsFrom)).WithMessage(ReceiptPayorMessages.EnterWorksheet);
        RuleFor(f => f).Must(f => !(f.Scan && f.Mode == "bordx") || !string.IsNullOrWhiteSpace(f.BordxFrom)).WithMessage(ReceiptPayorMessages.EnterBordx);
    }
}

/// <summary>The checks of one ticked claim.</summary>
public sealed class ReceiptPayorItemValidator : RequestValidator<ReceiptPayorItem>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptPayorItemValidator()
    {
        RuleFor(i => i.CaseNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Select the claims to be received.");
        RuleFor(i => i.BordxNo).Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Select the claims to be received.");
        RuleFor(i => i.Status).Must(s => s is "F" or "P").WithMessage("The payment of a claim is Full or Partial.");
        RuleFor(i => i.PaymentAmt).Must(a => a is > 0).WithMessage("Please enter the amount paying for the claim.");
        RuleFor(i => i).Must(i => string.IsNullOrWhiteSpace(i.ContraCase) == string.IsNullOrWhiteSpace(i.ContraVersion)).WithMessage("Invalid Contra Case..., Please Keyin Contra Case No. or Contra Version ");
        RuleFor(i => i).Must(i => string.IsNullOrWhiteSpace(i.ContraCase) || i.ContraAmt is not null).WithMessage("Please Keyin Contra Case Amount ");
    }
}

/// <summary>cmdSave_Click's checks, in the desktop's order.</summary>
public sealed class ReceiptPayorSaveRequestValidator : RequestValidator<ReceiptPayorSaveRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptPayorSaveRequestValidator()
    {
        RuleFor(r => r.Mode).Must(m => m is "bordx" or "worksheet").WithMessage("Select Receipt Type By Bordx or By Worksheet.");
        RuleFor(r => r).Must(r => (r.Mode == "bordx" ? r.BordxNos.Count : r.Items.Count) > 0).WithMessage(ReceiptPayorMessages.SelectOne);
        RuleFor(r => r.ReceiptAmt).Must(a => a is not null).WithMessage("Please Enter Receipt Amt!");
        RuleFor(r => r.ChequeNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Cheque No!");
        RuleFor(r => r.ReceiptDate).Must(d => d is not null).WithMessage("Please Enter Receipt Date");
        RuleFor(r => r).Must(r => r.ReceiptAmt == r.CheckedAmount || !string.IsNullOrWhiteSpace(r.Remark)).WithMessage("Please Enter Remark!");
        RuleForEach(r => r.Items).SetValidator(new ReceiptPayorItemValidator());
    }
}

/// <summary>CmdUpdate_Click's checks, in the desktop's order (the receipt number is the one being changed).</summary>
public sealed class ReceiptPayorUpdateRequestValidator : RequestValidator<ReceiptPayorUpdateRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptPayorUpdateRequestValidator()
    {
        RuleFor(r => r.Mode).Must(m => m is "bordx" or "worksheet").WithMessage("Select Receipt Type By Bordx or By Worksheet.");
        RuleFor(r => r).Must(r => (r.Mode == "bordx" ? r.BordxNos.Count : r.Items.Count) == 1).WithMessage("Please select the record");
        RuleFor(r => r.ReceiptNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Please select the record");
        RuleFor(r => r.ReceiptAmt).Must(a => a is not null).WithMessage("Please Enter Rec Amt!");
        RuleFor(r => r.ChequeNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Cheque No!");
        RuleFor(r => r.ReceiptDate).Must(d => d is not null).WithMessage("Please Enter Receipt Date");
        RuleForEach(r => r.Items).SetValidator(new ReceiptPayorItemValidator());
    }
}

/// <summary>Remove's checks.</summary>
public sealed class ReceiptPayorDeleteRequestValidator : RequestValidator<ReceiptPayorDeleteRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptPayorDeleteRequestValidator()
    {
        RuleFor(r => r.Mode).Must(m => m is "bordx" or "worksheet").WithMessage("Select Receipt Type By Bordx or By Worksheet.");
        RuleFor(r => r).Must(r => r.Mode != "bordx" || !string.IsNullOrWhiteSpace(r.BordxNo)).WithMessage("Please Select Bordx to delete!");
        RuleFor(r => r).Must(r => r.Mode != "worksheet" || (!string.IsNullOrWhiteSpace(r.CaseNo) && r.Version is not null)).WithMessage("Please Select Claim to delete!");
    }
}

/// <summary>Reads the bordx and claims and writes the receipts.</summary>
public interface IReceiptPayorRepository
{
    /// <summary>The bordx that match.</summary>
    Task<List<PayorBordxRow>> BordxAsync(IDbSession session, ReceiptPayorFilter filter);

    /// <summary>The claims that match.</summary>
    Task<List<PayorWorksheetRow>> WorksheetsAsync(IDbSession session, ReceiptPayorFilter filter);

    /// <summary>The group companies of a payor.</summary>
    Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor);

    /// <summary>Whether the user is authorised (USR.AuthorisedStatus is not N).</summary>
    Task<bool> AuthorisedAsync(IDbSession session, string userCode);

    /// <summary>The status of a bordx in the Bordx table (F, P or empty); null when there is no such bordx.</summary>
    Task<string?> BordxStatusAsync(IDbSession session, string bordxNo);

    /// <summary>The status of a claim (F, P or empty); null when the claim is not in the list.</summary>
    Task<string?> ClaimStatusAsync(IDbSession session, string caseNo, int version);

    /// <summary>The next receipt number for a prefix.</summary>
    Task<string> NextNumberAsync(IDbSession session, string prefix);

    /// <summary>Writes the receipt of a whole bordx (the claims it holds).</summary>
    Task<int> SaveBordxAsync(IDbSession session, ReceiptPayorSaveRequest r, string bordxNo, string receiptNo, bool insert, string userCode);

    /// <summary>Writes the receipt of one claim.</summary>
    Task<string> SaveWorksheetAsync(IDbSession session, ReceiptPayorSaveRequest r, ReceiptPayorItem item, string receiptNo, bool insert, string userCode);

    /// <summary>Removes receipts.</summary>
    Task<int> DeleteAsync(IDbSession session, ReceiptPayorDeleteRequest request);

    /// <summary>The lines of the official receipt.</summary>
    Task<List<ReceiptPayorOfficialLine>> OfficialReceiptAsync(IDbSession session, string receiptNo);
}

/// <inheritdoc />
public sealed class ReceiptPayorRepository : IReceiptPayorRepository
{
    private static string? Text(string? value) => WorksheetLimits.Text(value)?.ToUpperInvariant();

    private static string? Payor(string? value) => WorksheetLimits.Text(value)?.Split('-')[0].Trim();

    /// <inheritdoc />
    public Task<List<PayorBordxRow>> BordxAsync(IDbSession session, ReceiptPayorFilter f) =>
        session.Connection.ProcQueryAsync<PayorBordxRow>("genisis.ReceiptPayor_Bordx", new
        {
            payor = Payor(f.Payor), bordxFrom = Text(f.BordxFrom), bordxTo = f.Scan ? Text(f.BordxFrom) : Text(f.BordxTo), status = f.Status, recFrom = WorksheetLimits.Text(f.RecFrom), recTo = WorksheetLimits.Text(f.RecTo),
            recDateFrom = WorksheetLimits.Day(f.RecDateFrom), recDateTo = WorksheetLimits.Day(f.RecDateTo), mthFrom = f.MthFrom, mthTo = f.MthTo, yrFrom = f.YrFrom, yrTo = f.YrTo,
            bordxDateFrom = WorksheetLimits.Day(f.BordxDateFrom), bordxDateTo = WorksheetLimits.Day(f.BordxDateTo), groupCompany = WorksheetLimits.Text(f.GroupCompany), policy = WorksheetLimits.Text(f.Policy), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<PayorWorksheetRow>> WorksheetsAsync(IDbSession session, ReceiptPayorFilter f) =>
        session.Connection.ProcQueryAsync<PayorWorksheetRow>("genisis.ReceiptPayor_Worksheet", new
        {
            payor = Payor(f.Payor), bordxFrom = Text(f.BordxFrom), bordxTo = Text(f.BordxTo), wsFrom = Text(f.WsFrom), wsTo = f.Scan ? Text(f.WsFrom) : Text(f.WsTo), wsStatus = f.WsStatus, status = f.Status,
            recFrom = WorksheetLimits.Text(f.RecFrom), recTo = WorksheetLimits.Text(f.RecTo), recDateFrom = WorksheetLimits.Day(f.RecDateFrom), recDateTo = WorksheetLimits.Day(f.RecDateTo), mthFrom = f.MthFrom, mthTo = f.MthTo,
            yrFrom = f.YrFrom, yrTo = f.YrTo, bordxDateFrom = WorksheetLimits.Day(f.BordxDateFrom), bordxDateTo = WorksheetLimits.Day(f.BordxDateTo), closeFrom = WorksheetLimits.Day(f.CloseFrom),
            closeTo = WorksheetLimits.Day(f.CloseTo), groupCompany = WorksheetLimits.Text(f.GroupCompany), policy = WorksheetLimits.Text(f.Policy), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) => session.Connection.ProcQueryAsync<string>("genisis.ReceiptPayor_GroupCompanies", new { payor }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> AuthorisedAsync(IDbSession session, string userCode) => await session.Connection.ProcScalarAsync<bool>("genisis.ReceiptMember_UserAuthorised", new { userCode }, session.Transaction);

    /// <inheritdoc />
    public async Task<string?> BordxStatusAsync(IDbSession session, string bordxNo) => await session.Connection.ProcScalarAsync<string?>("genisis.ReceiptPayor_BordxStatus", new { bordxNo }, session.Transaction);

    /// <inheritdoc />
    public async Task<string?> ClaimStatusAsync(IDbSession session, string caseNo, int version)
    {
        var rows = await session.Connection.ProcQueryAsync<PayorWorksheetRow>("genisis.ReceiptPayor_Worksheet", new { wsFrom = caseNo, wsTo = caseNo, max = 200 }, session.Transaction);
        var claim = rows.Where(r => r.ClaimVersion == version).ToList();
        return claim.Count == 0 ? null : claim.Any(r => r.ClaimStatus == "F") ? "F" : claim.FirstOrDefault(r => !string.IsNullOrEmpty(r.ClaimStatus))?.ClaimStatus ?? "";
    }

    /// <inheritdoc />
    public async Task<string> NextNumberAsync(IDbSession session, string prefix) => (await session.Connection.ProcScalarAsync<string>("genisis.FinanceReceipt_NextNumber", new { prefix }, session.Transaction))!;

    /// <inheritdoc />
    public async Task<int> SaveBordxAsync(IDbSession session, ReceiptPayorSaveRequest r, string bordxNo, string receiptNo, bool insert, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.ReceiptPayor_SaveBordx", new
        {
            bordxNo, receiptNo, receiptDate = r.ReceiptDate!.Value.Date, receiptAmt = r.ReceiptAmt, chequeNo = r.ChequeNo!.Trim(), pvNo = WorksheetLimits.Text(r.PvNo), pvDate = WorksheetLimits.Day(r.PvDate), accMonth = r.AccMonth,
            accYear = r.AccYear, remark = WorksheetLimits.Text(r.Remark), insert, userCode, today = DateTime.Today,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> SaveWorksheetAsync(IDbSession session, ReceiptPayorSaveRequest r, ReceiptPayorItem i, string receiptNo, bool insert, string userCode) =>
        (await session.Connection.ProcScalarAsync<string>("genisis.ReceiptPayor_SaveWorksheet", new
        {
            cas = i.CaseNo!.Trim().ToUpperInvariant(), version = i.Version, bordxNo = i.BordxNo!.Trim(), status = i.Status, paymentAmt = i.PaymentAmt, bankCharge = i.BankCharge ?? 0m, gainLoss = i.GainLoss ?? 0m, misc = i.Misc ?? 0m,
            discount = i.Discount ?? 0m, writeOff = i.WriteOff ?? 0m, medixReceipt = i.MedixReceipt ?? 0m, payRemarks = WorksheetLimits.Text(i.PayRemarks), receiptNo, receiptDate = r.ReceiptDate!.Value.Date, receiptAmt = r.ReceiptAmt,
            chequeNo = r.ChequeNo!.Trim(), receiptRemark = WorksheetLimits.Text(r.Remark), pvNo = WorksheetLimits.Text(r.PvNo), pvDate = WorksheetLimits.Day(r.PvDate), accMonth = r.AccMonth, accYear = r.AccYear,
            contraCase = WorksheetLimits.Text(i.ContraCase), contraVersion = WorksheetLimits.Text(i.ContraVersion), contraAmt = i.ContraAmt, recIndex = insert ? null : i.ReceiptIndex, userCode, today = DateTime.Today,
        }, session.Transaction))!;

    /// <inheritdoc />
    public async Task<int> DeleteAsync(IDbSession session, ReceiptPayorDeleteRequest r) =>
        await session.Connection.ProcScalarAsync<int>("genisis.ReceiptPayor_Delete", new { bordxNo = r.Mode == "bordx" ? WorksheetLimits.Text(r.BordxNo) : null, cas = r.Mode == "worksheet" ? Text(r.CaseNo) : null, version = r.Mode == "worksheet" ? r.Version : null }, session.Transaction);

    /// <inheritdoc />
    public Task<List<ReceiptPayorOfficialLine>> OfficialReceiptAsync(IDbSession session, string receiptNo) =>
        session.Connection.ProcQueryAsync<ReceiptPayorOfficialLine>("genisis.ReceiptPayor_OfficialReceipt", new { receiptNo }, session.Transaction);
}

/// <summary>Receipt > Payor (legacy <c>FrmReceiptPayor</c>, "Receipt From Payor").</summary>
public class ReceiptPayorService(IDbSessions sessions, IReceiptPayorRepository repository, ILogger<ReceiptPayorService> log)
{
    /// <summary>cmdSearch_Click: the bordx or the claims with what has been received; nothing found is a <see cref="NotFoundException"/> ("No record found!").</summary>
    public async Task<PayorSearchResult> SearchAsync(ReceiptPayorFilter filter, string userCode)
    {
        await using var session = await sessions.OpenAsync();
        var authorised = await repository.AuthorisedAsync(session, userCode);
        if (filter.Mode == "bordx")
        {
            var capped = WorksheetLimits.Cap(await repository.BordxAsync(session, filter));
            if (capped.Rows.Count == 0) throw new NotFoundException("No record found!");
            return new PayorSearchResult(capped.Rows, [], capped.Rows.GroupBy(r => r.BordxNo).Sum(g => g.First().BordxAmount ?? 0), capped.Rows.Sum(r => r.CheckAmt ?? 0), authorised, capped.Truncated);
        }

        var ws = WorksheetLimits.Cap(await repository.WorksheetsAsync(session, filter));
        if (ws.Rows.Count == 0) throw new NotFoundException("No record found!");
        return new PayorSearchResult([], ws.Rows, ws.Rows.Sum(r => r.Amount ?? 0), ws.Rows.Sum(r => r.CheckAmt ?? 0), authorised, ws.Truncated);
    }

    /// <summary>cboPayor_Click: the group companies of the payor.</summary>
    public async Task<List<string>> GroupCompaniesAsync(string? payor)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GroupCompaniesAsync(session, (payor ?? "").Split('-')[0].Trim());
    }

    private async Task<string> PrefixOfAsync(IDbSession session, string bordxNo) =>
        ((await repository.BordxAsync(session, new ReceiptPayorFilter { Mode = "bordx", Scan = true, BordxFrom = bordxNo })).FirstOrDefault()?.ReceiptPrefix ?? "").Trim();

    /// <summary>
    /// cmdSave_Click. By Bordx: for each ticked bordx a fully received bordx is refused to a user whose status is not authorised ("Record has been paid !") and a partly received one to everybody ("Record has been
    /// Partial Paid ! Please Pay by Receipt Type - Worksheet !"); one new receipt number is taken from the prefix of the first ticked bordx's payor (the desktop used the first line of the list) and every claim of
    /// each bordx gets a receipt row. By Worksheet: the same for each claim, with its amounts and contra case. One transaction.
    /// </summary>
    public async Task<(string ReceiptNo, int Saved)> SaveAsync(ReceiptPayorSaveRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var authorised = await repository.AuthorisedAsync(session, userCode);
        var bordx = request.Mode == "bordx" ? request.BordxNos.Select(b => b.Trim().ToUpperInvariant()).ToList() : request.Items.Select(i => i.BordxNo!.Trim().ToUpperInvariant()).Distinct().ToList();
        foreach (var b in bordx)
        {
            var status = await repository.BordxStatusAsync(session, b) ?? throw new NotFoundException($"Bordx {b} not found");
            if (request.Mode == "bordx")
            {
                if (status == "F" && !authorised) throw new ConflictException(ReceiptPayorMessages.Paid);
                if (status == "P") throw new ConflictException(ReceiptPayorMessages.PartialPaid);
            }
        }

        if (request.Mode == "worksheet")
            foreach (var item in request.Items)
                if (!authorised && await repository.ClaimStatusAsync(session, item.CaseNo!.Trim().ToUpperInvariant(), item.Version) == "F") throw new ConflictException(ReceiptPayorMessages.Paid);

        var receiptNo = await repository.NextNumberAsync(session, await PrefixOfAsync(session, bordx[0]));
        var saved = 0;
        if (request.Mode == "bordx")
            foreach (var b in bordx) saved += await repository.SaveBordxAsync(session, request, b, receiptNo, true, userCode);
        else
            foreach (var item in request.Items)
            {
                await repository.SaveWorksheetAsync(session, request, item, receiptNo, true, userCode);
                saved++;
            }

        await session.CommitAsync();
        log.LogInformation("Payor receipt {Receipt} saved for {Count} {Kind} by {User}", receiptNo, saved, request.Mode, userCode);
        return (receiptNo, saved);
    }

    /// <summary>CmdUpdate_Click: changes the receipt of the one bordx (every claim's row of the receipt number) or the one receipt row of a claim.</summary>
    public async Task UpdateAsync(ReceiptPayorUpdateRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var receiptNo = request.ReceiptNo!.Trim();
        if (request.Mode == "bordx") await repository.SaveBordxAsync(session, request, request.BordxNos[0].Trim().ToUpperInvariant(), receiptNo, false, userCode);
        else
        {
            var item = request.Items[0];
            if (item.ReceiptIndex is null) throw new BusinessRuleException("Please select the record");
            await repository.SaveWorksheetAsync(session, request, item, receiptNo, false, userCode);
        }

        await session.CommitAsync();
        log.LogInformation("Payor receipt {Receipt} updated by {User}", receiptNo, userCode);
    }

    /// <summary>cmdRemove_Click: deletes the receipt rows of a bordx or of a claim and clears the receipt status; nothing to delete is a <see cref="NotFoundException"/>.</summary>
    public async Task<int> DeleteAsync(ReceiptPayorDeleteRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var deleted = await repository.DeleteAsync(session, request);
        if (deleted == 0) throw new NotFoundException("No receipt to delete.");
        await session.CommitAsync();
        log.LogInformation("Payor receipt rows deleted ({Count}) for {Kind} by {User}", deleted, request.Mode == "bordx" ? request.BordxNo : $"{request.CaseNo}-{request.Version}", userCode);
        return deleted;
    }

    /// <summary>cmdPrintPV_Click: the lines of the official receipt of a receipt number.</summary>
    public async Task<List<ReceiptPayorOfficialLine>> OfficialReceiptAsync(string? receiptNo)
    {
        if (string.IsNullOrWhiteSpace(receiptNo)) throw new BusinessRuleException("Please Enter or Generate Receipt No");
        await using var session = await sessions.OpenAsync();
        var lines = await repository.OfficialReceiptAsync(session, receiptNo.Trim());
        return lines.Count == 0 ? throw new NotFoundException("No record found!") : lines;
    }
}
