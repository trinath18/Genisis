using System.Globalization;
using System.Text;
using Genisis.Api.Data;
using Genisis.Api.Membership;

namespace Genisis.Api.Bordx;

/// <summary>Generate ASCII of Upload Bordx (CmdGenerate_Click / PrincipalASCII / SupplementaryASCII).</summary>
public sealed class BordxAsciiExporter(DbConnections db)
{
    private sealed record AsciiMember(string MembershipNo, string? MemberType, string? InsuredType, string? PolicyNo, DateTime? EffectiveDate, string? Ic,
        string? Name, string? PlanCode, string? EmployeeNo, string? GroupCompany, string? PolicySubNo, DateTime? PolicyIssued, string? TransNo, string? PayNo,
        string? PaySeqNo, string? ClientNo, string? ClientId);
    private sealed record AsciiCovered(string? Relationship, string? Name, string? Ic, string? MembershipNo, string? CoverId, string? PayNo, string? PaySeqNo,
        string? ClientNo, string? ClientId);

    private const string FileDateFormat = "ddMMyyyy";
    private const string FileNameDateFormat = "yyMMdd";
    private const string LineEnd = "\r\n";
    /// <summary>The desktop wrote a bare carriage return at the end of each member record before the line end.</summary>
    private const string RecordEnd = "\r";

    /// <summary>Plan-code ranges of the desktop's Medicalife plan filter (cboPlan).</summary>
    private static readonly IReadOnlyDictionary<string, (string From, string To)> MedicalifePlans = new Dictionary<string, (string From, string To)>
    {
        ["Medicalife200"] = ("ML11", "ML14"),
        ["Medicalife206"] = ("ML61", "ML64"),
        ["Medicalife207"] = ("ML71", "ML74"),
        ["Medicalife206 - Rider"] = ("MR61", "MR64"),
        ["Medicalife207 - Rider"] = ("MR71", "MR74"),
    };

    public static IReadOnlyCollection<string> PlanFilters => [.. MedicalifePlans.Keys];

    /// <summary>Left-justifies and pads or truncates to the column width (the desktop's LSet).</summary>
    internal static string Pad(string? value, int width)
    {
        var text = value ?? "";
        return text.Length >= width ? text[..width] : text.PadRight(width);
    }

    private static string Date(DateTime? date) => date?.ToString(FileDateFormat, CultureInfo.InvariantCulture) ?? "";

    /// <returns>The file, or <c>null</c> when no member matches.</returns>
    public async Task<AsciiFile?> ExportAsync(AsciiRequest request, CancellationToken cancellationToken)
    {
        var health = request.HealthCode!.Trim().ToUpperInvariant();
        var payor = request.PayorCode!.Trim().ToUpperInvariant();
        var byBatch = request.ExportOption!.Trim().ToUpperInvariant() == AsciiExportOptions.Batch;
        var from = byBatch ? request.ReceiptDate!.Value.Date : request.BordxFrom!.Value.Date;
        var to = byBatch ? request.ReceiptDate!.Value.Date : request.BordxTo!.Value.Date;
        (string From, string To)? planRange = !byBatch && !string.IsNullOrWhiteSpace(request.Plan) ? MedicalifePlans[request.Plan.Trim()] : null;

        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var members = await conn.ProcQueryAsync<AsciiMember>("genisis.Bordx_AsciiMembers", new
        {
            hlt = health, pay = payor, from, to, batch = byBatch ? request.BatchNo : null, planFrom = planRange?.From, planTo = planRange?.To,
        });
        if (members.Count == 0) return null;

        var file = new StringBuilder();
        file.Append(RecordTypes.NewFileHeader).Append(payor).Append(Date(from)).Append(LineEnd);
        int principals = 0, supplementaries = 0;
        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            principals++;
            file.Append(PrincipalRecord(member)).Append(LineEnd);
            if (!InsuredTypes.IsFamily(member.InsuredType)) continue;
            foreach (var covered in await conn.ProcQueryAsync<AsciiCovered>("genisis.Bordx_AsciiCovered", new { number = member.MembershipNo }))
            {
                supplementaries++;
                file.Append(SupplementaryRecord(member, covered)).Append(LineEnd);
            }
        }
        file.Append(RecordTypes.Trailer).Append(Pad(principals.ToString(CultureInfo.InvariantCulture), AsciiLayout.TrailerCount))
            .Append(Pad(supplementaries.ToString(CultureInfo.InvariantCulture), AsciiLayout.TrailerCount)).Append(Pad("", AsciiLayout.TrailerFiller))
            .Append(LineEnd);

