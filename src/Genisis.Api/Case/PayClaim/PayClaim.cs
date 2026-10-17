using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.PayClaim;

/// <summary>A member or covered person listed on the Member Details tab.</summary>
public record PayClaimMember
{
    /// <summary>MBM.MBMNumber.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Cover ID (00 = principal).</summary>
    public string CoverId { get; set; } = "";

    /// <summary>Patient name.</summary>
    public string? PatientName { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Policy effective date.</summary>
    public DateTime? PolicyEffective { get; set; }

    /// <summary>Policy expiry date.</summary>
    public DateTime? PolicyExpiry { get; set; }

    /// <summary>Address line 1 (principal only).</summary>
    public string? Add1 { get; set; }

    /// <summary>Address line 2 (principal only).</summary>
    public string? Add2 { get; set; }

    /// <summary>Address line 3 (principal only).</summary>
    public string? Add3 { get; set; }

    /// <summary>Post code (principal only).</summary>
    public string? PostCode { get; set; }

    /// <summary>State (principal only).</summary>
    public string? State { get; set; }

    /// <summary>The payor's company name (principal only).</summary>
    public string? InsuranceCompany { get; set; }
}

/// <summary>The case header the Pay &amp; Claim Details tab shows.</summary>
public record PayClaimCase
{
    /// <summary>CAS.CASNumber.</summary>
    public string CaseNumber { get; set; } = "";

