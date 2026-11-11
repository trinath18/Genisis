using Dapper;
using Genisis.Api.ClaimBordx.Storage;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Storage > Document: the validators and the service, against fake repositories.</summary>
public class StorageDocumentRulesTests
{
    private sealed class FakeEnquiry : IStorageEnquiryRepository
    {
        public Task<StorageLookups> LookupsAsync(IDbSession session) => Task.FromResult(new StorageLookups(["C1"], ["USER1"]));

        public Task<List<StorageCartonRow>> CartonsAsync(IDbSession session, StorageCartonFilter filter) => Task.FromResult(new List<StorageCartonRow>());

        public Task<List<StorageFileRow>> FilesAsync(IDbSession session, StorageFileFilter filter) => Task.FromResult(new List<StorageFileRow>());

        public Task<List<StorageCartonFile>> CartonFilesAsync(IDbSession session, string carton) => Task.FromResult(new List<StorageCartonFile>());
    }

    private sealed class FakeRepository(Func<string, int>? result = null) : IStorageDocumentRepository
    {
        public List<string> Saved { get; } = [];

        public Task<List<string>> DepartmentsAsync(IDbSession session) => Task.FromResult(new List<string> { "ACCOUNT" });

        public Task<List<StorageDocumentRow>> SearchAsync(IDbSession session, StorageDocumentFilter filter) => Task.FromResult(Enumerable.Range(0, 5002).Select(i => new StorageDocumentRow { DocumentNo = "D" + i }).ToList());

        public Task<int> SaveAsync(IDbSession session, string documentNo, StorageDocumentSaveRequest request, string userCode, DateTime today)
        {
            Saved.Add(documentNo);
            return Task.FromResult(result?.Invoke(documentNo) ?? 0);
        }
    }

    private static StorageDocumentSaveRequest Valid() => new() { Documents = ["B1"], DocType = "B - Bordx", Carton = "C1", FileStatus = "I - In" };

