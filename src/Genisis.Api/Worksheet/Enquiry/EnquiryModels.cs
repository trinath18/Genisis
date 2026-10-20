namespace Genisis.Api.Worksheet.Enquiry;

/// <summary>One case of the list the Search button opens (frmCASList).</summary>
public record EnquiryCaseRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>The patient.</summary>
    public string? PatientName { get; set; }

    /// <summary>Hospital name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Admitted { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Discharged { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Case status code (O open, C closed, R repudiated).</summary>
    public string? CaseStatus { get; set; }

    /// <summary>Claimability code.</summary>
    public string? Claimability { get; set; }

    /// <summary>How many worksheet versions the case has.</summary>
    public int Versions { get; set; }
}

/// <summary>One worksheet version of a case.</summary>
public record EnquiryVersion
{
    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Claim status (C closed, O open, R rejected).</summary>
    public string? ClaimStatus { get; set; }

    /// <summary>Worksheet status code (AA, RR, CC ...).</summary>
    public string? WsStatus { get; set; }

    /// <summary>True when the version is in a bordx.</summary>
    public bool Bordxed { get; set; }
}

/// <summary>The case, member, policy and worksheet header of one version (what cmdDisplay_Click shows above the tabs).</summary>
public record EnquiryHeader
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version the header was read for.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Covered-person ID of the patient (00 = the principal).</summary>
    public string? PatientCovId { get; set; }

    /// <summary>Member (principal) name.</summary>
    public string? MemberName { get; set; }

    /// <summary>The patient: the principal for cover ID 00, otherwise the covered person.</summary>
    public string? PatientName { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Payor company name.</summary>
    public string? PayorName { get; set; }

    /// <summary>Plan code: the covered person's own plan when the patient is a dependant and has one, otherwise the member's.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Insured type code.</summary>
    public string? InsCode { get; set; }

    /// <summary>Health type code.</summary>
    public string? HltCode { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Payor effective date.</summary>
    public DateTime? PayorEffDate { get; set; }

    /// <summary>Payor expiry date.</summary>
    public DateTime? PayorExpDate { get; set; }

    /// <summary>Policy effective date.</summary>
    public DateTime? PolicyEffDate { get; set; }

    /// <summary>Policy status code.</summary>
    public string? PolicyStatusCode { get; set; }

    /// <summary>Policy status description.</summary>
    public string? PolicyStatus { get; set; }

    /// <summary>Expiry status.</summary>
    public string? ExpiredStatus { get; set; }

    /// <summary>Case status code.</summary>
    public string? CaseStatus { get; set; }

    /// <summary>Claimability code.</summary>
    public string? Claimability { get; set; }

    /// <summary>Pay type code.</summary>
    public string? PayType { get; set; }

    /// <summary>Hospital code.</summary>
    public string? HosCode { get; set; }

    /// <summary>Hospital name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Admitted { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Discharged { get; set; }

    /// <summary>Claim status code.</summary>
    public string? ClaimStatus { get; set; }

    /// <summary>Worksheet status code.</summary>
    public string? WsStatus { get; set; }

    /// <summary>Date the worksheet version was opened.</summary>
    public DateTime? WorksheetOpened { get; set; }

    /// <summary>True when the version is in a bordx.</summary>
    public bool Bordxed { get; set; }

    /// <summary>Bordx reference number.</summary>
    public string? BordxRefNo { get; set; }

    /// <summary>Ex-gratia flag (Y / N).</summary>
    public string? ExGratia { get; set; }

    /// <summary>Bordx indicator (Y / N).</summary>
    public string? BordxInd { get; set; }

    /// <summary>Linked case number.</summary>
    public string? LinkCase { get; set; }

    /// <summary>Linked claim version.</summary>
    public int? LinkVersion { get; set; }

    /// <summary>N when a guarantee letter is not allowed for the member.</summary>
    public string? GlIssuance { get; set; }

    /// <summary>Top-up member number.</summary>
    public string? TopupNumber { get; set; }

    /// <summary>Top-up indicator.</summary>
    public string? TopupInd { get; set; }

    /// <summary>Bal Lmt: the live available limit (without a top-up policy's balance).</summary>
    public decimal? AvailableLimit { get; set; }

    /// <summary>Lifetime Lmt: the live lifetime limit of a plan with lifetime guarantee renewal.</summary>
    public decimal? LifetimeLimit { get; set; }

    /// <summary>T/O Lmt: take-over limit.</summary>
    public decimal? TakeOverLimit { get; set; }

    /// <summary>Total limit exceeded, as saved with the worksheet.</summary>
    public decimal? LimitExceeded { get; set; }

    /// <summary>Balance limit before this worksheet.</summary>
    public decimal? PreviousBalance { get; set; }

    /// <summary>Balance limit after this worksheet.</summary>
    public decimal? BalanceAfterClaim { get; set; }

    /// <summary>Lifetime balance saved with the worksheet.</summary>
    public decimal? LifetimeBalance { get; set; }

    /// <summary>Total approved.</summary>
    public decimal? TotalApproved { get; set; }

    /// <summary>Final approved amount.</summary>
    public decimal? FinalApprove { get; set; }

    /// <summary>Who collects the limit exceeded: P payor, H hospital, M member.</summary>
    public string? LimitCollectedBy { get; set; }

    /// <summary>Who collects the excess: P payor, H hospital, M member.</summary>
    public string? ExcessCollectedBy { get; set; }

    /// <summary>True when the worksheet is marked non-payment.</summary>
    public bool NonPayment { get; set; }

    /// <summary>Ex-gratia (not payable) amount.</summary>
    public decimal? ExGratiaAmount { get; set; }

    /// <summary>Processing fee (case management fee).</summary>
    public decimal? ProcessingFee { get; set; }

    /// <summary>Major medical fee.</summary>
    public decimal? MajorMedicalFee { get; set; }

    /// <summary>Main / top-up policy amount.</summary>
    public decimal? MainTopupAmount { get; set; }

    /// <summary>Amount approved under the main policy.</summary>
    public decimal? CompanyApproved { get; set; }

    /// <summary>Amount approved under the top-up policy.</summary>
    public decimal? TopupApproved { get; set; }

    /// <summary>Top-up balance before this worksheet.</summary>
    public decimal? TopupPreviousBalance { get; set; }

    /// <summary>Underwriting excess.</summary>
    public decimal? UnderwritingExcess { get; set; }

    /// <summary>Debit / credit note reference.</summary>
    public string? DebitCreditRefNo { get; set; }

    /// <summary>Debit / credit note date.</summary>
    public DateTime? DebitCreditDate { get; set; }

    /// <summary>Hospital debit / credit note reference.</summary>
    public string? HosDebitCreditRefNo { get; set; }

    /// <summary>Hospital debit / credit note amount.</summary>
    public decimal? HosDebitCreditAmount { get; set; }

    /// <summary>Date of the last document.</summary>
    public DateTime? LastDocumentDate { get; set; }

    /// <summary>Pay to.</summary>
    public string? PayTo { get; set; }

    /// <summary>Bank.</summary>
    public string? Bank { get; set; }

    /// <summary>Bank account number.</summary>
    public string? AccountNo { get; set; }

    /// <summary>General practitioner name.</summary>
    public string? GpName { get; set; }

    /// <summary>Claimability in words.</summary>
    public string? ClaimabilityText { get; set; }

    /// <summary>Case status in words.</summary>
    public string? CaseStatusText { get; set; }

    /// <summary>Worksheet status in words.</summary>
    public string? WorksheetStatusText { get; set; }
}

/// <summary>One diagnosis line (up to three first and three final).</summary>
public record EnquiryDiagnosis
{
    /// <summary>First or Final.</summary>
    public string Source { get; set; } = "";

    /// <summary>Line number, 1 to 3.</summary>
    public int Slot { get; set; }

    /// <summary>Diagnosis.</summary>
    public string? Diagnosis { get; set; }

    /// <summary>Date on-set.</summary>
    public string? OnsetDate { get; set; }

    /// <summary>ICD code.</summary>
    public string? IcdCode { get; set; }
}

/// <summary>One invoice of the worksheet (a row of the Invoices list).</summary>
public record EnquiryInvoice
{
    /// <summary>Invoice index.</summary>
    public decimal InvIndex { get; set; }

    /// <summary>True when the invoice is in a bordx.</summary>
    public bool Bordxed { get; set; }

    /// <summary>True when the invoice is on a worksheet.</summary>
    public bool WsDone { get; set; }

    /// <summary>Worksheet version the invoice belongs to.</summary>
    public string? WsVersion { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvDate { get; set; }

    /// <summary>Invoice amount.</summary>
    public decimal? InvAmount { get; set; }

    /// <summary>H pay to hospital, M pay to member.</summary>
    public string? PayTo { get; set; }

    /// <summary>Payee.</summary>
    public string? Payee { get; set; }

    /// <summary>Claim type.</summary>
    public string? ClaimType { get; set; }

    /// <summary>Follow-up date.</summary>
    public DateTime? FollowUpDate { get; set; }

    /// <summary>Room and board days.</summary>
    public decimal? DailyRb { get; set; }

    /// <summary>Room and board rate.</summary>
    public decimal? RbRate { get; set; }

    /// <summary>ICU days.</summary>
    public decimal? IcuDays { get; set; }

    /// <summary>ICU rate.</summary>
    public decimal? IcuRate { get; set; }

    /// <summary>Hospital supplies and services (HSS).</summary>
    public decimal? HospitalServices { get; set; }

    /// <summary>Operating theatre (OT).</summary>
    public decimal? OperatingTheatre { get; set; }

    /// <summary>Pre-hospital service.</summary>
    public decimal? PreHospitalService { get; set; }

    /// <summary>In-hospital physician visit.</summary>
    public decimal? PhysicianVisit { get; set; }

    /// <summary>Post-hospital treatment.</summary>
    public decimal? PostHospitalTreatment { get; set; }

    /// <summary>Not covered: pre-hospital more than 60 days.</summary>
    public decimal? PreHospitalOver60 { get; set; }

    /// <summary>Not covered: pre-hospital GP fee.</summary>
    public decimal? PreHospitalGpFee { get; set; }

    /// <summary>Not covered: pre-hospital medicine.</summary>
    public decimal? PreHospitalMedicine { get; set; }

    /// <summary>Not covered: pre-hospital medical report.</summary>
    public decimal? PreHospitalMedicalReport { get; set; }

    /// <summary>Not covered: pre-hospital subsequent specialist fee.</summary>
    public decimal? PreHospitalSpecialistFee { get; set; }

    /// <summary>Not covered: post-hospital more than 31 days.</summary>
    public decimal? PostHospitalOver31Days { get; set; }

    /// <summary>Not covered: post-hospital different physician.</summary>
    public decimal? PostHospitalDifferentPhysician { get; set; }

    /// <summary>Not covered: others.</summary>
    public decimal? NotCoveredOthers { get; set; }
}

/// <summary>One benefit row of the worksheet grid.</summary>
public record EnquiryBenefit
{
    /// <summary>Grid row, 1 to 29.</summary>
    public int Slot { get; set; }

    /// <summary>Benefit sub-category code.</summary>
    public string? BenefitCode { get; set; }

    /// <summary>Benefit category.</summary>
    public string? Category { get; set; }

    /// <summary>Benefit name.</summary>
    public string Benefit { get; set; } = "";

    /// <summary>Days / months limit of the benefit.</summary>
    public string? Days { get; set; }

    /// <summary>Limit.</summary>
    public decimal? Limit { get; set; }

    /// <summary>Actual amount.</summary>
    public decimal? Actual { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal? Approved { get; set; }

    /// <summary>Payable to the hospital.</summary>
    public decimal? Hospital { get; set; }

    /// <summary>Due to / (from) the member.</summary>
    public decimal? Member { get; set; }
}

/// <summary>The saved summary figures of the worksheet, added up over the versions shown.</summary>
public record EnquirySummary
{
    /// <summary>Total actual.</summary>
    public decimal TotalActual { get; set; }

    /// <summary>Total approved.</summary>
    public decimal TotalApproved { get; set; }

    /// <summary>Co-payment R&amp;B.</summary>
    public decimal CoPayRb { get; set; }

    /// <summary>Total excess.</summary>
    public decimal TotalExcess { get; set; }

    /// <summary>Limit exceeded.</summary>
    public decimal LimitExceeded { get; set; }

    /// <summary>Ex-gratia (not payable).</summary>
    public decimal ExGratia { get; set; }

    /// <summary>BTBG.</summary>
    public decimal Btbg { get; set; }

    /// <summary>Processing fee.</summary>
    public decimal ProcessingFee { get; set; }

    /// <summary>Major medical fee.</summary>
    public decimal MajorMedicalFee { get; set; }

    /// <summary>Main / top-up policy.</summary>
    public decimal MainTopup { get; set; }

    /// <summary>Hospital / payor to collect.</summary>
    public decimal HospitalPayorToCollect { get; set; }

    /// <summary>Final total payable to the hospital.</summary>
    public decimal PayableToHospital { get; set; }

    /// <summary>Final total due to / (from) the member.</summary>
    public decimal PayableToMember { get; set; }

    /// <summary>Final approved amount.</summary>
    public decimal FinalApprove { get; set; }

    /// <summary>Bordereaux amount.</summary>
    public decimal BordxAmount { get; set; }
}

/// <summary>The column totals of the benefit rows.</summary>
public record EnquiryTotals
{
    /// <summary>Total actual.</summary>
    public decimal Actual { get; set; }

    /// <summary>Total approved.</summary>
    public decimal Approved { get; set; }

    /// <summary>Total payable to the hospital.</summary>
    public decimal Hospital { get; set; }

    /// <summary>Total due to / (from) the member.</summary>
    public decimal Member { get; set; }
}

/// <summary>Remarks, payee address and payment details of one version (ClaimOthers).</summary>
public record EnquiryOthers
{
    /// <summary>Worksheet remarks.</summary>
    public string? WorksheetRemarks { get; set; }

    /// <summary>Credit / debit note remarks.</summary>
    public string? CreditDebitRemarks { get; set; }

    /// <summary>Address line 1.</summary>
    public string? Address1 { get; set; }

    /// <summary>Address line 2.</summary>
    public string? Address2 { get; set; }

    /// <summary>Address line 3.</summary>
    public string? Address3 { get; set; }

    /// <summary>Note.</summary>
    public string? Note { get; set; }

    /// <summary>Post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>City.</summary>
    public string? City { get; set; }

    /// <summary>State.</summary>
    public string? State { get; set; }

    /// <summary>Attention.</summary>
    public string? Attention { get; set; }

    /// <summary>Payment mode.</summary>
    public string? PaymentMode { get; set; }
}

/// <summary>One guarantee letter of the case (the LG Listing tab).</summary>
public record EnquiryLgRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Guarantee letter number.</summary>
    public int LgNo { get; set; }

    /// <summary>Guarantee letter date.</summary>
    public DateTime? LgDate { get; set; }

    /// <summary>Guarantee letter amount.</summary>
    public decimal? LgAmt { get; set; }

    /// <summary>Issued by.</summary>
    public string? LgIssuedBy { get; set; }

    /// <summary>Remark.</summary>
    public string? LgRemark { get; set; }

    /// <summary>B for a bordx letter, otherwise a normal letter.</summary>
    public string? LgStatus { get; set; }

    /// <summary>Time entered by the user.</summary>
    public string? LgUserTime { get; set; }

    /// <summary>Time recorded by the system.</summary>
    public string? LgSystemTime { get; set; }

    /// <summary>The reference the desktop listed: B or L, the case number, a dash and the number.</summary>
    public string? Reference { get; set; }
}

/// <summary>The worksheet enquiry of one case and version: everything cmdDisplay_Click puts on the screen.</summary>
/// <param name="Header">The case, member, policy and worksheet header.</param>
/// <param name="Versions">The versions the case has, in order.</param>
/// <param name="SelectedVersion">The version selection that was shown: n, Mn or Master.</param>
/// <param name="Diagnoses">The first and final diagnoses.</param>
/// <param name="Invoices">The invoices of the selection.</param>
/// <param name="Benefits">The benefit rows of the worksheet grid.</param>
/// <param name="Totals">The column totals of the benefit rows.</param>
/// <param name="Summary">The saved summary figures under the grid.</param>
/// <param name="Others">Remarks, payee address and payment details.</param>
/// <param name="LgListing">The case's guarantee letters.</param>
/// <param name="Notices">Messages the desktop showed after loading (for example GL not allowed).</param>
public record EnquiryResult(EnquiryHeader Header, List<EnquiryVersion> Versions, string SelectedVersion, List<EnquiryDiagnosis> Diagnoses, List<EnquiryInvoice> Invoices,
    List<EnquiryBenefit> Benefits, EnquiryTotals Totals, EnquirySummary Summary, EnquiryOthers? Others, List<EnquiryLgRow> LgListing, List<string> Notices);
