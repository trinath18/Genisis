using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.MmaEntry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class MmaEntryTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    private static readonly Dictionary<string, MmaRate> Rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C7980"] = new MmaRate { Code = "C7980", MmaSurgeonFee = 3600, MmaAnaesthetistFee = 1845, ScheduleSurgeonMax = 4000, ScheduleSurgeonMin = 3000, ScheduleAnaesthetist = 1500 },
        ["A7300"] = new MmaRate { Code = "A7300", MmaSurgeonFee = 725, ScheduleSurgeonMax = 800, ScheduleSurgeonMin = 600 },
    };

    [Fact]
    public void Surgical_lines_sum_every_column_and_flag_fees_above_the_13th_schedule()
    {
        var result = MmaFeeCalculator.Calculate(MmaEntryCodes.Surgical,
        [
            new() { Code = "c7980", ActualFee = 4200, ActualAnaesthetistFee = 1400 },
            new() { Code = "A7300", ActualFee = 700, ActualAnaesthetistFee = 100 },
            new() { Code = " " },
            new() { Code = "NOPE", ActualFee = 50 },
        ], Rates);

        Assert.Equal(["C7980", "A7300", "NOPE"], result.Lines.Select(l => l.Code));
        Assert.Equal((true, false), (result.Lines[0].FeeAboveSchedule, result.Lines[0].AnaesthetistFeeAboveSchedule));
        Assert.Equal((false, true), (result.Lines[1].FeeAboveSchedule, result.Lines[1].AnaesthetistFeeAboveSchedule));
        Assert.False(result.Lines[2].Found);
        Assert.True(result.Lines[2].FeeAboveSchedule);
        Assert.Equal(4950m, result.TotalActualFee);
        Assert.Equal(1500m, result.TotalActualAnaesthetistFee);
        Assert.Equal(4325m, result.TotalMmaFee);
        Assert.Equal(1845m, result.TotalMmaAnaesthetistFee);
        Assert.Equal((4800m, 3600m, 1500m), (result.TotalScheduleMax, result.TotalScheduleMin, result.TotalScheduleAnaesthetist));
    }

    [Fact]
    public void Non_surgical_lines_have_no_anaesthetist_fee()
    {
        var result = MmaFeeCalculator.Calculate(MmaEntryCodes.NonSurgical, [new() { Code = "C7980", ActualFee = 100, ActualAnaesthetistFee = 999 }], Rates);
        Assert.Equal((100m, 0m, 0m, 0m), (result.TotalActualFee, result.TotalActualAnaesthetistFee, result.TotalMmaAnaesthetistFee, result.TotalScheduleAnaesthetist));
        Assert.False(result.Lines[0].AnaesthetistFeeAboveSchedule);
    }

    [Fact]
    public void Requests_are_limited_to_six_lines_and_a_known_mode()
    {
        var validator = new MmaEntryRequestValidator();
        Assert.True(validator.Validate(new MmaEntryRequest { Mode = "S", Lines = [new() { Code = "A1" }] }).IsValid);
        Assert.False(validator.Validate(new MmaEntryRequest { Mode = "X" }).IsValid);
        Assert.False(validator.Validate(new MmaEntryRequest { Mode = "S", Lines = [.. Enumerable.Range(0, 7).Select(_ => new MmaEntryLine())] }).IsValid);
        Assert.False(validator.Validate(new MmaEntryRequest { Mode = "N", Lines = [new() { Code = "A1", ActualFee = -1 }] }).IsValid);
    }

    [Fact]
    public async Task Real_codes_are_looked_up_in_the_schedule()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        string? code;
        await using (var conn = new SqlConnection(MainDb))
            code = await conn.ExecuteScalarAsync<string?>("SELECT TOP (1) RTRIM(OPCSCode) FROM genisis.Maint_MMASchedule WHERE SurgeonsFees IS NOT NULL AND Surgeon13th IS NOT NULL");
        if (code is null) return;
        var sessions = new DbSessions(new DbConnections(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build()));

        var result = await new MmaEntryService(sessions, new MmaEntryRepository()).CalculateAsync(new MmaEntryRequest
        {
            Mode = MmaEntryCodes.Surgical, Lines = [new() { Code = code, ActualFee = 1 }, new() { Code = "NO-SUCH" }],
        });

        Assert.True(result.Lines[0].Found);
        Assert.NotNull(result.Lines[0].Rate!.MmaSurgeonFee);
        Assert.False(result.Lines[1].Found);
        Assert.Equal(result.Lines[0].Rate!.ScheduleSurgeonMax, result.TotalScheduleMax);
    }
}
