using System.Globalization;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>How a plan limits what a member may claim, which decides the previous balance and the balance limit of the version row.</summary>
public enum LimitKind
{
    /// <summary>An annual limit: the member's available limit before the worksheet is the previous balance.</summary>
    Annual,

    /// <summary>A limit per disability: the plan's per-disability limit is the previous balance.</summary>
    PerDisability,

    /// <summary>No limit to keep: the previous balance and the balance limit are the final approve.</summary>
    None,
}

/// <summary>What <c>SaveClaim</c> reads besides the grid: the case, who saves, the worksheet status, the payee and bank, the collected-by choices and the member's state.</summary>
public sealed record ClaimVersionInput
{
    /// <summary>Case number.</summary>
    public required string CaseNo { get; init; }

    /// <summary>The worksheet version being written.</summary>
    public required int Version { get; init; }

    /// <summary>The user who saves (opens, closes and last updates the version).</summary>
    public required string UserCode { get; init; }

    /// <summary>The date of the save (server time): the open date, the last update date and the close date.</summary>
    public required DateTime Today { get; init; }

    /// <summary>The worksheet status as chosen, <c>"AA - Approve"</c>: its first two letters are <c>CLMWSStatus</c>; <c>XR</c> makes the version void.</summary>
    public required string WorksheetStatus { get; init; }

    /// <summary>The pay-to of the first invoice: <c>H</c> hospital, otherwise the member.</summary>
    public string? PayToCode { get; init; }

    /// <summary>The stay type of the first invoice.</summary>
    public string? StayType { get; init; }

    /// <summary>The payee's name (<c>txtPayTo</c>).</summary>
    public string? PayTo { get; init; }

    /// <summary>The bank (<c>cboBank</c>).</summary>
    public string? Bank { get; init; }

    /// <summary>The bank account number.</summary>
    public string? AccountNo { get; init; }

    /// <summary>The GP's name.</summary>
    public string? GpName { get; init; }

    /// <summary>The date of the last document.</summary>
    public DateTime? LastDocumentDate { get; init; }

    /// <summary>Who collects the excess: <c>P</c> payor, <c>H</c> hospital, <c>M</c> member.</summary>
    public char ExcessCollectedBy { get; init; } = 'M';

    /// <summary>Who collects what is above the limit: <c>P</c>, <c>H</c> or <c>M</c>.</summary>
    public char LimitCollectedBy { get; init; } = 'M';

    /// <summary>The first letter of the ex-gratia choice, or <c>null</c> for none.</summary>
    public string? ExGratia { get; init; }

    /// <summary>The ex-gratia amount (zero in every saved worksheet).</summary>
    public decimal ExGratiaAmount { get; init; }

    /// <summary>The case management fees (zero in every saved worksheet).</summary>
    public decimal CaseMgmtFees { get; init; }

    /// <summary>The member is a BTBG member (<c>MBMOthersTwo.MBMBTBG</c> = Y).</summary>
    public bool MemberBtbg { get; init; }

    /// <summary>Which limit applies to the member's plan.</summary>
    public LimitKind Limit { get; init; } = LimitKind.Annual;

    /// <summary>The member's available limit before the worksheet (annual limits).</summary>
    public decimal AvailableLimit { get; init; }

    /// <summary>The plan's per-disability limit (per-disability limits).</summary>
    public decimal PerDisabilityLimit { get; init; }

    /// <summary>The lifetime balance of a lifetime guarantee that renews.</summary>
    public decimal LifetimeBalance { get; init; }

    /// <summary>The take-over limit.</summary>
    public decimal TakeOverLimit { get; init; }

    /// <summary>The case the worksheet is linked to.</summary>
    public string? LinkCase { get; init; }

    /// <summary>The worksheet version of the linked case.</summary>
    public int? LinkVersion { get; init; }
}

/// <summary>
/// The version row of <c>Claim</c> as <c>SaveClaim("A")</c> writes it through <c>Insert_WS_Claim_2012</c>. The result is keyed by the procedure's parameter names. Top-up members (a top-up amount, the
/// four-database duplicate claim) are not covered: no saved worksheet has one, and their balances need the top-up routines that are not built.
/// </summary>
public static class ClaimVersionRow
{
    /// <summary>The worksheet status that voids the version.</summary>
    public const string VoidStatus = "XR";

