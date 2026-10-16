using Genisis.Api.Data;
using Genisis.Api.Membership;
using Genisis.Api.Registration;

namespace Genisis.Api.Bordx;

/// <summary>
/// One principal or supplementary line of a new-member file (desktop NewMBMInfoProcessing): resolves the plan, calculates HIS fees,
/// lifetime limit and clinical fees, then writes the member to the UploadDb staging tables.
/// </summary>
internal sealed class BordxMemberImporter(BordxUploadContext context)
{
    private sealed record PlanRow(string PlanCode, string? LifetimeStatus, string? PayorCode, decimal? AgeLimit);
    private sealed record HisPlan(string? TopupStatus, string? ProRate, string? PremiumInd, string? McoInd, string? AnnualLimitInd, string? LifetimeStatus,
        decimal? AgeLimit, string? RoundUpStatus, string? PremGenderStatus, string? GuaranteeStatus);
    private sealed record ClinicalPlan(string? DcStatus, string? PremiumInd, string? McoInd, string? AnnualLimitInd, string? VisitLimitInd, string? PayorCode,
        string? DentalStatus, string? SpecialistStatus, string? MepStatus, decimal? AgeLimit, string? RoundUpStatus, string? McoProcFeeStatus, string? ProRate);
    private sealed record ClinicalLimit(string? SuppLimitStatus, decimal? AnnLimit, decimal? SuppLimit, decimal? DentalLimit, decimal? SpecialistLimit, decimal? MepLimit);
    private sealed record ClinicalPremium(string? SuppPrmStatus, decimal? PRMAmount, decimal? SuppPrmAmt);
    private sealed record ClinicalMco(string? SuppMcoStatus, decimal? Adult, decimal? Child, decimal? SuppAdult, decimal? SuppChild);
    private sealed record Mco(string? SuppMcoStatus, string? CovIDChrg, decimal? MCOAmountAdult, decimal? MCOAmountChild, decimal? MCOSuppAdultAmt, decimal? MCOSuppChildAmt);
    private sealed record Premium(string? SuppPrmStatus, decimal? PRMAmount, decimal? PRMAmountFemale, decimal? SuppPrmAmt);
    private sealed record Limit(string? SuppLimitStatus, decimal? AnnLimit, decimal? SuppLimit, decimal? LifeTimeLimit, decimal? SuppLifeTimeLimit);
    private sealed record PreviousLifetime(string MBMNumber, string? MBMTopupNumber, decimal? MBMAvailableLimit);
    private sealed record Amount(decimal? Value);

    /// <summary>The member being imported, after principal values have been carried to a supplementary line.</summary>
    private sealed class Member
    {
        public required BordxLine Line;
        public required string CoverId;
        public required string DataType;
        public required bool IsPrincipal;
        public required string HealthCode;
        public required string Policy;
        public required string Name;
        public required string Ic;
        public required DateTime DateOfBirth;
        public required string Sex;
        public required string Payor;
        public string Plan = "", Insured = "", Guarantee = "", Renewal = "";
        public DateTime? Effective, Expiry;
        public string ClinicalPlan = "", ClinicalInsured = "";
        public DateTime? ClinicalEffective, ClinicalExpiry;
        public decimal PolicyDiscount, RenewalDiscount;
        public string AgeCode = "";
        public string AdultChild => Membership.AdultChild.ForAgeBand(AgeCode);
        public string Label => $"Line {Line.Number} ({Name})";
    }

    private sealed record HisFees(decimal Premium, decimal BasicPremium, decimal Mco, decimal BasicMco, decimal AnnualLimit, decimal NextLimit,
        string TopupNumber, string TopupIndicator);

    private sealed record ClinicalFees(decimal Premium, decimal BasicPremium, decimal Mco, decimal BasicMco, decimal Balance, decimal Visits,
        decimal Dental, decimal Specialist, decimal Mep, string MemberType, string Payor, bool FeeForService);

    // Principal values carried to the following supplementary lines (desktop form-level variables).
    private string membershipNumber = "", principalPlan = "", principalInsured = "", principalGuarantee = "", previousLifetimeNumber = "";
    private DateTime? principalEffective, principalExpiry;

    private bool ZurichTak => context.Format.IsZurichTakLayout;

    public async Task ImportAsync(BordxLine line, string coverId)
    {
        var member = await ReadMemberAsync(line, coverId);
        var hisPlan = await LoadHisPlanAsync(member);
        var annualSupplementaryStatus = await AnnualSupplementaryStatusAsync(member, hisPlan);
        var clinicalPlan = await LoadClinicalPlanAsync(member);
        await AssignAgeBandAsync(member);

        var fees = member.Plan == "" ? new HisFees(0, 0, 0, 0, 0, 0, "", "") : await HisFeesAsync(member, hisPlan);
        string? securityCode = member.IsPrincipal ? await AssignMembershipNumberAsync(member) : null;
        fees = await LifetimeLimitAsync(member, hisPlan, annualSupplementaryStatus, fees);
        var clinical = await ClinicalFeesAsync(member, clinicalPlan);

        if (member.IsPrincipal)
        {
            var memberType = annualSupplementaryStatus != SupplementaryCharge.PrincipalOnly ? MemberTypes.PrincipalSeparate : MemberTypes.PrincipalCombined;
            await StagePrincipalAsync(member, memberType, fees, clinical, clinicalPlan, securityCode);
        }
        else if (member.DataType != DataTypes.Reinstatement)
        {
            await StageSupplementaryAsync(member, hisPlan, fees, clinical, clinicalPlan);
        }
    }

