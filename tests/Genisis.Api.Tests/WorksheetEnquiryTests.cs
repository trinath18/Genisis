using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet.Enquiry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet Enquiry: the rules and the service without a database, and the repository against the development databases.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetEnquiryTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    // ---------------- rules ----------------

    [Theory]
    [InlineData("1", EnquiryVersionKind.Single, 1)]
    [InlineData(" 12 ", EnquiryVersionKind.Single, 12)]
    [InlineData("M3", EnquiryVersionKind.Cumulative, 3)]
    [InlineData("m26", EnquiryVersionKind.Cumulative, 26)]
    [InlineData("Master", EnquiryVersionKind.Master, 0)]
    [InlineData("master", EnquiryVersionKind.Master, 0)]
    public void A_version_selection_reads_as_one_version_the_first_n_or_all(string text, EnquiryVersionKind kind, int number) =>
        Assert.Equal(new EnquirySelection(kind, number), EnquiryRules.ParseVersion(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("M")]
    [InlineData("M-1")]
    [InlineData("1234")]
    [InlineData("Masters")]
    public void Anything_else_is_not_a_version(string? text) => Assert.Null(EnquiryRules.ParseVersion(text));

    [Fact]
    public void A_selection_is_spelled_as_the_drop_down_spells_it()
    {
        Assert.Equal("3", new EnquirySelection(EnquiryVersionKind.Single, 3).Text);
        Assert.Equal("M3", new EnquirySelection(EnquiryVersionKind.Cumulative, 3).Text);
        Assert.Equal("Master", new EnquirySelection(EnquiryVersionKind.Master, 0).Text);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("B", null)]
    [InlineData("C", "H")]
    [InlineData("r", "M")]
    public void Cashless_keeps_pay_to_hospital_and_reimbursement_pay_to_member(string? claimType, string? payTo) => Assert.Equal(payTo, EnquiryRules.PayTo(claimType));

    [Fact]
    public void Codes_are_shown_in_the_desktops_words()
    {
        Assert.Equal("Accepted", EnquiryRules.ClaimabilityText("AA "));
        Assert.Equal("Pay & Claim/Rejected", EnquiryRules.ClaimabilityText("PR"));
        Assert.Null(EnquiryRules.ClaimabilityText("ZZ"));
        Assert.Equal("AA - Accepted", EnquiryRules.WorksheetStatusText("AA"));
        Assert.Equal("RR - Rejected", EnquiryRules.WorksheetStatusText("RR"));
        Assert.Equal("AI - Accepted/Insurance", EnquiryRules.WorksheetStatusText("AI"));
        Assert.Null(EnquiryRules.WorksheetStatusText(""));
        Assert.Equal("Open", EnquiryRules.CaseStatusText("O"));
        Assert.Equal("Close", EnquiryRules.CaseStatusText("c"));
        Assert.Equal("Repudiated", EnquiryRules.CaseStatusText("R"));
        Assert.Null(EnquiryRules.CaseStatusText(null));
    }

    [Fact]
    public void Payor_ib_renames_the_psychiatric_and_maternity_rows()
    {
        Assert.Equal("Maternity", EnquiryRules.BenefitName(24, "HOME NURSING / DAY CARE", "IB"));
        Assert.Equal("Psychiatric", EnquiryRules.BenefitName(16, "Physiotherapy Treatment", " ib "));
        Assert.Equal("Room & Board", EnquiryRules.BenefitName(1, "Room & Board", "IB"));
        Assert.Equal("HOME NURSING / DAY CARE", EnquiryRules.BenefitName(24, "HOME NURSING / DAY CARE", "AH"));
    }

    [Fact]
    public void Totals_add_each_column_and_treat_a_blank_as_zero()
    {
        var totals = EnquiryRules.Totals([
            new EnquiryBenefit { Actual = 100, Approved = 80, Hospital = 80, Member = 0 },
            new EnquiryBenefit { Actual = 50.5m, Approved = null, Hospital = null, Member = -10 },
        ]);
        Assert.Equal(new EnquiryTotals { Actual = 150.5m, Approved = 80, Hospital = 80, Member = -10 }, totals);
    }

    [Fact]
    public void A_guarantee_letter_is_listed_as_b_or_l_case_dash_number()
    {
        Assert.Equal("BAH001-2", EnquiryRules.LgReference("B", "AH001 ", 2));
        Assert.Equal("LAH001-3", EnquiryRules.LgReference(" ", "AH001", 3));
    }

    [Fact]
    public void The_desktop_warned_when_a_guarantee_letter_is_not_allowed()
    {
        Assert.Equal(["GL is NOT allowed for this member"], EnquiryRules.Notices(new EnquiryHeader { GlIssuance = "N " }));
        Assert.Empty(EnquiryRules.Notices(new EnquiryHeader { GlIssuance = "Y" }));
        Assert.Empty(EnquiryRules.Notices(new EnquiryHeader()));
    }

    [Fact]
    public void A_diagnosis_line_is_listed_when_anything_on_it_is_filled()
    {
        Assert.False(EnquiryRules.HasDiagnosis(new EnquiryDiagnosis { Diagnosis = "  ", OnsetDate = null, IcdCode = "\t" }));
        Assert.True(EnquiryRules.HasDiagnosis(new EnquiryDiagnosis { IcdCode = "K60" }));
        Assert.True(EnquiryRules.HasDiagnosis(new EnquiryDiagnosis { OnsetDate = "01/01/2021" }));
    }

    // ---------------- validators ----------------

    [Fact]
    public void The_search_needs_two_characters_and_a_known_kind()
    {
        var validator = new EnquiryCaseSearchValidator();
        Assert.Equal("Enter at least 2 characters.", validator.FirstError(new EnquiryCaseSearch { Q = " a " }));
        Assert.Equal("Search by number, member, patient, ic or policy.", validator.FirstError(new EnquiryCaseSearch { By = "name", Q = "AH" }));
        Assert.Null(validator.FirstError(new EnquiryCaseSearch { By = "IC", Q = "AH" }));
        Assert.Null(validator.FirstError(new EnquiryCaseSearch { Q = "AH" }));
    }

    [Fact]
    public void The_version_and_claim_type_must_be_ones_the_drop_downs_offer()
    {
        var validator = new EnquiryQueryValidator();
        Assert.Equal("Please select a version nombor!", validator.FirstError(new EnquiryQuery { Version = "x" }));
        Assert.Equal("Select Both, Cashless or Reimbursement.", validator.FirstError(new EnquiryQuery { Version = "M2", ClaimType = "X" }));
        Assert.Null(validator.FirstError(new EnquiryQuery { Version = "Master", ClaimType = "c" }));
        Assert.Null(validator.FirstError(new EnquiryQuery()));
    }

    // ---------------- service (fake repository) ----------------

    private sealed class FakeEnquiryRepository : IEnquiryRepository
    {
        public List<EnquiryVersion> Versions = [new() { ClaimVersion = 1 }, new() { ClaimVersion = 2 }, new() { ClaimVersion = 3 }];
        public Dictionary<int, EnquiryHeader> Headers = new() { [1] = Header(), [2] = Header(), [3] = Header() };
        public List<EnquiryBenefit> Benefits = [];
        public List<EnquiryDiagnosis> Diagnoses = [];
        public List<EnquiryLgRow> Lgs = [];
        public (int From, int To)? BenefitRange;
        public int? HeaderVersion, InvoiceVersion, OthersVersion;
        public string? InvoicePayTo;
        public EnquiryCaseSearch? Search;

        private static EnquiryHeader Header() => new() { CaseNo = "AH001", Payor = "AH", Claimability = "AA", CaseStatus = "O", WsStatus = "AA" };

        public Task<List<EnquiryCaseRow>> SearchCasesAsync(IDbSession session, EnquiryCaseSearch search, int max)
        {
            Search = search;
            return Task.FromResult(new List<EnquiryCaseRow> { new() { CaseNo = "AH001" } });
        }

        public Task<List<EnquiryVersion>> VersionsAsync(IDbSession session, string caseNo) => Task.FromResult(Versions);

        public Task<EnquiryHeader?> HeaderAsync(IDbSession session, string caseNo, int version)
        {
            HeaderVersion = version;
            return Task.FromResult(Headers.GetValueOrDefault(version));
        }

        public Task<List<EnquiryDiagnosis>> DiagnosesAsync(IDbSession session, string caseNo) => Task.FromResult(Diagnoses);

        public Task<List<EnquiryInvoice>> InvoicesAsync(IDbSession session, string caseNo, int? version, string? payTo)
        {
            (InvoiceVersion, InvoicePayTo) = (version, payTo);
            return Task.FromResult(new List<EnquiryInvoice>());
        }

        public Task<List<EnquiryBenefit>> BenefitsAsync(IDbSession session, string caseNo, int fromVersion, int toVersion)
        {
            BenefitRange = (fromVersion, toVersion);
            return Task.FromResult(Benefits);
        }

        public Task<EnquiryOthers?> OthersAsync(IDbSession session, string caseNo, int version)
        {
            OthersVersion = version;
            return Task.FromResult<EnquiryOthers?>(new EnquiryOthers { Note = "n" });
        }

        public Task<List<EnquiryLgRow>> LgListAsync(IDbSession session, string caseNo) => Task.FromResult(Lgs);
    }

    private static EnquiryService Service(FakeSessions sessions, FakeEnquiryRepository repository) => new(sessions, repository, NullLogger<EnquiryService>.Instance);

    [Fact]
    public async Task One_version_shows_that_versions_worksheet_and_invoices()
    {
        var repository = new FakeEnquiryRepository();
        var sessions = new FakeSessions();
        var result = await Service(sessions, repository).GetAsync(" AH001 ", new EnquiryQuery { Version = "2" });

        Assert.Equal("2", result.SelectedVersion);
        Assert.Equal((2, 2), repository.BenefitRange);
        Assert.Equal(2, repository.HeaderVersion);
        Assert.Equal(2, repository.InvoiceVersion);
        Assert.Equal(2, repository.OthersVersion);
        Assert.Equal([1, 2, 3], result.Versions.Select(v => v.ClaimVersion));
        Assert.All(sessions.Sessions, s => Assert.False(s.Transactional));
    }

    [Fact]
    public async Task A_cumulative_version_adds_versions_one_to_n_and_lists_all_accepted_invoices()
    {
        var repository = new FakeEnquiryRepository();
        var result = await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery { Version = "M2" });

        Assert.Equal("M2", result.SelectedVersion);
        Assert.Equal((1, 2), repository.BenefitRange);
        Assert.Equal(2, repository.HeaderVersion);
        Assert.Null(repository.InvoiceVersion);
    }

    [Fact]
    public async Task Master_adds_every_version_and_reads_the_header_of_the_last()
    {
        var repository = new FakeEnquiryRepository();
        var result = await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery { Version = "Master" });

        Assert.Equal("Master", result.SelectedVersion);
        Assert.Equal((1, 3), repository.BenefitRange);
        Assert.Equal(3, repository.HeaderVersion);
        Assert.Null(repository.InvoiceVersion);
    }

    [Fact]
    public async Task No_version_chosen_means_version_one_or_the_first_there_is()
    {
        var repository = new FakeEnquiryRepository();
        Assert.Equal("1", (await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery())).SelectedVersion);

        repository.Versions = [new() { ClaimVersion = 2 }, new() { ClaimVersion = 3 }];
        Assert.Equal("2", (await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery())).SelectedVersion);
    }

    [Fact]
    public async Task The_claim_type_only_filters_the_invoice_list()
    {
        var repository = new FakeEnquiryRepository();
        await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery { Version = "1", ClaimType = "C" });
        Assert.Equal("H", repository.InvoicePayTo);
        await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery { Version = "1", ClaimType = "R" });
        Assert.Equal("M", repository.InvoicePayTo);
        await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery { Version = "1", ClaimType = "B" });
        Assert.Null(repository.InvoicePayTo);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("M9")]
    public async Task A_version_the_case_does_not_have_is_record_not_found(string version)
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), new FakeEnquiryRepository()).GetAsync("AH001", new EnquiryQuery { Version = version }));
        Assert.Equal("Record not found!", error.Message);
    }

    [Fact]
    public async Task A_case_with_no_worksheet_or_no_header_is_record_not_found()
    {
        var noVersions = new FakeEnquiryRepository { Versions = [] };
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), noVersions).GetAsync("AH001", new EnquiryQuery()));

        var noHeader = new FakeEnquiryRepository { Headers = [] };
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), noHeader).GetAsync("AH001", new EnquiryQuery { Version = "1" }));
        Assert.Equal("Record not found!", error.Message);
    }

    [Fact]
    public async Task The_result_carries_the_words_totals_renamed_rows_diagnoses_letters_and_notices()
    {
        var repository = new FakeEnquiryRepository
        {
            Benefits = [new() { Slot = 16, Benefit = "Physiotherapy Treatment", Actual = 10, Approved = 8 }, new() { Slot = 1, Benefit = "Room & Board", Actual = 5, Approved = 5 }],
            Diagnoses = [new() { Source = "Final", Slot = 1, Diagnosis = "FISTULA" }, new() { Source = "Final", Slot = 2, Diagnosis = " ", IcdCode = " " }],
            Lgs = [new() { CaseNo = "AH001", LgNo = 1, LgStatus = "B" }, new() { CaseNo = "AH001", LgNo = 2, LgStatus = "" }],
        };
        repository.Headers[1] = new EnquiryHeader { CaseNo = "AH001", Payor = "IB", Claimability = "PA", CaseStatus = "C", WsStatus = "SA", GlIssuance = "N" };

        var result = await Service(new FakeSessions(), repository).GetAsync("AH001", new EnquiryQuery { Version = "1" });

        Assert.Equal("Pay & Claim/Accepted", result.Header.ClaimabilityText);
        Assert.Equal("Close", result.Header.CaseStatusText);
        Assert.Equal("SA - Special Approval", result.Header.WorksheetStatusText);
        Assert.Equal("Psychiatric", result.Benefits[0].Benefit);
        Assert.Equal("Room & Board", result.Benefits[1].Benefit);
        Assert.Equal(new EnquiryTotals { Actual = 15, Approved = 13 }, result.Totals);
        Assert.Equal("FISTULA", Assert.Single(result.Diagnoses).Diagnosis);
        Assert.Equal(["BAH001-1", "LAH001-2"], result.LgListing.Select(l => l.Reference));
        Assert.Equal(["GL is NOT allowed for this member"], result.Notices);
    }

    [Fact]
    public async Task The_search_passes_the_kind_and_text_through()
    {
        var repository = new FakeEnquiryRepository();
        var rows = await Service(new FakeSessions(), repository).SearchCasesAsync(new EnquiryCaseSearch { By = "member", Q = "AHHA" });
        Assert.Single(rows);
        Assert.Equal(("member", "AHHA"), (repository.Search!.By, repository.Search.Q));
    }

    // ---------------- database ----------------

    private static EnquiryService Live() => new(TestDb.Sessions(), new EnquiryRepository(), NullLogger<EnquiryService>.Instance);

    /// <summary>A case with a worksheet, its case and its member, and the most versions the development data has.</summary>
    private static async Task<string> RichCase()
    {
        await using var session = await TestDb.Sessions().OpenAsync();
        return await session.Connection.QueryFirstAsync<string>(
            """
            SELECT TOP (1) RTRIM(c.CASNumber) FROM genisis.WS_Claim c
            WHERE EXISTS (SELECT 1 FROM dbo.CAS s JOIN dbo.MBM m ON m.MBMNumber = s.MBMNumber WHERE s.CASNumber = c.CASNumber)
            GROUP BY c.CASNumber HAVING COUNT(*) BETWEEN 3 AND 8 AND MAX(CASE WHEN c.CLMTotalApproved > 0 THEN 1 ELSE 0 END) = 1
            ORDER BY c.CASNumber
            """, transaction: session.Transaction);
    }

    [Fact]
    public async Task The_search_finds_cases_by_number_and_says_how_many_versions_each_has()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var cas = await RichCase();
        var rows = await Live().SearchCasesAsync(new EnquiryCaseSearch { By = "number", Q = cas });
        var row = Assert.Single(rows, r => r.CaseNo == cas);
        Assert.InRange(row.Versions, 3, 8);
        Assert.Empty(await Live().SearchCasesAsync(new EnquiryCaseSearch { By = "number", Q = "NO SUCH CASE" }));
    }

    [Fact]
    public async Task A_case_shows_its_header_versions_invoices_benefits_and_the_three_version_views_agree()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var cas = await RichCase();
        var service = Live();

        var first = await service.GetAsync(cas, new EnquiryQuery { Version = "" });
        Assert.True(first.Versions.Count >= 3);
        Assert.Equal(cas, first.Header.CaseNo);
        Assert.False(string.IsNullOrWhiteSpace(first.Header.MemberNo));
        Assert.False(string.IsNullOrWhiteSpace(first.Header.Hospital));
        Assert.NotNull(first.Header.CaseStatusText);
        Assert.Equal(29, first.Benefits.Count);
        Assert.Equal(Enumerable.Range(1, 29), first.Benefits.Select(b => b.Slot));
        Assert.Equal("Room & Board", first.Benefits[0].Benefit);
        Assert.Equal("Service Tax - Lodger", first.Benefits[28].Benefit);

        var numbers = first.Versions.Select(v => v.ClaimVersion).ToList();
        var singleResults = new List<EnquiryResult>();
        foreach (var n in numbers) singleResults.Add(await service.GetAsync(cas, new EnquiryQuery { Version = n.ToString() }));
        var singles = singleResults.Select(r => r.Totals).ToList();

        var master = await service.GetAsync(cas, new EnquiryQuery { Version = "Master" });
        Assert.Equal(singles.Sum(t => t.Actual), master.Totals.Actual);
        Assert.Equal(singles.Sum(t => t.Approved), master.Totals.Approved);
        Assert.Equal(singles.Sum(t => t.Hospital), master.Totals.Hospital);
        Assert.Equal(numbers.Max(), master.Header.ClaimVersion);

        var cumulative = await service.GetAsync(cas, new EnquiryQuery { Version = $"M{numbers[1]}" });
        Assert.Equal(singles.Take(2).Sum(t => t.Actual), cumulative.Totals.Actual);
        Assert.Equal(numbers[1], cumulative.Header.ClaimVersion);

        var one = await service.GetAsync(cas, new EnquiryQuery { Version = numbers[0].ToString() });
        Assert.All(one.Invoices, i => Assert.Equal(numbers[0].ToString(), i.WsVersion));
        var cashless = await service.GetAsync(cas, new EnquiryQuery { Version = "Master", ClaimType = "C" });
        Assert.All(cashless.Invoices, i => Assert.Equal("H", i.PayTo));
        var reimbursement = await service.GetAsync(cas, new EnquiryQuery { Version = "Master", ClaimType = "R" });
        Assert.All(reimbursement.Invoices, i => Assert.Equal("M", i.PayTo));
    }

    [Fact]
    public async Task A_missing_case_or_version_is_record_not_found()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await Assert.ThrowsAsync<NotFoundException>(() => Live().GetAsync("NO SUCH CASE", new EnquiryQuery { Version = "1" }));
        var cas = await RichCase();
        await Assert.ThrowsAsync<NotFoundException>(() => Live().GetAsync(cas, new EnquiryQuery { Version = "999" }));
    }

    /// <summary>
    /// The row to column map of the 29 benefit slots is checked against the totals the desktop saved on the Claim row: the actual amounts
    /// of the grid add up to CLMTotalActual for the worksheets sampled. Approved is capped by the member's remaining limit when a
    /// worksheet exceeds it, so it matches CLMTotalApproved for most but not all.
    /// </summary>
    [Fact]
    public async Task The_benefit_grid_adds_up_to_the_totals_saved_on_the_claim()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, decimal? Actual, decimal? Approved)>(
            """
            SELECT TOP (150) RTRIM(c.CASNumber), c.ClaimVersion, c.CLMTotalActual, c.CLMTotalApproved FROM genisis.WS_Claim c
            WHERE c.ClaimStatus = 'C' AND c.CLMTotalActual > 0
              AND EXISTS (SELECT 1 FROM dbo.CAS s JOIN dbo.MBM m ON m.MBMNumber = s.MBMNumber WHERE s.CASNumber = c.CASNumber)
            ORDER BY c.CASNumber, c.ClaimVersion
            """, transaction: session.Transaction)).ToList();
        Assert.True(sample.Count >= 50, "expected a sample of worksheets");

        var repository = new EnquiryRepository();
        int actualMatches = 0, approvedMatches = 0;
        foreach (var (cas, version, actual, approved) in sample)
        {
            var totals = EnquiryRules.Totals(await repository.BenefitsAsync(session, cas, version, version));
            if (Math.Abs(totals.Actual - (actual ?? 0)) < 0.02m) actualMatches++;
            if (Math.Abs(totals.Approved - (approved ?? 0)) < 0.02m) approvedMatches++;
        }
        Assert.True(actualMatches >= sample.Count * 0.98, $"actual matched {actualMatches} of {sample.Count}");
        Assert.True(approvedMatches >= sample.Count * 0.85, $"approved matched {approvedMatches} of {sample.Count}");
    }
}
