namespace Genisis.Api.Maintenance.Security;

/// <summary>One checkbox of frmUserPermissionMaintenance: a position of USR.USRAccess, its tab, its group and its caption.</summary>
/// <param name="Position">1-based position in USRAccess (the ACS control index), which is what CheckAccess and [RequireAccess] test.</param>
/// <param name="Tab">The form's tab: General, Maintenance or Access Control (CIS).</param>
/// <param name="Group">The group the desktop's per-group "Check" button covers.</param>
/// <param name="Caption">The checkbox caption.</param>
public sealed record AccessEntry(int Position, string Tab, string Group, string Caption);

/// <summary>
/// The access matrix of User Permission and the rules for building USR.USRAccess from it. Pure functions, no database.
/// The entries were read from the form's layout (ACS(1) to ACS(178)); positions after the last one are kept as they are, so
/// rights added later by other screens are not lost.
/// </summary>
public static class AccessMatrix
{
    /// <summary>The character of a granted position.</summary>
    public const char Granted = '1';

    /// <summary>The character of a position that is not granted.</summary>
    public const char Denied = '0';

    /// <summary>Maintenance > Security > Department (ACS 111).</summary>
    public const int DepartmentPosition = 111;

    /// <summary>Maintenance > Security > User Permission (ACS 112); also guards Role Management.</summary>
    public const int UserPermissionPosition = 112;

