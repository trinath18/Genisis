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
/// <param name="PayMbmStatus">The Y/N flag of each worksheet row (<see cref="PayMbmStatusRow"/>, <c>savePayMBMStatus</c>).</param>
/// <param name="ExcessLetter">The columns of the case's excess letter (<see cref="ExcessLetterRow.Build"/>), or <c>null</c> when the worksheet leaves no excess to write one for.</param>
/// <param name="SameLimit">What the case has left of its limits and days (<see cref="SameLimitRow"/>, <c>saveSameLimit</c>); the days are worked out from the case's row in the transaction.</param>
public sealed record WorksheetVersionRows(
    Dictionary<string, object?> Claim,
    Dictionary<string, object?> Others,
    Dictionary<string, string> Limit,
    Dictionary<string, string> Actual,
    Dictionary<string, string> ActualGst,
    Dictionary<string, string> Approve,
    Dictionary<string, string> Hospital,
    Dictionary<string, string> Member,
    Dictionary<string, string> PayMbmStatus,
    Dictionary<string, object?>? ExcessLetter = null,
    SameLimitInput? SameLimit = null)
{
    /// <summary>The case the rows are for.</summary>
    public string CaseNo => Convert.ToString(Claim["CasNumber"])!.Trim();

    /// <summary>The worksheet version the rows are for.</summary>
    public int Version => Convert.ToInt32(Claim["claimversion"], System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The worksheet is void (status <c>XR</c>): its invoices are bordxed under the reference VOID.</summary>
    public bool Void => Convert.ToString(Claim["CLMWSStatus"])?.Trim() == ClaimVersionRow.VoidStatus;
}

/// <summary>What <c>UpdateMBMBalance</c> did to the member's available limit.</summary>
/// <param name="Reason">Why the limit was not touched (the claim is not accepted, a top-up member, a lifetime plan, no annual limit, a kind D limit...), or <c>null</c> when it was.</param>
/// <param name="Record">Whose limit: <c>MBM</c> or <c>MBMCoveredPersons</c> and the cover.</param>
/// <param name="LimitKind">The plan's kind of limit: A, B, C or D.</param>
/// <param name="FinalApprove">What the worksheet approved.</param>
/// <param name="Before">The available limit before.</param>
/// <param name="After">The available limit after (never below 0).</param>
/// <param name="Applied">The limit was written.</param>
/// <param name="Unsupported">The worksheet is of a kind the save cannot do yet (a top-up member, a lifetime plan, a take-over case, a kind D limit, a member without the limit record): the save refuses it.</param>
public sealed record MemberBalanceResult(string? Reason, string? Record, string? LimitKind, decimal? FinalApprove, decimal? Before, decimal? After, bool Applied, bool Unsupported);

/// <summary>What writing a version changed besides its own rows.</summary>
/// <param name="InvoicesMarked">Invoices of the version marked as worksheeted.</param>
/// <param name="ExcessNumber">The number the excess sequence gave (<c>E</c>, the case, a dash and the sequence), or <c>null</c> when the worksheet leaves no excess.</param>
/// <param name="ExcessLetterAdded">The case had no excess letter and one was added.</param>
/// <param name="SameLimitLeft">What the case has left after the worksheet (days and undertaking), or <c>null</c> when the rows carry no same-limit input.</param>
/// <param name="MemberBalance">What was done to the member's available limit.</param>
public sealed record RegistrationWriteResult(int InvoicesMarked, string? ExcessNumber = null, bool ExcessLetterAdded = false, SameLimitLeft? SameLimitLeft = null, MemberBalanceResult? MemberBalance = null);

/// <summary>Writes the rows of a worksheet version, in the order of the desktop's save, inside the caller's transaction. Nothing here commits.</summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// <c>SaveClaim</c>, <c>SaveClaimRemarks</c>, <c>saveLimit</c>, <c>saveActual</c>, <c>saveActualGST</c>, <c>saveApprove</c>, <c>saveHos</c>, <c>saveMember</c>, <c>savePayMBMStatus</c>, <c>saveExcessLetter</c> and
    /// <c>saveSameLimit</c> (<c>saveApproveGST</c> is not run: no worksheet saved since 2019 has its GST approve row), then <c>UpdateInvoice</c>, <c>UpdateCasReserve</c> and <c>UpdateMBMBalance</c>.
    /// </summary>
    Task<RegistrationWriteResult> WriteVersionAsync(IDbSession session, WorksheetVersionRows rows);

    /// <summary>
    /// What the save would do to the member's available limit, worked out before anything is written. <c>Unsupported</c> in the result means the worksheet is of a kind the save cannot do yet (a top-up member, a lifetime
    /// plan, a take-over case, a kind D limit): <see cref="WriteVersionAsync"/> refuses it, and so should the caller before asking the user to confirm.
    /// </summary>
    Task<MemberBalanceResult?> CheckMemberBalanceAsync(IDbSession session, WorksheetVersionRows rows);
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
    public Task<MemberBalanceResult?> CheckMemberBalanceAsync(IDbSession session, WorksheetVersionRows rows) =>
        session.Connection.ProcFirstOrDefaultAsync<MemberBalanceResult>("genisis.WorksheetSave_MemberBalance", new { cas = rows.CaseNo, version = rows.Version, apply = false, amount = Convert.ToDecimal(rows.Claim["CLMFinalApprove"], System.Globalization.CultureInfo.InvariantCulture) }, session.Transaction);

    /// <inheritdoc />
    public async Task<RegistrationWriteResult> WriteVersionAsync(IDbSession session, WorksheetVersionRows rows)
    {
        var connection = session.Connection;
        var transaction = session.Transaction;
        var cas = rows.CaseNo;
        var version = rows.Version;
        if (await CheckMemberBalanceAsync(session, rows) is { Unsupported: true } unsupported) throw new InvalidOperationException($"The worksheet of {cas} cannot be saved yet: {unsupported.Reason}.");
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
        var status = new DynamicParameters();
        status.Add("cas", cas);
        status.Add("version", version);
        foreach (var column in PayMbmStatusRow.AllColumns) status.Add(column, rows.PayMbmStatus.GetValueOrDefault(column));
        await connection.ProcScalarAsync<int>("genisis.WorksheetSave_PayMbmStatus", status, transaction);
        string? excessNumber = null;
        var letterAdded = false;
        if (rows.ExcessLetter is { } letter)
        {
            excessNumber = await connection.ProcScalarAsync<string>("genisis.WorksheetSave_NextExcessNumber", new { cas }, transaction);
            var p = new DynamicParameters();
            p.Add("cas", cas);
            p.Add("number", excessNumber);
            foreach (var (key, value) in letter.Where((l) => l.Key != "EXCIssuedDate"))
                p.Add(key switch { "CASNumber" => "cas", "INVNo" => "invoiceNo", "INVAmt" => "invoiceAmount", "EXCDate" => "date", "ExcessAmt" => "excessAmount", "EXCIssuedBy" => "issuedBy", "EXCOthersDesc" => "othersDesc", _ => key }, value);
            letterAdded = await connection.ProcScalarAsync<int>("genisis.WorksheetSave_ExcessLetter", p, transaction) == 1;
        }
        SameLimitLeft? left = null;
        if (rows.SameLimit is { } same)
        {
            var previous = await connection.ProcFirstOrDefaultAsync<SameLimitLeft>("genisis.WorksheetSave_SameLimitOf", new { cas }, transaction);
            var days = SameLimitRow.NextDays(previous?.Days, same.Allowed, same.Used);
            var undertaking = SameLimitRow.NextUndertaking(previous?.UndertakingExcess, same.MemberUndertaking, same.UndertakingUsed);
            left = new SameLimitLeft(days.Board, days.Icu, days.PhysicianVisits, days.KidneyMonths, days.Cash, days.Guardian, undertaking);
            var p = new DynamicParameters();
            p.Add("cas", cas);
            foreach (var (column, text) in same.Limits) p.Add(column, text);
            p.Add("CLMBoardDays", WorksheetRows.Number(days.Board));
            p.Add("CLMICUDays", WorksheetRows.Number(days.Icu));
            p.Add("CLMINHOSPhyDays", WorksheetRows.Number(days.PhysicianVisits));
            p.Add("CLMKIDDCDays", WorksheetRows.Number(days.KidneyMonths));
            p.Add("CLMCashDays", WorksheetRows.Number(days.Cash));
            p.Add("CLMGurdianDays", WorksheetRows.Number(days.Guardian));
            p.Add("CLMUndExcess", undertaking);
            p.Add("CLMPerDisLmt", same.PerDisabilityLimit);
            await connection.ProcScalarAsync<int>("genisis.WorksheetSave_SameLimit", p, transaction);
        }
        var marked = await connection.ProcScalarAsync<int>("genisis.WorksheetSave_MarkInvoices", new { cas, version, @void = rows.Void }, transaction);
        await connection.ProcScalarAsync<int>("genisis.WorksheetSave_ClearReserve", new { cas }, transaction);
        var balance = await connection.ProcFirstOrDefaultAsync<MemberBalanceResult>("genisis.WorksheetSave_MemberBalance", new { cas, version, apply = true }, transaction);
        await connection.ProcScalarAsync<int>("genisis.WorksheetSave_PreviousUndertaking", new { cas, version, amount = left?.UndertakingExcess ?? 0m }, transaction);
        return new RegistrationWriteResult(marked, excessNumber, letterAdded, left, balance);
    }
}