    private static StorageDocumentService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, new FakeEnquiry(), repository, NullLogger<StorageDocumentService>.Instance);

    [Fact]
    public void A_save_is_checked_in_the_desktops_order()
    {
        var validator = new StorageDocumentSaveRequestValidator();
        var r = new StorageDocumentSaveRequest();
        Assert.Equal("Please checked at least 1 Claim !", validator.FirstError(r));
        r.Documents = ["B1"];
        Assert.Equal("Please Enter Document Type", validator.FirstError(r));
        r.DocType = "B - Bordx";
        Assert.Equal("Please Enter Carton Number", validator.FirstError(r));
        r.Carton = "C1";
        Assert.Equal("Please Enter File Status", validator.FirstError(r));
        r.FileStatus = "I - In";
        Assert.Null(validator.FirstError(r));
        r.RequestBy = "USER1";
        Assert.Equal("Please Enter Date Of Request", validator.FirstError(r));
        r.RequestDate = new DateTime(2021, 5, 10);
        r.DeliveryDate = new DateTime(2021, 5, 9);
        Assert.Equal("Invalid Date ! Date of Delivery must greater than Date of Request", validator.FirstError(r));
        r.DeliveryDate = new DateTime(2021, 5, 10);
        r.ReturnDate = new DateTime(2021, 5, 9);
        Assert.Equal("Invalid Date ! Date of Return must greater than Date of Delivery", validator.FirstError(r));
        r.ReturnDate = new DateTime(2021, 5, 10);
        Assert.Null(validator.FirstError(r));
    }

    [Fact]
    public void A_return_date_needs_a_delivery_date_and_the_dates_are_not_checked_without_a_request_by()
    {
        var validator = new StorageDocumentSaveRequestValidator();
        var r = Valid();
        r.RequestBy = "USER1";
        r.RequestDate = new DateTime(2021, 5, 10);
        r.ReturnDate = new DateTime(2021, 5, 12);
        Assert.Equal("Invalid Date ! Date of Return must greater than Date of Delivery", validator.FirstError(r));
        r.RequestBy = "";
        r.RequestDate = null;
        Assert.Null(validator.FirstError(r));
    }

    [Theory]
    [InlineData("X", "A", false)]
    [InlineData("P - Payment Voucher", "A - All", true)]
    [InlineData("r", "o", true)]
    [InlineData("B", "Z", false)]
    public void The_search_takes_the_drop_down_choices(string docType, string fileStatus, bool valid) =>
        Assert.Equal(valid, new StorageDocumentFilterValidator().FirstError(new StorageDocumentFilter { DocType = docType, FileStatus = fileStatus }) is null);

    [Fact]
    public void Editing_is_for_one_document_only()
    {
        var r = Valid();
        r.Edit = true;
        r.Documents = ["B1", "B2"];
        Assert.Equal("Select the one document to change.", new StorageDocumentSaveRequestValidator().FirstError(r));
    }

    [Fact]
    public async Task Each_checked_document_is_stored_once_in_one_transaction()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        var request = Valid();
        request.Documents = ["b1", " B2 ", "B1"];
        await Service(sessions, repository).SaveAsync(request, "USER1");
        Assert.Equal(["B1", "B2"], repository.Saved);
        Assert.True(sessions.Last.Committed);
    }

    [Theory]
    [InlineData(1, typeof(ConflictException), "Record Duplicated! Please Change The Corresponding Field")]
    [InlineData(2, typeof(NotFoundException), "Record not found")]
    [InlineData(3, typeof(BusinessRuleException), "Carton Number not found")]
    public async Task A_document_that_cannot_be_saved_says_why_and_nothing_is_committed(int code, Type exception, string message)
    {
        var sessions = new FakeSessions();
        var failure = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(sessions, new FakeRepository(d => d == "B2" ? code : 0)).SaveAsync(new StorageDocumentSaveRequest
        {
            Documents = ["B1", "B2"], DocType = "B", Carton = "C1", FileStatus = "I",
        }, "USER1"));
        Assert.Equal((exception, message), (failure.GetType(), failure.Message));
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task The_lookups_add_the_departments_and_a_search_is_cut_at_five_thousand_rows()
    {
        var service = Service(new FakeSessions(), new FakeRepository());
        Assert.Equal(["ACCOUNT"], (await service.LookupsAsync()).Departments);
        var result = await service.SearchAsync(new StorageDocumentFilter());
        Assert.Equal((5000, true), (result.Rows.Count, result.Truncated));
    }
}

