using System.Globalization;
using System.Text;
using Genisis.Api.Bordx;
using Genisis.Api.Data;

namespace Genisis.Api.Pluto;

public enum PlutoAsciiKind
{
    Text,
    Date,
    Filler,
}

/// <summary>One fixed-width field of a Membership ASCII (Pluto) member record; <see cref="Column"/> is the staging column.</summary>
public sealed record PlutoAsciiField(string? Column, int Width, PlutoAsciiKind Kind = PlutoAsciiKind.Text);

/// <summary>The record layout of frmMembershipASCIIPluto (CmdGenerate_Click), in file order.</summary>
public static class PlutoAsciiLayout
{
    public const string HeaderRecord = "F";
    public const string TrailerRecord = "T";
    public const int TrailerCount = 10;
    public const int TrailerFiller = 4778;
    public const string PrincipalRelationship = "P";
    public const string DateFormat = "ddMMyyyy";

    private static PlutoAsciiField T(string column, int width) => new(column, width);
    private static PlutoAsciiField D(string column) => new(column, 8, PlutoAsciiKind.Date);
    private static PlutoAsciiField Blank(int width) => new(null, width, PlutoAsciiKind.Filler);

    public static readonly IReadOnlyList<PlutoAsciiField> Fields =
    [
        T("RELCode", 1), T("MBMPolicyNo", 25), T("MBMAgentCode", 15), T("MBMSalutation", 5), T("MBMName", 60),
        T("MBMAddress1", 30), T("MBMAddress2", 30), T("MBMAddress3", 30), T("CTYCode", 20), T("MBMPostCode", 5), T("STACode", 15),
        T("MBMTelNoH", 12), T("MBMTelNoO", 12), T("MBMTelNoM", 12), T("MBMIcBcPp", 20), D("MBMDOB"), T("MBMSex", 1), T("NATCode", 10),
        T("MBMWeight", 3), T("MBMHeight", 3), T("LGNCode", 1), T("RACCode", 1), T("MBMOccupation", 30), T("MBMWorkNature", 1),
        T("MBMBlood", 3), T("MBMAllergic", 20), D("MBMPayorEffDate"), D("MBMPayorExpDate"), T("PLNCode", 4), T("INSCode", 1),
        T("MBMGroupCompany", 40), T("MBMRenewFlag", 1), T("MBMExclusion", 2500), T("MBMSmoking", 1), T("MBMAlcohol", 1),
        T("MBMMaritalStatus", 1), T("BRNCode", 15), T("MBMPrevPolNo", 25), T("MBMPrevMemNo", 18), T("MBMEmployeeNo", 15),
        D("MBMJoinedDate"), T("Clinical", 1), T("ClinicName1", 50), T("ClinicName2", 50), T("ClinicName3", 50), T("ClinicName4", 50),
        T("MBMDepartment", 30), T("MBMGRPCompanyAdd1", 30), T("MBMGRPCompanyAdd2", 30), T("MBMGRPCompanyAdd3", 30),
        T("MBMGRPCompanyAdd4", 30), T("MBMPrevTakeOverPlnCode", 4), T("MBMInstallment", 1), T("MBMPOLSubNo1", 10),
        T("MBMPOLSubNo2", 10), D("MBMPOLIssuedDate"), T("MBMRiskNo", 4), T("MBMTransNo", 5), T("MBMPAYNo", 8), T("MBMPAYSeqNo", 2),
        T("MBMClientNo", 8), T("MBMClientID", 2), T("MBMPrevPLNCode", 15), T("MBMRBAmt", 8), T("MBMPrevInsCom", 60),
        T("MBMPAYRemarks", 60), D("MBMCLMNonPayFr"), D("MBMCLMNonPayTo"), T("MBMUpgradedPLN", 4), D("MBMNewEffDate"),
        T("MBMEndorsementNo", 20), T("MBMAppName", 60), T("MBMPayorPremNet", 12), T("MBMPayorPremGross", 12), T("MBMPayorMCONet", 12),
        T("MBMPayorMCOGross", 12), T("MBMFamPolicyDisc", 5), T("MBMRenewDisc", 5), T("MBMIcBcPp2nd", 20), T("MBMRnBStatus", 2),
        T("MBMLoadPrem", 12), T("MBMFamDiscAmt", 12), T("MBMRenewDiscAmt", 12), T("MBMBlackListed", 5), T("MBMBankACNo", 25),
        T("MBMUnderwritingExcess", 12),
        // MBMGuaRenewal: the staging table has no such column, so its 5 characters are blank.
        Blank(5),
        Blank(2861), T("MBMCLNPlanCode", 4), T("MBMCLNInsType", 1), D("MBMCLNEffDate"), D("MBMCLNExpDate"), T("MBMCLNExclusion", 2500),
        Blank(20), Blank(752),
    ];
}

