using Genisis.Api.Common.Errors;

namespace Genisis.Api.Pluto;

public enum PlutoValueKind
{
    Text,
    Date,
    Number,
}

/// <summary>One Excel column of Upload Membership Excel (Pluto) and the MBMExcellUploadShelly column it fills.</summary>
public sealed record PlutoColumn(int Excel, string Name, PlutoValueKind Kind, int MaxLength = 0);

/// <summary>
/// The worksheet layout read by frmMembershipExcelPluto (cmdReturnDate_Click): first sheet, headings in row 1, data from row 2
/// until the first row whose column A is empty. Column 30 (remarks) has no column in MBMExcellUploadShelly and is not read.
/// </summary>
public static class PlutoExcelLayout
{
    public const int FirstDataRow = 2;
    /// <summary>The desktop stops reading at row 60000.</summary>
    public const int LastRow = 59_999;

    /// <summary>Column A, the primary key of MBMExcellUploadShelly.</summary>
    public const string NumberColumn = "No";

    public static readonly IReadOnlyList<PlutoColumn> Columns =
    [
        new(1, NumberColumn, PlutoValueKind.Number),
        new(2, "RELCode", PlutoValueKind.Text, 1),
        new(3, "MBMPolicyNo", PlutoValueKind.Text, 25),
        new(4, "MBMName", PlutoValueKind.Text, 60),
        new(5, "MBMAddress1", PlutoValueKind.Text, 30),
        new(6, "MBMAddress2", PlutoValueKind.Text, 30),
        new(7, "MBMAddress3", PlutoValueKind.Text, 30),
        new(8, "CTYCode", PlutoValueKind.Text, 20),
        new(9, "STACode", PlutoValueKind.Text, 15),
        new(10, "MBMPostCode", PlutoValueKind.Text, 5),
        new(11, "MBMGroupCompany", PlutoValueKind.Text, 40),
        new(12, "MBMDepartment", PlutoValueKind.Text, 30),
        new(13, "BRNCode", PlutoValueKind.Text, 15),
        new(14, "MBMSex", PlutoValueKind.Text, 1),
        new(15, "MBMRenewFlag", PlutoValueKind.Text, 1),
        new(16, "MBMIcBcPp", PlutoValueKind.Text, 20),
        new(17, "MBMDOB", PlutoValueKind.Date),
        new(18, "PLNCode", PlutoValueKind.Text, 4),
        new(19, "INSCode", PlutoValueKind.Text, 1),
        new(20, "MBMPayorEffDate", PlutoValueKind.Date),
        new(21, "MBMPayorExpDate", PlutoValueKind.Date),
        new(22, "MBMExclusion", PlutoValueKind.Text, 2500),
        new(23, "MBMCLNPlanCode", PlutoValueKind.Text, 4),
        new(24, "MBMCLNInsType", PlutoValueKind.Text, 1),
        new(25, "MBMCLNEffDate", PlutoValueKind.Date),
        new(26, "MBMCLNExpDate", PlutoValueKind.Date),
        new(27, "MBMCLNExclusion", PlutoValueKind.Text, 2500),
        new(28, "PAYCode", PlutoValueKind.Text, 2),
        new(29, "MBMBordxDate", PlutoValueKind.Date),
    ];
}

/// <summary>Messages of frmMembershipExcelPluto and frmMembershipASCIIPluto; the desktop wording is kept.</summary>
public static class PlutoMessages
{
    public const string FileRequired = "Please choose the Excel file to upload.";
    public const string NotAnExcelFile = "The file is not an Excel workbook (.xls or .xlsx).";
    public const string NoRows = "The first worksheet has no data rows (column A of row 2 is empty).";
    public const string TooManyRows = "The worksheet has data beyond row 59999; the desktop stopped there without uploading. Split the file.";
    public const string Uploaded = "Records have been uploaded!";

    public const string EnterHealthCode = "Please Enter Health Code";
    public const string EnterPayorCode = "Please Enter Payor Code";
    public const string EnterBordxDate = "Please Enter Bordx Date";
    public const string NoRecord = "No record found!";
    public const string Generated = "File Generated Sucessfully...";

    public static string AlreadyUploaded(int row, object? number) =>
        $"Row {row}: No {number} has already been uploaded (MBMExcellUploadShelly already has that number). Nothing was uploaded.";

    public static string TooLong(int row, PlutoColumn column) => $"Row {row}: {column.Name} is longer than {column.MaxLength} characters.";
    public static string NotADate(int row, PlutoColumn column) => $"Row {row}: {column.Name} is not a valid date.";
    public static string NotANumber(int row, PlutoColumn column) => $"Row {row}: {column.Name} is not a number.";
}

public static class PlutoLimits
{
    public const long MaximumFileBytes = 30L * 1024 * 1024;
    /// <summary>Row errors reported at once; the upload is refused if there is any.</summary>
    public const int MaximumErrors = 100;
}

public sealed class PlutoException(string message) : BusinessRuleException(message);
