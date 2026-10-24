using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Worksheet.Engine;

namespace Genisis.Api.Worksheet.Calculation;

/// <summary>
/// The rows of one worksheet version, each as the parameters of the desktop's procedure that writes it: the version row of <c>Claim</c> and of <c>ClaimOthers</c> (typed), and the tables of
/// <c>saveLimit</c>, <c>saveActual</c>, <c>saveActualGST</c>, <c>saveApprove</c>, <c>saveHos</c> and <c>saveMember</c> as the text of their cells by column.
/// </summary>
/// <param name="Claim">The parameters of <c>Insert_WS_Claim_2012</c> (<see cref="ClaimVersionRow.Build"/>).</param>
/// <param name="Others">The parameters of <c>Insert_WS_ClaimOthers_Jul08</c> (<see cref="ClaimOthersRow.Build"/>).</param>
/// <param name="Limit">The limits and the days (<see cref="WorksheetRows.Limits"/>).</param>
/// <param name="Actual">What the invoices add up to.</param>
/// <param name="ActualGst">The GST copy of the actual row.</param>
/// <param name="Approve">What the plan lets through.</param>
/// <param name="Hospital">Payable to the hospital.</param>
/// <param name="Member">Payable to, or due from, the member.</param>
public sealed record WorksheetVersionRows(
    Dictionary<string, object?> Claim,
    Dictionary<string, object?> Others,
    Dictionary<string, string> Limit,
    Dictionary<string, string> Actual,
    Dictionary<string, string> ActualGst,
    Dictionary<string, string> Approve,
    Dictionary<string, string> Hospital,
    Dictionary<string, string> Member)
{
    /// <summary>The case the rows are for.</summary>
    public string CaseNo => Convert.ToString(Claim["CasNumber"])!.Trim();

    /// <summary>The worksheet version the rows are for.</summary>
    public int Version => Convert.ToInt32(Claim["claimversion"], System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The worksheet is void (status <c>XR</c>): its invoices are bordxed under the reference VOID.</summary>
    public bool Void => Convert.ToString(Claim["CLMWSStatus"])?.Trim() == ClaimVersionRow.VoidStatus;
}

/// <summary>What writing a version changed besides its own rows.</summary>
/// <param name="InvoicesMarked">Invoices of the version marked as worksheeted.</param>
public sealed record RegistrationWriteResult(int InvoicesMarked);

/// <summary>Writes the rows of a worksheet version, in the order of the desktop's save, inside the caller's transaction. Nothing here commits.</summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// <c>SaveClaim</c>, <c>SaveClaimRemarks</c>, <c>saveLimit</c>, <c>saveActual</c>, <c>saveActualGST</c>, <c>saveApprove</c>, <c>saveHos</c> and <c>saveMember</c> (<c>saveApproveGST</c> is not run: no worksheet
    /// saved since 2019 has its GST approve row), then <c>UpdateInvoice</c> and <c>UpdateCasReserve</c>.
    /// </summary>
    Task<RegistrationWriteResult> WriteVersionAsync(IDbSession session, WorksheetVersionRows rows);
}

/// <inheritdoc />
public sealed class RegistrationRepository : IRegistrationRepository
{
    /// <summary>How many characters of the bank account number <c>Insert_WS_Claim_2012</c> keeps (its parameter is <c>nvarchar(15)</c>; the column holds 30).</summary>
    public const int ProcedureAccountNoLength = 15;

    private static DynamicParameters Grid(string caseNo, int version, IReadOnlyDictionary<string, string> cells, IEnumerable<string> columns)
    {
        var p = new DynamicParameters();
        p.Add("CasNumber", caseNo);
        p.Add("CLMversion", version);
        foreach (var column in columns) p.Add(column, cells.GetValueOrDefault(column, ""));
        return p;
    }

    private static DynamicParameters Typed(IReadOnlyDictionary<string, object?> values)
    {
        var p = new DynamicParameters();
        foreach (var (name, value) in values) p.Add(name, value);
        return p;
    }

    /// <inheritdoc />
    public async Task<RegistrationWriteResult> WriteVersionAsync(IDbSession session, WorksheetVersionRows rows)
    {
        var connection = session.Connection;
        var transaction = session.Transaction;
        var cas = rows.CaseNo;
        var version = rows.Version;
        await connection.ProcExecAsync("genisis.WS_Insert_WS_Claim_2012", Typed(rows.Claim), transaction);
        if (Convert.ToString(rows.Claim["CLMAccountNo"]) is { Length: > ProcedureAccountNoLength } accountNo)
            await connection.ProcScalarAsync<int>("genisis.WorksheetSave_SetAccountNo", new { cas, version, accountNo }, transaction);
        await connection.ProcScalarAsync<int>("genisis.WorksheetSave_ClosePending", new { cas }, transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimOthers_Jul08", Typed(rows.Others), transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimLimit", Grid(cas, version, rows.Limit, [.. WorksheetRows.AllColumns, .. WorksheetRows.DayColumns]), transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimActual", Grid(cas, version, rows.Actual, WorksheetRows.AllColumns), transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimActualGST", Grid(cas, version, rows.ActualGst, WorksheetRows.AllColumns), transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimApprove", Grid(cas, version, rows.Approve, WorksheetRows.AllColumns), transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimHOS", Grid(cas, version, rows.Hospital, WorksheetRows.AllColumns), transaction);
        await connection.ProcExecAsync("genisis.WS_Insert_WS_ClaimMBM", Grid(cas, version, rows.Member, WorksheetRows.AllColumns), transaction);
        var marked = await connection.ProcScalarAsync<int>("genisis.WorksheetSave_MarkInvoices", new { cas, version, @void = rows.Void }, transaction);
        await connection.ProcScalarAsync<int>("genisis.WorksheetSave_ClearReserve", new { cas }, transaction);
        return new RegistrationWriteResult(marked);
    }
}