    /// <summary>The parameters of <c>Insert_WS_Claim_2012</c>, in its order.</summary>
    public static readonly IReadOnlyList<string> Parameters =
    [
        "CasNumber", "claimversion", "ClaimOpendate", "ClaimOpenBy", "ClaimLastUpdateUser", "ClaimLastUpdateDate", "ClaimBordxStatus", "CLMBordxWSStatus", "CLMBordxWSASCII", "LimitCollectedBy",
        "CLMTopMBMNumber", "CLMCashType", "CLMLastDocDate", "CLMExGratia", "CLMStayType", "CLMTotalActual", "CLMTotalApproved", "CLMPayableByMedix2H", "CLMCompExcess", "ExcessCollectedBy",
        "CLMTotalCompApp", "CLMExcess", "CLMReimburse", "CLMBalanceLimit", "ClaimCloseDate", "CLMPreviousBalance", "CLMPreviousNewLimit", "CLMLifetimeBal", "CLMPrevTOLmt", "BordxRefNo",
        "CLMPaidBYMedix2M", "CLMPaidByMedix2H", "CLMLimitExceeded", "CLMPayableByMedix2M", "CLMWSStatus", "CLMCoPayRB", "CLMExGratiaAmt", "CLMPrevCancerLmt", "CLMPrevHomeNCLmt", "ClaimStatus",
        "CLMPrevPerDisLmt", "CLMBordxAmt", "CLMBTBGAmt", "CLMFinalApprove", "CLMPayto", "CLMAccountNo", "CLMBank", "MBMTopupInd", "CLMMainTUAmt", "CLMGPName", "CaseMgmtFees", "CLMPrevMajorMed",
        "CLMMajorMedFee", "CLMDeductibleAmt", "CLMLinkCase", "CLMLinkVersion",
    ];

    /// <summary>The worksheet row whose limit is saved as the previous cancer limit (the monthly row) and the one saved as the previous home nursing limit.</summary>
    public const int MonthlySlot = 17, HomeNursingSlot = 24;