        var fileName = byBatch
            ? $"{payor}{from.ToString(FileNameDateFormat, CultureInfo.InvariantCulture)}({request.BatchNo})"
            : $"{payor}{to.ToString(FileNameDateFormat, CultureInfo.InvariantCulture)}";
        return new AsciiFile(fileName, BordxFile.Encoding.GetBytes(file.ToString()), principals, supplementaries);
    }

    private static string PrincipalRecord(AsciiMember m)
    {
        // Separate-limit principals (Q) are written as P.
        var recordType = m.MemberType?.Trim() == MemberTypes.PrincipalSeparate ? MemberTypes.PrincipalCombined : Pad(m.MemberType, AsciiLayout.RecordType);
        return recordType + Pad(m.PolicyNo, AsciiLayout.PolicyNo) + Pad(m.EmployeeNo, AsciiLayout.EmployeeNo) + Pad(m.Name, AsciiLayout.Name)
            + Pad(m.Ic, AsciiLayout.IcNumber) + Pad(m.MembershipNo, AsciiLayout.MembershipNo) + CoverIds.Principal + Pad(Date(m.EffectiveDate), AsciiLayout.Date)
            + Pad(m.GroupCompany, AsciiLayout.GroupCompany) + Pad(Date(m.PolicyIssued), AsciiLayout.Date) + Pad(m.PlanCode, AsciiLayout.PlanCode)
            + Pad(m.TransNo, AsciiLayout.TransactionNo) + Pad(m.PayNo, AsciiLayout.PayNo) + Pad(m.PaySeqNo, AsciiLayout.PaySequenceNo)
            + Pad(m.ClientNo, AsciiLayout.ClientNo) + Pad(m.ClientId, AsciiLayout.ClientId) + Pad(m.PolicySubNo, AsciiLayout.PolicySubNo)
            + Pad("", AsciiLayout.Filler) + RecordEnd;
    }

    private static string SupplementaryRecord(AsciiMember m, AsciiCovered c)
    {
        var cover = int.TryParse(c.CoverId, out var number) ? CoverIds.Format(number) : (c.CoverId ?? "").Trim();
        return Pad(c.Relationship, AsciiLayout.RecordType) + Pad(m.PolicyNo?.Trim(), AsciiLayout.PolicyNo) + Pad(m.EmployeeNo?.Trim(), AsciiLayout.EmployeeNo)
            + Pad(c.Name, AsciiLayout.Name) + Pad(c.Ic, AsciiLayout.IcNumber) + Pad(c.MembershipNo, AsciiLayout.MembershipNo) + Pad(cover, AsciiLayout.CoverId)
            + Pad(Date(m.EffectiveDate), AsciiLayout.Date) + Pad("", AsciiLayout.GroupCompany) + Pad("", AsciiLayout.Date) + Pad(m.PlanCode, AsciiLayout.PlanCode)
            + Pad("", AsciiLayout.TransactionNo) + Pad(c.PayNo, AsciiLayout.PayNo) + Pad(c.PaySeqNo, AsciiLayout.PaySequenceNo) + Pad(c.ClientNo, AsciiLayout.ClientNo)
            + Pad(c.ClientId, AsciiLayout.ClientId) + Pad(m.PolicySubNo, AsciiLayout.PolicySubNo) + Pad("", AsciiLayout.Filler) + RecordEnd
            + Pad("", AsciiLayout.SupplementaryTrailerFiller) + RecordEnd;
    }
}