    /// <summary>Membership number.</summary>
    public string? MembershipNo { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>The principal's name.</summary>
    public string? PrincipalName { get; set; }

    /// <summary>The patient's name.</summary>
    public string? PatientName { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? AdmissionDate { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? DischargeDate { get; set; }

    /// <summary>Hospital name.</summary>
    public string? HospitalName { get; set; }
}

/// <summary>One Pay &amp; Claim record; the flags are "Y" / "N" as stored.</summary>
public record PayClaimRecord
{
    /// <summary>CASPayClaim.CASNumber.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Pay &amp; Claim date.</summary>
    public DateTime? PayClaimDate { get; set; }

    /// <summary>Awaiting lab test result.</summary>
    public string? LabTest { get; set; }

    /// <summary>Conflicting information provided.</summary>
    public string? ConflictingInfo { get; set; }

    /// <summary>Further information / investigation required.</summary>
    public string? Investigation { get; set; }

    /// <summary>Coverage subject to radiology / scan result.</summary>
    public string? Radiology { get; set; }

    /// <summary>Non-panel hospital.</summary>
    public string? NonPanel { get; set; }

    /// <summary>No cashless 90 days.</summary>
    public string? NoCashless { get; set; }

    /// <summary>Others.</summary>
    public string? Others { get; set; }

    /// <summary>The "Others" remarks.</summary>
    public string? Remarks { get; set; }
}

/// <summary>Everything the Load button shows.</summary>
/// <param name="Case">The case header.</param>
/// <param name="Members">The principal and, for family / group types, the covered persons.</param>
/// <param name="Record">The case's Pay &amp; Claim record, or null.</param>
public record PayClaimView(PayClaimCase Case, List<PayClaimMember> Members, PayClaimRecord? Record);

/// <summary>The values Save sends.</summary>
public class PayClaimRequest
{
    /// <summary>True for Add (insert), false for Edit (update).</summary>
    public bool IsNew { get; set; }

    /// <summary>Pay &amp; Claim date.</summary>
    public DateTime? PayClaimDate { get; set; }

    /// <summary>Awaiting lab test result.</summary>
    public bool LabTest { get; set; }

    /// <summary>Conflicting information provided.</summary>
    public bool ConflictingInfo { get; set; }

    /// <summary>Further information / investigation required.</summary>
    public bool FurtherInfo { get; set; }

    /// <summary>Coverage subject to radiology / scan result.</summary>
    public bool Radiology { get; set; }

    /// <summary>Non-panel hospital.</summary>
    public bool NonPanel { get; set; }

    /// <summary>No cashless 90 days.</summary>
    public bool NoCashless { get; set; }

    /// <summary>Others.</summary>
    public bool Others { get; set; }

    /// <summary>Remarks, required when <see cref="Others"/> is set.</summary>
    public string? OthersRemark { get; set; }
}

/// <summary>SavePayClaim's checks, in the desktop's order.</summary>
public sealed class PayClaimRequestValidator : RequestValidator<PayClaimRequest>
{
    /// <summary>Longest "Others" remark the CASOthers column holds.</summary>
    public const int MaxRemark = 80;

    /// <summary>Declares the rules.</summary>
    public PayClaimRequestValidator()
    {
        RuleFor(r => r.PayClaimDate).NotNull().WithMessage("Please enter Pay&Claim Date");
        RuleFor(r => r).Must(r => !r.Others || !string.IsNullOrWhiteSpace(r.OthersRemark)).WithMessage("Please enter Remarks");
        RuleFor(r => r).Must(r => !r.Others || r.OthersRemark!.Trim().Length <= MaxRemark).WithMessage($"Remarks cannot be longer than {MaxRemark} characters.");
    }
}

/// <summary>Reads and writes Pay &amp; Claim data.</summary>
public interface IPayClaimRepository
{
    /// <summary>The case header, or null when the case does not exist.</summary>
    Task<PayClaimCase?> GetCaseAsync(IDbSession session, string cas);

    /// <summary>The members of the case.</summary>
    Task<List<PayClaimMember>> GetMembersAsync(IDbSession session, string cas);

    /// <summary>The case's Pay &amp; Claim record, or null.</summary>
    Task<PayClaimRecord?> GetRecordAsync(IDbSession session, string cas);

    /// <summary>Inserts or updates; returns <see cref="StoredProcedures.Ok"/>, <see cref="StoredProcedures.NotFound"/> or <see cref="StoredProcedures.Conflict"/>.</summary>
    Task<int> SaveAsync(IDbSession session, string cas, PayClaimRequest request, string userCode);
}

/// <inheritdoc />
public sealed class PayClaimRepository : IPayClaimRepository
{
    private static string Flag(bool value) => value ? "Y" : "N";

    /// <inheritdoc />
    public Task<PayClaimCase?> GetCaseAsync(IDbSession session, string cas) =>
        session.Connection.ProcFirstOrDefaultAsync<PayClaimCase>("genisis.PayClaim_Case", new { cas }, session.Transaction);

    /// <inheritdoc />
    public Task<List<PayClaimMember>> GetMembersAsync(IDbSession session, string cas) =>
        session.Connection.ProcQueryAsync<PayClaimMember>("genisis.PayClaim_Members", new { cas }, session.Transaction);

    /// <inheritdoc />
    public Task<PayClaimRecord?> GetRecordAsync(IDbSession session, string cas) =>
        session.Connection.ProcFirstOrDefaultAsync<PayClaimRecord>("genisis.PayClaim_Get", new { cas }, session.Transaction);

    /// <inheritdoc />
    public Task<int> SaveAsync(IDbSession session, string cas, PayClaimRequest request, string userCode) =>
        session.Connection.ProcSingleAsync<int>("genisis.PayClaim_Save", new
        {
            cas, isNew = request.IsNew, payClaimDate = request.PayClaimDate!.Value.Date,
            lab = Flag(request.LabTest), confInfo = Flag(request.ConflictingInfo), infoInv = Flag(request.FurtherInfo),
            covgSub = Flag(request.Radiology), nonPan = Flag(request.NonPanel), nonCash = Flag(request.NoCashless),
            cOthers = Flag(request.Others), others = request.Others ? request.OthersRemark!.Trim() : "", userCode,
        }, session.Transaction);
}

/// <summary>Case > Pay &amp; Claim (legacy frmCASPayClaim).</summary>
public class PayClaimService(IDbSessions sessions, IPayClaimRepository repository, ILogger<PayClaimService> log)
{
    /// <summary>cmdShow_Click. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task<PayClaimView> LoadAsync(string cas)
    {
        cas = cas.Trim();
        await using var session = await sessions.OpenAsync();
        var header = await repository.GetCaseAsync(session, cas) ?? throw new NotFoundException("Record not found!");
        return new(header, await repository.GetMembersAsync(session, cas), await repository.GetRecordAsync(session, cas));
    }

    /// <summary>cmdSave_Click: Add or Edit. Throws <see cref="ConflictException"/> when Add finds a record, <see cref="NotFoundException"/> when Edit finds none.</summary>
    public async Task SaveAsync(string cas, PayClaimRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        await SaveAsync(session, cas, request, userCode);
        await session.CommitAsync();
        log.LogInformation("Pay & Claim of case {CaseNumber} {Action} by {User}", cas, request.IsNew ? "added" : "updated", userCode);
    }

    /// <summary>Saves inside the caller's unit of work (nothing is committed here).</summary>
    public async Task SaveAsync(IDbSession session, string cas, PayClaimRequest request, string userCode)
    {
        switch (await repository.SaveAsync(session, cas.Trim(), request, userCode))
        {
            case StoredProcedures.Conflict:
                throw new ConflictException("This case already has a Pay & Claim record. Use Edit to change it.");
            case StoredProcedures.NotFound:
                throw new NotFoundException(request.IsNew ? "Record not found!" : "This case has no Pay & Claim record to edit.");
        }
    }
}
