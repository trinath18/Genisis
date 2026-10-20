using Genisis.Api.Common.Errors;

namespace Genisis.Api.Reinstatement;

/// <summary>Messages of frmMBMReintstatement; the desktop wording is kept, including its trailing spaces.</summary>
public static class ReinstatementMessages
{
    public const string EnterMembershipNumber = "Please Enter Membership Number ";
    public const string NotFound = "Membership not found!";
    public const string StillActive = "Membership is active! ";
    public const string Reinstated = "Membership Reinstated Successfully! ";

    // The desktop runs the inserts with these fields empty and fails with a SQL error; the web version asks for them first.
    public const string EnterEndtBordxDate = "Please Enter Endt. Bordx Date!";
    public const string EnterReinstateDate = "Please Enter Reinstatement Date!";
    public const string EnterLapsedDate = "Please Enter Lapsed Date!";
    public const string EnterExpiryDate = "Please Enter Expiry Date!";
    public const string EnterBatchNo = "Please Enter Batch No!";
    public const string InvalidBatchNo = "Batch No must be a number of up to 5 digits.";

    public const string AgeOutOfPremiumRange = "Age Out of Premium Range! Please check your Premium Table.";
}

public static class ReinstatementLimits
{
    /// <summary>MBMReinstatement.MBMEndtBordxBatch is char(5).</summary>
    public const int BatchNoMaxLength = 5;
}

/// <summary>MBMAmendHistory.MBMAmendEdtType written for a reinstatement.</summary>
public static class ReinstatementCodes
{
    public const string AmendType = "R";
}

public sealed class ReinstatementException(string message) : BusinessRuleException(message);
