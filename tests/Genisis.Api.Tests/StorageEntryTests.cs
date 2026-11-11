using Dapper;
using Genisis.Api.ClaimBordx.Storage;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Storage > Claim files: the validators and the service, against a fake repository.</summary>
public class StorageEntryRulesTests
{
    private sealed class FakeRepository : IStorageEntryRepository
    {
        public Dictionary<string, int> CartonResults { get; } = [];

        public Queue<int> FileResults { get; } = new();

        public List<string> Calls { get; } = [];

        public int DeleteResult { get; set; }

        public int RemoveResult { get; set; }

        public Task<int> SaveCartonAsync(IDbSession session, string carton, StorageCartonSaveRequest request, string userCode, DateTime today)
        {
            Calls.Add($"carton {carton} {(request.Edit ? "edit" : "add")}");
            return Task.FromResult(CartonResults.GetValueOrDefault(carton));
        }

        public Task<int> DeleteCartonAsync(IDbSession session, string carton) => Task.FromResult(DeleteResult);

        public Task<int> SaveFileAsync(IDbSession session, StorageClaimRef claim, StorageFileSaveRequest request, string userCode, DateTime today)
        {
            Calls.Add($"file {claim.CaseNo}-{claim.Version}");
            return Task.FromResult(FileResults.Count > 0 ? FileResults.Dequeue() : 0);
        }

        public Task<int> RemoveFileAsync(IDbSession session, StorageClaimRef claim) => Task.FromResult(RemoveResult);
    }

