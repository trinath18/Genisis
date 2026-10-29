using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.ClaimBordx.ReturnCase;

/// <summary>The worksheet and its bordx as the screen shows them (the Payor, Claim No, App. Amount, App. Status and Bordx boxes).</summary>
public record BordxReturnCaseRow
{
    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>The worksheet's number as the screen shows it: case-version.</summary>
    public string ClaimNo => $"{CaseNo.Trim()}-{ClaimVersion}";

    /// <summary>Approved amount of the worksheet.</summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>The worksheet's approved status as it is now.</summary>
    public string? AppStatus { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Bordx amount.</summary>
    public decimal? BordxAmount { get; set; }

    /// <summary>Bordx status.</summary>
    public string? BordxStatus { get; set; }
}

/// <summary>The worksheet to look up (the Claim No and Version boxes).</summary>
public class BordxReturnCaseFilter
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version (the screen starts on 1).</summary>
    public int Version { get; set; } = 1;
}

/// <summary>The approved status to set on a worksheet (<c>CmdToPayor_Click</c>).</summary>
public class BordxReturnCaseRequest
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The approved status: <c>N</c> (No) or <c>Y</c> (Yes).</summary>
    public string? Status { get; set; }
}

/// <summary>CmdSearch_Click's check: a case number is needed.</summary>
public sealed class BordxReturnCaseFilterValidator : RequestValidator<BordxReturnCaseFilter>
{
    /// <summary>Declares the rules.</summary>
    public BordxReturnCaseFilterValidator()
    {
        RuleFor(f => f.CaseNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Claim No");
        RuleFor(f => f.Version).GreaterThan(0).WithMessage("Please Enter Claim No");
    }
}

/// <summary>CmdToPayor_Click's checks: a worksheet and an approved status.</summary>
public sealed class BordxReturnCaseRequestValidator : RequestValidator<BordxReturnCaseRequest>
{
    /// <summary>Declares the rules.</summary>
    public BordxReturnCaseRequestValidator()
    {
        RuleFor(r => r.CaseNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Claim No");
        RuleFor(r => r.Status).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Please Enter Approved Status");
        RuleFor(r => r.Status).Must(s => s!.Trim().ToUpperInvariant() is "N" or "Y").WithMessage("Approved Status must be N - No or Y - Yes.");
    }
}

/// <summary>Reads one worksheet of a bordx sent to the payor and sets its approved status.</summary>
public interface IBordxReturnCaseRepository
{
    /// <summary>Every version of a case.</summary>
    Task<List<int>> VersionsAsync(IDbSession session, string caseNo);

    /// <summary>The worksheet and its bordx, or null when the bordx has not been sent to the payor.</summary>
    Task<BordxReturnCaseRow?> FindAsync(IDbSession session, string caseNo, int version);

    /// <summary>Sets the approved status; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> SetAsync(IDbSession session, string caseNo, int version, string status);
}

/// <inheritdoc />
public sealed class BordxReturnCaseRepository : IBordxReturnCaseRepository
{
    /// <inheritdoc />
    public async Task<List<int>> VersionsAsync(IDbSession session, string caseNo) =>
        (await session.Connection.ProcQueryAsync<int>("genisis.BordxReturnCase_Versions", new { cas = caseNo.Trim() }, session.Transaction)).ToList();

    /// <inheritdoc />
    public Task<BordxReturnCaseRow?> FindAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<BordxReturnCaseRow>("genisis.BordxReturnCase_Find", new { cas = caseNo.Trim(), version }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SetAsync(IDbSession session, string caseNo, int version, string status) =>
        await session.Connection.ProcScalarAsync<int>("genisis.BordxReturnCase_Set", new { cas = caseNo.Trim(), version, status = status.Trim().ToUpperInvariant() }, session.Transaction);
}

/// <summary>Claim Bordx > Bor Delivery > Returned Fr Payor (Not App) (legacy <c>FrmReturnPayorCase</c>).</summary>
public class BordxReturnCaseService(IDbSessions sessions, IBordxReturnCaseRepository repository, ILogger<BordxReturnCaseService> log)
{
    /// <summary>TxtCaseNo_LostFocus: the versions of the case for the drop-down.</summary>
    public async Task<List<int>> VersionsAsync(string caseNo)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.VersionsAsync(session, caseNo);
    }

    /// <summary>CmdSearch_Click: the worksheet and its bordx. Throws <see cref="NotFoundException"/> ("Bordx not send to Payor!") when its bordx has not been sent to the payor.</summary>
    public async Task<BordxReturnCaseRow> FindAsync(BordxReturnCaseFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.FindAsync(session, filter.CaseNo!, filter.Version) ?? throw new NotFoundException("Bordx not send to Payor!");
    }

    /// <summary>CmdToPayor_Click: sets the worksheet's approved status. Throws <see cref="NotFoundException"/> when its bordx has not been sent to the payor.</summary>
    public async Task SetAsync(BordxReturnCaseRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.SetAsync(session, request.CaseNo!, request.Version, request.Status!) == StoredProcedures.NotFound) throw new NotFoundException("Bordx not send to Payor!");
        await session.CommitAsync();
        log.LogInformation("Approved status of {Case}-{Version} set to {Status} by {User}", request.CaseNo!.Trim(), request.Version, request.Status!.Trim().ToUpperInvariant(), userCode);
    }
}