    private async Task<Member> ReadMemberAsync(BordxLine line, string coverId)
    {
        var dataType = line.Text(MemberLayout.DataType);
        var health = context.Settings.HealthCode;
        if (context.FileStatus == FileStatuses.Reinstatement && dataType == DataTypes.Reinstatement)
        {
            dataType = DataTypes.Principal;
            health = HealthTypes.NonSihat;
        }
        var name = line.Text(MemberLayout.Name);
        var renewal = line.Text(MemberLayout.RenewalType);

        string plan, payor;
        if (ZurichTak)
        {
            var row = await context.Connection.ProcFirstOrDefaultAsync<PlanRow>("genisis.Bordx_Plan",
                new { plan = (string?)null, payorPlan = line.Text(MemberLayout.PayorPlanCode) }, context.Transaction);
            if (row is not null) context.PlanPayor = (row.PayorCode ?? "").Trim();
            plan = row?.PlanCode ?? "";
            payor = context.PlanPayor;
        }
        else
        {
            plan = line.Text(MemberLayout.PlanCode);
            payor = context.Settings.PayorCode;
        }

        var member = new Member
        {
            Line = line, CoverId = coverId, DataType = dataType, IsPrincipal = dataType == DataTypes.Principal, HealthCode = health,
            Policy = line.Text(MemberLayout.PolicyNoFor(ZurichTak)), Name = name, Ic = line.Text(MemberLayout.IcNumber),
            DateOfBirth = line.Date(MemberLayout.DateOfBirth) ?? throw new BordxException($"Line {line.Number} ({name}): invalid date of birth. Verify the file first."),
            Sex = line.Text(MemberLayout.Sex), Payor = payor, Plan = plan, Insured = line.Text(MemberLayout.InsuredTypeFor(ZurichTak)),
            Effective = line.Date(MemberLayout.EffectiveDate), Expiry = line.Date(MemberLayout.ExpiryDate),
            Renewal = renewal is "C" or "Y" or "" ? YesNo.No : renewal,
            PolicyDiscount = ZurichTak ? 0 : line.Amount(MemberLayout.PolicyDiscountPercent),
            RenewalDiscount = ZurichTak ? 0 : line.Amount(MemberLayout.RenewalDiscountPercent),
            Guarantee = ZurichTak ? "" : line.Text(MemberLayout.GuaranteeRenewal),
            ClinicalPlan = line.Text(MemberLayout.ClinicalPlan), ClinicalInsured = line.Text(MemberLayout.ClinicalInsuredType),
            ClinicalEffective = line.Date(MemberLayout.ClinicalEffectiveDate), ClinicalExpiry = line.Date(MemberLayout.ClinicalExpiryDate),
        };

        if (member.IsPrincipal)
        {
            principalPlan = member.Plan; principalInsured = member.Insured; principalEffective = member.Effective; principalExpiry = member.Expiry;
            principalGuarantee = member.Guarantee;
        }
        else if (membershipNumber == "")
        {
            throw new BordxException($"{member.Label}: supplementary record has no principal before it.");
        }
        if (member.Plan == "") member.Plan = principalPlan;
        if (member.Insured == "") member.Insured = principalInsured;
        member.Effective ??= principalEffective;
        member.Expiry ??= principalExpiry;
        return member;
    }

    private async Task<HisPlan?> LoadHisPlanAsync(Member member)
    {
        if (member.Plan == "") return null;
        if (member.Effective is null || member.Expiry is null)
            throw new BordxException($"{member.Label}: invalid HIS effective / expiry date. Verify the file first.");
        var plan = await context.Connection.ProcFirstOrDefaultAsync<HisPlan>("genisis.Bordx_HisPlan",
            new { plan = member.Plan, hlt = member.HealthCode, pay = member.Payor }, context.Transaction);
        if (plan is null) context.Warnings.Add($"Line {member.Line.Number}: PLN Information Is Not Correct (plan {member.Plan}).");
        var planGuarantee = plan?.GuaranteeStatus?.Trim() ?? "";
        member.Guarantee = planGuarantee != "" ? planGuarantee : member.Guarantee == "" ? principalGuarantee : member.Guarantee;
        return plan;
    }

