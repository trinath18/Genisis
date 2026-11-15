using Genisis.Api.Common;
using Genisis.Api.Common.Export;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Export of the lists the screens show (the Export button's PDF choice; CSV and Excel are made in the browser).</summary>
[Route("api/export")]
public class ExportController(TimeProvider clock) : ApiControllerBase
{
    /// <summary>The largest print request accepted (20,000 rows of short cells fit well inside it).</summary>
    public const long MaxRequestBytes = 20 * 1024 * 1024;

    /// <summary>The list sent, as a landscape A4 PDF with its heading row repeated on every page.</summary>
    [HttpPost("pdf")]
    [RequestSizeLimit(MaxRequestBytes)]
    [Produces("application/pdf")]
    public IActionResult Pdf(TablePdfRequest request)
    {
        if (TablePdfRules.Problem(request) is { } problem) return BadRequest(new { message = problem });
        var pdf = TablePdf.Render(request, UserCode, clock.GetLocalNow().DateTime);
        return File(pdf, "application/pdf", FileName(request.Title) + ".pdf");
    }

    /// <summary>A file name from the title: letters, digits and dashes.</summary>
    public static string FileName(string title)
    {
        var name = new string(title.Trim().Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        while (name.Contains("--")) name = name.Replace("--", "-");
        name = name.Trim('-');
        return name.Length == 0 ? "export" : name;
    }
}
