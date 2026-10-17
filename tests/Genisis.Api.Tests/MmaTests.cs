using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Mma;
using Genisis.Api.Plans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class MmaTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Messages_follow_desktop()
    {
        var p = new MmaProcedureRequest();
        Assert.Equal("Please Enter Main Body Part", MmaValidator.Validate(p));
        p.BodyPart = "Eye";
        Assert.Equal("Please Enter OPCS Narrative", MmaValidator.Validate(p));
        p.Narrative = "Test";
        Assert.Equal("Please Enter Page No", MmaValidator.Validate(p));
        p.PageNo = "2a";
        Assert.Equal("Page No must be a number.", MmaValidator.Validate(p));
        p.PageNo = "29";
        Assert.Null(MmaValidator.Validate(p));
        p.Part = "d";
        Assert.Equal("Part must be A, B or C.", MmaValidator.Validate(p));
        p.Part = "b";
        p.Narrative = new string('x', 401);
        Assert.Equal("OPCS Narrative cannot be longer than 400 characters.", MmaValidator.Validate(p));

        Assert.Equal("Please Enter Surgeon's or Anaesthetist's Category", MmaValidator.Validate(new MmaFeeRequest { SurgeonFee = 10 }));
        Assert.Equal("Fees must be from 0 to 99,999,999.", MmaValidator.Validate(new MmaFeeRequest { SurgeonCategory = "S", SurgeonFee = -1 }));
        Assert.Null(MmaValidator.Validate(new MmaFeeRequest { AnaesthetistCategory = "A1" }));
    }

    [Fact]
    public async Task Procedure_and_fee_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        async Task<List<MmaProcedureRow>> Procedures(string code) =>
            await conn.ProcQueryAsync<MmaProcedureRow>("genisis.MmaProcedure_Search", new { top = 10, code }, tx);
        async Task<List<MmaFeeRow>> Fees(string surgeonCategory) =>
            await conn.ProcQueryAsync<MmaFeeRow>("genisis.MmaFee_Search", new { surgeonCategory }, tx);

        var p = new MmaProcedureRequest
        {
            BodyPart = " Test Body ", Code = "ZZT01", Narrative = "Test procedure", SurgeonCategory = "ZZSCAT", AnaesthetistCategory = "ZZACAT",
            Part = "b", PartACost = "80-200", PageNo = "29",
        };
        var index = await MmaService.SaveProcedureAsync(conn, tx, null, p, "TEST");
        Assert.Equal(new MmaProcedureRow(index, "Test Body", "ZZT01", "Test procedure", "ZZSCAT", "ZZACAT", "B", "80-200", "29"),
            Assert.Single(await Procedures("ZZT01")));
        p.Narrative = "Changed";
        p.Part = null;
        await MmaService.SaveProcedureAsync(conn, tx, index, p, "TEST");
        var changed = Assert.Single(await Procedures("ZZT01"));
        Assert.Equal(("Changed", (string?)null), (changed.Narrative, changed.Part));

        var fee = await MmaService.SaveFeeAsync(conn, tx, null, new MmaFeeRequest { SurgeonCategory = "ZZSCAT", SurgeonFee = 275, AnaesthetistFee = 230 }, "TEST");
        Assert.Equal(new MmaFeeRow(fee, "ZZSCAT", 275, null, 230), Assert.Single(await Fees("ZZSCAT")));

        var row = await conn.QueryFirstOrDefaultAsync<MmaScheduleRow>("genisis.MmaSchedule_Move", new { code = "ZZT01", direction = 0 }, tx,
            commandType: System.Data.CommandType.StoredProcedure);
        Assert.Equal(("ZZT01", "Changed", (decimal?)275), (row!.Code, row.MmaDescription, row.MmaSurgeonFee));

        Assert.Equal("This record cannot be deleted because it is used in Procedures table!",
            (await Assert.ThrowsAsync<MmaException>(() => MmaService.DeleteFeeAsync(conn, tx, fee))).Message);
        await MmaService.DeleteProcedureAsync(conn, tx, index);
        await MmaService.DeleteFeeAsync(conn, tx, fee);
        Assert.Empty(await Procedures("ZZT01"));
        Assert.Empty(await Fees("ZZSCAT"));
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<MmaException>(() => MmaService.DeleteProcedureAsync(conn, tx, index))).Message);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<MmaException>(() => MmaService.SaveFeeAsync(conn, tx, fee, new MmaFeeRequest { SurgeonCategory = "X" }, "TEST"))).Message);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Schedule_moves_through_codes_in_order()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = new DbConnections(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build());
        var mma = new MmaService(db);

        async Task<MmaScheduleRow?> Move(string? code, ScheduleDirection direction) => (await mma.MoveScheduleAsync(code, direction, default)).Row;

        var first = await Move(null, ScheduleDirection.Next);
        var second = await Move(first!.Code, ScheduleDirection.Next);
        Assert.NotEqual(first.Code, second!.Code);
        Assert.Equal(first.Code, (await Move(second.Code, ScheduleDirection.Previous))!.Code);
        var beforeFirst = await mma.MoveScheduleAsync(first.Code, ScheduleDirection.Previous, default);
        Assert.Equal((null, MmaMessages.FirstRecord), (beforeFirst.Row, beforeFirst.Message));
        var last = await Move(null, ScheduleDirection.Previous);
        Assert.Equal(MmaMessages.LastRecord, (await mma.MoveScheduleAsync(last!.Code, ScheduleDirection.Next, default)).Message);
        Assert.Equal(second.Code, (await Move(second.Code, ScheduleDirection.Find))!.Code);
        Assert.Equal(ScheduleDirection.Previous, MmaValidator.ParseDirection(" PREV "));
        Assert.Equal(ScheduleDirection.Next, MmaValidator.ParseDirection(null));
    }
}