    private async Task<string> AnnualSupplementaryStatusAsync(Member member, HisPlan? plan)
    {
        if (member.Plan == "" || !YesNo.IsYes(plan?.AnnualLimitInd)) return SupplementaryCharge.PrincipalOnly;
        var limit = await context.Connection.ProcFirstOrDefaultAsync<Limit>("genisis.Registration_AnnualLimit",
            new { ins = member.Insured, plan = member.Plan, hlt = member.HealthCode, eff = member.Effective }, context.Transaction);
        var status = limit?.SuppLimitStatus?.Trim().ToUpperInvariant() ?? "";
        return status == "" ? SupplementaryCharge.PrincipalOnly : status;
    }

    private async Task<ClinicalPlan?> LoadClinicalPlanAsync(Member member)
    {
        if (member.ClinicalPlan == "") return null;
        if (member.ClinicalEffective is null || member.ClinicalExpiry is null)
            throw new BordxException($"{member.Label}: invalid clinical effective / expiry date. Verify the file first.");
        var plan = await context.Connection.ProcFirstOrDefaultAsync<ClinicalPlan>("genisis.Bordx_CisPlan", new { plan = member.ClinicalPlan }, context.Transaction);
        if (plan is null) context.Warnings.Add($"Line {member.Line.Number}: PLN (Clinical) Information Is Not Correct (plan {member.ClinicalPlan}).");
        return plan;
    }

    /// <summary>Age band on the HIS effective date (or the clinical one for clinical-only members).</summary>
    private async Task AssignAgeBandAsync(Member member)
    {
        var onDate = member.Plan != "" ? member.Effective : member.ClinicalEffective;
        if (onDate is not { } date) return;
        var agePlan = member.Plan != "" ? member.Plan : member.ClinicalPlan;
        var bands = await context.Connection.ProcQueryAsync<LegacyRules.AgeBand>("genisis.Registration_AgeBands", new { plan = agePlan }, context.Transaction);
        member.AgeCode = LegacyRules.PickAgeBand(bands, LegacyRules.Age(date, member.DateOfBirth), agePlan)?.AgeCode.Trim() ?? "";
        if (member.AgeCode == "") context.Warnings.Add($"{member.Label}: Age Band not found or Date out of Acceptable Range!");
    }

    private async Task<HisFees> HisFeesAsync(Member member, HisPlan? plan)
    {
        var effective = member.Effective!.Value;
        var expiry = member.Expiry!.Value;
        decimal premium = 0, basicPremium = 0, mco = 0, basicMco = 0, annual = 0;
        var topupNumber = "";
        var topupIndicator = "";

        if (YesNo.IsYes(plan?.PremiumInd))
        {
            var row = await context.Connection.ProcFirstOrDefaultAsync<Premium>("genisis.Registration_Premium",
                new { ins = member.Insured, hlt = member.HealthCode, plan = member.Plan, ageCode = member.AgeCode, eff = effective, pay = member.Payor }, context.Transaction);
            if (row is null) context.Warnings.Add($"{member.Label}: Age Out of Premium Range! Please check your Premium Table.");
            var female = YesNo.IsYes(plan?.PremGenderStatus) && member.Sex == Sex.Female;
            var amount = (female ? row?.PRMAmountFemale : member.IsPrincipal ? row?.PRMAmount : row?.SuppPrmAmt) ?? 0;
            if (!FeeRules.IsCharged(member.IsPrincipal, row?.SuppPrmStatus, member.CoverId)) amount = 0;
            premium = basicPremium = LegacyRules.Money(amount);
            if (member.PolicyDiscount > 0) premium = premium * (100 - member.PolicyDiscount) / 100;
            if (member.RenewalDiscount > 0) premium = premium * (100 - member.RenewalDiscount) / 100;
            premium = FeeRules.ProRateMoney(effective, expiry, LegacyRules.Money(premium));
        }
        if (YesNo.IsYes(plan?.McoInd))
        {
            var row = await context.Connection.ProcFirstOrDefaultAsync<Mco>("genisis.Registration_Mco",
                new { ins = member.Insured, hlt = member.HealthCode, plan = member.Plan, eff = effective }, context.Transaction);
            var child = member.AdultChild == AdultChild.Child;
            var amount = (member.IsPrincipal
                ? child ? row?.MCOAmountChild : row?.MCOAmountAdult
                : child ? row?.MCOSuppChildAmt : row?.MCOSuppAdultAmt) ?? 0;
            if (!FeeRules.IsCharged(member.IsPrincipal, row?.SuppMcoStatus, member.CoverId, row?.CovIDChrg)) amount = 0;
            mco = basicMco = LegacyRules.Money(amount);
            if (YesNo.IsNotNo(plan?.ProRate)) mco = FeeRules.ProRateMoney(effective, expiry, basicMco);
            if (YesNo.IsNotNo(plan?.RoundUpStatus)) mco = FeeRules.VbRound(mco);
        }
        if (YesNo.IsYes(plan?.AnnualLimitInd))
        {
            var row = await context.Connection.ProcFirstOrDefaultAsync<Limit>("genisis.Registration_AnnualLimit",
                new { ins = member.Insured, plan = member.Plan, hlt = member.HealthCode, eff = effective }, context.Transaction);
            if (row is null) context.Warnings.Add($"Line {member.Line.Number}: Annual Limit Information Is Not Correct! (Plan Code {member.Plan})");
            annual = (member.IsPrincipal ? row?.AnnLimit : row?.SuppLimit) ?? 0;
            if (!FeeRules.IsCharged(member.IsPrincipal, row?.SuppLimitStatus, member.CoverId)) annual = 0;
        }
        if (YesNo.IsYes(plan?.TopupStatus)
            && await context.Connection.ProcScalarAsync<string?>("genisis.Bordx_TopupByIc",
                new { ic = member.Ic, pay = member.Payor, ins = member.Insured }, context.Transaction) is { } topup)
        {
            topupNumber = topup.Trim();
            topupIndicator = YesNo.Yes;
        }
        return new HisFees(premium, basicPremium, mco, basicMco, annual, annual, topupNumber, topupIndicator);
    }