    /// <summary>Every checkbox, in position order.</summary>
    public static readonly IReadOnlyList<AccessEntry> Entries =
    [
        new(1, "General", "Membership", "Registration"),
        new(2, "General", "Membership", "Adjustment"),
        new(3, "General", "Membership", "Enquiry"),
        new(4, "General", "Membership", "Leaflet & Card"),
        new(5, "General", "Membership", "Deletion"),
        new(6, "General", "Membership", "Tracking"),
        new(7, "General", "Membership", "Tracking (Jupiter)"),
        new(8, "General", "Membership", "Upload Bordx"),
        new(9, "General", "Membership", "Generate ASCII"),
        new(10, "General", "Membership", "Upload Bordx by IC"),
        new(11, "General", "Case", "Registration"),
        new(12, "General", "Case", "Adjustment"),
        new(13, "General", "Case", "Inquiry"),
        new(14, "General", "Case", "Tracking"),
        new(15, "General", "Case", "Tracking (Jupiter)"),
        new(16, "General", "Case", "Audit Review"),
        new(17, "General", "Case", "LG Tracking"),
        new(18, "General", "Case", "Claims ASCII"),
        new(19, "General", "MCO Fee", "Invoice"),
        new(20, "General", "MCO Fee", "Fee Listing"),
        new(21, "General", "MCO Fee", "Refund Listing"),
        new(22, "General", "Invoice", "Processing"),
        new(23, "General", "Invoice", "MBM Accounting"),
        new(24, "General", "Invoice", "HOS Accounting"),
        new(25, "General", "Invoice", "HOS (DR to MBM)"),
        new(26, "General", "Invoice", "HOS (CR to MBM)"),
        new(27, "General", "Invoice", "Tracking"),
        new(28, "General", "Invoice", "Tracking (Jupiter)"),
        new(29, "General", "Invoice", "Main DCN Checking"),
        new(30, "General", "Claim Bordx", "Insert Worksheet"),
        new(31, "General", "Claim Bordx", "Remove Worksheet"),
        new(32, "General", "Claim Bordx", "Print Bordx"),
        new(33, "General", "Claim Bordx", "Print Bordx Excess"),
        new(34, "General", "Claim Bordx", "Update Bordx"),
        new(35, "General", "Claim Bordx", "Send to Payor"),
        new(36, "General", "Claim Bordx", "Returned Fr Payor"),
        new(37, "General", "Claim Bordx", "Send to MNRB"),
        new(38, "General", "Claim Bordx", "Send to Accounts"),
        new(39, "General", "Claim Bordx", "A/C Period"),
        new(40, "General", "Claim Bordx", "Bordx Control"),
        new(41, "General", "Claim Bordx", "Outstanding Bordx"),
        new(42, "General", "Claim Bordx", "Bordx & Worksheet"),
        new(43, "General", "Claim Bordx", "Bordx Tracking"),
        new(44, "General", "Claim Bordx", "CLM Tracking 1"),
        new(45, "General", "Claim Bordx", "CLM Tracking 2"),
        new(46, "General", "Claim Bordx", "CLM Tracking 3"),
        new(47, "General", "Claim Bordx", "CLM Tracking 4"),
        new(48, "General", "Claim Bordx", "Storage Entry"),
        new(49, "General", "Claim Bordx", "Storage Enquiry"),
        new(50, "General", "Claim Bordx", "Tracking 1 (Jup.)"),
        new(51, "General", "Claim Bordx", "Tracking 2 (Jup.)"),
        new(52, "General", "Claim Bordx", "Tracking 3 (Jup.)"),
        new(53, "General", "Claim Bordx", "Tracking 4 (Jup.)"),
        new(54, "General", "Claim Bordx", "Date Selection"),
        new(55, "General", "Accounts", "Payment to HOS"),
        new(56, "General", "Accounts", "Payment to MBM"),
        new(57, "General", "Accounts", "Update Invoice"),
        new(58, "General", "Accounts", "Payment Tracking"),
        new(59, "General", "Accounts", "Receipt fr MBM"),
        new(60, "General", "Accounts", "Receipt fr Others"),
        new(61, "General", "Accounts", "Receipt fr Payor"),
        new(62, "General", "Accounts", "Receipt fr MNRB"),
        new(63, "General", "Accounts", "Receipt Tracking"),
        new(64, "General", "Worksheet", "Registration"),
        new(65, "General", "Worksheet", "Adjustment"),
        new(66, "General", "Worksheet", "Send to A/C"),
        new(67, "General", "Worksheet", "Tracking"),
        new(68, "General", "Worksheet", "Tracking (Jupiter)"),
        new(69, "Maintenance", "Entity", "Access"),
        new(70, "Maintenance", "Entity", "Hospital Info"),
        new(71, "Maintenance", "Entity", "Clinic Info"),
        new(72, "Maintenance", "Entity", "Payor Info"),
        new(73, "Maintenance", "Entity", "Payor Branch"),
        new(74, "Maintenance", "Entity", "Service Provider"),
        new(75, "Maintenance", "Entity", "Hospital Group"),
        new(76, "Maintenance", "Entity", "Clinic Group"),
        new(77, "Maintenance", "Entity", "Payor Group"),
        new(78, "Maintenance", "Entity", "Agent"),
        new(79, "Maintenance", "Entity", "Special Approval"),
        new(80, "Maintenance", "Plan", "Health Type"),
        new(81, "Maintenance", "Plan", "Insured Type"),
        new(82, "Maintenance", "Plan", "Plan Code"),
        new(83, "Maintenance", "Plan", "Premium"),
        new(84, "Maintenance", "Plan", "Annual Limit"),
        new(85, "Maintenance", "Plan", "MCO Fee"),
        new(86, "Maintenance", "Benefits", "Category"),
        new(87, "Maintenance", "Benefits", "Sub Category"),
        new(88, "Maintenance", "Benefits", "Benefit Entry"),
        new(89, "Maintenance", "Benefits", "Benefit Listing"),
        new(90, "Maintenance", "Case", "ICD Selection"),
        new(91, "Maintenance", "Case", "ICD Maintenance"),
        new(92, "Maintenance", "Case", "ICD Category"),
        new(93, "Maintenance", "Case", "Repudiation Code"),
        new(94, "Maintenance", "Case", "Repudiation Category"),
        new(95, "Maintenance", "Case", "Nature of Claim"),
        new(96, "Maintenance", "Case", "Treatment Modality"),
        new(97, "Maintenance", "Case", "Hospitalisation Type"),
        new(98, "Maintenance", "Others", "Age Band"),
        new(99, "Maintenance", "Others", "Occupation"),
        new(100, "Maintenance", "Others", "Relationship"),
        new(101, "Maintenance", "Others", "Nationality"),
        new(102, "Maintenance", "Others", "Race"),
        new(103, "Maintenance", "Others", "Language"),
        new(104, "Maintenance", "Others", "City"),
        new(105, "Maintenance", "Others", "State"),
        new(106, "Maintenance", "Others", "Work Nature"),
        new(107, "Maintenance", "Others", "Endorsement Status"),
        new(108, "Maintenance", "Others", "Policy Status"),
        new(109, "Maintenance", "Date", "Valid Date Range"),
        new(110, "Maintenance", "Suspension", "Suspension"),
        new(111, "Maintenance", "Security", "Department"),
        new(112, "Maintenance", "Security", "User Permission"),
        new(113, "Access Control (CIS)", "Membership", "Enquiry"),
        new(114, "Access Control (CIS)", "Membership", "Tracking"),
        new(115, "Access Control (CIS)", "MCO Fee", "MCO Invoice"),
        new(116, "Access Control (CIS)", "MCO Fee", "Fee Listing"),
        new(117, "Access Control (CIS)", "Case", "Clinic"),
        new(118, "Access Control (CIS)", "Case", "Member"),
        new(119, "Access Control (CIS)", "Case", "Adjustment"),
        new(120, "Access Control (CIS)", "Claim Bordx", "Claim Tracking 2"),
        new(121, "Access Control (CIS)", "Accounts", "To Clinic - Batch"),
        new(122, "Access Control (CIS)", "Claim Bordx", "Insert Worksheet"),
        new(123, "Access Control (CIS)", "Claim Bordx", "Remove Worksheet"),
        new(124, "Access Control (CIS)", "Claim Bordx", "Print Bordx"),
        new(125, "Access Control (CIS)", "Claim Bordx", "Send to Payor"),
        new(126, "Access Control (CIS)", "Claim Bordx", "Returned Fr Payor"),
        new(127, "Access Control (CIS)", "Claim Bordx", "Send to Accounts"),
        new(128, "Access Control (CIS)", "Claim Bordx", "A/C Period"),
        new(129, "Access Control (CIS)", "Claim Bordx", "Bordx Control"),
        new(130, "Access Control (CIS)", "Claim Bordx", "Outstanding Bordx"),
        new(131, "Access Control (CIS)", "Claim Bordx", "Bordx & Worksheet"),
        new(132, "Access Control (CIS)", "Claim Bordx", "Claim Tracking"),
        new(133, "Access Control (CIS)", "Accounts", "To Clinic"),
        new(134, "Access Control (CIS)", "Accounts", "To Member"),
        new(135, "Access Control (CIS)", "Accounts", "Fr Member"),
        new(136, "Access Control (CIS)", "Accounts", "Fr Payor"),
        new(137, "Access Control (CIS)", "Maintenance", "Entity"),
        new(138, "Access Control (CIS)", "Maintenance", "Plan"),
        new(139, "Access Control (CIS)", "Maintenance", "Case"),
        new(140, "Access Control (CIS)", "Maintenance", "Calculation"),
        new(141, "Access Control (CIS)", "Maintenance", "Benefit - Member"),
        new(142, "Access Control (CIS)", "Maintenance", "Benefit - Clinic"),
        new(143, "Access Control (CIS)", "Maintenance", "Check Item"),
        new(144, "Access Control (CIS)", "Tracking", "Clinic"),
        new(145, "General", "Accounts", "Enq -  Pay2Hos"),
        new(146, "General", "Accounts", "Enq -  Pay2MBM"),
        new(147, "Maintenance", "MMA", "MMA"),
        new(148, "General", "MCO Fee", "Invoice Manual Upd"),
        new(149, "General", "Membership", "NIAM Report"),
        new(150, "General", "Case", "MMA Fees"),
        new(151, "General", "Claim Bordx", "Storage Rep Case"),
        new(152, "General", "Claim Bordx", "Storage Rep Enq"),
        new(153, "Access Control (CIS)", "Accounts", "To Member - Batch"),
        new(154, "General", "Case", "Send to Claims"),
        new(155, "General", "Claim Bordx", "DCU Report 1"),
        new(156, "General", "Claim Bordx", "DCU Report 2"),
        new(157, "General", "Accounts", "Trust Account"),
        new(158, "Maintenance", "Print Control", "Listing"),
        new(159, "Maintenance", "Print Control", "Tracking"),
        new(160, "Access Control (CIS)", "Membership", "Tracking (Jupiter)"),
        new(161, "Access Control (CIS)", "Case", "Tracking"),
        new(162, "Access Control (CIS)", "Case", "Tracking (Jupiter)"),
        new(163, "Access Control (CIS)", "Claim Bordx", "Claim Tracking (Jupiter)"),
        new(164, "Access Control (CIS)", "Claim Bordx", "Claim Tracking 2 (Jupiter)"),
        new(165, "Access Control (CIS)", "Accounts", "Tracking"),
        new(166, "General", "Worksheet", "Claim Tracking (Jupiter)"),
        new(167, "Access Control (CIS)", "Accounts", "Tracking (Jupiter)"),
        new(168, "Maintenance", "Security", "Data Transfer"),
        new(169, "General", "Accounts", "Invoice - HOS"),
        new(170, "General", "Accounts", "Invoice - MBM"),
        new(171, "General", "Accounts", "Receipt - MNRB"),
        new(172, "General", "Accounts", "Receipt - Payor"),
        new(173, "General", "Accounts", "Payment - HOS"),
        new(174, "General", "Accounts", "Payment - MBM"),
        new(175, "General", "Accounts", "Bordx - MNRB"),
        new(176, "General", "Accounts", "Bordx - Payor"),
        new(177, "General", "Accounts", "Sales - Invoice"),
        new(178, "General", "Accounts", "Print Payment"),
    ];

