namespace Genisis.Api.Tracking;

/// <summary>Limits of the Tracking screen.</summary>
public static class TrackingLimits
{
    /// <summary>Rows returned to the screen; Print to File (CSV export) returns every row.</summary>
    public const int ScreenRows = 5000;

    /// <summary>The desktop's ADO command timeout for these reports (seconds).</summary>
    public const int CommandTimeoutSeconds = 300;
}

/// <summary>"MBM Type" choice of the desktop (CboMbmType).</summary>
public static class MemberTypeSelection
{
    public const string Both = "B";
    public const string Principal = "P";
    public const string Supplementary = "S";
}

/// <summary>"Principal Type" choice of the desktop (cboPrincipalType).</summary>
public static class PrincipalTypeSelection
{
    public const string Both = "BOTH";
    public const string CombinedLimit = "C";
    public const string SeparateLimit = "S";
}

/// <summary>Codes understood by genisis.Tracking_Run's @memberTypeFilter.</summary>
public static class MemberTypeFilters
{
    public const string DetailCombined = "DETAIL_C";
    public const string DetailSeparate = "DETAIL_S";

    /// <summary>General Listing (Prin. Name): NAME_&lt;member type&gt;_&lt;principal type&gt;, e.g. NAME_P_C.</summary>
    public static string PrincipalName(string memberType, string principalType) => $"NAME_{memberType}_{principalType}";
}

/// <summary>Codes understood by genisis.Tracking_Run's @extra (insured type, optionally with adult / child).</summary>
public static class TrackingCountFilters
{
    public const string Individual = "I";
    public const string Family = "F";
    public const string GroupIndividual = "G";
    public const string GroupFamily = "H";
    public const string IndividualChild = "I_C";
    public const string IndividualAdult = "I_A";
    public const string GroupIndividualChild = "G_C";
    public const string GroupIndividualAdult = "G_A";
}

/// <summary>Column names returned by the MBMTracking case queries, plus the ones the API adds.</summary>
public static class TrackingColumns
{
    public const string RowType = "Type";
    public const string PrincipalCount = "TotalP";
    public const string SupplementaryCount = "TotalS";
    public const string Mco = "MCOGross";
    public const string Premium = "PremiumGross";
    public const string AccessFee = "EAFee";
    public const string SupplementaryMco = "SuppMCOGross";
    public const string SupplementaryPremium = "SuppPremiumGross";
    public const string Branch = "BRNCode";
    public const string Agent = "MBMAgentCode";
    public const string ClaimCount = "TotalClaim";
    public const string ClaimApproved = "AppAmt";
}

/// <summary>Value of the <see cref="TrackingColumns.RowType"/> column.</summary>
public static class TrackingRowTypes
{
    public const string Principal = "P";
    public const string Supplementary = "S";
}

/// <summary>MBMTracking cases used by Branch &gt; By Agent for its per-insured-type columns (no branch / agent restriction).</summary>
public static class BranchByAgentCases
{
    public const int Principals = 66;
    public const int Supplementaries = 70;
    public const int Claims = 74;
}
