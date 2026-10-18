namespace Genisis.Api.Tracking;

/// <summary>Keys of the reports the service treats specially.</summary>
public static class TrackingReportKeys
{
    public const string BordxMco = "bordx.mco";
    public const string BordxPremium = "bordx.premium";
    public const string BranchByAgent = "branch.agent";
    public const string InvoiceMco = "mco.ins-mco";
    public const string InvoicePremium = "mco.ins-premium";
}

/// <summary>Keys of the filter fields whose options come from the database or that only have a "blank" box.</summary>
public static class TrackingFieldKeys
{
    public const string Payor = "payor";
    public const string State = "state";
    public const string ProductCategory = "productCategory";
    public const string MemberTypeBlank = "memberTypeBlank";
    public const string SupplementaryRegistrationDate = "dorSupp";
}

/// <summary>Report types and selections of FrmNewMBMTracking (CboReportType / CboRptSelection) mapped to their MBMTracking cases.</summary>
public static class TrackingCatalog
{
    private static TrackingReport Detail(string key, int typeIndex, string type, string selection, int principalCase, int supplementaryOnlyCase,
        int bothSupplementaryCase, bool principalName = false) =>
        new(key, typeIndex, type, selection, ReportKind.Detail, principalCase, supplementaryOnlyCase, bothSupplementaryCase, principalName);

    private static TrackingReport Joined(string key, int typeIndex, string type, string selection, int principalCase, int supplementaryCase) =>
        new(key, typeIndex, type, selection, ReportKind.Joined, principalCase, supplementaryCase);

    private static TrackingReport Appended(string key, int typeIndex, string type, string selection, int principalCase, int supplementaryCase) =>
        new(key, typeIndex, type, selection, ReportKind.Appended, principalCase, supplementaryCase);

    private static TrackingReport Multi(string key, int typeIndex, string type, string selection, int principalCase, int supplementaryCase) =>
        new(key, typeIndex, type, selection, ReportKind.Multi, principalCase, supplementaryCase);

