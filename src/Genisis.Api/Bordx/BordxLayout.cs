namespace Genisis.Api.Bordx;

/// <summary>A fixed-width field: 1-based start column (as in the desktop's Mid() calls) and length.</summary>
public readonly record struct BordxField(int Start, int Length);

/// <summary>Header line of a bordereaux file.</summary>
public static class HeaderLayout
{
    public static readonly BordxField RecordType = new(1, 1);
    public static readonly BordxField PayorCode = new(2, 2);
    public static readonly BordxField SubmissionDate = new(4, 8);
    public static readonly BordxField FileStatus = new(12, 1);
    /// <summary>The desktop stores Mid(12, 19) (status letter included) as the file name.</summary>
    public static readonly BordxField FileName = new(12, 19);
    /// <summary>"ZURICH" or "TAK..." marks the ZURICH / TAK layout.</summary>
    public static readonly BordxField PayorFormat = new(28, 6);
}

/// <summary>Trailer line of an endorsement file.</summary>
public static class TrailerLayout
{
    public static readonly BordxField TotalPrincipals = new(2, 10);
    public static readonly BordxField TotalSupplementaries = new(12, 10);
}

/// <summary>
/// Member line of a new-member file (FrmNewMBMEndt10 NewMBMInfoProcessing / VerifyNewMember). Where the ZURICH / TAK layout differs,
/// the field has a <c>ZurichTak</c> twin; use the <c>For</c> helpers.
/// </summary>
public static class MemberLayout
{
    public static readonly BordxField DataType = new(1, 1);
    public static readonly BordxField PolicyNo = new(2, 25);
    public static readonly BordxField PolicyNoZurichTak = new(2, 17);
    public static readonly BordxField AgentCode = new(27, 15);
    public static readonly BordxField Salutation = new(42, 5);
    public static readonly BordxField Name = new(47, 60);
    public static readonly BordxField Address = new(107, 90);
    public static readonly BordxField Address1 = new(107, 30);
    public static readonly BordxField Address2 = new(137, 30);
    public static readonly BordxField Address3 = new(167, 30);
    public static readonly BordxField City = new(197, 20);
    public static readonly BordxField PostCode = new(217, 5);
    public static readonly BordxField State = new(222, 15);
    public static readonly BordxField TelephoneHome = new(237, 12);
    public static readonly BordxField TelephoneOffice = new(249, 12);
    public static readonly BordxField TelephoneMobile = new(261, 12);
    public static readonly BordxField IcNumber = new(273, 20);
    public static readonly BordxField DateOfBirth = new(293, 8);
    public static readonly BordxField Sex = new(301, 1);
    public static readonly BordxField Nationality = new(302, 10);
    public static readonly BordxField Weight = new(312, 3);
    public static readonly BordxField Height = new(315, 3);
    public static readonly BordxField Language = new(318, 1);
    public static readonly BordxField Race = new(319, 1);
    public static readonly BordxField Occupation = new(320, 30);
    public static readonly BordxField WorkNature = new(350, 1);
    public static readonly BordxField BloodType = new(351, 3);
    public static readonly BordxField Allergies = new(354, 20);
    public static readonly BordxField EffectiveDate = new(374, 8);
    public static readonly BordxField ExpiryDate = new(382, 8);
    public static readonly BordxField PlanCode = new(390, 4);
    /// <summary>The payor's own plan code (ZURICH / TAK layout and cancellations), matched to PLN.PLNPayorPlan.</summary>
    public static readonly BordxField PayorPlanCode = new(390, 7);
    public static readonly BordxField InsuredType = new(394, 1);
    public static readonly BordxField InsuredTypeZurichTak = new(397, 1);
    public static readonly BordxField GroupCompany = new(395, 40);
    public static readonly BordxField GroupCompanyZurichTak = new(398, 37);
    public static readonly BordxField RenewalType = new(435, 1);
    public static readonly BordxField Exclusion = new(436, 2500);
    public static readonly BordxField Smoking = new(2936, 1);
    public static readonly BordxField Alcohol = new(2937, 1);
    public static readonly BordxField MaritalStatus = new(2938, 1);
    public static readonly BordxField Branch = new(2939, 15);
    public static readonly BordxField PreviousPolicyNo = new(2954, 25);
    public static readonly BordxField PreviousMembershipNo = new(2979, 18);
    public static readonly BordxField EmployeeNo = new(2997, 15);
    public static readonly BordxField FirstJoinedDate = new(3012, 8);
    public static readonly BordxField Department = new(3221, 30);
    public static readonly BordxField GroupAddress1 = new(3251, 30);
    public static readonly BordxField GroupAddress2 = new(3281, 30);
    public static readonly BordxField GroupAddress3 = new(3311, 30);
    public static readonly BordxField GroupAddress4 = new(3341, 30);
    public static readonly BordxField PreviousTakeOverPlan = new(3371, 4);
    public static readonly BordxField PremiumMode = new(3375, 1);
    public static readonly BordxField PolicySubNo1 = new(3376, 10);
    public static readonly BordxField PolicySubNo2 = new(3386, 10);
    public static readonly BordxField PolicyIssuedDate = new(3396, 8);
    public static readonly BordxField RiskNo = new(3404, 4);
    public static readonly BordxField TransactionNo = new(3408, 5);
    public static readonly BordxField PayNo = new(3413, 8);
    public static readonly BordxField PaySequenceNo = new(3421, 2);
    public static readonly BordxField ClientNo = new(3423, 8);
    public static readonly BordxField ClientId = new(3431, 2);
    public static readonly BordxField PreviousPlan = new(3433, 15);
    public static readonly BordxField RoomAndBoardAmount = new(3448, 8);
    public static readonly BordxField PreviousInsurer = new(3456, 60);
    public static readonly BordxField PaymentRemarks = new(3516, 60);
    public static readonly BordxField ClaimNonPayFrom = new(3576, 8);
    public static readonly BordxField ClaimNonPayTo = new(3584, 8);
    public static readonly BordxField TakeOver = new(3592, 1);
    public static readonly BordxField NewPlan = new(3592, 4);
    public static readonly BordxField NewPolicyEffectiveDate = new(3596, 8);
    public static readonly BordxField EndorsementReferenceNo = new(3604, 20);
    public static readonly BordxField ZurichTakBasicMco = new(3608, 12);
    public static readonly BordxField ApplicationName = new(3624, 60);
    /// <summary>Payor premium (net); MCO net in the ZURICH / TAK layout; the MCO refund on cancellations and the fee on reinstatements.</summary>
    public static readonly BordxField PayorAmount1 = new(3684, 12);
    /// <summary>Payor premium (gross); MCO gross in the ZURICH / TAK layout.</summary>
    public static readonly BordxField PayorAmount2 = new(3696, 12);
    public static readonly BordxField PayorMcoNet = new(3708, 12);
    public static readonly BordxField PayorMcoGross = new(3720, 12);
    public static readonly BordxField PolicyDiscountPercent = new(3732, 5);
    public static readonly BordxField RenewalDiscountPercent = new(3737, 5);
    public static readonly BordxField OtherIcNumber = new(3742, 20);
    public static readonly BordxField CoPayRoomAndBoard = new(3762, 2);
    public static readonly BordxField LoadingPremium = new(3764, 12);
    public static readonly BordxField FamilyDiscountAmount = new(3776, 12);
    public static readonly BordxField RenewalDiscountAmount = new(3788, 12);
    public static readonly BordxField PolicyCondition = new(3800, 5);
    public static readonly BordxField BankAccountNo = new(3805, 25);
    public static readonly BordxField UnderwritingExcess = new(3830, 12);
    public static readonly BordxField GuaranteeRenewal = new(3842, 5);
    public static readonly BordxField AgentName = new(3845, 90);
    public static readonly BordxField GstAmount = new(3847, 12);
    public static readonly BordxField GstPercent = new(3859, 12);
    public static readonly BordxField AgentEmail = new(3949, 30);
    public static readonly BordxField AgentPhone = new(3979, 20);
    /// <summary>GST columns used for payor MX in the ZURICH / TAK layout.</summary>
    public static readonly BordxField GstAmountMx = new(3999, 12);
    public static readonly BordxField GstPercentMx = new(4011, 12);
    public static readonly BordxField ClinicalPlan = new(6708, 4);
    public static readonly BordxField ClinicalInsuredType = new(6712, 1);
    public static readonly BordxField ClinicalEffectiveDate = new(6713, 8);
    public static readonly BordxField ClinicalExpiryDate = new(6721, 8);
    public static readonly BordxField ClinicalWarranty = new(6729, 2500);
    public static readonly BordxField ClinicalCardType = new(9229, 20);

