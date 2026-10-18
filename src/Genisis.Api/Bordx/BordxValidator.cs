namespace Genisis.Api.Bordx;

/// <summary>Required-field checks of Upload Bordx Verify / Upload / Generate (cmdVerify_Click, cmdUpload_Click, CmdGenerate_Click).</summary>
public static class BordxValidator
{
    public const string FileRequired = "Please Enter Filename";
    public const string NotATextFile = "The file is not a text bordereaux file.";

    /// <param name="settings">The bordx format settings of the payor.</param>
    /// <param name="header">First line of the file, or <c>null</c> when the file is empty.</param>
    /// <param name="upload">True when the file is being uploaded, false when it is only being checked.</param>
    public static string? Validate(BordxSettings settings, BordxLine? header, bool upload)
    {
        if (header is null) return "The file is empty.";
        if (upload && settings.HealthCode == "") return "Please Select Health Type";
        if (settings.FileType is not (FileTypes.New or FileTypes.Endorsement)) return upload ? "Please Select New / Endt file" : "Please Select Bordx Type";
        if (settings.PayorCode == "" && !BordxFormat.IsZurichTak(header)) return "Please Enter Payor Code";
        return null;
    }

    public static string? Validate(AsciiRequest request)
    {
        static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
        var option = request.ExportOption?.Trim().ToUpperInvariant();
        if (Blank(request.HealthCode)) return "Please Enter Health Plan";
        if (Blank(request.PayorCode)) return "Please Enter Payor Code";
        if (Blank(request.Version)) return "Please Select ASCII Version";
        if (option is not (AsciiExportOptions.Batch or AsciiExportOptions.DateRange)) return "Please Enter Export Option";
        if (option == AsciiExportOptions.Batch && (request.ReceiptDate is null || request.BatchNo is null)) return "Please Enter Receipt Date or Batch No";
        if (option == AsciiExportOptions.DateRange && (request.BordxFrom is null || request.BordxTo is null)) return "Please Enter Bordx Date";
        if (option == AsciiExportOptions.DateRange && request.BordxTo < request.BordxFrom) return "Bordx To cannot be before Bordx From.";
        if (option == AsciiExportOptions.DateRange && !Blank(request.Plan) && !BordxAsciiExporter.PlanFilters.Contains(request.Plan!.Trim()))
            return "Unknown plan filter.";
        return null;
    }
}
