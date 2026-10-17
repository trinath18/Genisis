using System.Text;
using Dapper;
using Genisis.Api.Case;
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

/// <summary>Runs the nine Case screens' repositories and services against the development databases when GENISIS_TEST_MAINDB is set.
/// Writes happen in a session that is rolled back (ASCII files go to a temporary folder).</summary>
[Collection(DatabaseWriteCollection.Name)]
public class CaseScreensDatabaseTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    private static Task<string?> CaseWhere(IDbSession session, string where) =>
        session.Connection.ExecuteScalarAsync<string?>($"SELECT TOP (1) RTRIM(c.CASNumber) FROM dbo.CAS c WHERE {where} ORDER BY c.CASNumber DESC", transaction: session.Transaction);

    private sealed class StubDocuments(bool available, Func<string, bool>? has = null) : IScannedDocuments
    {
        public bool Available => available;
        public bool HasDocuments(string cas) => has?.Invoke(cas) ?? false;
    }

    // ---- Panel Hospitals ----

    [Fact]
    public async Task Panel_hospitals_list_the_codes_and_their_hospitals()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = new PanelHospitalsService(TestDb.Sessions(), new PanelHospitalsRepository());
        var codes = await service.GetCodesAsync();
        Assert.NotEmpty(codes);

        List<PanelHospital>? found = null;
        foreach (var code in codes.Take(10))
            if (await service.GetHospitalsAsync(code.Code) is { Count: > 0 } list) { found = list; break; }
        Assert.NotNull(found);
        Assert.All(found, h => Assert.False(string.IsNullOrWhiteSpace(h.Code)));
        Assert.Empty(await service.GetHospitalsAsync("NO SUCH PANEL"));
        Assert.NotEmpty(await new Genisis.Api.Case.Lookups.PayorLookupService(TestDb.Sessions(), new Genisis.Api.Case.Lookups.PayorRepository()).GetPayorsAsync());
    }

    // ---- Non-Disclosure ----

    [Fact]
    public async Task Non_disclosure_saves_normalised_values_and_inserts_a_missing_row()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new NonDisclosureRepository();
        var service = new NonDisclosureService(TestDb.Sessions(), repository, NullLogger<NonDisclosureService>.Instance);
        await using var session = await TestDb.Sessions().BeginAsync();
        var cas = await CaseWhere(session, "EXISTS (SELECT 1 FROM dbo.CASOthersTwo o WHERE o.CASNumber = c.CASNumber)");
        Assert.NotNull(cas);

        var request = new NonDisclosureRequest
        {
            PreCategory = "4", PreDate = new(2020, 2, 1), PreOthers = "old injury", PreInformedBy = "Dr A",
            UndCategory = "1", UndDate = new(2021, 3, 4), UndOthers = "dropped", UndInformedBy = "Dr B",
        };
        await service.SaveAsync(session, cas!, request);
        var saved = await repository.GetAsync(session, cas!);
        Assert.NotNull(saved);
        Assert.Equal(("4", "old injury", "Dr A"), (saved.PreCategory, saved.PreOthers, saved.PreInformedBy));
        Assert.Equal(new DateTime(2020, 2, 1), saved.PreDate);
        Assert.Equal(("1", "", "Dr B"), (saved.UndCategory, saved.UndOthers, saved.UndInformedBy));

        // A case with no CASOthersTwo row gets one (the desktop reported success without saving).
        await session.Connection.ExecuteAsync("DELETE dbo.CASOthersTwo WHERE CASNumber = @cas", new { cas }, session.Transaction);
        Assert.False((await repository.GetAsync(session, cas!))!.HasRecord);
        await service.SaveAsync(session, cas!, request);
        Assert.True((await repository.GetAsync(session, cas!))!.HasRecord);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(session, "NO SUCH CASE", request));
        Assert.Null(await repository.GetAsync(session, "NO SUCH CASE"));
        await session.RollbackAsync();
    }

    // ---- Pay & Claim ----

    [Fact]
    public async Task Pay_and_claim_loads_members_and_adds_then_edits_one_record_per_case()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = TestDb.Sessions();
        var repository = new PayClaimRepository();
        var service = new PayClaimService(sessions, repository, NullLogger<PayClaimService>.Instance);
        string? family;
        await using (var probe = await sessions.OpenAsync())
            family = await CaseWhere(probe, "EXISTS (SELECT 1 FROM dbo.MBM m JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = m.MBMNumber WHERE m.MBMNumber = c.MBMNumber AND RTRIM(m.INSCode) IN ('F', 'H'))");
        Assert.NotNull(family);
        var view = await service.LoadAsync(family!);
        Assert.Equal(family, view.Case.CaseNumber);
        Assert.Equal("00", view.Members[0].CoverId);
        Assert.True(view.Members.Count > 1);
        await Assert.ThrowsAsync<NotFoundException>(() => service.LoadAsync("NO SUCH CASE"));

        await using var session = await sessions.BeginAsync();
        var free = await CaseWhere(session, "NOT EXISTS (SELECT 1 FROM dbo.CASPayClaim p WHERE p.CASNumber = c.CASNumber)");
        var other = await CaseWhere(session, $"NOT EXISTS (SELECT 1 FROM dbo.CASPayClaim p WHERE p.CASNumber = c.CASNumber) AND c.CASNumber <> '{free}'");
        var request = new PayClaimRequest { IsNew = true, PayClaimDate = new(2026, 10, 17), LabTest = true, Others = true, OthersRemark = "waiting" };

        await service.SaveAsync(session, free!, request, "UITEST");
        await Assert.ThrowsAsync<ConflictException>(() => service.SaveAsync(session, free!, request, "UITEST"));
        var added = await repository.GetRecordAsync(session, free!);
        Assert.NotNull(added);
        Assert.Equal(("Y", "N", "Y", "waiting"), (added.LabTest, added.ConflictingInfo, added.Others, added.Remarks));

        request.IsNew = false;
        request.LabTest = false;
        request.ConflictingInfo = true;
        request.Others = false;
        await service.SaveAsync(session, free!, request, "UITEST2");
        var edited = await repository.GetRecordAsync(session, free!);
        Assert.Equal(("N", "Y", "N", ""), (edited!.LabTest, edited.ConflictingInfo, edited.Others, edited.Remarks));
        Assert.Equal("UITEST", await session.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(CASRegUser) FROM dbo.CASPayClaim WHERE CASNumber = @free", new { free }, session.Transaction));
        Assert.Equal("UITEST2", await session.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(LastUpdateUser) FROM dbo.CASPayClaim WHERE CASNumber = @free", new { free }, session.Transaction));

        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(session, other!, request, "UITEST"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(session, "NO SUCH CASE", new() { IsNew = true, PayClaimDate = DateTime.Today }, "UITEST"));
        await session.RollbackAsync();
    }

    // ---- MMA / CPT ----

    [Fact]
    public async Task Mma_procedures_and_fees_can_be_added_changed_searched_and_deleted()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = TestDb.Sessions();
        var repository = new MmaRepository();
        var service = new MmaService(sessions, repository, NullLogger<MmaService>.Instance);
        Assert.NotEmpty(await service.SearchProceduresAsync(new() { MainBodyPart = "Conj" }));
        Assert.All(await service.SearchProceduresAsync(new() { OpcsCode = "Z009" }), p => Assert.Contains("Z009", p.OpcsCode));
        Assert.NotEmpty(await service.SearchFeesAsync(new() { SurgeonCategory = "SCMO" }));
        Assert.Empty(await service.SearchProceduresAsync(new() { MainBodyPart = "no such body part" }));

        await using var session = await sessions.BeginAsync();
        var added = await service.SaveProcedureAsync(session, null,
            new() { MainBodyPart = "UI Test BP", OpcsCode = "T9999", Narrative = "Test narrative", Part = "a", PageNo = "7" }, "UITEST");
        var found = await repository.SearchProceduresAsync(session, "%UI Test%", null);
        Assert.Equal(added, Assert.Single(found).Index);
        Assert.Equal("A", found[0].Part);

        await service.SaveProcedureAsync(session, added,
            new() { MainBodyPart = "UI Test BP2", OpcsCode = "T9999", Narrative = "Changed", Part = "B", PageNo = "8", PartACost = "50" }, "UITEST");
        found = await repository.SearchProceduresAsync(session, "%BP2%", "T99%");
        Assert.Equal(("Changed", "B", "50"), (found[0].Narrative, found[0].Part, found[0].PartACost));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveProcedureAsync(session, -1, new() { MainBodyPart = "x", OpcsCode = "x", Narrative = "x", PageNo = "1" }, "UITEST"));
        Assert.True(await repository.DeleteProcedureAsync(session, added));
        Assert.False(await repository.DeleteProcedureAsync(session, added));

        var fee = await service.SaveFeeAsync(session, null, new() { SurgeonCategory = "SCTEST", SurgeonFees = 100.5m, AnaesthetistCategory = "ACTEST", AnaesthetistFees = 50 }, "UITEST");
        await service.SaveFeeAsync(session, fee, new() { SurgeonCategory = "SCTEST2", SurgeonFees = 200 }, "UITEST");
        var fees = await repository.SearchFeesAsync(session, "%TEST2%", null);
        Assert.Equal((200m, (string?)null, (decimal?)null), (fees[0].SurgeonFees!.Value, fees[0].AnaesthetistCategory, fees[0].AnaesthetistFees));
        Assert.True(await repository.DeleteFeeAsync(session, fee));
        Assert.False(await repository.DeleteFeeAsync(session, fee));
        await session.RollbackAsync();
    }

    // ---- Case audit review ----

    [Fact]
    public async Task Audit_review_loads_the_case_and_saves_its_own_record_and_the_audit_status()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = TestDb.Connections();
        var sessions = new DbSessions(db);
        var adjustment = new CaseAdjustmentService(db, new CaseRegistrationService(db, NullLogger<CaseRegistrationService>.Instance), NullLogger<CaseAdjustmentService>.Instance);
        var repository = new AuditReviewRepository();
        var service = new AuditReviewService(sessions, repository, adjustment, NullLogger<AuditReviewService>.Instance);

        List<string> candidates;
        await using (var probe = await sessions.OpenAsync())
            candidates = (await probe.Connection.QueryAsync<string>("SELECT TOP (40) RTRIM(c.CASNumber) FROM dbo.CAS c WHERE EXISTS (SELECT 1 FROM dbo.MBM m WHERE m.MBMNumber = c.MBMNumber) ORDER BY c.CASNumber DESC")).ToList();
        string? cas = null;
        AuditReviewView? view = null;
        foreach (var candidate in candidates)
        {
            try { (cas, view) = (candidate, await service.LoadAsync(candidate, "UITEST")); break; }
            catch (NotFoundException) { /* SearchCasAdjustment finds no row for this case; try the next one */ }
        }
        Assert.NotNull(view);
        Assert.Equal(cas, view.Case.Case["CASNumber"]?.ToString()?.Trim());
        Assert.NotNull(view.Payments);
        Assert.NotNull(view.Member);
        Assert.False(string.IsNullOrWhiteSpace(view.Member.MemberNo));
        await Assert.ThrowsAsync<NotFoundException>(() => service.LoadAsync("NO SUCH CASE", "UITEST"));

        await using var session = await sessions.BeginAsync();
        var request = new AuditReviewRequest
        {
            InvoiceNo = "INV-1", InvoiceDate = new(2026, 10, 1), InvoiceAmount = 123.45m, DiscountReceived = 10, DiscountType = "A", Status = "O",
            LetterSent = new(2026, 10, 2), DateOpen = new(2026, 10, 3), Query = "the query", Answer = "the answer", Remarks = "rem",
            VisitDate = new(2026, 10, 5), VisitTime = "14:30", VisitAction = "visited", Officer = "Officer X",
        };
        await service.SaveAsync(session, cas!, request, "UITEST");
        var audit = (await repository.GetExtrasAsync(session, cas!)).Audit;
        Assert.NotNull(audit);
        Assert.Equal(("INV-1", 123.45m, "A", "O", "the query"), (audit.InvoiceNo, audit.InvoiceAmount, audit.DiscountType, audit.Status, audit.Query));
        Assert.Equal(("14:30", new DateTime(2026, 10, 5)), (audit.VisitTime, audit.VisitDate));
        Assert.Equal("O", await session.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(CASARStatus) FROM genisis.Maint_CASCrossReference WHERE CASNumber = @cas", new { cas }, session.Transaction));

        request.Status = "C";
        request.InvoiceAmount = null;
        await service.SaveAsync(session, cas!, request, "UITEST2");
        Assert.Equal(0m, await session.Connection.ExecuteScalarAsync<decimal>("SELECT CASInvAmt FROM dbo.CASAuditReview WHERE CASNumber = @cas", new { cas }, session.Transaction));
        Assert.Equal("C", await session.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(CASARStatus) FROM dbo.CASAuditReview WHERE CASNumber = @cas", new { cas }, session.Transaction));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(session, "NO SUCH CASE", request, "UITEST"));
        await session.RollbackAsync();
    }

    // ---- Case transfer ----

    [Fact]
    public async Task Transfer_moves_the_case_and_its_approved_amount_between_members()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = TestDb.Sessions();
        var repository = new TransferRepository();
        var service = new TransferService(sessions, repository, NullLogger<TransferService>.Instance);
        await using var session = await sessions.BeginAsync();
        var cas = await CaseWhere(session,
            "EXISTS (SELECT 1 FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber AND w.CLMTotalApproved > 0) " +
            "AND EXISTS (SELECT 1 FROM dbo.MBM m WHERE m.MBMNumber = c.MBMNumber AND RTRIM(m.INSCode) NOT IN ('F', 'H')) AND c.MBMPatientCovID = '00'");
        Assert.NotNull(cas);
        var oldMember = (await repository.GetCaseAsync(session, cas!))!.MemberNo;
        var newMember = await session.Connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(MBMNumber) FROM dbo.MBM WHERE MBMNumber <> @oldMember AND MBMAvailableLimit IS NOT NULL ORDER BY MBMNumber DESC", new { oldMember }, session.Transaction);
        Assert.NotNull(newMember);

        Task<decimal?> Limit(string member) => session.Connection.ExecuteScalarAsync<decimal?>("SELECT MBMAvailableLimit FROM dbo.MBM WHERE MBMNumber = @member", new { member }, session.Transaction);
        var (oldBefore, newBefore) = (await Limit(oldMember), await Limit(newMember!));

        var result = await service.TransferAsync(session, cas!, new() { MemberNo = newMember, CoverId = "00" }, "UITEST");
        Assert.True(result.Moved > 0);
        Assert.Equal(oldBefore + result.Moved, await Limit(oldMember));
        Assert.Equal(newBefore - result.Moved, await Limit(newMember!));
        var moved = (await repository.GetCaseAsync(session, cas!))!;
        Assert.Equal((newMember, "00"), (moved.MemberNo, moved.CoverId));
        Assert.Contains($"{oldMember}-00->{newMember}-00", await session.Connection.ExecuteScalarAsync<string>("SELECT TransRemarks FROM genisis.Maint_CASCrossReference WHERE CASNumber = @cas", new { cas }, session.Transaction));
        Assert.Equal(newMember, await session.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(MBMNumber) FROM genisis.Maint_CASCrossReference WHERE CASNumber = @cas", new { cas }, session.Transaction));

        async Task<string> Refused(string member, string cover, string? caseNo = null) =>
            (await Assert.ThrowsAsync<BusinessRuleException>(() => service.TransferAsync(session, caseNo ?? cas!, new() { MemberNo = member, CoverId = cover }, "UITEST"))).Message;
        Assert.Equal("The case is already on this member and cover.", await Refused(newMember!, "00"));
        Assert.Equal("Member No not found.", await Refused("NO SUCH MEMBER", "00"));
        Assert.Equal("Invalid Cover ID. Please try again.", await Refused(oldMember, "77"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.TransferAsync(session, "NO SUCH CASE", new() { MemberNo = oldMember, CoverId = "00" }, "UITEST"));
        await session.RollbackAsync();

        await using var after = await sessions.OpenAsync();
        Assert.Equal(oldBefore, await after.Connection.ExecuteScalarAsync<decimal?>("SELECT MBMAvailableLimit FROM dbo.MBM WHERE MBMNumber = @oldMember", new { oldMember }));
    }

    // ---- Inpatient case listing ----

    private static ListingItem ToItem(ListingRow r) => new()
    {
        Dod = r.Dod, Hospital = r.Hospital, GroupCompany = r.GroupCompany, InsuredType = r.InsuredType, Claimability = r.Claimability, HltType = r.HltType,
    };

    [Fact]
    public async Task Listing_searches_pending_worksheets_and_generates_single_and_multiple_bordx()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = TestDb.Sessions();
        var repository = new ListingRepository();
        var root = Directory.CreateTempSubdirectory().FullName;
        var files = new CaseFilesService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Documents:RootPath"] = root }).Build(), NullLogger<CaseFilesService>.Instance);
        var real = new ListingService(sessions, repository, new ScannedDocuments(files), NullLogger<ListingService>.Instance);

        var all = await real.SearchAsync(new ListingFilter { UseScan = false });
        Assert.NotEmpty(all);
        var one = all[0];
        var scan = await real.SearchAsync(new ListingFilter { WksNoFrom = one.CaseNo, Version = one.ClaimVersion });
        Assert.Equal(one.WksNo, Assert.Single(scan).WksNo);
        var range = await real.SearchAsync(new ListingFilter { UseScan = false, WksNoFrom = one.CaseNo, WksNoTo = one.CaseNo });
        Assert.All(range, r => Assert.Equal(one.CaseNo, r.CaseNo));
        Assert.Empty(await real.SearchAsync(new ListingFilter { WksNoFrom = "NO SUCH WORKSHEET" }));
        Assert.All(await real.SearchAsync(new ListingFilter { UseScan = false, DodEmpty = true }), r => Assert.Null(r.Dod));
        Assert.All(await real.SearchAsync(new ListingFilter { UseScan = false, DoaFrom = new(2021, 7, 22) }), r => Assert.True(r.Doa >= new DateTime(2021, 7, 22)));
        var (available, docs) = real.Documents([one.CaseNo, " ", one.CaseNo]);
        Assert.True(available);
        Assert.False(Assert.Single(docs).Value);
        Directory.CreateDirectory(Path.Combine(root, "ScannedDoc", one.CaseNo));
        File.WriteAllText(Path.Combine(root, "ScannedDoc", one.CaseNo, "scan.PDF"), "x");
        Assert.True(real.Documents([one.CaseNo]).Cases[one.CaseNo]);

        var pickable = all.Where(r => ListingRules.SelectionProblem(ToItem(r), null) is null).Take(2).ToList();
        Assert.Equal(2, pickable.Count);
        var picks = pickable.Select(r => new ListingPick(r.CaseNo, r.ClaimVersion)).ToList();
        var service = new ListingService(sessions, repository, new StubDocuments(false), NullLogger<ListingService>.Instance);

        await using var session = await sessions.BeginAsync();
        var items = new List<ListingItem>();
        foreach (var p in picks) items.Add((await repository.GetItemAsync(session, p.CaseNo, p.ClaimVersion))!);
        var expectedTotal = items.Sum(ListingRules.Amount);

        var multiple = await service.GenerateAsync(session, new() { Option = "Multiple Claims", Items = picks }, "UITEST");
        Assert.Equal(2, multiple.Count);
        Assert.Single(multiple.Select(l => l.BordxNo).Distinct());
        Assert.All(multiple, l => Assert.Equal(expectedTotal, l.BordxAmount));
        var no = multiple[0].BordxNo;
        Assert.Equal(expectedTotal, await session.Connection.ExecuteScalarAsync<decimal>("SELECT BordxClaimAmt FROM genisis.WS_Bordx WHERE BordxRefNo = @no", new { no }, session.Transaction));
        Assert.Equal(2, await session.Connection.ExecuteScalarAsync<int>("SELECT CAST(BordxNoClaims AS int) FROM genisis.WS_Bordx WHERE BordxRefNo = @no", new { no }, session.Transaction));
        Assert.Equal(2, await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @no AND ClaimBordxStatus = 1 AND CLMPending = 'N'", new { no }, session.Transaction));
        Assert.Null(await repository.GetItemAsync(session, picks[0].CaseNo, picks[0].ClaimVersion));
        var again = await Assert.ThrowsAsync<CaseListingException>(() => service.GenerateAsync(session, new() { Option = "Multiple Claims", Items = picks }, "UITEST"));
        Assert.Contains("no longer waiting for a bordx", again.Message);
        await session.RollbackAsync();

        await using var session2 = await sessions.BeginAsync();
        var single = await service.GenerateAsync(session2, new() { Option = "Single Claim", Items = picks }, "UITEST");
        Assert.Equal(2, single.Select(l => l.BordxNo).Distinct().Count());
        Assert.Equal(items.Select(ListingRules.Amount), single.Select(l => l.BordxAmount));
        Assert.Equal(2, await session2.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Bordx WHERE BordxRefNo IN (@a, @b) AND BordxNoClaims = 1", new { a = single[0].BordxNo, b = single[1].BordxNo }, session2.Transaction));
        await session2.RollbackAsync();

        Assert.Equal(all.Count, (await real.SearchAsync(new ListingFilter { UseScan = false })).Count);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Listing_refuses_a_worksheet_that_breaks_the_selection_rules()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = TestDb.Sessions();
        var service = new ListingService(sessions, new ListingRepository(), new StubDocuments(false), NullLogger<ListingService>.Instance);
        var bad = (await service.SearchAsync(new ListingFilter { UseScan = false })).FirstOrDefault(r => ListingRules.SelectionProblem(ToItem(r), null) is not null);
        Assert.NotNull(bad);
        await using var session = await sessions.BeginAsync();
        var ex = await Assert.ThrowsAsync<CaseListingException>(() =>
            service.GenerateAsync(session, new() { Option = "Multiple Claims", Items = [new(bad.CaseNo, bad.ClaimVersion)] }, "UITEST"));
        Assert.StartsWith($"{bad.CaseNo}-{bad.ClaimVersion}: ", ex.Message);
        Assert.False(service.Documents([bad.CaseNo]).Available);
        await session.RollbackAsync();
    }

    // ---- Claim ASCII / Case ASCII ----

    private static async Task<string[]> ReadLinesAsync(string folder, string name) =>
        Encoding.Latin1.GetString(await File.ReadAllBytesAsync(Path.Combine(folder, name))).Split("\r\n", StringSplitOptions.None);

    [Fact]
    public async Task Ascii_exports_write_the_payor_files_number_the_batch_and_flag_what_was_exported()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var claims = Directory.CreateTempSubdirectory().FullName;
        var cases = Directory.CreateTempSubdirectory().FullName;
        var store = new AsciiFileStore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ascii:ClaimFolder"] = claims, ["Ascii:CaseFolder"] = cases,
        }).Build());
        var sessions = TestDb.Sessions();
        var claimService = new ClaimAsciiService(sessions, new AsciiRepository(), store, NullLogger<ClaimAsciiService>.Instance);
        var caseService = new CaseAsciiService(sessions, new AsciiRepository(), store, NullLogger<CaseAsciiService>.Instance);
        var from = new DateTime(2021, 7, 1);
        var to = new DateTime(2021, 8, 31);
        try
        {
            await using var session = await sessions.BeginAsync();

            // accepted worksheets: header + records + trailer, flagged as exported
            var accepted = await claimService.GenerateAsync(session, new() { Payor = "PN - X", DateFrom = from, DateTo = to, ExportStatus = "N", WorksheetStatus = "0" });
            var lines = await ReadLinesAsync(claims, accepted.FileName);
            Assert.Matches(@"^PN\d{6}\(\d{5}\)$", accepted.FileName);
            Assert.StartsWith("FMX" + accepted.BatchNo, lines[0]);
            Assert.Equal(16, lines[0].Length);
            Assert.Equal(accepted.Records + 3, lines.Length);
            Assert.Equal("", lines[^1]);
            Assert.StartsWith("T" + accepted.Records, lines[^2]);
            Assert.Equal(175, lines[^2].Length);
            Assert.All(lines[1..^2], l => Assert.Equal(5000, l.Length));
            await Assert.ThrowsAsync<NotFoundException>(() => claimService.GenerateAsync(session, new() { Payor = "PN", DateFrom = from, DateTo = to, ExportStatus = "N", WorksheetStatus = "0" }));
            var reprint = await claimService.GenerateAsync(session, new() { Payor = "PN", DateFrom = from, DateTo = to, ExportStatus = "R", WorksheetStatus = "0" });
            Assert.Equal(accepted.Records, reprint.Records);
            Assert.Equal(int.Parse(accepted.BatchNo) + 1, int.Parse(reprint.BatchNo));

            // repudiated cases
            var rejected = await claimService.GenerateAsync(session, new() { Payor = "ST", Variant = "rejected", DateFrom = from, DateTo = to });
            var rejectedLines = await ReadLinesAsync(claims, rejected.FileName);
            Assert.StartsWith("FST", rejectedLines[0]);
            Assert.All(rejectedLines[1..^2], l => Assert.Equal(5001, l.Length));
            Assert.All(rejectedLines[1..^2], l => Assert.StartsWith("N", l));

            // registered cases: reprint is empty until a case has been exported
            await Assert.ThrowsAsync<NotFoundException>(() => caseService.GenerateAsync(session, new() { Payor = "ST", DateFrom = from, DateTo = to, ExportStatus = "R", WorksheetStatus = "0" }));
            var caseFile = await caseService.GenerateAsync(session, new() { Payor = "ST", DateFrom = from, DateTo = to, ExportStatus = "N", WorksheetStatus = "0" });
            var caseLines = await ReadLinesAsync(cases, caseFile.FileName);
            Assert.Equal(caseFile.Records + 1, caseLines.Length);
            Assert.All(caseLines[..^1], l => Assert.Equal(4975, l.Length));
            var reexport = await caseService.GenerateAsync(session, new() { Payor = "ST", DateFrom = from, DateTo = to, ExportStatus = "R", WorksheetStatus = "0" });
            Assert.Equal(caseFile.Records, reexport.Records);
            await Assert.ThrowsAsync<NotFoundException>(() => caseService.GenerateAsync(session, new() { Payor = "NO SUCH PAYOR", DateFrom = from, DateTo = to, ExportStatus = "N", WorksheetStatus = "0" }));
            await session.RollbackAsync();

            Assert.Equal(accepted.FileName, Path.GetFileName(claimService.FindFile(accepted.FileName)));
            Assert.Null(claimService.FindFile("..\\secret.txt"));
            Assert.Null(claimService.FindFile("missing"));
            Assert.Null(caseService.FindFile(accepted.FileName));
        }
        finally
        {
            Directory.Delete(claims, true);
            Directory.Delete(cases, true);
        }
    }
}