    public static BordxField PolicyNoFor(bool zurichTak) => zurichTak ? PolicyNoZurichTak : PolicyNo;
    public static BordxField InsuredTypeFor(bool zurichTak) => zurichTak ? InsuredTypeZurichTak : InsuredType;
    public static BordxField GroupCompanyFor(bool zurichTak) => zurichTak ? GroupCompanyZurichTak : GroupCompany;
}

/// <summary>Member line of an endorsement file (VerifyENDtMember).</summary>
public static class EndorsementLayout
{
    public static readonly BordxField EffectiveDate = new(2, 8);
    public static readonly BordxField DataType = new(10, 1);
    public static readonly BordxField MembershipNo = new(11, 18);
    public static readonly BordxField PolicyNo = new(29, 25);
    public static readonly BordxField Name = new(74, 60);
    public static readonly BordxField Address = new(134, 90);
    public static readonly BordxField IcNumber = new(300, 20);
    public static readonly BordxField DateOfBirth = new(320, 8);
    public static readonly BordxField PayorEffectiveDate = new(401, 8);
    public static readonly BordxField PayorExpiryDate = new(409, 8);
    public static readonly BordxField PlanCode = new(417, 4);
    public static readonly BordxField InsuredType = new(421, 1);
}

/// <summary>Column widths of the Generate ASCII file (PrincipalASCII / SupplementaryASCII).</summary>
public static class AsciiLayout
{
    public const int RecordType = 1, PolicyNo = 25, EmployeeNo = 15, Name = 60, IcNumber = 20, MembershipNo = 18, CoverId = 2, Date = 8,
        GroupCompany = 40, PlanCode = 4, TransactionNo = 5, PayNo = 8, PaySequenceNo = 2, ClientNo = 8, ClientId = 2, PolicySubNo = 10,
        Filler = 64, SupplementaryTrailerFiller = 6, TrailerCount = 10, TrailerFiller = 164;
}

