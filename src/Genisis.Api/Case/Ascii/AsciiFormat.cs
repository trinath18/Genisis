using System.Globalization;
using System.Text;

namespace Genisis.Api.Case.Ascii;

/// <summary>
/// The fixed-width payor files of Claim ASCII (frmNewClaimAscii) and Case ASCII (frmCaseASCII): record layouts, header,
/// trailer and file names. <c>L</c> is Medix.bas <c>l()</c> (LSet: left-justified, padded or cut to the width).
/// Like the desktop, a record ends with CR and the line terminator is CRLF, so a record ends CR CR LF.
/// The desktop wrote 30 blanks for a missing patient IC in a 20-wide field, which shifts every later field; the field is 20 wide here.
/// </summary>
public static class AsciiFormat
{
    /// <summary>The line terminator (CRLF).</summary>
    public const string Eol = "\r\n";
    private const char Cr = '\r';

    /// <summary>Medix.bas <c>l()</c>: the text left-justified in <paramref name="width"/> characters, padded with spaces or cut.</summary>
    public static string L(string? value, int width)
    {
        var v = value ?? "";
        return v.Length >= width ? v[..width] : v.PadRight(width);
    }

    private static string Date(DateTime? d) => d is null ? "" : d.Value.ToString("ddMMyyyy", CultureInfo.InvariantCulture);

    /// <summary>Format(x, "#,##0.00").</summary>
    private static string Money(object? v) => v is null ? "" : Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>A VB Currency converted to text: no thousands separator, trailing zeros dropped.</summary>
    private static string Currency(object? v) => v is null ? "" : Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("0.####", CultureInfo.InvariantCulture);

    private static string S(IDictionary<string, object?> row, string key) => row.TryGetValue(key, out var v) ? (v?.ToString() ?? "").Trim() : "";
    private static DateTime? D(IDictionary<string, object?> row, string key) => row.TryGetValue(key, out var v) ? v as DateTime? : null;
    private static object? O(IDictionary<string, object?> row, string key) => row.TryGetValue(key, out var v) ? v : null;

    /// <summary>The member part both claim records share, from the member's cover ID to the patient's date of birth.</summary>
    private static string MemberAndPatient(IDictionary<string, object?> r) =>
        L(S(r, "MBMNumber"), 15) + L(S(r, "MBMPatientCovID"), 2) + L(S(r, "MBMPolicyNo"), 25) + L(S(r, "MBMPolSubNo1"), 25) + L(S(r, "MBMPolSubNo2"), 25)
        + L(Date(D(r, "MBMPayorEffDate")), 8) + L(Date(D(r, "MBMPayorExpDate")), 8) + L(S(r, "MBMAgentCode"), 15) + L(S(r, "MBMName"), 60)
        + L(S(r, "MBMIcBcPp"), 20) + L(S(r, "MBMSex"), 1) + L(S(r, "RACCode"), 1) + L(Date(D(r, "MBMDOB")), 8) + L("", 1)
        + L(S(r, "MBMRenewFlag"), 1) + L(S(r, "MBMTakeover"), 1) + L(S(r, "INSCode"), 2) + L(S(r, "PLNCode"), 4)
        + L(S(r, "MBMGroupCompany"), 50) + L(S(r, "BRNCode"), 50) + L(S(r, "AgeCode"), 2)
        + L(S(r, "PatientName"), 60) + L(S(r, "ICNo"), 20) + L(S(r, "Sex"), 1) + L(S(r, "RACCode"), 1) + L(S(r, "AgeBand"), 2) + L(Date(D(r, "DOB")), 8)
        + L(S(r, "HOSName"), 80) + L(S(r, "FinalDiag"), 5) + L(S(r, "FinalDiag2"), 5) + L(S(r, "FinalDiag3"), 5)
        + L(Date(D(r, "CASDateAdmitted")), 8) + L(Date(D(r, "CASDischargeDate")), 8);

    private static string CaseDetails(IDictionary<string, object?> r) =>
        L(S(r, "CASClaimability"), 1) + L(S(r, "CASAdmissionReason"), 200) + L(S(r, "AttendDoctor"), 100) + L(S(r, "ClaimNatureCode"), 5)
        + L(S(r, "ModalityCode"), 1) + L(S(r, "CASStayType"), 1) + L(S(r, "CASPAYType"), 1) + L(S(r, "CASUDReasons1"), 100);