    /// <summary>New membership number for a principal; returns the security code to save, if any.</summary>
    private async Task<string?> AssignMembershipNumberAsync(Member member)
    {
        var conn = context.Connection;
        var tx = context.Transaction;
        if (member.Plan == "" && member.ClinicalPlan != "")
        {
            var sequence = await conn.ProcSingleAsync<string>("genisis.Bordx_NextClinicalSeq", new { pay = member.Payor, ins = member.ClinicalInsured }, tx);
            membershipNumber = member.Payor + member.ClinicalInsured + sequence + await MembershipNumbers.NextCheckDigitsAsync(conn, tx);
            return await MembershipNumbers.NextSecurityCodeAsync(conn, tx);
        }
        var payorByPolicy = context.Format.IsZurichTakLayout || context.Format.Payor?.RegStatus?.Trim().ToUpperInvariant() == PayorRegistration.ByPolicy;
        var byPolicy = member.Plan != "" ? payorByPolicy : payorByPolicy && member.Payor == SpecialPayors.Xl;
        membershipNumber = await MembershipNumbers.GenerateAsync(conn, tx, member.Payor, member.Insured, member.DateOfBirth, member.Name, member.Policy,
            member.Renewal, byPolicy);
        return member.Plan != "" ? await MembershipNumbers.NextSecurityCodeAsync(conn, tx) : null;
    }

    /// <summary>Lifetime limit of guarantee renewal plans (desktop SaveMBMLifeTimeLmt).</summary>
    private async Task<HisFees> LifetimeLimitAsync(Member member, HisPlan? plan, string annualSupplementaryStatus, HisFees fees)
    {
        if (member.Guarantee == "" || member.Plan == "") return fees;
        if (!YesNo.IsYes(plan?.LifetimeStatus)) return fees with { TopupNumber = "" };

        var conn = context.Connection;
        var tx = context.Transaction;
        var topupNumber = fees.TopupNumber;
        var nextLimit = fees.AnnualLimit;
        if (member.IsPrincipal)
        {
            topupNumber = previousLifetimeNumber = membershipNumber;
            if (await conn.ProcFirstOrDefaultAsync<PreviousLifetime>("genisis.Registration_PreviousLifetime",
                    new { policy = member.Policy, pay = member.Payor, ins = member.Insured }, tx) is { } previous)
            {
                topupNumber = (previous.MBMTopupNumber ?? "").Trim();
                previousLifetimeNumber = previous.MBMNumber.Trim();
                nextLimit = previous.MBMAvailableLimit ?? 0;
            }
        }
        else if (await conn.ProcFirstOrDefaultAsync<Amount>("genisis.Bordx_PreviousCoveredLimit",
                     new { previousNo = previousLifetimeNumber, name = member.Name }, tx) is { } covered)
        {
            nextLimit = covered.Value ?? 0;
        }
        var annual = member.Guarantee != GuaranteeRenewal.LifetimeTracked ? nextLimit : fees.AnnualLimit;

        var balance = await conn.ProcFirstOrDefaultAsync<Amount>("genisis.Registration_LifetimeBalance", new { topupNo = topupNumber, coverId = member.CoverId }, tx);
        // Lifetime rows follow the annual-limit charge rule, where status D (charge from a cover ID) does not apply.
        var charged = annualSupplementaryStatus == SupplementaryCharge.Everyone
            || (annualSupplementaryStatus == SupplementaryCharge.PrincipalOnly && member.CoverId == CoverIds.Principal)
            || (annualSupplementaryStatus == SupplementaryCharge.FirstSupplementaryOnly && member.CoverId == CoverIds.FirstSupplementary);
        if (balance is null && member.Guarantee == GuaranteeRenewal.LifetimeTracked && charged && topupNumber != "")
        {
            var limits = await conn.ProcFirstOrDefaultAsync<Limit>("genisis.Registration_AnnualLimit",
                new { ins = member.Insured, plan = member.Plan, hlt = member.HealthCode, eff = member.Effective }, tx);
            var lifetime = (member.IsPrincipal ? limits?.LifeTimeLimit : limits?.SuppLifeTimeLimit) ?? 0;
            await context.ExecAsync("genisis.Registration_InsertLifetime",
                new { topupNo = topupNumber, coverId = member.CoverId, balance = lifetime - (annual - nextLimit) });
        }
        return fees with { AnnualLimit = annual, NextLimit = nextLimit, TopupNumber = topupNumber };
    }

