using Genisis.Api.Common.Errors;

namespace Genisis.Api.Deletion;

/// <summary>Desktop cboDeleteOption.</summary>
public static class DeleteOptions
{
    public const string BordxDate = "D";
    public const string Batch = "B";
    public const string Membership = "M";

    public static readonly IReadOnlySet<string> All = new HashSet<string>([BordxDate, Batch, Membership]);
}

/// <summary>Messages of frmDeleteMembers; the desktop wording is kept.</summary>
public static class DeletionMessages
{
    public const string EnterHealthPlan = "Please Enter Health Plan";
    public const string EnterPayorCode = "Please Enter Payor Code";
    public const string EnterOption = "Please Enter Export Option";
    public const string EnterReceiptDateOrBatch = "Please Enter Receipt Date or Batch No";
    public const string EnterBordxDate = "Please Enter Bordx Date";
    public const string EnterMembershipNo = "Please Enter Membership No";
    public const string NoRecord = "No Record Found";
    public const string Listed = "File Listed Sucessfully...";
    public const string Deleted = "File Deleted Sucessfully...";
    public const string AlreadyArchived = "One of these memberships is already in the archive database (MBMArchive), so nothing was deleted.";
    public const string ArchiveUnavailable =
        "The archive database cannot be written: a table's primary key index is disabled (MBMArchive / MBMCoveredPersonsArchive). " +
        "Ask the DBA to rebuild it (ALTER INDEX ALL ON <table> REBUILD); nothing was deleted.";
}

public static class DeletionLimits
{
    /// <summary>The list shows at most this many principals; Delete always applies to every selected membership.</summary>
    public const int ListLimit = 5000;
}

/// <summary>The health type, payor, delete option and that option's range, as on the desktop form.</summary>
public sealed class DeletionCriteria
{
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public string? Option { get; set; }
    public DateTime? BordxDateFrom { get; set; }
    public DateTime? BordxDateTo { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public int? BatchNo { get; set; }
    public string? NumberFrom { get; set; }
    public string? NumberTo { get; set; }
}

public sealed record DeletionOption(string Code, string? Name);

public sealed record DeletionLookups(List<DeletionOption> HealthCodes, List<DeletionOption> Payors);

public sealed class DeletionRow
{
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
    public string? Name { get; set; }
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }
}

public sealed record DeletionListResult(string Message, List<DeletionRow> Rows, int TotalPrincipal, int TotalSupplementary, int Memberships, bool Truncated);

public sealed record DeletionResult(string Message, int TotalPrincipal, int TotalSupplementary, int Memberships);

public sealed class DeletionException(string message) : BusinessRuleException(message);

internal sealed class DeletionPrincipal
{
    public string MembershipNo { get; set; } = "";
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }
    public string? Name { get; set; }
    public string? MemberType { get; set; }
    public string? InsuredType { get; set; }
}

internal sealed class DeletionCovered
{
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
    public string? Name { get; set; }
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }
    public string? MemberType { get; set; }
}

internal sealed record DeletionCounts(int Principals, int Supplementaries, int Memberships);