    /// <summary>PrincipalASCII: a worksheet with a bordx.</summary>
    public static string AcceptedRecord(IDictionary<string, object?> r) =>
        L(S(r, "BordxRefNo"), 15) + L(Date(D(r, "BordxDate")), 8) + L(S(r, "PAYCode"), 2) + L(S(r, "CASNumber"), 15)
        + L($"{S(r, "CASNumber")}-{S(r, "ClaimVersion")}", 15)
        + MemberAndPatient(r)
        + L(Money(O(r, "CLMTotalActual")), 12) + L(Money(O(r, "CLMTotalApproved")), 12)
        + CaseDetails(r) + L("", 3000) + L("", 984) + Cr;

    /// <summary>PrincipalASCIIRejectCase: a repudiated case (record type N, no bordx, no amounts, the repudiation codes and the reserve instead).</summary>
    public static string RejectedRecord(IDictionary<string, object?> r) =>
        L("N", 1) + L("", 15) + L("", 8) + L(S(r, "PAYCode"), 2) + L(S(r, "CASNumber"), 15) + L("", 15)
        + MemberAndPatient(r)
        + L("", 12) + L("", 12)
        + CaseDetails(r) + L("", 3000)
        + L(S(r, "CASREPCode1"), 10) + L(S(r, "CASREPCode2"), 10) + L(S(r, "CASREPCode3"), 10) + L(Money(O(r, "CASReserveAmount")), 12) + L("", 942) + Cr;

    /// <summary>Case ASCII: a registered case plus the batch it was exported in.</summary>
    public static string CaseRecord(IDictionary<string, object?> r, DateTime batchDate, string batchNo) =>
        L(S(r, "CASASCIIAmendStatus") is { Length: > 0 } amend ? amend : "N", 1) + L(S(r, "PAYCode"), 2) + L(S(r, "CASNumber"), 15)
        + L(S(r, "MBMNumber"), 15) + L(S(r, "MBMPatientCovID"), 2) + L(S(r, "MBMPolicyNo"), 25) + L(S(r, "MBMPolSubNo1"), 25) + L(S(r, "MBMPolSubNo2"), 25)
        + L(Date(D(r, "MBMPayorEffDate")), 8) + L(Date(D(r, "MBMPayorExpDate")), 8) + L(S(r, "MBMName"), 60) + L(S(r, "MBMIcBcPp"), 20)
        + L(Date(D(r, "MBMDOB")), 8) + L(S(r, "PLNCode"), 4) + L(S(r, "MBMGroupCompany"), 50)
        + L(S(r, "PatientName"), 60) + L(S(r, "ICNo"), 20) + L(Date(D(r, "DOB")), 8)
        + L(Date(D(r, "CASDateAdmitted")), 8) + L(Date(D(r, "CASDischargeDate")), 8)
        + L(S(r, "CASClaimability"), 1) + L(S(r, "CASAdmissionReason"), 200) + L(S(r, "CASStayType"), 1) + L(S(r, "CASPAYType"), 1)
        + L(Currency(O(r, "CASReserveAmount")), 8) + L(Currency(O(r, "CASLGAmt")), 8)
        + L(Date(batchDate), 8) + L(batchNo, 5) + L("", 4370) + Cr;

    /// <summary>The first line of a claim file (the desktop trimmed it, so it is not padded). Accepted files carry the sender code MX.</summary>
    public static string ClaimHeader(string senderCode, string batchNo, DateTime date) =>
        ("F" + senderCode + batchNo + Date(date) + L("", 174)).Trim();

    /// <summary>The last line of a claim file: <c>T</c> and the record count, padded to 175 characters.</summary>
    public static string ClaimTrailer(int records) => "T" + L(records.ToString(CultureInfo.InvariantCulture), 10) + L("", 164);

    /// <summary>txtFilName &amp; "(" &amp; batch &amp; ")": the payor code, the date as YYMMDD and the batch number; no extension.</summary>
    public static string FileName(string payor, DateTime date, string batchNo) =>
        $"{payor}{date.ToString("yyMMdd", CultureInfo.InvariantCulture)}({batchNo})";

    /// <summary>The lines with the CRLF terminator after each.</summary>
    public static string Join(IEnumerable<string> lines) => string.Concat(lines.Select(l => l + Eol));

    /// <summary>The file bytes: ANSI (Latin-1), like VB6's <c>Print #</c>.</summary>
    public static byte[] Encode(string text) => Encoding.Latin1.GetBytes(text);
}