    public static readonly IReadOnlyList<TrackingReport> Reports =
    [
        Appended("ageband.payor-supp", 0, "Ageband", "By HLT, INS (By Payor Supp.)", 1, 2),
        Appended("ageband.all-supp", 0, "Ageband", "By HLT, INS (All Payor Supp.)", 9, 10),
        Appended("ageband.hlt-supp", 0, "Ageband", "By HLT Type (Supp.)", 5, 6),
        Appended("ageband.ins-supp", 0, "Ageband", "By INS Type (Supp.)", 15, 16),
        Joined("ageband.payor-prin", 0, "Ageband", "By HLT, INS (By Payor Prin.)", 11, 12),
        Joined("ageband.all-prin", 0, "Ageband", "By HLT, INS (All Payor Prin.)", 7, 8),
        Joined("ageband.hlt-prin", 0, "Ageband", "By HLT Type (Prin.)", 3, 4),
        Joined("ageband.ins-prin", 0, "Ageband", "By INS Type (Prin.)", 13, 14),

        Detail("agents.detail", 1, "Agents", "Detail Listing", 31, 32, 32),
        Joined("agents.summary", 1, "Agents", "General Summary", 27, 28),
        Joined("agents.race", 1, "Agents", "By Race", 25, 26),
        Joined("agents.insured", 1, "Agents", "By Insured Type", 17, 18),
        Appended("agents.plan-supp", 1, "Agents", "By Plan No (Supp.)", 23, 24),
        Joined("agents.payor", 1, "Agents", "By Payor", 19, 20),
        Joined("agents.top20", 1, "Agents", "Top 20 Summary", 29, 30),
        Joined("agents.plan-prin", 1, "Agents", "By Plan No (Prin.)", 21, 22),

        Joined("bordx.summary", 2, "Bordx", "Summary", 57, 58),
        Detail("bordx.detail", 2, "Bordx", "Details Listing", 35, 36, 36),
        Multi(TrackingReportKeys.BordxMco, 2, "Bordx", "INS Type & MBM Type (MCO)", 37, 38),
        Multi(TrackingReportKeys.BordxPremium, 2, "Bordx", "INS Type & MBM Type (Premium)", 47, 56),
        Joined("bordx.dor", 2, "Bordx", "By DOR", 33, 34),

        Joined("branch.summary", 3, "Branch", "General Summary", 75, 76),
        Joined("branch.payor", 3, "Branch", "By Payor", 75, 76),
        Joined("branch.all", 3, "Branch", "All Payor", 59, 60),
        Multi(TrackingReportKeys.BranchByAgent, 3, "Branch", "By Agent", 61, 62),

        Detail("date", 4, "Date", "", 77, 78, 78),

        Joined("effective.summary", 5, "Effective Period", "General Summary", 80, 81),
        Detail("effective.detail", 5, "Effective Period", "Details Listing", 79, 79, 79),

        Appended("gender.payor-supp", 6, "Gender", "By Payor based on Supp.", 85, 87),
        Appended("gender.all-supp", 6, "Gender", "All Payor based on Supp.", 82, 84),
        Joined("gender.payor-prin", 6, "Gender", "By Payor based on Prin.", 85, 86),
        Joined("gender.all-prin", 6, "Gender", "All Payor based on Prin.", 82, 83),

        Detail("general.details", 7, "General Listing", "General Listing (Details)", 88, 90, 89),
        Detail("general.listing", 7, "General Listing", "General Listing", 88, 90, 89),
        Detail("general.prin-name", 7, "General Listing", "General Listing (Prin. Name)", 91, 93, 92, principalName: true),
        Detail("general.label", 7, "General Listing", "Print Label", 96, 97, 98),

        Joined("group.summary", 8, "Group Company", "Summary", 166, 167),
        Joined("group.department", 8, "Group Company", "Summary (By Department)", 168, 169),

        Detail("ic.listing", 9, "IC/Pol Year/Renewal", "General Listing", 101, 102, 102),
        Joined("ic.policy-year", 9, "IC/Pol Year/Renewal", "Policy Year Summary", 103, 104),
        Joined("ic.expiry", 9, "IC/Pol Year/Renewal", "Expiry Summary", 99, 100),
        Joined("ic.renewal", 9, "IC/Pol Year/Renewal", "Renewal Summary", 107, 108),
        Joined("ic.takeover", 9, "IC/Pol Year/Renewal", "Takeover Summary", 109, 110),
        Joined("ic.premium-mode", 9, "IC/Pol Year/Renewal", "Pre Mode Summary", 105, 106),

        Joined("ins.payor", 10, "INS Type & MBM Type", "By Payor", 113, 114),
        Joined("ins.all", 10, "INS Type & MBM Type", "All Payor", 111, 112),

        Joined("month.eff-payor", 11, "MBM By Month", "Eff. Month (By Payor)", 117, 118),
        Joined("month.eff-all", 11, "MBM By Month", "Eff. Month (All Payor)", 115, 116),
        Joined("month.exp-payor", 11, "MBM By Month", "Exp. Month (By Payor)", 121, 122),
        Joined("month.exp-all", 11, "MBM By Month", "Exp. Month (All Payor)", 119, 120),
        Joined("month.reg-payor", 11, "MBM By Month", "Reg. Month (By Payor)", 125, 126),
        Joined("month.reg-all", 11, "MBM By Month", "Reg. Month (All Payor)", 123, 124),
        Joined("month.billing-payor", 11, "MBM By Month", "Billing Month (By Payor)", 180, 181),
        Joined("month.billing-all", 11, "MBM By Month", "Billing Month (All Payor)", 178, 179),
        Joined("month.bordx-payor", 11, "MBM By Month", "Bordx Month (By Payor)", 188, 189),
        Joined("month.bordx-all", 11, "MBM By Month", "Bordx Month (All Payor)", 186, 187),

        Detail("mco.detail", 12, "MCO Invoice", "Detail Listing", 127, 129, 128),
        Joined("mco.summary", 12, "MCO Invoice", "Summary", 132, 133),
        Multi(TrackingReportKeys.InvoiceMco, 12, "MCO Invoice", "INS Type & MBM Type (MCO)", 130, 131),
        Multi(TrackingReportKeys.InvoicePremium, 12, "MCO Invoice", "INS Type & MBM Type (Premium)", 130, 131),

        Joined("payor", 13, "Payor", "", 136, 137),

        Detail("medix-fee.listing", 14, "Payor & Medix Fee", "General Listing", 94, 95, 95),
        Joined("medix-fee.summary", 14, "Payor & Medix Fee", "Summary", 138, 139),

        Joined("piam", 15, "PIAM", "", 140, 141),

        Appended("plan.payor-supp", 16, "Plan Code", "By HLT, INS (By Payor Supp.)", 144, 145),
        Appended("plan.all-supp", 16, "Plan Code", "By HLT, INS (All Payor Supp.)", 142, 143),
        Appended("plan.age-payor-supp", 16, "Plan Code", "By HLT, INS, AGE (By Payor Supp.)", 148, 151),
        Appended("plan.age-all-supp", 16, "Plan Code", "By HLT, INS, AGE (All Payor Supp.)", 146, 150),
        Joined("plan.payor-prin", 16, "Plan Code", "By HLT, INS (By Payor Prin.)", 144, 145),
        Joined("plan.all-prin", 16, "Plan Code", "By HLT, INS (All Payor Prin.)", 142, 143),
        Joined("plan.age-payor-prin", 16, "Plan Code", "By HLT, INS, AGE (By Payor Prin.)", 148, 149),
        Joined("plan.age-all-prin", 16, "Plan Code", "By HLT, INS, AGE (All Payor Prin.)", 146, 147),

        Joined("policy", 17, "Policy", "", 152, 153),

        Joined("race.payor", 18, "Race", "By Payor", 156, 157),
        Joined("race.all", 18, "Race", "All Payor", 154, 155),

        Appended("relationship.insured", 19, "Relationship", "By Insured Type", 160, 161),
        Appended("relationship.ageband", 19, "Relationship", "By Ageband", 158, 159),

        Joined("states.payor", 20, "States", "By Payor", 164, 165),
        Joined("states.all", 20, "States", "All Payor", 162, 163),

        Joined("product.summary", 21, "Product Category", "Summary", 170, 171),
        Joined("product.eff", 21, "Product Category", "By Eff Month", 172, 173),
        Joined("product.exp", 21, "Product Category", "By Exp Month", 174, 175),
        Joined("product.reg", 21, "Product Category", "By Reg Month", 176, 177),
    ];