    private static StorageEntryService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<StorageEntryService>.Instance);

    private static StorageCartonSaveRequest Carton(Action<StorageCartonSaveRequest>? tweak = null)
    {
        var r = new StorageCartonSaveRequest { Cartons = ["C1"], Status = "S - Store" };
        tweak?.Invoke(r);
        return r;
    }

    private static StorageFileSaveRequest File(Action<StorageFileSaveRequest>? tweak = null)
    {
        var r = new StorageFileSaveRequest { Files = [new("AB1", 1)], Carton = "C1", FileStatus = "I - In" };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public void The_carton_checks_come_in_the_desktops_order_and_refuse_a_date_without_the_date_before_it()
    {
        var validator = new StorageCartonSaveRequestValidator();
        var d = (int day) => new DateTime(2026, 10, day);
        Assert.Null(validator.FirstError(Carton()));
        Assert.Equal("Please Enter Carton Number", validator.FirstError(Carton(r => r.Cartons = [])));
        Assert.Equal("Please Enter Carton Status", validator.FirstError(Carton(r => r.Status = " ")));
        Assert.Equal("Invalid Date ! Date of Request must greater than Date of Send ", validator.FirstError(Carton(r => r.RequestDate = d(2))));
        Assert.Equal("Invalid Date ! Date of Request must greater than Date of Send ", validator.FirstError(Carton(r => (r.SendDate, r.RequestDate) = (d(5), d(2)))));
        Assert.Null(validator.FirstError(Carton(r => (r.SendDate, r.RequestDate) = (d(2), d(2)))));
        Assert.Equal("Invalid Date ! Date of Return must greater than Date of Delivery ", validator.FirstError(Carton(r => r.ReturnDate = d(9))));
        Assert.Equal("Invalid Date ! Date of Destroy must greater than Date of Return ", validator.FirstError(Carton(r => (r.DeliveryDate, r.ReturnDate, r.DestroyDate) = (d(1), d(5), d(3)))));
        Assert.Equal("Date Return cannot be null when the status is 'R' !", validator.FirstError(Carton(r => r.Status = "R - Return")));
        Assert.Null(validator.FirstError(Carton(r => (r.Status, r.DeliveryDate, r.ReturnDate) = ("R", d(1), d(2)))));
        Assert.Equal("Add one carton at a time.", validator.FirstError(Carton(r => r.Cartons = ["C1", "C2"])));
        Assert.Null(validator.FirstError(Carton(r => (r.Cartons, r.Edit) = (["C1", "C2"], true))));
    }

    [Fact]
    public void The_file_checks_come_in_the_desktops_order()
    {
        var validator = new StorageFileSaveRequestValidator();
        Assert.Null(validator.FirstError(File()));
        Assert.Equal("Please checked at least 1 Claim !", validator.FirstError(File(r => r.Files = [])));
        Assert.Equal("Please Enter Carton Number", validator.FirstError(File(r => r.Carton = "")));
        Assert.Equal("Please Enter File Status", validator.FirstError(File(r => r.FileStatus = null)));
        Assert.Equal("Please Enter Date Of Request", validator.FirstError(File(r => r.RequestBy = "A USER")));
        Assert.Equal("Invalid Date ! Date of Delivery must greater than Date of Request", validator.FirstError(File(r => (r.RequestBy, r.RequestDate, r.DeliveryDate) = ("A USER", new DateTime(2026, 10, 5), new DateTime(2026, 10, 2)))));
        Assert.Equal("Invalid Date ! Date of Return must greater than Date of Delivery", validator.FirstError(File(r => (r.RequestBy, r.RequestDate, r.ReturnDate) = ("A USER", new DateTime(2026, 10, 5), new DateTime(2026, 10, 9)))));
        Assert.Equal("Select the one claim to change.", validator.FirstError(File(r => (r.Edit, r.Files) = (true, [new("A", 1), new("B", 1)]))));
    }

    [Fact]
    public async Task Cartons_are_saved_in_one_transaction_and_a_duplicate_or_a_missing_carton_saves_nothing()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        await Service(repository, sessions).SaveCartonsAsync(Carton(r => (r.Cartons, r.Edit) = ([" c1 ", "C2", "c2"], true)), "U1");
        Assert.Equal(["carton C1 edit", "carton C2 edit"], repository.Calls);
        Assert.True(sessions.Last.Committed);

        repository.CartonResults["C1"] = 1;
        var sessions2 = new FakeSessions();
        var duplicate = await Assert.ThrowsAsync<ConflictException>(() => Service(repository, sessions2).SaveCartonsAsync(Carton(), "U1"));
        Assert.Equal("Record Duplicated! Please Change The Corresponding Field", duplicate.Message);
        Assert.False(sessions2.Last.Committed);

        repository.CartonResults["C1"] = 2;
        await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SaveCartonsAsync(Carton(r => r.Edit = true), "U1"));
    }

    [Fact]
    public async Task A_carton_with_files_is_not_deleted_and_a_missing_one_is_not_found()
    {
        var repository = new FakeRepository { DeleteResult = 2 };
        var failure = await Assert.ThrowsAsync<ConflictException>(() => Service(repository, new FakeSessions()).DeleteCartonAsync("c1", "U1"));
        Assert.Equal("This carton cannot be deleted because claim files are stored in it!", failure.Message);
        repository.DeleteResult = 1;
        await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).DeleteCartonAsync("c1", "U1"));
        repository.DeleteResult = 0;
        var sessions = new FakeSessions();
        await Service(repository, sessions).DeleteCartonAsync("c1", "U1");
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Claim_files_are_stored_together_and_the_first_refusal_saves_none()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        await Service(repository, sessions).SaveFilesAsync(File(r => r.Files = [new("ab1", 1), new("AB1", 1), new("AB2", 3)]), "U1");
        Assert.Equal(["file ab1-1", "file AB2-3"], repository.Calls);
        Assert.True(sessions.Last.Committed);

        foreach (var (code, expected) in new[] { (1, typeof(ConflictException)), (2, typeof(NotFoundException)), (3, typeof(BusinessRuleException)), (4, typeof(BusinessRuleException)) })
        {
            repository.FileResults.Enqueue(code);
            var failed = new FakeSessions();
            var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(repository, failed).SaveFilesAsync(File(), "U1"));
            Assert.IsType(expected, ex);
            Assert.False(failed.Last.Committed);
        }
    }

    [Fact]
    public async Task Removing_a_file_that_is_not_stored_is_not_found()
    {
        var repository = new FakeRepository { RemoveResult = 1 };
        await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).RemoveFileAsync(new StorageClaimRef("AB1", 1), "U1"));
        repository.RemoveResult = 0;
        var sessions = new FakeSessions();
        await Service(repository, sessions).RemoveFileAsync(new StorageClaimRef("AB1", 1), "U1");
        Assert.True(sessions.Last.Committed);
    }
}

