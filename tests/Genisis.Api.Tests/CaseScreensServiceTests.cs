using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The business logic of the Case screens' services, tested against fake repositories (no database).</summary>
public class CaseScreensServiceTests
{
    // ---------------- Panel Hospitals ----------------

    private sealed class FakePanelRepository : IPanelHospitalsRepository
    {
        public string? AskedFor;
        public Task<List<PanelCode>> GetCodesAsync(IDbSession session) => Task.FromResult<List<PanelCode>>([new() { Code = "AC - AXA" }]);

        public Task<List<PanelHospital>> GetHospitalsAsync(IDbSession session, string code)
        {
            AskedFor = code;
            return Task.FromResult<List<PanelHospital>>([new() { Code = "H1", Name = "Hospital" }]);
        }
    }

    [Fact]
    public async Task Panel_hospitals_trims_the_code_and_uses_read_only_sessions()
    {
        var sessions = new FakeSessions();
        var repository = new FakePanelRepository();
        var service = new PanelHospitalsService(sessions, repository);
        Assert.Single(await service.GetCodesAsync());
        Assert.Single(await service.GetHospitalsAsync("  AC - AXA "));
        Assert.Equal("AC - AXA", repository.AskedFor);
        Assert.All(sessions.Sessions, s => Assert.False(s.Transactional));
    }

    // ---------------- Non-Disclosure ----------------

    private sealed class FakeNonDisclosureRepository : INonDisclosureRepository
    {
        public NonDisclosureView? View = new() { CaseNumber = "AC1", HasRecord = true };
        public int SaveResult;
        public NonDisclosureRequest? Saved;
        public Task<NonDisclosureView?> GetAsync(IDbSession session, string cas) => Task.FromResult(View);

        public Task<int> SaveAsync(IDbSession session, string cas, NonDisclosureRequest values)
        {
            Saved = values;
            return Task.FromResult(SaveResult);
        }
    }