    private async Task<ClinicalFees?> ClinicalFeesAsync(Member member, ClinicalPlan? plan)
    {
        if (member.ClinicalPlan == "") return null;
        var effective = member.ClinicalEffective!.Value;
        var expiry = member.ClinicalExpiry!.Value;
        var payor = (plan?.PayorCode ?? "").Trim() is { Length: > 0 } planPayor ? planPayor : member.Payor;

        using var grid = await context.Connection.ProcMultipleAsync("genisis.Bordx_CisRates",
            new { ins = member.ClinicalInsured, plan = member.ClinicalPlan, hlt = member.HealthCode, eff = effective, premiumDate = expiry, pay = payor, ageCode = member.AgeCode },
            context.Transaction);
        var limit = await grid.ReadFirstOrDefaultAsync<ClinicalLimit>();
        var visits = await grid.ReadFirstOrDefaultAsync<decimal?>();
        var premiumRow = await grid.ReadFirstOrDefaultAsync<ClinicalPremium>();
        var mcoRow = await grid.ReadFirstOrDefaultAsync<ClinicalMco>();

        var supplementaryStatus = YesNo.IsYes(plan?.AnnualLimitInd) && limit is not null && (limit.SuppLimitStatus ?? "").Trim() != ""
            ? limit.SuppLimitStatus!.Trim().ToUpperInvariant()
            : SupplementaryCharge.PrincipalOnly;
        decimal premium = 0, basicPremium = 0, mco = 0, basicMco = 0, balance = 0;
        if (YesNo.IsYes(plan?.PremiumInd))
        {
            if (premiumRow is null) context.Warnings.Add($"{member.Label}: clinical premium not found for age band {member.AgeCode}.");
            basicPremium = FeeRules.IsCharged(member.IsPrincipal, premiumRow?.SuppPrmStatus, member.CoverId)
                ? LegacyRules.Money((member.IsPrincipal ? premiumRow?.PRMAmount : premiumRow?.SuppPrmAmt) ?? 0)
                : 0;
            premium = LegacyRules.Money(FeeRules.VbRound(FeeRules.ProRateMoney(effective, expiry, basicPremium)));
        }
        if (YesNo.IsYes(plan?.McoInd))
        {
            var child = member.AdultChild == AdultChild.Child;
            basicMco = FeeRules.IsCharged(member.IsPrincipal, mcoRow?.SuppMcoStatus, member.CoverId)
                ? LegacyRules.Money((member.IsPrincipal ? child ? mcoRow?.Child : mcoRow?.Adult : child ? mcoRow?.SuppChild : mcoRow?.SuppAdult) ?? 0)
                : 0;
            mco = YesNo.IsNotNo(plan?.ProRate) ? FeeRules.ProRateMoney(effective, expiry, basicMco) : basicMco;
            if (YesNo.IsNotNo(plan?.RoundUpStatus)) mco = FeeRules.VbRound(mco);
        }
        if (YesNo.IsYes(plan?.AnnualLimitInd))
            balance = FeeRules.IsCharged(member.IsPrincipal, limit?.SuppLimitStatus, member.CoverId)
                ? (member.IsPrincipal ? limit?.AnnLimit : limit?.SuppLimit) ?? 0
                : 0;
        const string SuppliedByPlan = "S";
        return new ClinicalFees(premium, basicPremium, mco, basicMco, balance,
            YesNo.IsYes(plan?.VisitLimitInd) ? visits ?? 0 : 0,
            plan?.DentalStatus?.Trim() == SuppliedByPlan ? limit?.DentalLimit ?? 0 : 0,
            plan?.SpecialistStatus?.Trim() == SuppliedByPlan ? limit?.SpecialistLimit ?? 0 : 0,
            plan?.MepStatus?.Trim() == SuppliedByPlan ? limit?.MepLimit ?? 0 : 0,
            supplementaryStatus == SupplementaryCharge.PrincipalOnly ? MemberTypes.PrincipalCombined : MemberTypes.PrincipalSeparate,
            payor, YesNo.IsYes(plan?.McoProcFeeStatus));
    }

    private string HospitalClinical(Member member, ClinicalPlan? clinicalPlan) => (member.Plan, member.ClinicalPlan) switch
    {
        ("", not "") => HospitalClinicalIndicator.Clinical,
        (not "", not "") => YesNo.IsYes(clinicalPlan?.DcStatus) ? HospitalClinicalIndicator.BothDirectClaim : HospitalClinicalIndicator.Both,
        _ => HospitalClinicalIndicator.Hospital,
    };