/// <summary>Storage Entry against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class StorageEntryDatabaseTests
{
    private static StorageEntryRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string CartonChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CRTCartonNo, CRTCartonStatus, DOSend, DORequest, DODelivery, DOReturn, DODestroy, CRTRemarks, LastUpdateBy)), 0) + COUNT(*) FROM genisis.WS_Carton";

    private const string StorageChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(STRCaseNo, STRCLMVersion, STRCartonNo, STRFileStatus, STRRequestBy, DORequest, DODelivery, DOReturn, STRRemarks, LastUpdateBy)), 0) + COUNT(*) FROM genisis.WS_Storage";

    [Fact]
    public async Task The_entry_list_also_shows_the_claims_that_are_not_stored_and_filters_on_the_bordx_date()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var enquiry = new StorageEnquiryRepository();
        var unstored = await session.Connection.QueryFirstAsync<(string Case, int Version)>("SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c WHERE NOT EXISTS (SELECT 1 FROM genisis.WS_Storage s WHERE s.STRCaseNo = c.CASNumber) ORDER BY c.CASNumber DESC", transaction: session.Transaction);
        var scanned = await enquiry.FilesAsync(session, new StorageFileFilter { UseScan = true, CaseFrom = unstored.Case, IncludeUnstored = true });
        Assert.Contains(scanned, r => r.CaseNo == unstored.Case && r.ClaimVersion == unstored.Version && r.CartonNo is null);
        Assert.Empty(await enquiry.FilesAsync(session, new StorageFileFilter { UseScan = true, CaseFrom = unstored.Case }));

        var withBordx = scanned.FirstOrDefault(r => r.BordxDate is not null);
        if (withBordx is not null)
        {
            var day = withBordx.BordxDate!.Value.Date;
            var byBordx = await enquiry.FilesAsync(session, new StorageFileFilter { UseScan = true, CaseFrom = withBordx.CaseNo, IncludeUnstored = true, BordxFrom = day, BordxTo = day });
            Assert.All(byBordx, r => Assert.Equal(day, r.BordxDate!.Value.Date));
            Assert.Empty(await enquiry.FilesAsync(session, new StorageFileFilter { UseScan = true, CaseFrom = withBordx.CaseNo, IncludeUnstored = true, BordxFrom = day.AddYears(50) }));
        }
    }

    [Fact]
    public async Task A_carton_is_added_refused_twice_changed_and_deleted_and_a_carton_with_files_is_not_deleted_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, CartonChecksum);
        var usedCarton = await read.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(STRCartonNo) FROM genisis.WS_Storage WHERE STRCartonNo IS NOT NULL ORDER BY STRCartonNo", transaction: read.Transaction);
        var day = new DateTime(2026, 10, 30, 9, 0, 0);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var request = new StorageCartonSaveRequest { Cartons = ["ZZ-E2E-CTN"], Status = "S - Store", SendDate = day, Remarks = "a remark" };
            Assert.Equal(0, await Repository.SaveCartonAsync(write, "ZZ-E2E-CTN", request, "E2E", day));
            var row = await write.Connection.QuerySingleAsync("SELECT CRTCartonStatus AS Status, DOSend AS Sent, CRTRemarks AS Remarks, LastUpdateBy AS UserCode, LastUpdateDate AS Updated FROM genisis.WS_Carton WHERE CRTCartonNo = 'ZZ-E2E-CTN'", transaction: write.Transaction);
            Assert.Equal(("S", day.Date, "A REMARK", "E2E", day.Date), ((string)row.Status, (DateTime)row.Sent, (string)row.Remarks, (string)row.UserCode, (DateTime)row.Updated));
            Assert.Equal(1, await Repository.SaveCartonAsync(write, "ZZ-E2E-CTN", request, "E2E", day));
            Assert.Equal(2, await Repository.SaveCartonAsync(write, "ZZ-NO-SUCH", new StorageCartonSaveRequest { Edit = true, Status = "S" }, "E2E", day));

            var edit = new StorageCartonSaveRequest { Cartons = ["ZZ-E2E-CTN"], Status = "R - Return", DeliveryDate = day, ReturnDate = day.AddDays(2), Edit = true };
            Assert.Equal(0, await Repository.SaveCartonAsync(write, "ZZ-E2E-CTN", edit, "E2E", day));
            var changed = await write.Connection.QuerySingleAsync("SELECT CRTCartonStatus AS Status, DOReturn AS Returned, DOSend AS Sent FROM genisis.WS_Carton WHERE CRTCartonNo = 'ZZ-E2E-CTN'", transaction: write.Transaction);
            Assert.Equal(("R", day.AddDays(2).Date, (DateTime?)null), ((string)changed.Status, (DateTime)changed.Returned, (DateTime?)changed.Sent));

            Assert.Equal(2, await Repository.DeleteCartonAsync(write, usedCarton));
            Assert.Equal(0, await Repository.DeleteCartonAsync(write, "ZZ-E2E-CTN"));
            Assert.Equal(1, await Repository.DeleteCartonAsync(write, "ZZ-E2E-CTN"));
        }

        Assert.Equal(before, await Count(read, CartonChecksum));
    }

    [Fact]
    public async Task A_claim_file_is_stored_found_changed_and_removed_and_a_second_store_a_missing_carton_or_a_missing_claim_is_refused_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, StorageChecksum);
        var claim = await read.Connection.QueryFirstAsync<(string Case, int Version)>("SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c WHERE NOT EXISTS (SELECT 1 FROM genisis.WS_Storage s WHERE s.STRCaseNo = c.CASNumber) ORDER BY c.CASNumber DESC", transaction: read.Transaction);
        var carton = await read.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(CRTCartonNo) FROM genisis.WS_Carton WHERE LEN(CRTCartonNo) <= 20 ORDER BY CRTCartonNo DESC", transaction: read.Transaction);
        var day = new DateTime(2026, 10, 30, 9, 0, 0);
        var request = new StorageFileSaveRequest { Files = [new(claim.Case, claim.Version)], Carton = carton, FileStatus = "I - In", RequestBy = "A USER", RequestDate = day, DeliveryDate = day.AddDays(1), Remarks = "kept" };
        var reference = new StorageClaimRef(claim.Case, claim.Version);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            Assert.Equal(0, await Repository.SaveFileAsync(write, reference, request, "E2E", day));
            var row = await write.Connection.QuerySingleAsync("SELECT STRCartonNo AS Carton, STRFileStatus AS FileStatus, STRRequestBy AS RequestBy, DORequest AS Requested, DODelivery AS Delivered, DOReturn AS Returned, STRRemarks AS Remarks, LastUpdateBy AS UserCode FROM genisis.WS_Storage WHERE STRCaseNo = @c AND STRCLMVersion = @v", new { c = claim.Case, v = claim.Version.ToString() }, write.Transaction);
            Assert.Equal((carton, "I", "A USER", day.Date, day.AddDays(1).Date, (DateTime?)null, "KEPT", "E2E"), (((string)row.Carton).Trim(), ((string)row.FileStatus).Trim(), (string)row.RequestBy, (DateTime)row.Requested, (DateTime)row.Delivered, (DateTime?)row.Returned, (string)row.Remarks, (string)row.UserCode));
            Assert.Equal(1, await Repository.SaveFileAsync(write, reference, request, "E2E", day));
            Assert.Equal(3, await Repository.SaveFileAsync(write, reference, new StorageFileSaveRequest { Files = request.Files, Carton = "ZZ-NO-CARTON", FileStatus = "I" }, "E2E", day));
            Assert.Equal(4, await Repository.SaveFileAsync(write, new StorageClaimRef("ZZ-NO-CASE", 1), request, "E2E", day));

            var edit = new StorageFileSaveRequest { Files = request.Files, Carton = carton, FileStatus = "O - Out", RequestBy = "", RequestDate = day, Edit = true };
            Assert.Equal(0, await Repository.SaveFileAsync(write, reference, edit, "E2E", day));
            var changed = await write.Connection.QuerySingleAsync("SELECT STRFileStatus AS FileStatus, DORequest AS Requested, DODelivery AS Delivered FROM genisis.WS_Storage WHERE STRCaseNo = @c AND STRCLMVersion = @v", new { c = claim.Case, v = claim.Version.ToString() }, write.Transaction);
            Assert.Equal(("O", (DateTime?)null, (DateTime?)null), (((string)changed.FileStatus).Trim(), (DateTime?)changed.Requested, (DateTime?)changed.Delivered));

            Assert.Equal(0, await Repository.RemoveFileAsync(write, reference));
            Assert.Equal(1, await Repository.RemoveFileAsync(write, reference));
            Assert.Equal(2, await Repository.SaveFileAsync(write, reference, edit, "E2E", day));
        }

        Assert.Equal(before, await Count(read, StorageChecksum));
    }
}