    [Fact]
    public async Task Non_disclosure_saves_the_normalised_values_and_commits()
    {
        var sessions = new FakeSessions();
        var repository = new FakeNonDisclosureRepository();
        var service = new NonDisclosureService(sessions, repository, NullLogger<NonDisclosureService>.Instance);
        await service.SaveAsync("AC1", new() { PreCategory = "1", PreOthers = "ignored", UndCategory = "3", UndOthers = " gout " }, "TESTER");
        Assert.Equal(("", "gout"), (repository.Saved!.PreOthers, repository.Saved.UndOthers));
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Non_disclosure_reports_a_missing_case_and_does_not_commit()
    {
        var sessions = new FakeSessions();
        var service = new NonDisclosureService(sessions, new FakeNonDisclosureRepository { View = null, SaveResult = StoredProcedures.NotFound }, NullLogger<NonDisclosureService>.Instance);
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync("X", new(), "TESTER"))).Message);
        Assert.False(sessions.Last.Committed);
        Assert.True(sessions.Last.RolledBack);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync("X"));
    }

    // ---------------- Pay & Claim ----------------

    private sealed class FakePayClaimRepository : IPayClaimRepository
    {
        public PayClaimCase? Case = new() { CaseNumber = "AC1" };
        public PayClaimRecord? Record;
        public int SaveResult;
        public Task<PayClaimCase?> GetCaseAsync(IDbSession session, string cas) => Task.FromResult(Case);
        public Task<List<PayClaimMember>> GetMembersAsync(IDbSession session, string cas) => Task.FromResult<List<PayClaimMember>>([new() { MemberNo = "M1", CoverId = "00" }]);
        public Task<PayClaimRecord?> GetRecordAsync(IDbSession session, string cas) => Task.FromResult(Record);
        public Task<int> SaveAsync(IDbSession session, string cas, PayClaimRequest request, string userCode) => Task.FromResult(SaveResult);
    }

    [Fact]
    public async Task Pay_claim_load_combines_header_members_and_record()
    {
        var repository = new FakePayClaimRepository { Record = new() { CaseNo = "AC1", LabTest = "Y" } };
        var view = await new PayClaimService(new FakeSessions(), repository, NullLogger<PayClaimService>.Instance).LoadAsync(" AC1 ");
        Assert.Equal(("AC1", 1, "Y"), (view.Case.CaseNumber, view.Members.Count, view.Record!.LabTest));
        repository.Case = null;
        await Assert.ThrowsAsync<NotFoundException>(() => new PayClaimService(new FakeSessions(), repository, NullLogger<PayClaimService>.Instance).LoadAsync("AC1"));
    }

    [Theory]
    [InlineData(StoredProcedures.Conflict, true, typeof(ConflictException), "This case already has a Pay & Claim record. Use Edit to change it.")]
    [InlineData(StoredProcedures.NotFound, true, typeof(NotFoundException), "Record not found!")]
    [InlineData(StoredProcedures.NotFound, false, typeof(NotFoundException), "This case has no Pay & Claim record to edit.")]
    public async Task Pay_claim_save_maps_the_procedure_result_to_an_exception(int result, bool isNew, Type exception, string message)
    {
        var sessions = new FakeSessions();
        var service = new PayClaimService(sessions, new FakePayClaimRepository { SaveResult = result }, NullLogger<PayClaimService>.Instance);
        var thrown = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => service.SaveAsync("AC1", new() { IsNew = isNew, PayClaimDate = DateTime.Today }, "TESTER"));
        Assert.IsType(exception, thrown);
        Assert.Equal(message, thrown.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Pay_claim_save_commits_when_the_procedure_succeeds()
    {
        var sessions = new FakeSessions();
        await new PayClaimService(sessions, new FakePayClaimRepository(), NullLogger<PayClaimService>.Instance)
            .SaveAsync("AC1", new() { IsNew = true, PayClaimDate = DateTime.Today }, "TESTER");
        Assert.True(sessions.Last.Committed);
    }

    // ---------------- MMA / CPT ----------------

    private sealed class FakeMmaRepository : IMmaRepository
    {
        public string? ProcedurePattern, OpcsPattern, SurgeonPattern;
        public MmaProcedureRequest? Saved;
        public MmaSaveResult SaveResult = new(StoredProcedures.Ok, 7);
        public bool Deleted = true;
        public Task<List<MmaProcedure>> SearchProceduresAsync(IDbSession session, string? mainBodyPartPattern, string? opcsCodePattern)
        {
            (ProcedurePattern, OpcsPattern) = (mainBodyPartPattern, opcsCodePattern);
            return Task.FromResult(new List<MmaProcedure>());
        }

        public Task<MmaSaveResult> SaveProcedureAsync(IDbSession session, int? index, MmaProcedureRequest values, string userCode)
        {
            Saved = values;
            return Task.FromResult(SaveResult);
        }

        public Task<bool> DeleteProcedureAsync(IDbSession session, int index) => Task.FromResult(Deleted);

        public Task<List<MmaFee>> SearchFeesAsync(IDbSession session, string? surgeonPattern, string? anaesthetistPattern)
        {
            SurgeonPattern = surgeonPattern;
            return Task.FromResult(new List<MmaFee>());
        }

        public Task<MmaSaveResult> SaveFeeAsync(IDbSession session, int? index, MmaFeeRequest values, string userCode) => Task.FromResult(SaveResult);
        public Task<bool> DeleteFeeAsync(IDbSession session, int index) => Task.FromResult(Deleted);
    }

    [Fact]
    public async Task Mma_searches_with_escaped_contains_patterns()
    {
        var repository = new FakeMmaRepository();
        var service = new MmaService(new FakeSessions(), repository, NullLogger<MmaService>.Instance);
        await service.SearchProceduresAsync(new() { MainBodyPart = "eye_1", OpcsCode = " " });
        Assert.Equal(("%eye[_]1%", null), (repository.ProcedurePattern, repository.OpcsPattern));
        await service.SearchFeesAsync(new() { SurgeonCategory = "SC%" });
        Assert.Equal("%SC[%]%", repository.SurgeonPattern);
    }

    [Fact]
    public async Task Mma_save_normalises_the_procedure_and_returns_its_index()
    {
        var sessions = new FakeSessions();
        var repository = new FakeMmaRepository();
        var service = new MmaService(sessions, repository, NullLogger<MmaService>.Instance);
        Assert.Equal(7, await service.SaveProcedureAsync(null, new() { MainBodyPart = " Eye ", Part = "b" }, "TESTER"));
        Assert.Equal(("Eye", "B"), (repository.Saved!.MainBodyPart, repository.Saved.Part));
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Mma_reports_rows_that_no_longer_exist()
    {
        var repository = new FakeMmaRepository { SaveResult = new(StoredProcedures.NotFound, 0), Deleted = false };
        var service = new MmaService(new FakeSessions(), repository, NullLogger<MmaService>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveProcedureAsync(5, new(), "TESTER"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveFeeAsync(5, new(), "TESTER"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteProcedureAsync(5, "TESTER"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteFeeAsync(5, "TESTER"));
    }

    // ---------------- Audit review ----------------

    private sealed class FakeAuditRepository(int saveResult) : IAuditReviewRepository
    {
        public Task<AuditExtras> GetExtrasAsync(IDbSession session, string cas) => Task.FromResult(new AuditExtras(null, [], null));
        public Task<int> SaveAsync(IDbSession session, string cas, AuditReviewRequest request, string userCode) => Task.FromResult(saveResult);
    }

    [Fact]
    public async Task Audit_review_save_commits_or_reports_a_missing_case()
    {
        var sessions = new FakeSessions();
        await new AuditReviewService(sessions, new FakeAuditRepository(StoredProcedures.Ok), null!, NullLogger<AuditReviewService>.Instance).SaveAsync("AC1", new(), "TESTER");
        Assert.True(sessions.Last.Committed);

        var missing = new AuditReviewService(sessions, new FakeAuditRepository(StoredProcedures.NotFound), null!, NullLogger<AuditReviewService>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => missing.SaveAsync("X", new(), "TESTER"));
        Assert.False(sessions.Last.Committed);
    }

    // ---------------- Case transfer ----------------

    private sealed class FakeTransferRepository : ITransferRepository
    {
        public TransferCase? Case = new() { CaseNumber = "AC1", MemberNo = "M1", CoverId = "00" };
        public Dictionary<(string, string), TransferMember> Members = new()
        {
            [("M2", "00")] = new() { MemberNo = "M2", CoverId = "00", PatientName = "ALI", PolicyNo = "P2", Status = "A", CoverFound = true },
            [("M1", "00")] = new() { MemberNo = "M1", CoverId = "00", PatientName = "BEN", PolicyNo = "P1", Status = "A", CoverFound = true },
            [("M3", "00")] = new() { MemberNo = "M3", CoverId = "00", PatientName = "CAL", PolicyNo = "P3", Status = "D", CoverFound = true },
        };
        public TransferApplied Applied = new(StoredProcedures.Ok, 125.5m);
        public int Applies;
        public Task<TransferCase?> GetCaseAsync(IDbSession session, string cas) => Task.FromResult(Case);
        public Task<TransferMember?> GetMemberAsync(IDbSession session, string memberNo, string coverId) => Task.FromResult(Members.GetValueOrDefault((memberNo, coverId)));

        public Task<TransferApplied> ApplyAsync(IDbSession session, string cas, string memberNo, string coverId, string userCode)
        {
            Applies++;
            return Task.FromResult(Applied);
        }
    }

    [Fact]
    public async Task Transfer_moves_the_case_commits_and_reports_the_amount()
    {
        var sessions = new FakeSessions();
        var repository = new FakeTransferRepository();
        var result = await new TransferService(sessions, repository, NullLogger<TransferService>.Instance).TransferAsync("AC1", new() { MemberNo = " M2 ", CoverId = "00" }, "TESTER");
        Assert.Equal(125.5m, result.Moved);
        Assert.Equal(1, repository.Applies);
        Assert.True(sessions.Last.Committed);
    }

    [Theory]
    [InlineData("NOBODY", "00", "Member No not found.")]
    [InlineData("M1", "00", "The case is already on this member and cover.")]
    [InlineData("M3", "00", "Member status - 'DELETE'.")]
    public async Task Transfer_refuses_a_target_that_breaks_the_rules_and_changes_nothing(string member, string cover, string message)
    {
        var sessions = new FakeSessions();
        var repository = new FakeTransferRepository();
        var service = new TransferService(sessions, repository, NullLogger<TransferService>.Instance);
        var thrown = await Assert.ThrowsAsync<BusinessRuleException>(() => service.TransferAsync("AC1", new() { MemberNo = member, CoverId = cover }, "TESTER"));
        Assert.Equal(message, thrown.Message);
        Assert.Equal(0, repository.Applies);
        Assert.False(sessions.Last.Committed);
    }

    [Theory]
    [InlineData(2, "Member No not found.")]
    [InlineData(3, "The case is already on this member and cover.")]
    [InlineData(4, "The case's current member is not on the member table, so its limit cannot be given back. Contact IT.")]
    public async Task Transfer_maps_the_procedure_refusals_to_messages(int code, string message)
    {
        var sessions = new FakeSessions();
        var repository = new FakeTransferRepository { Applied = new(code, 0) };
        var service = new TransferService(sessions, repository, NullLogger<TransferService>.Instance);
        var thrown = await Assert.ThrowsAsync<BusinessRuleException>(() => service.TransferAsync("AC1", new() { MemberNo = "M2", CoverId = "00" }, "TESTER"));
        Assert.Equal(message, thrown.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Transfer_reports_unknown_cases_and_members()
    {
        var repository = new FakeTransferRepository { Case = null };
        var service = new TransferService(new FakeSessions(), repository, NullLogger<TransferService>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => service.LoadAsync("X"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.TransferAsync("X", new() { MemberNo = "M2", CoverId = "00" }, "TESTER"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.LookupAsync("NOBODY", "00"));
        Assert.Equal("M2", (await service.LookupAsync(" M2 ", " 00 ")).MemberNo);
    }

    // ---------------- Inpatient case listing ----------------

    private sealed class FakeListingRepository : IListingRepository
    {
        public Dictionary<string, ListingItem> Items = [];
        public List<string> Applied = [];
        public List<(string BordxNo, decimal Amount, int Claims, string CashType)> Bordx = [];
        public List<string> PayorsAsked = [];
        private int _next;

        public Task<List<ListingRow>> SearchAsync(IDbSession session, ListingFilter filter) => Task.FromResult(new List<ListingRow>());
        public Task<ListingItem?> GetItemAsync(IDbSession session, string cas, int version) => Task.FromResult(Items.GetValueOrDefault($"{cas}-{version}"));

        public Task<string> NextBordxNoAsync(IDbSession session, string payor)
        {
            PayorsAsked.Add(payor);
            return Task.FromResult($"N{payor}{++_next:0000000}");
        }

        public Task ApplyAsync(IDbSession session, string cas, int version, string bordxNo, string userCode)
        {
            Applied.Add($"{cas}-{version}:{bordxNo}");
            return Task.CompletedTask;
        }

        public Task SaveBordxAsync(IDbSession session, string bordxNo, decimal amount, int claims, string cashType, string userCode)
        {
            Bordx.Add((bordxNo, amount, claims, cashType));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDocuments(bool available, params string[] withDocuments) : IScannedDocuments
    {
        public bool Available => available;
        public bool HasDocuments(string cas) => withDocuments.Contains(cas);
    }

    private static ListingItem Worksheet(string cas, int version, decimal approved, string cashType = "H") => new()
    {
        CaseNo = cas, ClaimVersion = version, Dod = DateTime.Today, Hospital = "H", Claimability = "AA", HltType = "N", InsuredType = "G", GroupCompany = "ACME",
        CashType = cashType, TotalApproved = approved, CaseMgmtFees = 1,
    };

    private static FakeListingRepository Repository(params ListingItem[] items) => new() { Items = items.ToDictionary(i => $"{i.CaseNo}-{i.ClaimVersion}") };

    [Fact]
    public async Task Listing_multiple_claims_share_one_bordx_with_the_summed_amount()
    {
        var repository = Repository(Worksheet("AC001", 1, 100), Worksheet("AC002", 1, 50, "M"));
        var sessions = new FakeSessions();
        var service = new ListingService(sessions, repository, new FakeDocuments(false), NullLogger<ListingService>.Instance);
        var lines = await service.GenerateAsync(new() { Option = "Multiple Claims", Items = [new("AC001", 1), new("AC002", 1)] }, "TESTER");

        Assert.Equal(["NAC0000001"], lines.Select(l => l.BordxNo).Distinct());
        Assert.All(lines, l => Assert.Equal(152m, l.BordxAmount));
        Assert.Equal(["AC001-1:NAC0000001", "AC002-1:NAC0000001"], repository.Applied);
        Assert.Equal([("NAC0000001", 152m, 2, "M")], repository.Bordx);
        Assert.Equal(["AC"], repository.PayorsAsked);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Listing_single_claim_gives_every_worksheet_its_own_bordx()
    {
        var repository = Repository(Worksheet("AC001", 1, 100), Worksheet("TA002", 3, 50, "M"));
        var service = new ListingService(new FakeSessions(), repository, new FakeDocuments(false), NullLogger<ListingService>.Instance);
        var lines = await service.GenerateAsync(new() { Option = "Single Claim", Items = [new("AC001", 1), new("TA002", 3)] }, "TESTER");

        Assert.Equal(["NAC0000001", "NTA0000002"], lines.Select(l => l.BordxNo));
        Assert.Equal([101m, 51m], lines.Select(l => l.BordxAmount));
        Assert.Equal([("NAC0000001", 101m, 1, "H"), ("NTA0000002", 51m, 1, "M")], repository.Bordx);
        Assert.Equal(["AC", "TA"], repository.PayorsAsked);
    }

    [Fact]
    public async Task Listing_refuses_a_worksheet_that_is_no_longer_pending_and_commits_nothing()
    {
        var sessions = new FakeSessions();
        var repository = Repository(Worksheet("AC001", 1, 100));
        var service = new ListingService(sessions, repository, new FakeDocuments(false), NullLogger<ListingService>.Instance);
        var thrown = await Assert.ThrowsAsync<CaseListingException>(() => service.GenerateAsync(new() { Option = "Single Claim", Items = [new("AC001", 1), new("AC999", 2)] }, "TESTER"));
        Assert.Equal("Worksheet AC999-2 is no longer waiting for a bordx. Search again.", thrown.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Listing_applies_the_selection_rules_and_the_document_check_on_the_server()
    {
        var individual = Worksheet("AC001", 1, 100) with { InsuredType = "I", GroupCompany = null };
        var repository = Repository(individual, Worksheet("AC002", 1, 100) with { Dod = null });

        var noDocuments = new ListingService(new FakeSessions(), repository, new FakeDocuments(true), NullLogger<ListingService>.Instance);
        var missing = await Assert.ThrowsAsync<CaseListingException>(() => noDocuments.GenerateAsync(new() { Option = "Single Claim", Items = [new("AC001", 1)] }, "T"));
        Assert.Equal("AC001-1: Please ensure scanned documents are in system!", missing.Message);

        var withDocuments = new ListingService(new FakeSessions(), repository, new FakeDocuments(true, "AC001"), NullLogger<ListingService>.Instance);
        Assert.Single(await withDocuments.GenerateAsync(new() { Option = "Single Claim", Items = [new("AC001", 1)] }, "T"));

        var skipped = new ListingService(new FakeSessions(), Repository(individual), new FakeDocuments(false), NullLogger<ListingService>.Instance);
        Assert.Single(await skipped.GenerateAsync(new() { Option = "Single Claim", Items = [new("AC001", 1)] }, "T"));

        var rule = await Assert.ThrowsAsync<CaseListingException>(() => withDocuments.GenerateAsync(new() { Option = "Single Claim", Items = [new("AC002", 1)] }, "T"));
        Assert.Equal("AC002-1: Date of Discharge is Empty.", rule.Message);
    }

    [Fact]
    public void Listing_document_check_reports_availability_per_case()
    {
        var service = new ListingService(new FakeSessions(), new FakeListingRepository(), new FakeDocuments(true, "AC1"), NullLogger<ListingService>.Instance);
        var (available, cases) = service.Documents(["AC1", " AC2 ", "AC1", ""]);
        Assert.True(available);
        Assert.Equal(new Dictionary<string, bool> { ["AC1"] = true, ["AC2"] = false }, cases);
        var (unavailable, none) = new ListingService(new FakeSessions(), new FakeListingRepository(), new FakeDocuments(false, "AC1"), NullLogger<ListingService>.Instance).Documents(["AC1"]);
        Assert.False(unavailable);
        Assert.False(none["AC1"]);
    }

    // ---------------- ASCII exports ----------------

    private sealed class FakeAsciiRepository : IAsciiRepository
    {
        public List<Dictionary<string, object?>> Rows = [];
        public string? BatchKind, Payor, WorksheetStatus;
        public bool? Reprint;
        public List<string> ExportedClaims = [];
        public List<string> ExportedCases = [];

        public Task<List<Dictionary<string, object?>>> GetAcceptedClaimsAsync(IDbSession session, string payor, DateTime from, DateTime to, bool reprint, string worksheetStatus)
        {
            (Payor, Reprint, WorksheetStatus) = (payor, reprint, worksheetStatus);
            return Task.FromResult(Rows);
        }

        public Task<List<Dictionary<string, object?>>> GetRejectedClaimsAsync(IDbSession session, string payor, DateTime from, DateTime to)
        {
            Payor = payor;
            return Task.FromResult(Rows);
        }

        public Task<List<Dictionary<string, object?>>> GetCasesAsync(IDbSession session, string payor, DateTime from, DateTime to, bool reprint, string endorsement)
        {
            (Payor, Reprint, WorksheetStatus) = (payor, reprint, endorsement);
            return Task.FromResult(Rows);
        }

        public Task<string> NextBatchAsync(IDbSession session, string kind, string payor, DateTime date)
        {
            BatchKind = kind;
            return Task.FromResult("00042");
        }

        public Task MarkClaimExportedAsync(IDbSession session, string cas, int version)
        {
            ExportedClaims.Add($"{cas}-{version}");
            return Task.CompletedTask;
        }

        public Task MarkCaseExportedAsync(IDbSession session, string cas)
        {
            ExportedCases.Add(cas);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileStore : IAsciiFileStore
    {
        public string? Kind, Name, Text;

        public Task<string> WriteAsync(string kind, string name, string text)
        {
            (Kind, Name, Text) = (kind, name, text);
            return Task.FromResult(name);
        }

        public string? Find(string kind, string name) => kind == Kind && name == Name ? "C:/fake/" + name : null;
    }

    private static Dictionary<string, object?> Row(string cas, int version = 1) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["CASNumber"] = cas, ["ClaimVersion"] = version, ["PAYCode"] = "AC", ["BordxRefNo"] = "NAC0000001", ["BordxDate"] = new DateTime(2021, 8, 6),
    };

    [Fact]
    public async Task Claim_ascii_without_rows_is_reported_and_nothing_is_written()
    {
        var sessions = new FakeSessions();
        var files = new FakeFileStore();
        var service = new ClaimAsciiService(sessions, new FakeAsciiRepository(), files, NullLogger<ClaimAsciiService>.Instance);
        var thrown = await Assert.ThrowsAsync<NotFoundException>(() => service.GenerateAsync(new() { Payor = "AC", DateFrom = DateTime.Today, DateTo = DateTime.Today, ExportStatus = "N", WorksheetStatus = "0" }, "T"));
        Assert.Equal("No record found!", thrown.Message);
        Assert.Null(files.Name);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Claim_ascii_accepted_writes_header_records_trailer_and_flags_every_worksheet()
    {
        var sessions = new FakeSessions();
        var repository = new FakeAsciiRepository { Rows = [Row("AC001"), Row("AC001", 2), Row("AC002")] };
        var files = new FakeFileStore();
        var service = new ClaimAsciiService(sessions, repository, files, NullLogger<ClaimAsciiService>.Instance);
        var result = await service.GenerateAsync(new() { Payor = "AC - AXA AFFIN", DateFrom = new(2021, 8, 1), DateTo = new(2021, 8, 31), ExportStatus = "R", WorksheetStatus = "1" }, "T");

        Assert.Equal((3, "00042"), (result.Records, result.BatchNo));
        Assert.Equal(("AC", true, "1", "CLM"), (repository.Payor, repository.Reprint, repository.WorksheetStatus, repository.BatchKind));
        Assert.Equal(["AC001-1", "AC001-2", "AC002-1"], repository.ExportedClaims);
        Assert.Equal(AsciiFileStore.ClaimKind, files.Kind);
        Assert.Equal($"AC{DateTime.Today:yyMMdd}(00042)", result.FileName);
        var lines = files.Text!.Split("\r\n");
        Assert.StartsWith("FMX00042", lines[0]);
        Assert.StartsWith("T3 ", lines[^2]);
        Assert.Equal(6, lines.Length);
        Assert.True(sessions.Last.Committed);
        Assert.Equal(result.FileName, Path.GetFileName(service.FindFile(result.FileName)));
        Assert.Null(service.FindFile("other"));
    }

    [Fact]
    public async Task Claim_ascii_rejected_uses_the_payor_as_sender_and_flags_nothing()
    {
        var repository = new FakeAsciiRepository { Rows = [Row("AC001")] };
        var files = new FakeFileStore();
        var service = new ClaimAsciiService(new FakeSessions(), repository, files, NullLogger<ClaimAsciiService>.Instance);
        await service.GenerateAsync(new() { Payor = "ST", Variant = "Rejected", DateFrom = DateTime.Today, DateTo = DateTime.Today }, "T");
        Assert.StartsWith("FST00042", files.Text);
        Assert.Empty(repository.ExportedClaims);
        Assert.StartsWith("N", files.Text!.Split("\r\n")[1]);
    }

    [Fact]
    public async Task Case_ascii_writes_one_record_per_case_without_header_and_flags_the_cases()
    {
        var sessions = new FakeSessions();
        var repository = new FakeAsciiRepository { Rows = [Row("AC001"), Row("AC002")] };
        var files = new FakeFileStore();
        var service = new CaseAsciiService(sessions, repository, files, NullLogger<CaseAsciiService>.Instance);
        var result = await service.GenerateAsync(new() { Payor = "AC", DateFrom = DateTime.Today, DateTo = DateTime.Today, ExportStatus = "N", WorksheetStatus = "0" }, "T");

        Assert.Equal(2, result.Records);
        Assert.Equal(("CAS", AsciiFileStore.CaseKind), (repository.BatchKind, files.Kind));
        Assert.Equal(["AC001", "AC002"], repository.ExportedCases);
        var lines = files.Text!.Split("\r\n");
        Assert.Equal(3, lines.Length);
        Assert.All(lines[..2], l => Assert.Equal(4975, l.Length));
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Ascii_file_store_serves_only_plain_names_inside_its_folder_and_needs_a_folder()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var settings = new Dictionary<string, string?> { ["Ascii:ClaimFolder"] = folder };
            var store = new AsciiFileStore(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
            Assert.Equal("AX261017(00001)", await store.WriteAsync(AsciiFileStore.ClaimKind, "AX261017(00001)", "line\r\n"));
            Assert.NotNull(store.Find(AsciiFileStore.ClaimKind, "AX261017(00001)"));
            Assert.Null(store.Find(AsciiFileStore.ClaimKind, "..\\x"));
            Assert.Null(store.Find(AsciiFileStore.ClaimKind, "nothing"));
            Assert.Null(store.Find(AsciiFileStore.CaseKind, "AX261017(00001)"));
            var unconfigured = new AsciiFileStore(new ConfigurationBuilder().Build());
            Assert.Contains("not configured", (await Assert.ThrowsAsync<AsciiException>(() => unconfigured.WriteAsync(AsciiFileStore.CaseKind, "x", "y"))).Message);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