/// <summary>First character of each line.</summary>
public static class RecordTypes
{
    public const char NewFileHeader = 'F';
    public const char EndorsementHeader = 'E';
    public const char Trailer = 'T';
    public const char Principal = 'P';
    public const char Reinstatement = 'R';
    public const char Cancellation = 'C';
}

/// <summary>Data type of a member line: P principal, R reinstatement, else the supplementary's relationship.</summary>
public static class DataTypes
{
    public const string Principal = "P";
    public const string Reinstatement = "R";
    public const string Son = "S";
    public const string Daughter = "D";
    public const string Husband = "H";
    public const string Wife = "W";
}

/// <summary>Header status letter (HeaderLayout.FileStatus).</summary>
public static class FileStatuses
{
    public const string New = "N";
    public const string Cancellation = "C";
    public const string Renewal = "R";
    public const string Reinstatement = "I";
}

/// <summary>Bordx type chosen on the screen (cboFileType).</summary>
public static class FileTypes
{
    public const string New = "N";
    public const string Endorsement = "E";
}

/// <summary>PAY.BordxFormatType values.</summary>
public static class BordxFormats
{
    public const string ZurichTak = "3";
    public const string Axa = "4";
    public const string AxaLife = "5";
    public const string ZurichMarker = "ZURICH";
    public const string TakMarkerPrefix = "TAK";
}

/// <summary>MBM.HSClnInd: hospital, clinical, or both (M when the clinical plan is a DC plan).</summary>
public static class HospitalClinicalIndicator
{
    public const string Hospital = "H";
    public const string Clinical = "C";
    public const string Both = "B";
    public const string BothDirectClaim = "M";
}

/// <summary>Payor codes the desktop treats specially.</summary>
public static class SpecialPayors
{
    /// <summary>Uses the alternative GST columns in the ZURICH / TAK layout.</summary>
    public const string Mx = "MX";
    /// <summary>Clinical-only principals of this payor still get policy-based membership numbers.</summary>
    public const string Xl = "XL";
}

/// <summary>PAY.PAYMBMRegStatus: P = membership numbers follow the policy number.</summary>
public static class PayorRegistration
{
    public const string ByPolicy = "P";
}

/// <summary>Verification limits of VerifyNewMember.</summary>
public static class BordxRules
{
    public const int MinimumCoverageDays = 30;
    public const int MaximumCoverageDays = 365;
    public const int PrincipalAgeWarning = 60;
    public const int ChildAgeWarning = 22;
    public const int SpouseAgeWarning = 60;
    public const long MaximumFileBytes = 200L * 1024 * 1024;
}

/// <summary>Values the upload writes.</summary>
public static class BordxValues
{
    public const string StagingKeyPrefix = "WEB-";
    public const string VoidInvoice = "VOID";
    public const string FeeForServiceInvoice = "FFS";
    public const string ExpiredNo = "N";
}
