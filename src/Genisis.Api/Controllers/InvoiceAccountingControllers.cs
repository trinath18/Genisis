using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Invoice > Invoice HOS Accounting (legacy FrmInvoiceHOSAC, "Invoice Hospital Accounting").</summary>
[Route("api/invoice/hospital-accounting")]
[RequireAccess(AccessRight.InvoiceHospitalAccounting)]
public class InvoiceHospitalAccountingController(HospitalAccountingService service) : ApiControllerBase
{
    /// <summary>ComboListing: the hospitals of the drop-down.</summary>
    [HttpGet("hospitals")]
    public async Task<ActionResult<List<InvoiceOption>>> Hospitals() => Ok(await service.HospitalsAsync());

    /// <summary>CmdSearch_Click: the invoices paid to hospitals that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<HospitalAccountingRow>>> Search([FromQuery] HospitalAccountingFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdPrintFile_Click: marks the invoices the criteria list as printed in a new batch (a reprint marks nothing).</summary>
    [HttpPost("print")]
    public async Task<ActionResult<AccountingPrintResult>> Print(HospitalAccountingFilter filter) => Ok(await service.PrintAsync(filter, UserCode));
}

/// <summary>Invoice > Invoice MBM Accounting (legacy FrmInvoiceMBMAC, "Member Invoice Accounting").</summary>
[Route("api/invoice/member-accounting")]
[RequireAccess(AccessRight.InvoiceMemberAccounting)]
public class InvoiceMemberAccountingController(MemberAccountingService service, PayorLookupService payors) : ApiControllerBase
{
    /// <summary>ComboListing: every payor.</summary>
    [HttpGet("payors")]
    public async Task<ActionResult<List<PayorOption>>> Payors() => Ok(await payors.GetPayorsAsync());

    /// <summary>CmdSearch_Click: the invoices paid to members that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<MemberAccountingRow>>> Search([FromQuery] MemberAccountingFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdPrintFile_Click: marks the invoices the criteria list as printed in a new batch (a reprint marks nothing).</summary>
    [HttpPost("print")]
    public async Task<ActionResult<AccountingPrintResult>> Print(MemberAccountingFilter filter) => Ok(await service.PrintAsync(filter, UserCode));

    /// <summary>cmdUpdate_Click: gives the ticked invoices an accounting period.</summary>
    [HttpPost("period")]
    public async Task<IActionResult> SetPeriod(AccountingPeriodRequest request) =>
        Ok(new { message = "Record updated...", updated = await service.SetPeriodAsync(request, UserCode) });
}