    public static TrackingReport? Find(string? key) => Reports.FirstOrDefault(r => r.Key == key);

    /// <summary>Options written as the desktop's combo items: "code - name".</summary>
    private static TrackingOption[] Options(params string[] items) => items.Select(item =>
    {
        var separator = item.IndexOf(OptionSeparator, StringComparison.Ordinal);
        return new TrackingOption(item[..separator], item[(separator + OptionSeparator.Length)..]);
    }).ToArray();

    private const string OptionSeparator = " - ";

    /// <summary>The desktop's filter controls (SearchRecord). Payor, state and product category options are filled from the database.</summary>
    public static readonly IReadOnlyList<TrackingField> Fields =
    [
        new("health", "Health Type", "Membership", FieldKind.Select, false, true, Options("S - Sihat", "N - Non-Sihat")),
        new(TrackingFieldKeys.Payor, "Payor", "Membership", FieldKind.Select, false, true),
        new("insuredGroup", "Insured Type", "Membership", FieldKind.Select, false, true,
            Options("0 - Individual", "1 - Family", "2 - Grp Individual", "3 - Grp Family", "4 - Ind & Fam", "5 - Ind & Grp Ind", "6 - Fam & Grp Fam", "7 - Grp Ind & Grp Fam")),
        new("status", "Status", "Membership", FieldKind.Select, false, true,
            Options("0 - Active", "1 - Cancel", "2 - Delete", "3 - Active & Cancel", "4 - Active & Suspended", "5 - Active & Suspended & Cancel", "6 - Suspended")),
        new("dataStatus", "Data Status", "Membership", FieldKind.Select, false, true, Options("A - Active", "D - Delete")),
        new("expired", "Expired", "Membership", FieldKind.Select, false, true, Options("N - No", "Y - Yes")),
        new("renewal", "Renewal", "Membership", FieldKind.Select, false, true, Options("N - New", "P - Partial", "R - ReNew", "T - Timely")),
        new("takeOver", "Take Over", "Membership", FieldKind.Select, false, true, Options("N - None", "Y - Take Over", "C - Conversion")),
        new("premiumMode", "Premium Mode", "Membership", FieldKind.Select, false, true, Options("A - Annually", "Q - Quarterly", "S - Semi Annual", "M - Monthly")),
        new(TrackingFieldKeys.MemberTypeBlank, "Member Type", "Membership", FieldKind.Text, false, true, BlankOnly: true),
        new("labelStatus", "Label Status", "Membership", FieldKind.Select, false, true, Options("Y - Yes")),
        new(TrackingFieldKeys.ProductCategory, "Product Category", "Membership", FieldKind.Select, false, true),
        new("membershipNo", "Membership No", "Membership", FieldKind.Text, false, true),
        new("policyNo", "Policy No", "Membership", FieldKind.Text, true, true),
        new("plan", "Plan Code", "Membership", FieldKind.Text, true, true),
        new("ageBand", "Age Band", "Membership", FieldKind.Text, true, true),
        new("policyYear", "Policy Year", "Membership", FieldKind.Text, true, true),
        new("groupCompany", "Group Company", "Membership", FieldKind.Text, false, true),
        new("department", "Department", "Membership", FieldKind.Text, false, true),
        new("employeeNo", "Employee No", "Membership", FieldKind.Text, true, true),
        new("agentCode", "Agent Code", "Membership", FieldKind.Text, false, true),

        new("ic", "IC No", "Person", FieldKind.Text, true, true),
        new("sex", "Sex", "Person", FieldKind.Select, false, true, Options("M - Male", "F - Female")),
        new("race", "Race", "Person", FieldKind.Select, false, true, Options("C - Chinese", "I - Indian", "M - Malay", "O - Other")),
        new("adultChild", "Adult / Child", "Person", FieldKind.Select, false, true, Options("A - Adult", "C - Child")),
        new("relationship", "Relationship", "Person", FieldKind.Select, false, true, Options("D - Daughter", "H - Husband", "S - Son", "W - Wife")),
        new(TrackingFieldKeys.State, "State", "Person", FieldKind.Select, false, true),
        new("postCode", "Post Code", "Person", FieldKind.Text, true, true),
        new("dob", "Date of Birth", "Person", FieldKind.Date, true, true),

        new("effDate", "Effective Date", "Dates", FieldKind.Date, true, true),
        new("expDate", "Expiry Date", "Dates", FieldKind.Date, true, true),
        new("dateJoined", "Date Joined", "Dates", FieldKind.Date, true, true),
        new("dor", "Date of Registration", "Dates", FieldKind.Date, true, true),
        new(TrackingFieldKeys.SupplementaryRegistrationDate, "Supp. Registration Date", "Dates", FieldKind.Date, true, true),
        new("bordxDate", "Bordx Date", "Dates", FieldKind.Date, true, true),
        new("batch", "Batch No", "Dates", FieldKind.Number, true, true),
        new("endtEffDate", "Endt. Effective Date", "Dates", FieldKind.Date, true, true),
        new("cancelDate", "Cancellation Date", "Dates", FieldKind.Date, true, true),

        new("invoiceNo", "MCO Invoice No", "Fees", FieldKind.Text, true, true),
        new("invoiceDate", "MCO Invoice Date", "Fees", FieldKind.Date, true, true),
        new("premium", "Premium Amount", "Fees", FieldKind.Number, true, true),
        new("mcoFee", "MCO Fee", "Fees", FieldKind.Number, true, true),

        new("dayInsured", "Days Insured", "Days", FieldKind.Number, true, true),
        new("ageDays", "Age (Days)", "Days", FieldKind.Number, true, true),
        new("delayBordx", "Delay Bordx", "Days", FieldKind.Number, true, true),
        new("delayInvBordx", "Delay Invoice Bordx", "Days", FieldKind.Number, true, true),
        new("delayReg", "Delay Registration", "Days", FieldKind.Number, true, true),
        new("delayInvD", "Delay Invoice (D)", "Days", FieldKind.Number, true, true),
        new("delayInvE", "Delay Invoice (E)", "Days", FieldKind.Number, true, true),
    ];
}