/// <summary>Storage Document against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class StorageDocumentDatabaseTests
{
    private static StorageDocumentRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string Checksum = "SELECT CHECKSUM_AGG(CHECKSUM(STRDocumentNo, STRCartonNo, STRDocType, STRDept, STRFileStatus, STRRequestBy, DORequest, DODelivery, DOReturn, STRRemarks, LastUpdateBy)) + COUNT(*) FROM genisis.WS_StorageDocument";

    [Fact]
    public async Task The_search_gives_what_the_tables_hold_for_each_document_type()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        Assert.True((await Repository.DepartmentsAsync(session)).Count > 0);

        var pv = await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "P - Payment Voucher" });
        Assert.Equal(Math.Min(await Count(session, "SELECT COUNT(*) FROM genisis.WS_StoragePVDoc"), Genisis.Api.Worksheet.WorksheetLimits.FetchRows), pv.Count);
        var stored = await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "P", FileStatus = "I - In" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_StoragePVDoc WHERE STRFileStatus = 'I'"), stored.Count);
        var one = pv[0].DocumentNo;
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_StoragePVDoc WHERE SearchPVNo >= @n AND SearchPVNo <= @n", new { n = one }),
            (await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "P", DocFrom = " " + one.ToLowerInvariant() + " ", DocTo = one })).Count);
        Assert.Empty(await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "P", DocFrom = one, DocTo = one, Carton = "NO-SUCH-CARTON" }));

        var receipts = await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "R" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_StorageRecDoc"), receipts.Count);

        var bordx = await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "B" });
        Assert.Equal(Math.Min(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Bordx WHERE BordxRefNo IS NOT NULL"), Genisis.Api.Worksheet.WorksheetLimits.FetchRows), bordx.Count);
        var firstNo = bordx[0].DocumentNo;
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Bordx WHERE BordxRefNo >= @n AND BordxRefNo <= @n", new { n = firstNo }), (await Repository.SearchAsync(session, new StorageDocumentFilter { DocType = "B", DocFrom = firstNo.ToLowerInvariant(), DocTo = firstNo })).Count);
        Assert.All(bordx.Where(b => b.CartonNo is null), b => Assert.Null(b.DocType));
    }

    [Fact]
    public async Task A_bordx_is_stored_found_with_its_carton_changed_and_a_second_store_is_refused_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var unstored = await read.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(b.BordxRefNo) FROM genisis.WS_Bordx b WHERE NOT EXISTS (SELECT 1 FROM genisis.WS_StorageDocument s WHERE s.STRDocumentNo = b.BordxRefNo) ORDER BY b.BordxRefNo DESC", transaction: read.Transaction);
        var carton = await read.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(CRTCartonNo) FROM genisis.WS_Carton WHERE LEN(CRTCartonNo) <= 20 ORDER BY CRTCartonNo DESC", transaction: read.Transaction);
        var day = new DateTime(2026, 10, 30, 9, 0, 0);
        var request = new StorageDocumentSaveRequest
        {
            Documents = [unstored], DocType = "B - Bordx", Carton = carton.ToLowerInvariant(), FileStatus = "O - Out", RequestBy = "zz tester", Department = "ACCOUNT", RequestDate = new DateTime(2026, 10, 1),
            DeliveryDate = new DateTime(2026, 10, 2), ReturnDate = new DateTime(2026, 10, 3), Remarks = "box one",
        };

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            Assert.Equal(2, await Repository.SaveAsync(write, unstored, new StorageDocumentSaveRequest { Documents = [unstored], DocType = "B", Carton = carton, FileStatus = "I", Edit = true }, "ZZTEST", day));
            Assert.Equal(3, await Repository.SaveAsync(write, unstored, new StorageDocumentSaveRequest { Documents = [unstored], DocType = "B", Carton = "NO-SUCH-CARTON", FileStatus = "I" }, "ZZTEST", day));
            Assert.Equal(0, await Repository.SaveAsync(write, unstored, request, "ZZTEST", day));
            Assert.Equal(1, await Repository.SaveAsync(write, unstored, request, "ZZTEST", day));

            var row = Assert.Single(await Repository.SearchAsync(write, new StorageDocumentFilter { DocType = "B", DocFrom = unstored, DocTo = unstored, FileStatus = "O", RequestDate = new DateTime(2026, 10, 1) }));
            Assert.Equal((unstored, carton, "B", "O", "ZZ TESTER", new DateTime(2026, 10, 1), new DateTime(2026, 10, 2), new DateTime(2026, 10, 3), "BOX ONE", "ACCOUNT", day.Date),
                (row.DocumentNo, row.CartonNo, row.DocType, row.FileStatus, row.RequestBy, row.RequestDate, row.DeliveryDate, row.ReturnDate, row.Remarks, row.Department, row.RegisterDate));

            request.Edit = true;
            request.RequestBy = "";
            request.Remarks = "moved";
            Assert.Equal(0, await Repository.SaveAsync(write, unstored, request, "ZZTEST2", day));
            var changed = await write.Connection.QueryFirstAsync("SELECT RTRIM(STRRemarks) AS Remarks, DORequest, DODelivery, DOReturn, RTRIM(LastUpdateBy) AS UserCode FROM genisis.WS_StorageDocument WHERE STRDocumentNo = @unstored", new { unstored }, write.Transaction);
            Assert.Equal(("MOVED", null, null, null, "ZZTEST2"), ((string)changed.Remarks, (DateTime?)changed.DORequest, (DateTime?)changed.DODelivery, (DateTime?)changed.DOReturn, (string)changed.UserCode));
        }

        Assert.Equal(before, await Count(read, Checksum));
    }
}