public sealed record PlutoAsciiFile(string FileName, byte[] Content, int Principals, int Supplementaries);

/// <summary>Generate Membership ASCII (Pluto): the staged rows of one payor and bordx date as a fixed-width file.</summary>
public sealed class PlutoAsciiExporter(DbConnections db)
{
    private const string LineEnd = "\r\n";

    /// <returns>The file, or <c>null</c> when nothing is staged for the payor and date.</returns>
    public async Task<PlutoAsciiFile?> ExportAsync(string payorCode, DateTime bordxDate, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var rows = (await conn.ProcQueryAsync<dynamic>("genisis.Pluto_AsciiRows", new { bordxDate = bordxDate.Date, pay = payorCode }))
            .Select(r => (IDictionary<string, object?>)r).ToList();
        return rows.Count == 0 ? null : Build(payorCode, bordxDate, rows);
    }

    /// <summary>Header "F" + payor + date, one trimmed record per row, and the untrimmed trailer with the counts.</summary>
    public static PlutoAsciiFile Build(string payorCode, DateTime bordxDate, IReadOnlyList<IDictionary<string, object?>> rows)
    {
        var date = bordxDate.ToString(PlutoAsciiLayout.DateFormat, CultureInfo.InvariantCulture);
        var file = new StringBuilder().Append(PlutoAsciiLayout.HeaderRecord).Append(payorCode).Append(date).Append(LineEnd);
        int principals = 0, supplementaries = 0;
        foreach (var row in rows)
        {
            file.Append(Record(row)).Append(LineEnd);
            if (Text(row.TryGetValue("RELCode", out var relationship) ? relationship : null).Trim() == PlutoAsciiLayout.PrincipalRelationship) principals++;
            else supplementaries++;
        }
        file.Append(PlutoAsciiLayout.TrailerRecord)
            .Append(BordxAsciiExporter.Pad(principals.ToString(CultureInfo.InvariantCulture), PlutoAsciiLayout.TrailerCount))
            .Append(BordxAsciiExporter.Pad(supplementaries.ToString(CultureInfo.InvariantCulture), PlutoAsciiLayout.TrailerCount))
            .Append(BordxAsciiExporter.Pad("", PlutoAsciiLayout.TrailerFiller)).Append(LineEnd);
        return new PlutoAsciiFile($"{payorCode}{date}.txt", BordxFile.Encoding.GetBytes(file.ToString()), principals, supplementaries);
    }

    /// <summary>The desktop wrote each record with Print #1, Trim(Text1): leading and trailing spaces are removed.</summary>
    public static string Record(IDictionary<string, object?> row)
    {
        var line = new StringBuilder();
        foreach (var field in PlutoAsciiLayout.Fields)
        {
            var value = field.Column is null || !row.TryGetValue(field.Column, out var cell) ? null : cell;
            var text = field.Kind switch
            {
                PlutoAsciiKind.Date => value is DateTime d ? d.ToString(PlutoAsciiLayout.DateFormat, CultureInfo.InvariantCulture) : "",
                PlutoAsciiKind.Text => Text(value),
                _ => "",
            };
            line.Append(BordxAsciiExporter.Pad(text, field.Width));
        }
        return line.ToString().Trim(' ');
    }

    /// <summary>VB's string conversion: money without trailing zeros.</summary>
    private static string Text(object? value) => value switch
    {
        null or DBNull => "",
        decimal m => m.ToString("0.####", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}
