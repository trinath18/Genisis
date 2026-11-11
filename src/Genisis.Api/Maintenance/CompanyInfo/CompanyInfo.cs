using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.CompanyInfo;

/// <summary>The company's details (the one row of <c>Company</c>).</summary>
public record CompanyInfoRow
{
    /// <summary>False when <c>dbo.Company</c> is not in this database.</summary>
    public bool TableExists { get; set; }

    /// <summary>The table has a row ("Record not found" when it does not).</summary>
    public bool Found { get; set; }

    /// <summary>Company code.</summary>
    public string? Code { get; set; }

    /// <summary>Company name.</summary>
    public string? Name { get; set; }

    /// <summary>Address.</summary>
    public string? Address { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Fax number.</summary>
    public string? Fax { get; set; }

    /// <summary>E-mail.</summary>
    public string? Email { get; set; }

    /// <summary>Homepage.</summary>
    public string? Homepage { get; set; }

    /// <summary>Current year.</summary>
    public string? CurrentYear { get; set; }
}

/// <summary>The details to save (<c>cmdUpdate_Click</c>).</summary>
public class CompanyInfoRequest
{
    /// <summary>Company code.</summary>
    public string? Code { get; set; }

    /// <summary>Company name.</summary>
    public string? Name { get; set; }

    /// <summary>Address.</summary>
    public string? Address { get; set; }

    /// <summary>Phone number (digits, "-" and "." as the desktop's number boxes allowed).</summary>
    public string? Phone { get; set; }

    /// <summary>Fax number.</summary>
    public string? Fax { get; set; }

    /// <summary>E-mail.</summary>
    public string? Email { get; set; }

    /// <summary>Homepage.</summary>
    public string? Homepage { get; set; }

    /// <summary>Current year (four digits).</summary>
    public string? CurrentYear { get; set; }
}

/// <summary>cmdUpdate_Click's checks, in the desktop's order, and the box lengths of the form.</summary>
public sealed partial class CompanyInfoRequestValidator : RequestValidator<CompanyInfoRequest>
{
    [GeneratedRegex(@"^[-.0-9]*$")]
    private static partial Regex NumberBox();

    [GeneratedRegex(@"^[0-9]{1,4}$")]
    private static partial Regex Year();

    /// <summary>Declares the rules.</summary>
    public CompanyInfoRequestValidator()
    {
        RuleFor(r => r.Code).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Please Enter Company Code");
        RuleFor(r => r.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Please Enter Company Name");
        RuleFor(r => r.CurrentYear).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Please Enter Company Current Year");
        RuleFor(r => r.CurrentYear).Must(v => Year().IsMatch(v!.Trim())).WithMessage("Company Current Year must be a year of four digits.");
        RuleFor(r => r.Code).MaximumLength(50).WithMessage("Company Code is too long.");
        RuleFor(r => r.Name).MaximumLength(150).WithMessage("Company Name is too long.");
        RuleFor(r => r.Address).MaximumLength(200).WithMessage("Address is too long.");
        RuleFor(r => r.Phone).Must(v => string.IsNullOrEmpty(v) || NumberBox().IsMatch(v.Trim())).WithMessage("Phone No must be a number.");
        RuleFor(r => r.Phone).MaximumLength(15).WithMessage("Phone No is too long.");
        RuleFor(r => r.Fax).Must(v => string.IsNullOrEmpty(v) || NumberBox().IsMatch(v.Trim())).WithMessage("Fax No must be a number.");
        RuleFor(r => r.Fax).MaximumLength(15).WithMessage("Fax No is too long.");
        RuleFor(r => r.Email).MaximumLength(150).WithMessage("Email is too long.");
        RuleFor(r => r.Homepage).MaximumLength(150).WithMessage("Homepage is too long.");
    }
}

/// <summary>Reads and saves the company's details.</summary>
public interface ICompanyInfoRepository
{
    /// <summary>The row, or one with <see cref="CompanyInfoRow.Found"/> false.</summary>
    Task<CompanyInfoRow> GetAsync(IDbSession session);

    /// <summary>Updates the row, or adds it when the table is empty: 0 updated, 1 added, -1 the table is not in this database.</summary>
    Task<int> SaveAsync(IDbSession session, CompanyInfoRequest request, string userCode, DateTime today);
}

/// <inheritdoc />
public sealed class CompanyInfoRepository : ICompanyInfoRepository
{
    /// <inheritdoc />
    public async Task<CompanyInfoRow> GetAsync(IDbSession session) =>
        await session.Connection.ProcFirstOrDefaultAsync<CompanyInfoRow>("genisis.CompanyInfo_Get", null, session.Transaction) ?? new CompanyInfoRow();

    /// <inheritdoc />
    public Task<int> SaveAsync(IDbSession session, CompanyInfoRequest r, string userCode, DateTime today) =>
        session.Connection.ProcScalarAsync<int>("genisis.CompanyInfo_Save", new
        {
            code = r.Code!.Trim(), name = r.Name!.Trim(), address = r.Address?.Trim() ?? "", phone = r.Phone?.Trim() ?? "", fax = r.Fax?.Trim() ?? "", email = r.Email?.Trim() ?? "",
            homepage = r.Homepage?.Trim() ?? "", currentYear = r.CurrentYear!.Trim(), userCode, today = today.Date,
        }, session.Transaction);
}

/// <summary>Maintenance > Company Info (legacy <c>FrmMaintenanceComInfo</c>, "Company Information").</summary>
public class CompanyInfoService(IDbSessions sessions, ICompanyInfoRepository repository, ILogger<CompanyInfoService> log)
{
    /// <summary>The message when <c>dbo.Company</c> is missing.</summary>
    public const string MissingTable = "The Company table is not in this database.";

    /// <summary>LoadCOMInfo: the saved details.</summary>
    public async Task<CompanyInfoRow> GetAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetAsync(session);
    }

    /// <summary>cmdUpdate_Click: saves the details.</summary>
    public async Task SaveAsync(CompanyInfoRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.SaveAsync(session, request, userCode, DateTime.Today) < 0) throw new BusinessRuleException(MissingTable);
        await session.CommitAsync();
        log.LogInformation("Company information saved by {User}", userCode);
    }
}
