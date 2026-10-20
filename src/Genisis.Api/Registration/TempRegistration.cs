using Genisis.Api.Data;

namespace Genisis.Api.Registration;

/// <summary>A previous membership as read by genisis.TempRegistration_Source.</summary>
public sealed class RenewalSourceMember
{
    public string MembershipNo { get; set; } = "";
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public string? PolicyNo { get; set; }
    public int? PolicyYear { get; set; }
    public string? Salutation { get; set; }
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public string? OtherIc { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? RaceCode { get; set; }
    public string? NationalityCode { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? City { get; set; }
    public string? PostCode { get; set; }
    public string? State { get; set; }
    public string? TelHome { get; set; }
    public string? TelMobile { get; set; }
    public string? TelOffice { get; set; }
    public string? Email { get; set; }
    public string? InsuredType { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? DateJoined { get; set; }
    public string? TakeOver { get; set; }
    public string? GuaranteeRenewal { get; set; }
    public string? InstallmentMode { get; set; }
    public string? GroupCompany { get; set; }
    public string? EmployeeNo { get; set; }
    public string? Department { get; set; }
    public string? Branch { get; set; }
    public string? AgentCode { get; set; }
    public string? Exclusion { get; set; }
    public string? Allergic { get; set; }
    public string? Remarks { get; set; }
}

public sealed class RenewalSourceCovered
{
    public string CoverId { get; set; } = "";
    public string? Relationship { get; set; }
    public string? Salutation { get; set; }
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? Occupation { get; set; }
    public string? Exclusion { get; set; }
    public string? Allergic { get; set; }
}

/// <summary>The registration form pre-filled for a renewal, and the next policy year the desktop showed.</summary>
public sealed record RenewalDraft(RegistrationRequest Request, int PolicyYear, string SourceHealthCode);

public interface ITempRegistrationService
{
    Task<RenewalDraft?> GetDraftAsync(string previousMembershipNo, CancellationToken cancellationToken);
}

/// <summary>
/// Membership &gt; Temp MBM Reg (legacy FrmTempMBMReg). The form is an older copy of Registration (frmNewMBMReg05) whose extra is
/// cmdShow_Click: load a previous membership into the form as a renewal. Saving goes through Registration, so the current
/// numbering, fee and lifetime rules apply.
/// </summary>
public sealed class TempRegistrationService(DbConnections db) : ITempRegistrationService
{
    public const string RenewalFlag = "R";

    public async Task<RenewalDraft?> GetDraftAsync(string previousMembershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        using var grid = await conn.ProcMultipleAsync("genisis.TempRegistration_Source", new { number = previousMembershipNo.Trim() });
        var member = await grid.ReadFirstOrDefaultAsync<RenewalSourceMember>();
        if (member is null) return null;
        var covered = (await grid.ReadAsync<RenewalSourceCovered>()).ToList();
        return Draft(member, covered);
    }

    /// <summary>
    /// cmdShow_Click: the member's details carry over; renewal flag R, previous membership and policy number filled in, policy year +1.
    /// The new policy number, dates, bordx date and batch are left for the user, as in the desktop.
    /// </summary>
    public static RenewalDraft Draft(RenewalSourceMember m, IReadOnlyList<RenewalSourceCovered> covered)
    {
        var request = new RegistrationRequest
        {
            HealthCode = m.HealthCode, PayorCode = m.PayorCode, Renewal = RenewalFlag, PreviousMembershipNo = m.MembershipNo,
            PreviousPolicyNo = m.PolicyNo, Salutation = m.Salutation, Name = m.Name, IcBcPp = m.IcBcPp, OtherIc = m.OtherIc,
            DateOfBirth = m.DateOfBirth, Sex = m.Sex, RaceCode = m.RaceCode, NationalityCode = m.NationalityCode, MaritalStatus = m.MaritalStatus,
            Address1 = m.Address1, Address2 = m.Address2, Address3 = m.Address3, City = m.City, PostCode = m.PostCode, State = m.State,
            TelHome = m.TelHome, TelMobile = m.TelMobile, TelOffice = m.TelOffice, Email = m.Email, InsuredType = m.InsuredType,
            PlanCode = m.PlanCode, DateJoined = m.DateJoined, TakeOver = string.IsNullOrWhiteSpace(m.TakeOver) ? "N" : m.TakeOver,
            GuaranteeRenewal = m.GuaranteeRenewal, InstallmentMode = m.InstallmentMode, GroupCompany = m.GroupCompany,
            EmployeeNo = m.EmployeeNo, Department = m.Department, Branch = m.Branch, AgentCode = m.AgentCode, Exclusion = m.Exclusion,
            Allergic = m.Allergic, Remarks = m.Remarks,
            CoveredPersons = [.. covered.Select(c => new CoveredPersonRequest
            {
                Relationship = c.Relationship, Salutation = c.Salutation, Name = c.Name, IcBcPp = c.IcBcPp, DateOfBirth = c.DateOfBirth,
                Sex = c.Sex, Occupation = c.Occupation, Exclusion = c.Exclusion, Allergic = c.Allergic,
            })],
        };
        return new RenewalDraft(request, (m.PolicyYear ?? 0) + 1, m.HealthCode ?? "");
    }
}
