using Genisis.Api.Common.Errors;

namespace Genisis.Api.Mma;

public class MmaProcedureRequest
{
    public string? BodyPart { get; set; }
    public string? Code { get; set; }
    public string? Narrative { get; set; }
    public string? SurgeonCategory { get; set; }
    public string? AnaesthetistCategory { get; set; }
    public string? Part { get; set; }
    public string? PartACost { get; set; }
    public string? PageNo { get; set; }
}

public sealed record MmaProcedureRow(int Index, string? BodyPart, string? Code, string? Narrative, string? SurgeonCategory,
    string? AnaesthetistCategory, string? Part, string? PartACost, string? PageNo);

public sealed record MmaProcedureSearchResult(List<MmaProcedureRow> Rows, bool Truncated, int Limit);

public class MmaFeeRequest
{
    public string? SurgeonCategory { get; set; }
    public decimal? SurgeonFee { get; set; }
    public string? AnaesthetistCategory { get; set; }
    public decimal? AnaesthetistFee { get; set; }
}

public sealed record MmaFeeRow(int Index, string? SurgeonCategory, decimal? SurgeonFee, string? AnaesthetistCategory, decimal? AnaesthetistFee);

public sealed record MmaScheduleRow(string Code, string? MmaDescription, string? ScheduleDescription, string? MmaBodyPart, string? ScheduleBodyPart,
    string? MmaPage, string? SchedulePage, decimal? MmaSurgeonFee, decimal? MmaAnaesthetistFee, decimal? ScheduleSurgeonFeeMin,
    decimal? ScheduleSurgeonFeeMax, decimal? ScheduleAnaesthetistFee);

/// <summary>Accounts screen move: Prev / Next buttons, or Find (first code at or after the typed code).</summary>
public enum ScheduleDirection
{
    Previous = -1,
    Find = 0,
    Next = 1,
}

public sealed record ScheduleMove(MmaScheduleRow? Row, string? Message);

/// <summary>An Entity rule stopped the change (record missing, fee row still in use).</summary>
public class MmaException(string message) : BusinessRuleException(message);