    private async Task StagePrincipalAsync(Member member, string memberType, HisFees fees, ClinicalFees? clinical, ClinicalPlan? clinicalPlan, string? securityCode)
    {
        var line = member.Line;
        var standard = !ZurichTak;
        var bordx = context.SubmissionDate;
        var takeOver = line.Text(MemberLayout.TakeOver);
        var renewal = takeOver is "C" or "Y" || line.IsBlank(MemberLayout.RenewalType) ? YesNo.No : line.Text(MemberLayout.RenewalType);

        await context.ExecAsync("genisis.Bordx_StagePrincipal", new
        {
            runKey = context.StagingKey, pay = member.Payor, hlt = member.HealthCode, number = membershipNumber, policy = member.Policy, coverId = member.CoverId,
            ic = member.Ic, name = member.Name, dob = member.DateOfBirth, sex = member.Sex, race = line.Text(MemberLayout.Race), status = MemberStatus.Active,
            dataStatus = DataStatus.Active, memberType, add1 = line.Text(MemberLayout.Address1), add2 = line.Text(MemberLayout.Address2),
            add3 = line.Text(MemberLayout.Address3), city = line.Text(MemberLayout.City), postCode = line.Text(MemberLayout.PostCode),
            state = line.Text(MemberLayout.State), expiredStatus = BordxValues.ExpiredNo, valueDate = context.Today, eff = member.Effective,
            exp = member.Expiry, takeOver, renewal, bordx, batch = context.Batch, plan = member.Plan, ins = member.Insured, ageCode = member.AgeCode,
            adultChild = member.AdultChild, available = fees.AnnualLimit, hsClnInd = HospitalClinical(member, clinicalPlan), topupNo = fees.TopupNumber,
            topupInd = fees.TopupIndicator,
        });
        await context.ExecAsync("genisis.Bordx_StageMbmTwo", new
        {
            runKey = context.StagingKey, number = membershipNumber, salutation = line.Text(MemberLayout.Salutation),
            telHome = line.Text(MemberLayout.TelephoneHome), telMobile = line.Text(MemberLayout.TelephoneMobile), telOffice = line.Text(MemberLayout.TelephoneOffice),
            nat = line.Text(MemberLayout.Nationality), language = line.Text(MemberLayout.Language), user = context.UserCode,
            clinical = member.ClinicalPlan != "" ? YesNo.Yes : YesNo.No, appName = line.Text(MemberLayout.ApplicationName),
            otherIc = ZurichTak ? line.Text(MemberLayout.OtherIcNumber) : "", fileName = context.FileName, today = context.Today,
            agentName = line.Text(MemberLayout.AgentName), agentEmail = line.Text(MemberLayout.AgentEmail), agentPhone = line.Text(MemberLayout.AgentPhone),
        });
        await context.ExecAsync("genisis.Bordx_StageXref", new
        {
            runKey = context.StagingKey, number = membershipNumber, policy = member.Policy, ic = member.Ic, name = member.Name, eff = member.Effective,
            exp = member.Expiry, ageCode = member.AgeCode, ins = member.Insured, plan = member.Plan, bordx, topupNo = fees.TopupNumber,
            topupInd = fees.TopupIndicator, dataStatus = DataStatus.Active, status = MemberStatus.Active, dob = member.DateOfBirth, hlt = member.HealthCode,
            pay = member.Payor, batch = context.Batch,
        });

        // The ZURICH / TAK layout carries the MCO fees in the file instead of the calculated ones.
        var (othersMco, othersBasicMco) = !ZurichTak ? (fees.Mco, fees.BasicMco)
            : context.FileStatus is FileStatuses.Reinstatement or FileStatuses.Renewal
                ? (line.Amount(MemberLayout.PayorAmount1), line.Amount(MemberLayout.PayorAmount2))
                : (line.Amount(MemberLayout.PayorAmount2), line.Amount(MemberLayout.ZurichTakBasicMco));
        var (gstAmount, gstPercent) = context.Gst(line);
        await context.ExecAsync("genisis.Bordx_StageOthers", new
        {
            runKey = context.StagingKey, number = membershipNumber, agentCode = line.Text(MemberLayout.AgentCode),
            labelStatus = context.Settings.LabelPrinted ? YesNo.Yes : YesNo.No, premium = fees.Premium, basicPremium = fees.BasicPremium,
            basicMco = othersBasicMco, mco = othersMco, rel = member.DataType, policyDisc = member.PolicyDiscount, renewalDisc = member.RenewalDiscount,
            groupCompany = line.Text(MemberLayout.GroupCompanyFor(ZurichTak)), prevTakeOverPlan = line.Text(MemberLayout.PreviousTakeOverPlan),
            joined = line.Date(MemberLayout.FirstJoinedDate) ?? member.Effective, prevPolicy = line.Text(MemberLayout.PreviousPolicyNo),
            prevMember = line.Text(MemberLayout.PreviousMembershipNo), employeeNo = line.Text(MemberLayout.EmployeeNo),
            department = line.Text(MemberLayout.Department), installment = line.Text(MemberLayout.PremiumMode), polSubNo1 = line.Text(MemberLayout.PolicySubNo1),
            polSubNo2 = line.Text(MemberLayout.PolicySubNo2), branch = line.Text(MemberLayout.Branch),
            newPolicyEff = !line.IsBlank(MemberLayout.NewPlan) && !line.IsBlank(MemberLayout.NewPolicyEffectiveDate) ? line.Date(MemberLayout.NewPolicyEffectiveDate) : null,
            endtRefNo = line.Text(MemberLayout.EndorsementReferenceNo), polCondition = ZurichTak ? line.Text(MemberLayout.PolicyCondition) : "",
            undExcess = standard ? line.Amount(MemberLayout.UnderwritingExcess) : 0, guarantee = member.Guarantee,
            clmNonPayFrom = line.Date(MemberLayout.ClaimNonPayFrom), clmNonPayTo = line.Date(MemberLayout.ClaimNonPayTo),
            invoiceNo = context.FileStatus == FileStatuses.Cancellation ? BordxValues.VoidInvoice : "", gstAmount, gstPercent,
        });
        await context.ExecAsync("genisis.Bordx_StageOthersTwo", new
        {
            runKey = context.StagingKey, number = membershipNumber, weight = line.Text(MemberLayout.Weight), height = line.Text(MemberLayout.Height),
            workNature = line.Text(MemberLayout.WorkNature), blood = line.Text(MemberLayout.BloodType), occupation = line.Text(MemberLayout.Occupation),
            smoking = line.Text(MemberLayout.Smoking), alcohol = line.Text(MemberLayout.Alcohol), marital = line.Text(MemberLayout.MaritalStatus),
            grpAdd1 = line.Text(MemberLayout.GroupAddress1), grpAdd2 = line.Text(MemberLayout.GroupAddress2), grpAdd3 = line.Text(MemberLayout.GroupAddress3),
            grpAdd4 = line.Text(MemberLayout.GroupAddress4), riskNo = line.Text(MemberLayout.RiskNo), transNo = line.Text(MemberLayout.TransactionNo),
            payNo = line.Text(MemberLayout.PayNo), paySeqNo = line.Text(MemberLayout.PaySequenceNo),
            bankAccountNo = standard ? line.Text(MemberLayout.BankAccountNo) : "", clientNo = line.Text(MemberLayout.ClientNo),
            clientId = line.Text(MemberLayout.ClientId), rbAmount = line.Amount(MemberLayout.RoomAndBoardAmount),
            prevInsurer = line.Text(MemberLayout.PreviousInsurer), prevPlan = line.Text(MemberLayout.PreviousPlan),
            payRemarks = line.Text(MemberLayout.PaymentRemarks), coPayRb = standard ? line.Text(MemberLayout.CoPayRoomAndBoard) : "",
            exclusion = line.Text(MemberLayout.Exclusion), btbg = context.Settings.BackToBack ? YesNo.Yes : YesNo.No,
            payorPremNet = standard ? line.Amount(MemberLayout.PayorAmount1) : 0,
            payorMcoNet = line.Amount(standard ? MemberLayout.PayorMcoNet : MemberLayout.PayorAmount1),
            payorMcoGross = line.Amount(standard ? MemberLayout.PayorMcoGross : MemberLayout.PayorAmount2),
            loadingPremium = standard ? line.Amount(MemberLayout.LoadingPremium) : 0,
            familyDiscAmount = standard ? line.Amount(MemberLayout.FamilyDiscountAmount) : 0,
            renewalDiscAmount = standard ? line.Amount(MemberLayout.RenewalDiscountAmount) : 0,
            payorPremGross = standard ? line.Amount(MemberLayout.PayorAmount2) : 0, policyIssued = line.Date(MemberLayout.PolicyIssuedDate),
            allergic = line.Text(MemberLayout.Allergies),
        });
        if (clinical is not null)
            await context.ExecAsync("genisis.Bordx_StageClinical", new
            {
                runKey = context.StagingKey, memberType = clinical.MemberType, pay = clinical.Payor, number = membershipNumber, policy = member.Policy,
                plan = member.ClinicalPlan, ins = member.ClinicalInsured, warranty = line.Text(MemberLayout.ClinicalWarranty),
                basicPremium = clinical.BasicPremium, premium = clinical.Premium, basicMco = clinical.BasicMco, mco = clinical.Mco,
                balance = clinical.Balance, visit = clinical.Visits, dental = clinical.Dental, specialist = clinical.Specialist, mep = clinical.Mep,
                eff = member.ClinicalEffective, exp = member.ClinicalExpiry, cardType = line.Text(MemberLayout.ClinicalCardType),
                invoiced = clinical.FeeForService ? YesNo.Yes : "", invoiceNo = clinical.FeeForService ? BordxValues.FeeForServiceInvoice : "",
                invoiceDate = clinical.FeeForService ? context.Today : (DateTime?)null, invoiceBy = clinical.FeeForService ? context.UserCode : "",
            });
        if (securityCode is not null)
            await context.Connection.ProcScalarAsync<string>("genisis.Registration_SecurityCode",
                new { baseNumber = membershipNumber.Split('*')[0], generatedCode = securityCode }, context.Transaction);
        context.NewMembers++;
        context.MembershipNumbers.Add(membershipNumber);
    }

