using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Finance.InvoiceReceipt;
using Genisis.Api.Finance.ReceiptEnquiry;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Finance &gt; Invoice Receipt (Outstanding) and Short Payment Receipt (position 9, auto receipt number) and Notification / Suspension
/// (position 12, the manual form: typed receipt number). Legacy FrmRecMCO / frmRecMCOManual / frmReceiptAdjustment.
/// </summary>
[Route("api/finance/invoice-receipt")]
[RequireAccess(AccessRight.FinanceInvoiceReceipt, AccessRight.FinanceReceiptManual)]
public class FinanceInvoiceReceiptController(InvoiceReceiptService service) : ApiControllerBase
{
    /// <summary>Receipt types, receipt number prefixes and payors.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<InvoiceReceiptLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>Member MCO invoices with what has been received against them.</summary>
    [HttpGet("invoices")]
    public async Task<ActionResult<InvoiceSearchResult>> Invoices([FromQuery] InvoiceReceiptFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>The fee receipts recorded against one invoice.</summary>
    [HttpGet("invoices/{invoiceNo}/receipts")]
    public async Task<ActionResult<List<ReceiptRow>>> Receipts(string invoiceNo) => Ok(await service.ReceiptsAsync(invoiceNo));

    /// <summary>Records a fee receipt with the next receipt number (Invoice Receipt).</summary>
    [HttpPost("receipts")]
    [RequireAccess(AccessRight.FinanceInvoiceReceipt)]
    public async Task<ActionResult<InvoiceReceiptResult>> Record(InvoiceReceiptRequest request) => Ok(await service.RecordAsync(request, UserCode));

    /// <summary>Records a fee receipt with a typed receipt number (Notification / Suspension, the manual form).</summary>
    [HttpPost("receipts/manual")]
    [RequireAccess(AccessRight.FinanceReceiptManual)]
    public async Task<ActionResult<InvoiceReceiptResult>> RecordManual(ManualInvoiceReceiptRequest request) => Ok(await service.RecordManualAsync(request, UserCode));
}
