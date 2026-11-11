using Genisis.Api.Data;
using Genisis.Api.Membership;
using Genisis.Api.Registration;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Adjustment;

/// <summary>Values of one save, fixed before anything is written.</summary>
internal sealed record AdjustmentSave(string Type, DateTime EndorsementBordxDate, DateTime EndorsementEffectiveDate, DateTime Today, DateTime Now, string UserCode);

/// <summary>
/// Writes of one Adjustment save (the branches of frmNewMBMAdjustment.CmdSave_Click), inside the caller's transaction.
/// </summary>
internal sealed class AdjustmentWriter(SqlConnection conn, SqlTransaction tx, LoadedMembership current, AdjustmentRequest request, AdjustmentSave save)
{
    private const string FeeForServiceInvoice = "FFS";
    private const string SuppliedByPlan = "S";

    public string Number { get; } = (current.Member.MembershipNo ?? "").Trim();
    public List<string> Warnings { get; } = [];
    private AdjustmentMember Stored => current.Member;

    private static string Code(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static string Text(string? value) => value?.Trim() ?? "";
    private static string? TextOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FirstLetter(string? value) => Code(value) is { Length: > 0 } code ? code[..1] : "";

    /// <summary>Desktop Write2MBMnHistory: membership status, MBMTwo endorsement fields, one amend history row, and for
    /// cancellation / suspension / deletion every covered person's status. Cancel Supplementary leaves the status alone.</summary>
    public async Task WriteHistoryAsync(string status, AdjustmentMember shown, DateTime? finalExpiry = null)
    {
        if (status != EndorsementTypes.CancelSupplementary)
            await conn.ProcExecAsync("genisis.Adjustment_SetStatus", new { number = Number, status, finalExpiry }, tx);
        await conn.ProcExecAsync("genisis.Adjustment_SetEndorsement", new
        {
            number = Number, type = save.Type, endtEff = save.EndorsementEffectiveDate, endtBordx = save.EndorsementBordxDate,
            cancel = status == MemberStatus.Cancelled,
        }, tx);
        await InsertHistoryAsync(shown.PolicyNo, shown.IcBcPp, shown.Name, shown.PayorExpiryDate, shown.PlanCode);
        if (EndorsementTypes.EndsMembership(save.Type))
            await conn.ProcExecAsync("genisis.Adjustment_SetCoveredStatus", new { number = Number, status }, tx);
    }

    private Task InsertHistoryAsync(string? policy, string? ic, string? name, DateTime? expiry, string? plan, string? coverId = null) =>
        conn.ProcExecAsync("genisis.Adjustment_InsertAmendHistory", new
        {
            number = Number, policy = Text(policy), ic = Text(ic), name = Text(name), expiry = expiry?.Date, plan = Text(plan), type = save.Type,
            endtBordx = save.EndorsementBordxDate, endtEff = save.EndorsementEffectiveDate, user = save.UserCode, today = save.Today, batch = Stored.BatchNo,
            coverId,
        }, tx);

    public Task SetCoveredAdjustmentAsync(string status, string? coverId, DateTime? cancelDate) =>
        conn.ProcExecAsync("genisis.Adjustment_SetCoveredAdjustment", new { number = Number, status, coverId, cancelDate }, tx);

    public Task UpdateMcoPendingAsync() =>
        conn.ProcExecAsync("genisis.Adjustment_UpdateMcoPending", new { number = Number, bordx = request.Member.BordxDate!.Value.Date }, tx);

    private async Task<bool> RoundUpAsync(string? plan)
    {
        var settings = await conn.ProcFirstOrDefaultAsync<PlanSettings>("genisis.Adjustment_PlanSettings", new { plan = Code(plan) }, tx);
        return YesNo.IsNotNo(settings?.RoundUpStatus);
    }

    /// <summary>Desktop SaveMBMRefund: the unused share of the MCO fee and premium from the endorsement effective date.</summary>
    private async Task RefundAsync(string coverId, DateTime effective, DateTime expiry, decimal premium, decimal mco, bool roundUp, AdjustmentMember shown)
    {
        decimal Round(decimal value) => roundUp ? FeeRules.VbRound(value) : value;
        var cancel = save.EndorsementEffectiveDate;
        var mcoRefund = Round(LegacyRules.Money(FeeRules.McoRefund(expiry, cancel, effective, mco)));
        var premiumReduction = Round(LegacyRules.Money(FeeRules.PremiumReduction(expiry, cancel, effective, premium)));
        await conn.ProcExecAsync("genisis.Adjustment_InsertRefund", new
        {
            number = Number, coverId, premiumEffective = Round(LegacyRules.Money(premium - premiumReduction)), premiumReduction,
            mcoEffective = Round(LegacyRules.Money(mco - mcoRefund)), mcoRefund, pay = Code(shown.PayorCode),
            bordx = shown.BordxDate?.Date, batch = Stored.BatchNo, hlt = FirstLetter(shown.HealthCode),
        }, tx);
    }

    /// <summary>Desktop MBMMCORefundCal (cancellation and plan change): principal refund, then supplementary refunds for family plans.</summary>
    public async Task PrincipalRefundAsync(AdjustmentMember shown)
    {
        if (shown.PayorEffectiveDate is not { } effective || shown.PayorExpiryDate is not { } expiry) return;
        var roundUp = await RoundUpAsync(shown.PlanCode);
        await RefundAsync(CoverIds.Principal, effective, expiry, FeeRules.VbRound(shown.Premium ?? 0), FeeRules.VbRound(shown.McoFee ?? 0), roundUp, shown);
        if (!InsuredTypes.IsFamily(shown.InsuredType)) return;

        var rates = await conn.ProcFirstOrDefaultAsync<McoRates>("genisis.Registration_Mco",
            new { ins = Code(shown.InsuredType), hlt = FirstLetter(shown.HealthCode), plan = Code(shown.PlanCode), eff = effective }, tx);
        var status = Code(rates?.SuppMcoStatus) is { Length: > 0 } s ? s : SupplementaryCharge.PrincipalOnly;
        var chargeFrom = AdjustmentRules.CoverId(TextOrNull(rates?.CovIDChrg) ?? "");
        foreach (var person in current.Covered
            .Where(p => Code(p.Status) is not (MemberStatus.Cancelled or EndorsementTypes.CancelSupplementary))
            .OrderBy(p => AdjustmentRules.CoverId(p.CoverId), StringComparer.Ordinal))
        {
            var id = AdjustmentRules.CoverId(person.CoverId);
            var personEffective = person.EffectiveDate ?? effective;
            var personExpiry = person.ExpiryDate ?? expiry;
            var mcoCharged = RegistrationService.Charged(status, id, chargeFrom);
            var premium = person.Premium ?? 0;
            if (mcoCharged || premium != 0)
                await RefundAsync(id, personEffective, personExpiry, premium, mcoCharged ? FeeRules.VbRound(person.Mco ?? 0) : 0, roundUp, shown);
        }
    }

    /// <summary>Desktop "K" branch: one history row per cancelled supplementary, its refund, then status C with the cancel date.</summary>
    public async Task CancelSupplementaryAsync()
    {
        await conn.ProcExecAsync("genisis.Adjustment_SetEndorsement", new
        {
            number = Number, type = save.Type, endtEff = save.EndorsementEffectiveDate, endtBordx = save.EndorsementBordxDate, cancel = false,
        }, tx);
        var chosen = request.Covered.Where(p => CoveredActions.Is(p.Action, CoveredActions.Cancel)).Select(p => AdjustmentRules.CoverId(p.CoverId)).ToHashSet();
        var roundUp = await RoundUpAsync(Stored.PlanCode);
        foreach (var person in current.Covered.Where(p => chosen.Contains(AdjustmentRules.CoverId(p.CoverId))))
        {
            var id = AdjustmentRules.CoverId(person.CoverId);
            var effective = person.EffectiveDate ?? Stored.PayorEffectiveDate;
            var expiry = person.ExpiryDate ?? Stored.PayorExpiryDate;
            var plan = TextOrNull(person.PlanCode) ?? Stored.PlanCode;
            await InsertHistoryAsync(Stored.PolicyNo, person.IcBcPp, person.Name, expiry, plan, id);
            if (effective is { } from && expiry is { } to)
            {
                var rates = await conn.ProcFirstOrDefaultAsync<McoRates>("genisis.Registration_Mco",
                    new { ins = Code(Stored.InsuredType), hlt = FirstLetter(Stored.HealthCode), plan = Code(plan), eff = from }, tx);
                var status = Code(rates?.SuppMcoStatus) is { Length: > 0 } s ? s : SupplementaryCharge.PrincipalOnly;
                var mcoCharged = (status == SupplementaryCharge.FirstSupplementaryOnly && id == CoverIds.FirstSupplementary)
                    || status is SupplementaryCharge.Everyone or SupplementaryCharge.FromCoverId;
                var premium = person.Premium ?? 0;
                if (mcoCharged || premium != 0)
                    await RefundAsync(id, from, to, premium, mcoCharged ? person.Mco ?? 0 : 0, roundUp, Stored);
            }
            await SetCoveredAdjustmentAsync(MemberStatus.Cancelled, id, save.EndorsementEffectiveDate);
        }
    }

    /// <summary>Desktop DeleteSupplementary; refused while the supplementary has an outstanding HIS or clinical case.</summary>
    public async Task DeleteSupplementaryAsync()
    {
        var chosen = request.Covered.Where(p => CoveredActions.Is(p.Action, CoveredActions.Remove)).Select(p => AdjustmentRules.CoverId(p.CoverId)).ToList();
        var topupNumber = Code(Stored.GuaranteeRenewal).StartsWith(GuaranteeRenewal.LifetimeTracked, StringComparison.Ordinal) ? TextOrNull(Stored.TopupNumber) : null;
        foreach (var id in chosen)
        {
            var cases = await conn.ProcSingleAsync<OpenCases>("genisis.Adjustment_OpenCases", new { number = Number, coverId = id }, tx);
            if (cases.HisCase) throw new AdjustmentException($"Cover {id}: There is at least an outstanding HIS case under this Supplementary!");
            if (cases.ClinicalCase) throw new AdjustmentException($"Cover {id}: There is at least an outstanding Clinical case under this Supplementary!");
            await conn.ProcExecAsync("genisis.Adjustment_DeleteCovered", new { number = Number, coverId = id, topupNo = topupNumber }, tx);
        }
    }

    /// <summary>Desktop SavePrincipalAdjustment.</summary>
    public async Task SavePrincipalAsync(Func<RecalculateRequest, Task<Recalculation>> recalculate)
    {
        var member = request.Member;
        var name = Text(member.Name).ToUpperInvariant();
        var dateOfBirth = member.DateOfBirth!.Value.Date;

        // Renewal numbers (with *) keep a renewal key of payor*insured*dob*name; a name or birth date change moves it.
        if (Number.Contains('*') && Code(Stored.PlanCode) != "" && Code(Stored.InsuredType) != "")
        {
            var previousKey = AdjustmentRules.RenewalKey(Stored.PayorCode, Stored.InsuredType, Stored.DateOfBirth, Stored.Name);
            var newKey = AdjustmentRules.RenewalKey(Stored.PayorCode, Stored.InsuredType, dateOfBirth, name);
            if (previousKey != newKey)
                await conn.ProcExecAsync("genisis.Adjustment_RenameRenewals", new { baseNumber = Number.Split('*')[0], previousKey, newKey, name, dob = dateOfBirth }, tx);
        }

        var ageCode = Text(Stored.AgeCode);
        var feesChanged = member.DateOfBirth != Stored.DateOfBirth || member.PayorEffectiveDate != Stored.PayorEffectiveDate
            || Code(member.PlanCode) != Code(Stored.PlanCode);
        if (feesChanged && member.PayorEffectiveDate is { } effective)
            ageCode = (await recalculate(new RecalculateRequest
            {
                PlanCode = member.PlanCode, PayorEffectiveDate = effective, PayorExpiryDate = member.PayorExpiryDate, DateOfBirth = dateOfBirth, Sex = member.Sex,
            })).AgeCode;

        await conn.ProcExecAsync("genisis.Adjustment_SavePrincipal", new
        {
            number = Number, name, policy = Code(member.PolicyNo), sex = Code(member.Sex), add1 = Code(member.Address1), add2 = Code(member.Address2),
            add3 = Code(member.Address3), city = Code(member.City), state = Code(member.State), postCode = Text(member.PostCode), ic = Code(member.IcBcPp),
            dob = dateOfBirth, race = Code(member.RaceCode),
            ageCode = ageCode.Length > AdjustmentLimits.AgeCodeLength ? ageCode[..AdjustmentLimits.AgeCodeLength] : ageCode,
            eff = member.PayorEffectiveDate?.Date, exp = member.PayorExpiryDate?.Date, bordx = member.BordxDate!.Value.Date,
            takeOver = FirstLetter(member.TakeOver), renewal = FirstLetter(member.Renewal), dataStatus = FirstLetter(member.DataStatus),
            status = save.Type == EndorsementTypes.PlanChange ? AdjustmentStatuses.PlanChanged : MemberStatus.Active, plan = Code(member.PlanCode),
            availableLimit = request.ApplyAnnualLimit ? request.AnnualLimit : null,
        }, tx);

        await conn.ProcExecAsync("genisis.Adjustment_SaveMbmTwo", new
        {
            number = Number, salutation = Code(member.Salutation), email = Code(member.Email), lgn = FirstLetter(member.LgnCode), type = save.Type,
            nat = TextOrNull(member.NationalityCode)?.ToUpperInvariant(), telHome = Text(member.TelHome), telMobile = Text(member.TelMobile),
            telOffice = Text(member.TelOffice), now = save.Now, user = save.UserCode, mgmtNotes = Text(member.MgmtNotes),
        }, tx);

        // Combo values may arrive as "code - description"; the tables store the code.
        static string? ComboCode(string? value) => TextOrNull(value) is { } text ? (text.Contains('-') ? text[..text.IndexOf('-')].Trim() : text) : null;
        await conn.ProcExecAsync("genisis.Adjustment_SaveOthers", new
        {
            number = Number, department = Text(member.Department), groupCompany = Text(member.GroupCompany), polSubNo = Text(member.PolicySubNo),
            labelStatus = member.LabelStatus ? YesNo.Yes : null, employeeNo = Text(member.EmployeeNo), agentCode = Text(member.AgentCode),
            installment = FirstLetter(member.InstallmentMode), mco = member.McoFee, branch = Text(member.Branch).ToUpperInvariant(),
            basicPremium = member.BasicPremium, premium = member.Premium, joined = member.DateJoined?.Date, clmNonPayFrom = member.ClaimNonPayFrom?.Date,
            clmNonPayTo = member.ClaimNonPayTo?.Date, newPlan = Code(member.NewPlanCode), newPolicyEff = member.NewPolicyEffectiveDate?.Date,
            policyDisc = member.PolicyDisc ?? 0, renewalDisc = member.RenewalDisc ?? 0, polCondition = FirstLetter(member.PolicyCondition),
            undExcess = member.UnderwritingExcess ?? 0, endtRefNo = Text(member.EndtRefNo), costCenter = Text(member.CostCenter),
            position = Text(member.Position), groupCategory = Text(member.GroupCategory), division = Text(member.Division),
            subDivision = Text(member.SubDivision), subsidiary = Text(member.Subsidiary), nextDue = member.NextDueDate?.Date,
            allergic = Text(member.Allergic), remarks = Text(member.Remarks), weight = Text(member.Weight), height = Text(member.Height),
            workNature = ComboCode(member.WorkNature) ?? "", blood = Code(member.Blood), smoking = FirstLetter(member.Smoking),
            alcohol = FirstLetter(member.Alcohol), marital = TextOrNull(member.MaritalStatus) is { } marital ? FirstLetter(marital) : null,
            prevPlan = Text(member.PreviousPlanCode), rbAmount = member.RbAmount ?? 0, prevInsurer = Text(member.PreviousInsurer),
            occupation = ComboCode(member.Occupation) ?? "", coPayRb = Text(member.CoPayRb), genWp = member.GeneralWaitingDays,
            genWpEnd = member.GeneralWaitingEnd?.Date, preExWp = member.PreExistingWaitingDays, preExWpEnd = member.PreExistingWaitingEnd?.Date,
            speIllWp = member.SpecificIllnessWaitingDays, speIllWpEnd = member.SpecificIllnessWaitingEnd?.Date, staffDisc = FirstLetter(member.StaffDiscount),
            btbg = TextOrNull(member.Btbg) is { } backToBack ? FirstLetter(backToBack) : null, payorPremGross = member.PayorPremiumGross,
            payorPremNet = member.PayorPremiumNet ?? 0, payorMcoGross = member.PayorMcoGross ?? 0, payorMcoNet = member.PayorMcoNet ?? 0,
            familyDiscAmount = member.FamilyDiscountAmount ?? 0, renewalDiscAmount = member.RenewalDiscountAmount ?? 0,
            loadingPremium = member.LoadingPremium ?? 0, exclusion = Text(member.Exclusion), bankAccountNo = Text(member.BankAccountNo),
        }, tx);
    }

    /// <summary>Desktop SaveSupplementaryAdjustment: every existing covered person is written back; a plan change marks them P.</summary>
    public async Task SaveCoveredAsync()
    {
        var sent = request.Covered.Where(p => !CoveredActions.IsChange(p.Action)).ToDictionary(p => AdjustmentRules.CoverId(p.CoverId));
        foreach (var stored in current.Covered)
        {
            var person = sent.GetValueOrDefault(AdjustmentRules.CoverId(stored.CoverId)) ?? stored;
            await conn.ProcExecAsync("genisis.Adjustment_UpdateCovered", new
            {
                number = Number, coverId = Text(stored.CoverId), name = Code(person.Name), ic = Code(person.IcBcPp),
                dob = person.DateOfBirth?.Date ?? stored.DateOfBirth, sex = Code(person.Sex), adultChild = Code(person.AdultChild),
                rel = Code(person.Relationship), eff = person.EffectiveDate?.Date, exp = person.ExpiryDate?.Date, premium = person.Premium,
                plan = TextOrNull(person.PlanCode)?.ToUpperInvariant(),
                status = save.Type == EndorsementTypes.PlanChange
                    && Code(stored.Status) is not (EndorsementTypes.CancelSupplementary or MemberStatus.Cancelled)
                    ? AdjustmentStatuses.PlanChanged : null,
                salutation = Code(person.Salutation), allergic = Text(person.Allergic), exclusion = Text(person.Exclusion),
                clmNonPayFrom = person.ClaimNonPayFrom?.Date, clmNonPayTo = person.ClaimNonPayTo?.Date, policyDisc = person.PolicyDisc ?? 0,
                renewalDisc = person.RenewalDisc ?? 0, prevPlan = Text(person.PreviousPlanCode), rbAmount = person.RbAmount ?? 0,
                payRemarks = Text(person.PayorRemarks), prevInsurer = Text(person.PreviousInsurer), genWp = person.GeneralWaitingDays,
                genWpEnd = person.GeneralWaitingEnd?.Date, preExWp = person.PreExistingWaitingDays, preExWpEnd = person.PreExistingWaitingEnd?.Date,
                speIllWp = person.SpecificIllnessWaitingDays, speIllWpEnd = person.SpecificIllnessWaitingEnd?.Date,
                staffDisc = TextOrNull(person.StaffDiscount) is { } discount ? FirstLetter(discount) : null, payorPremGross = person.PayorPremiumGross,
                payorPremNet = person.PayorPremiumNet ?? 0, payorMcoGross = person.PayorMcoGross ?? 0, payorMcoNet = person.PayorMcoNet ?? 0,
                familyDiscAmount = person.FamilyDiscountAmount ?? 0, renewalDiscAmount = person.RenewalDiscountAmount ?? 0,
                loadingPremium = person.LoadingPremium ?? 0, undExcess = person.UnderwritingExcess ?? 0, payNo = Text(person.PayorMemberNo),
                paySeqNo = Text(person.PayorSeqNo), clientNo = Text(person.ClientNo), clientId = Text(person.ClientId),
            }, tx);
        }
    }

    /// <summary>Desktop AddSupplementaryAdjustment (and SaveCISCovPerson for clinical members).</summary>
    public async Task AddSupplementaryAsync()
    {
        var agePlan = Code(Stored.PlanCode);
        var ageInsured = Code(Stored.InsuredType);
        if (agePlan == "") { agePlan = Code(current.Clinic?.PlanCode); ageInsured = Code(current.Clinic?.InsuredType); }
        var health = Code(Stored.HealthCode);
        var payor = Code(Stored.PayorCode);
        var principalEffective = Stored.PayorEffectiveDate ?? current.Clinic?.EffectiveDate ?? save.Today;
        var principalExpiry = Stored.PayorExpiryDate ?? current.Clinic?.ExpiryDate ?? save.Today;
        var hasHisPlan = Code(Stored.PlanCode) != "";
        var guarantee = Code(Stored.GuaranteeRenewal);
        var discount = await conn.ProcFirstOrDefaultAsync<PlanDiscount>("genisis.Adjustment_DiscPlan", new { plan = Code(Stored.PlanCode) }, tx);
        var bands = await conn.ProcQueryAsync<LegacyRules.AgeBand>("genisis.Registration_AgeBands", new { plan = agePlan }, tx);
        var nextCover = await conn.ProcSingleAsync<int>("genisis.Adjustment_NextCoverId", new { number = Number }, tx);

        foreach (var person in request.Covered.Where(p => CoveredActions.Is(p.Action, CoveredActions.Add)))
        {
            var coverId = CoverIds.Format(nextCover++);
            var dateOfBirth = person.DateOfBirth!.Value.Date;
            var name = Text(person.Name);
            var band = LegacyRules.PickAgeBand(bands, LegacyRules.Age(principalEffective, dateOfBirth), agePlan)
                ?? throw new AdjustmentException($"{name}: Age Band not found or Date out of Acceptable Range! Please verify your maintenance table!");
            var ageCode = band.AgeCode.Trim();
            var adultChild = AdultChild.ForAgeBand(ageCode);
            var plan = Code(TextOrNull(person.PlanCode) ?? Stored.PlanCode);
            var effective = (person.EffectiveDate ?? principalEffective).Date;
            var expiry = (person.ExpiryDate ?? principalExpiry).Date;
            decimal annual = 0, basicPremium = 0, premium = 0, basicMco = 0, mco = 0;

            if (plan != "" && Code(Stored.InsuredType) != "")
            {
                var settings = await conn.ProcFirstOrDefaultAsync<PlanSettings>("genisis.Adjustment_PlanSettings", new { plan }, tx);
                if (Stored.PayorEffectiveDate is not null && Stored.PayorExpiryDate is not null)
                {
                    var supplementaryExpiry = AdjustmentRules.SupplementaryExpiry(dateOfBirth, (int)(settings?.AgeLimit ?? 0), effective, expiry);
                    expiry = supplementaryExpiry.ExpiryDate!.Value.Date;
                    if (supplementaryExpiry.OverAge) Warnings.Add($"{name}: Supplementary age is above {supplementaryExpiry.AgeLimit}");
                }

                var supplementaryLimit = await conn.ProcFirstOrDefaultAsync<SupplementaryLimit>("genisis.Adjustment_SuppLimit", new { plan }, tx);
                var limitStatus = Code(supplementaryLimit?.SuppLimitStatus) is { Length: > 0 } status ? status : SupplementaryCharge.PrincipalOnly;
                var limitCharged = AdjustmentRules.SupplementaryCharged(limitStatus, coverId);
                if (limitCharged) annual = supplementaryLimit?.SuppLimitAmount ?? 0;
                if (guarantee != "" && YesNo.IsYes(settings?.LifetimeStatus))
                    annual = await LifetimeAsync(coverId, limitCharged, annual, guarantee.StartsWith(GuaranteeRenewal.LifetimeTracked, StringComparison.Ordinal));

                var mcoStatusRow = await conn.ProcFirstOrDefaultAsync<McoRates>("genisis.Registration_Mco",
                    new { ins = Code(Stored.InsuredType), hlt = health, plan, eff = principalEffective }, tx);
                var mcoStatus = Code(mcoStatusRow?.SuppMcoStatus) is { Length: > 0 } ms ? ms : SupplementaryCharge.PrincipalOnly;
                if (RegistrationService.Charged(mcoStatus, coverId, mcoStatusRow?.CovIDChrg))
                {
                    var amounts = await conn.ProcFirstOrDefaultAsync<McoRates>("genisis.Registration_Mco",
                        new { ins = Code(Stored.InsuredType), hlt = health, plan, eff = effective }, tx);
                    basicMco = LegacyRules.Money((adultChild == AdultChild.Child ? amounts?.MCOSuppChildAmt : amounts?.MCOSuppAdultAmt) ?? 0);
                    mco = FeeRules.VbRound(FeeRules.ProRateMoney(effective, expiry, basicMco));
                }

                var supplementaryPremium = await conn.ProcFirstOrDefaultAsync<SupplementaryPremium>("genisis.Adjustment_SuppPremium",
                    new { plan, ageCode, ins = FirstLetter(Stored.InsuredType) }, tx);
                if (supplementaryPremium is not null && RegistrationService.PremiumCharged(Code(supplementaryPremium.SuppPrmStatus), coverId))
                {
                    basicPremium = supplementaryPremium.SuppPrmAmt ?? 0;
                    var rate = await conn.ProcFirstOrDefaultAsync<PremiumRates>("genisis.Registration_Premium",
                        new { ins = FirstLetter(Stored.InsuredType), hlt = health, plan, ageCode, eff = effective, pay = payor }, tx);
                    premium = FeeRules.ProRateMoney(effective, expiry, LegacyRules.Money(rate?.SuppPrmAmt ?? 0));
                    if (discount is not null)
                    {
                        if (person.RenewalDisc is { } renewalDiscount) premium = premium * (100 - renewalDiscount) / 100;
                        if (person.PolicyDisc is { } policyDiscount) premium = premium * (100 - policyDiscount) / 100;
                    }
                }
            }

            await conn.ProcExecAsync("genisis.Adjustment_InsertCovered", new
            {
                number = Number, coverId, ageCode, status = Code(Stored.Status), bordx = save.EndorsementBordxDate, batch = Stored.BatchNo,
                name = name.ToUpperInvariant(), ic = Code(person.IcBcPp), dob = dateOfBirth, sex = Code(person.Sex), adultChild,
                rel = Code(person.Relationship), today = save.Today, plan, eff = hasHisPlan ? effective : (DateTime?)null,
                exp = hasHisPlan ? expiry : (DateTime?)null, annual, basicPremium, premium, basicMco, mco, salutation = Code(person.Salutation),
                allergic = Text(person.Allergic), exclusion = Text(person.Exclusion), payorPremNet = person.PayorPremiumNet ?? 0,
                payorMcoGross = person.PayorMcoGross ?? 0, payorMcoNet = person.PayorMcoNet ?? 0, loadingPremium = person.LoadingPremium ?? 0,
                familyDiscAmount = person.FamilyDiscountAmount ?? 0, renewalDiscAmount = person.RenewalDiscountAmount ?? 0,
                undExcess = person.UnderwritingExcess ?? 0, renewalDisc = person.RenewalDisc ?? 0, policyDisc = person.PolicyDisc ?? 0,
                clmNonPayFrom = person.ClaimNonPayFrom?.Date, clmNonPayTo = person.ClaimNonPayTo?.Date,
            }, tx);

            if (TextOrNull(current.Clinic?.PlanCode) is not null && TextOrNull(current.Clinic?.InsuredType) is not null)
                await AddClinicalAsync(coverId, dateOfBirth, agePlan);
            _ = ageInsured;
        }
    }

    /// <summary>Lifetime part of AddSupplementaryAdjustment (GetBenefitLimit, PrincipalLifeTimeChecking, SaveMBMLifeTimeLmt).
    /// Returns the annual limit after it is capped by the remaining lifetime balance.</summary>
    private async Task<decimal> LifetimeAsync(string coverId, bool limitCharged, decimal annual, bool lifetimeTracked)
    {
        var insured = Code(Stored.InsuredType);
        var plan = Code(Stored.PlanCode);
        var health = Code(Stored.HealthCode);
        var effective = Stored.PayorEffectiveDate!.Value.Date;
        var benefit = await conn.ProcFirstOrDefaultAsync<BenefitLimit>("genisis.Registration_BenefitLimit", new { ins = insured, plan, hlt = health, eff = effective }, tx);
        if (benefit is null) Warnings.Add("Benefit Limit Information Is Not Correct! Please check you Maintenance Table");

        var previousNumber = Number;
        var previous = await conn.ProcFirstOrDefaultAsync<PreviousLifetime>("genisis.Registration_PreviousLifetime",
            new { policy = Text(Stored.PolicyNo), pay = Code(Stored.PayorCode), ins = insured }, tx);
        if (previous is not null) previousNumber = Text(previous.MBMTopupNumber);
        var writeLifetime = lifetimeTracked && await conn.ProcFirstOrDefaultAsync<AmountRow>("genisis.Registration_LifetimeBalance",
            new { topupNo = previousNumber, coverId = CoverIds.Principal }, tx) is not null;

        if (AdjustmentRules.SupplementaryCharged(benefit?.SuppBnfStatus, coverId))
            await conn.ProcExecAsync("genisis.Registration_InsertBenefitLimit", new
            {
                number = Number, coverId, kidney = benefit?.KidDialysisLmt ?? 0, cancer = benefit?.CancerLmt ?? 0, homeNursing = benefit?.HomeNursLmt ?? 0,
            }, tx);

        if (!limitCharged) return annual;
        var nextLimit = annual;
        var carried = await conn.ProcFirstOrDefaultAsync<AmountRow>("genisis.Registration_CarriedLimit", new { previousNo = previousNumber, coverId }, tx);
        if (carried is not null && !lifetimeTracked) nextLimit = carried.Value ?? 0;
        if (!writeLifetime) return annual;

        var existing = await conn.ProcFirstOrDefaultAsync<AmountRow>("genisis.Registration_LifetimeBalance", new { topupNo = previousNumber, coverId }, tx);
        if (existing is not null) return Math.Min(annual, existing.Value ?? 0);
        var limit = await conn.ProcFirstOrDefaultAsync<AnnualLimitRates>("genisis.Registration_AnnualLimit", new { ins = insured, plan, hlt = health, eff = effective }, tx);
        await conn.ProcExecAsync("genisis.Registration_InsertLifetime",
            new { topupNo = previousNumber, coverId, balance = (limit?.SuppLifeTimeLimit ?? 0) - (annual - nextLimit) }, tx);
        return annual;
    }

    /// <summary>Desktop SaveCISCovPerson: clinical premium, MCO and limits for a new supplementary of a clinical member.</summary>
    private async Task AddClinicalAsync(string coverId, DateTime dateOfBirth, string agePlan)
    {
        var plan = Code(current.Clinic!.PlanCode);
        var insured = Code(current.Clinic.InsuredType);
        var health = Code(Stored.HealthCode);
        var effective = (current.Clinic.EffectiveDate ?? save.Today).Date;
        var expiry = (current.Clinic.ExpiryDate ?? save.Today).Date;
        var settings = await conn.ProcFirstOrDefaultAsync<ClinicalPlanSettings>("genisis.Adjustment_CisPlan", new { plan, hlt = health }, tx);
        var directClaimPayor = YesNo.IsYes(settings?.DcStatus) ? Code(settings?.PayorCode) : Code(Stored.PayorCode);
        var roundUp = YesNo.IsNotNo(settings?.RoundUpStatus);
        var proRate = YesNo.IsNotNo(settings?.ProRate);

        var bands = await conn.ProcQueryAsync<LegacyRules.AgeBand>("genisis.Registration_AgeBands", new { plan = agePlan }, tx);
        var ageCode = LegacyRules.PickAgeBand(bands, LegacyRules.Age(effective, dateOfBirth), agePlan)?.AgeCode.Trim() ?? "";
        var planSettings = await conn.ProcFirstOrDefaultAsync<PlanSettings>("genisis.Adjustment_PlanSettings", new { plan }, tx);
        var supplementaryExpiry = AdjustmentRules.SupplementaryExpiry(dateOfBirth, (int)(planSettings?.AgeLimit ?? 0), effective, expiry).ExpiryDate!.Value.Date;

        using var limitGrid = await conn.ProcMultipleAsync("genisis.Adjustment_CisLimit", new { ins = insured, plan, hlt = health, eff = effective }, tx);
        var limit = await limitGrid.ReadFirstOrDefaultAsync<ClinicalLimitRates>();
        var visits = (await limitGrid.ReadFirstOrDefaultAsync<decimal?>()) ?? 0;
        using var premiumGrid = await conn.ProcMultipleAsync("genisis.Adjustment_CisPremium",
            new { ins = insured, plan, hlt = health, pay = directClaimPayor, ageCode, eff = effective }, tx);
        var premiumStatus = await premiumGrid.ReadFirstOrDefaultAsync<string?>();
        var premiumAmount = (await premiumGrid.ReadFirstOrDefaultAsync<decimal?>()) ?? 0;
        var mcoRates = await conn.ProcFirstOrDefaultAsync<ClinicalMcoRates>("genisis.Adjustment_CisMco", new { ins = insured, plan, hlt = health, eff = effective }, tx);

        decimal premium = 0, basicPremium = 0, mco = 0, basicMco = 0, annual = 0, visitLimit = 0, dental = 0, specialist = 0, mep = 0;
        if (AdjustmentRules.SupplementaryCharged(premiumStatus, coverId))
        {
            basicPremium = LegacyRules.Money(premiumAmount);
            premium = FeeRules.VbRound(FeeRules.ProRateMoney(effective, supplementaryExpiry, basicPremium));
        }
        if (AdjustmentRules.SupplementaryCharged(mcoRates?.SuppMcoStatus, coverId))
        {
            basicMco = LegacyRules.Money((ageCode == AdultChild.ChildAgeBand ? mcoRates?.SuppChild : mcoRates?.SuppAdult) ?? 0);
            mco = proRate ? FeeRules.ProRateMoney(effective, supplementaryExpiry, basicMco) : basicMco;
            if (roundUp) mco = FeeRules.VbRound(mco);
        }
        if (AdjustmentRules.SupplementaryCharged(limit?.SuppLimitStatus, coverId))
        {
            annual = LegacyRules.Money(limit?.SuppLimit ?? 0);
            visitLimit = visits;
            if (settings?.SuppDentalStatus?.Trim() == SuppliedByPlan) dental = limit?.DentalLimit ?? 0;
            if (settings?.SuppSpecialistStatus?.Trim() == SuppliedByPlan) specialist = limit?.SpecialistLimit ?? 0;
            if (settings?.SuppMepStatus?.Trim() == SuppliedByPlan) mep = limit?.MepLimit ?? 0;
        }
        var feeForService = YesNo.IsYes(settings?.McoProcFeeStatus);
        await conn.ProcExecAsync("genisis.Adjustment_InsertCoveredClinical", new
        {
            number = Number, coverId, mco, basicMco, premium, basicPremium, annual, visit = visitLimit, dental, specialist, mep, today = save.Today,
            user = save.UserCode, expiry = supplementaryExpiry, plan, invoiceNo = feeForService ? FeeForServiceInvoice : "",
            invoiceDate = feeForService ? save.Today : (DateTime?)null, invoiceBy = feeForService ? save.UserCode : "",
        }, tx);
    }
}