    private async Task StageSupplementaryAsync(Member member, HisPlan? hisPlan, HisFees fees, ClinicalFees? clinical, ClinicalPlan? clinicalPlan)
    {
        var line = member.Line;
        var standard = !ZurichTak;
        DateTime? expiry = member.Plan != "" ? FeeRules.SupplementaryExpiry(member.DateOfBirth, member.Expiry!.Value, hisPlan?.AgeLimit) : null;
        await context.ExecAsync("genisis.Bordx_StageCovered", new
        {
            runKey = context.StagingKey, rel = member.DataType, number = membershipNumber, coverId = member.CoverId, dob = member.DateOfBirth,
            memberType = MemberTypes.SupplementaryCombined, name = member.Name.Replace("'", ""), status = MemberStatus.Active, bordx = context.SubmissionDate,
            batch = context.Batch, ic = member.Ic, sex = member.Sex, plan = member.Plan, ageCode = member.AgeCode, adultChild = member.AdultChild,
            annual = fees.AnnualLimit, basicMco = fees.BasicMco, mco = fees.Mco, basicPremium = fees.BasicPremium, premium = fees.Premium,
            today = context.Today, eff = member.Effective, exp = expiry,
        });
        await context.ExecAsync("genisis.Bordx_StageCoveredTwo", new
        {
            runKey = context.StagingKey, number = membershipNumber, coverId = member.CoverId, salutation = line.Text(MemberLayout.Salutation),
            occupation = line.Text(MemberLayout.Occupation), workNature = line.Text(MemberLayout.WorkNature), blood = line.Text(MemberLayout.BloodType),
            allergic = line.Text(MemberLayout.Allergies), smoking = line.Text(MemberLayout.Smoking), alcohol = line.Text(MemberLayout.Alcohol),
            prevPlan = line.Text(MemberLayout.PreviousPlan), prevInsurer = line.Text(MemberLayout.PreviousInsurer),
            payRemarks = line.Text(MemberLayout.PaymentRemarks), exclusion = line.Text(MemberLayout.Exclusion), payNo = line.Text(MemberLayout.PayNo),
            paySeqNo = line.Text(MemberLayout.PaySequenceNo), clientNo = line.Text(MemberLayout.ClientNo), clientId = line.Text(MemberLayout.ClientId),
            rbAmount = line.Amount(MemberLayout.RoomAndBoardAmount), payorPremNet = standard ? line.Amount(MemberLayout.PayorAmount1) : 0,
            payorMcoNet = line.Amount(standard ? MemberLayout.PayorMcoNet : MemberLayout.PayorAmount1),
            payorMcoGross = line.Amount(standard ? MemberLayout.PayorMcoGross : MemberLayout.PayorAmount2),
            otherIc = standard ? line.Text(MemberLayout.OtherIcNumber) : "", loadingPremium = standard ? line.Amount(MemberLayout.LoadingPremium) : 0,
            familyDiscAmount = standard ? line.Amount(MemberLayout.FamilyDiscountAmount) : 0,
            renewalDiscAmount = standard ? line.Amount(MemberLayout.RenewalDiscountAmount) : 0,
            payorPremGross = standard ? line.Amount(MemberLayout.PayorAmount2) : 0, undExcess = standard ? line.Amount(MemberLayout.UnderwritingExcess) : 0,
            clmNonPayFrom = line.Date(MemberLayout.ClaimNonPayFrom), clmNonPayTo = line.Date(MemberLayout.ClaimNonPayTo),
            policyDisc = member.PolicyDiscount, renewalDisc = member.RenewalDiscount,
        });
        if (clinical is not null)
            await context.ExecAsync("genisis.Bordx_StageCoveredClinical", new
            {
                runKey = context.StagingKey, number = membershipNumber, coverId = member.CoverId, mco = clinical.Mco, basicMco = clinical.BasicMco,
                premium = clinical.Premium, basicPremium = clinical.BasicPremium, balance = clinical.Balance, visit = clinical.Visits,
                dental = clinical.Dental, specialist = clinical.Specialist, mep = clinical.Mep, today = context.Today, user = context.UserCode,
                exp = FeeRules.SupplementaryExpiry(member.DateOfBirth, member.ClinicalExpiry!.Value, clinicalPlan?.AgeLimit),
                cardType = line.Text(MemberLayout.ClinicalCardType), warranty = line.Text(MemberLayout.ClinicalWarranty), plan = member.ClinicalPlan,
                invoiceNo = clinical.FeeForService ? BordxValues.FeeForServiceInvoice : "",
                invoiceDate = clinical.FeeForService ? context.Today : (DateTime?)null, invoiceBy = clinical.FeeForService ? context.UserCode : "",
            });
        context.Supplementaries++;
    }
}