    /// <summary>VB's <c>IIf(IsNumeric(text), text, 0)</c> for a limit text: a figure with thousands separators is a number; FULL, a dash or nothing is 0.</summary>
    public static decimal NumericLimit(string? text) =>
        decimal.TryParse(text?.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    /// <summary>
    /// The bordx amount: the final approve for a reimbursement (100 per cent of 9,154 versions saved since July 2020) and the amount payable to the hospital plus to the member for a cashless one (99.95 per
    /// cent of 24,564); 99.97 per cent together.
    /// </summary>
    public static decimal BordxAmount(string? cashType, WorksheetFinal final, decimal payableToHospital, decimal payableToMember) =>
        string.Equals(cashType, "R", StringComparison.OrdinalIgnoreCase) ? final.FinalApprove : payableToHospital + payableToMember;

    /// <summary>
    /// The BTBG amount: for a BTBG member's cashless worksheet, what the member nets once the hospital has been paid (the final approve less the hospital's payable) when it has the opposite sign to the hospital's
    /// payable, otherwise 0. Over the 3,074 cashless versions of BTBG members saved since July 2020 this equals the saved amount in 3,052 (99.3 per cent).
    /// </summary>
    public static decimal BtbgAmount(bool memberBtbg, string? cashType, WorksheetFinal final, decimal payableToHospital)
    {
        if (!memberBtbg || !string.Equals(cashType, "C", StringComparison.OrdinalIgnoreCase)) return 0m;
        var net = final.FinalApprove - payableToHospital;
        return net < 0 && payableToHospital > 0 || net > 0 && payableToHospital < 0 ? net : 0m;
    }

    /// <summary>
    /// The previous balance and the balance limit. Annual: the available limit, for both (the desktop in use saves the previous balance as the balance limit: 94 per cent of the saved versions); per disability: the
    /// plan's limit for both (99.7 per cent for the previous balance, 95 per cent for the balance limit); none: the final approve for both (3,142 saved versions).
    /// </summary>
    public static (decimal Previous, decimal Balance) Balances(LimitKind kind, decimal availableLimit, decimal perDisabilityLimit, decimal finalApprove) => kind switch
    {
        LimitKind.Annual => (availableLimit, availableLimit),
        LimitKind.PerDisability => (perDisabilityLimit, perDisabilityLimit),
        _ => (finalApprove, finalApprove),
    };

    /// <summary>The version row's parameters for an evaluated worksheet and its summary rows.</summary>
    public static Dictionary<string, object?> Build(ClaimVersionInput i, WorksheetEvaluation grid, WorksheetFinal final)
    {
        var cash = WorksheetRows.CashType(i.PayToCode);
        var totals = WorksheetRows.ClaimTotals(grid, final, cash);
        var payableH = totals["CLMPayableByMedix2H"];
        var payableM = totals["CLMPayableByMedix2M"];
        var (previous, balance) = Balances(i.Limit, i.AvailableLimit, i.PerDisabilityLimit, final.FinalApprove);
        var status = i.WorksheetStatus.Trim();
        var statusCode = (status.Length >= 2 ? status[..2] : status).ToUpperInvariant();
        var voided = statusCode == VoidStatus;
        var today = i.Today.Date;
        string Limit(int slot) => grid.Lines.FirstOrDefault((l) => l.Slot == slot)?.Limit ?? "";

        return new Dictionary<string, object?>
        {
            ["CasNumber"] = i.CaseNo.Trim(),
            ["claimversion"] = i.Version,
            ["ClaimOpendate"] = today,
            ["ClaimOpenBy"] = i.UserCode,
            ["ClaimLastUpdateUser"] = i.UserCode,
            ["ClaimLastUpdateDate"] = today,
            ["ClaimBordxStatus"] = voided ? "1" : "0",
            ["CLMBordxWSStatus"] = "N",
            ["CLMBordxWSASCII"] = "N",
            ["LimitCollectedBy"] = i.LimitCollectedBy.ToString(),
            ["CLMTopMBMNumber"] = "",
            ["CLMCashType"] = cash ?? "",
            ["CLMLastDocDate"] = i.LastDocumentDate?.Date,
            ["CLMExGratia"] = string.IsNullOrWhiteSpace(i.ExGratia) ? "" : i.ExGratia.Trim()[..1],
            ["CLMStayType"] = i.StayType?.Trim() ?? "",
            ["CLMTotalActual"] = totals["CLMTotalActual"],
            ["CLMTotalApproved"] = totals["CLMTotalApproved"],
            ["CLMPayableByMedix2H"] = payableH,
            ["CLMCompExcess"] = 0m,
            ["ExcessCollectedBy"] = i.ExcessCollectedBy.ToString(),
            ["CLMTotalCompApp"] = 0m,
            ["CLMExcess"] = totals["CLMExcess"],
            ["CLMReimburse"] = totals["CLMReimburse"],
            ["CLMBalanceLimit"] = balance,
            ["ClaimCloseDate"] = today,
            ["CLMPreviousBalance"] = previous,
            ["CLMPreviousNewLimit"] = 0m,
            ["CLMLifetimeBal"] = i.LifetimeBalance,
            ["CLMPrevTOLmt"] = i.TakeOverLimit,
            ["BordxRefNo"] = voided ? "VOID" : "",
            ["CLMPaidBYMedix2M"] = 0m,
            ["CLMPaidByMedix2H"] = 0m,
            ["CLMLimitExceeded"] = totals["CLMLimitExceeded"],
            ["CLMPayableByMedix2M"] = payableM,
            ["CLMWSStatus"] = statusCode,
            ["CLMCoPayRB"] = totals["CLMCoPayRB"],
            ["CLMExGratiaAmt"] = i.ExGratiaAmount,
            ["CLMPrevCancerLmt"] = NumericLimit(Limit(MonthlySlot)),
            ["CLMPrevHomeNCLmt"] = NumericLimit(Limit(HomeNursingSlot)),
            ["ClaimStatus"] = "C",
            ["CLMPrevPerDisLmt"] = i.PerDisabilityLimit,
            ["CLMBordxAmt"] = BordxAmount(cash, final, payableH, payableM),
            ["CLMBTBGAmt"] = BtbgAmount(i.MemberBtbg, cash, final, payableH),
            ["CLMFinalApprove"] = totals["CLMFinalApprove"],
            ["CLMPayto"] = i.PayTo?.Trim() ?? "",
            ["CLMAccountNo"] = i.AccountNo?.Trim() ?? "",
            ["CLMBank"] = i.Bank?.Trim() ?? "",
            ["MBMTopupInd"] = "N",
            ["CLMMainTUAmt"] = 0m,
            ["CLMGPName"] = i.GpName?.Trim() ?? "",
            ["CaseMgmtFees"] = i.CaseMgmtFees,
            ["CLMPrevMajorMed"] = 0m,
            ["CLMMajorMedFee"] = 0m,
            ["CLMDeductibleAmt"] = 0m,
            ["CLMLinkCase"] = i.LinkCase?.Trim() ?? "",
            ["CLMLinkVersion"] = i.LinkVersion ?? 0,
        };
    }
}