    /// <summary>The number of checkboxes (the length of the access string the desktop wrote).</summary>
    public static int Size => Entries.Count;

    /// <summary>The granted positions of an access string (positions past the matrix included).</summary>
    public static List<int> GrantedPositions(string? access) =>
        [.. (access ?? "").Select((c, i) => (c, position: i + 1)).Where(x => x.c == Granted).Select(x => x.position)];

    /// <summary>Whether <paramref name="position"/> is granted in <paramref name="access"/>.</summary>
    public static bool IsGranted(string? access, int position) =>
        access is not null && position >= 1 && position <= access.Length && access[position - 1] == Granted;

    /// <summary>
    /// cmdSave_Click's AccessString: one character per checkbox (1 granted, 0 not), followed by whatever the existing string had
    /// after the matrix. Positions outside 1..<see cref="Size"/> in <paramref name="granted"/> are ignored.
    /// </summary>
    public static string Build(IEnumerable<int> granted, string? existing)
    {
        var set = granted.Where(p => p >= 1 && p <= Size).ToHashSet();
        var matrix = new string([.. Enumerable.Range(1, Size).Select(p => set.Contains(p) ? Granted : Denied)]);
        var tail = existing is { Length: var length } && length > Size ? existing[Size..] : "";
        return matrix + tail;
    }
}