using System.Data;
using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Registration;

/// <summary>
/// Port of the legacy standard (non-clinical) registration save: frmNewMBMReg05.NewRegistrationSub and helpers.
/// Main and maintenance databases are written on one connection inside one transaction, so a failure leaves nothing behind.
/// </summary>
public class RegistrationService(DbConnections db)
{
    private sealed record Plan(string? GRPCompany, string? PRMInd, string? MCOInd, string? AnnLmtIND, string? PLNLifeTimeStatus,
        string? PLNProRate, string? PLNRoundUpStatus, string? PLNPremGenderStatus);
    private sealed record Payor(string? RegStatus);
    private sealed record Premium(string? SuppPrmStatus, decimal? PRMAmount, decimal? PRMAmountFemale, decimal? SuppPrmAmt);
    private sealed record Mco(string? SuppMcoStatus, string? CovIDChrg, decimal? MCOAmountAdult, decimal? MCOAmountChild,
        decimal? MCOSuppAdultAmt, decimal? MCOSuppChildAmt);
    private sealed record Limit(string? SuppLimitStatus, decimal? AnnLimit, decimal? SuppLimit, decimal? LifeTimeLimit, decimal? SuppLifeTimeLimit);
    private sealed record Benefit(string? SuppBnfStatus, decimal? KidDialysisLmt, decimal? CancerLmt, decimal? HomeNursLmt);
    private sealed record PreviousLifetime(string MBMNumber, string? MBMTopupNumber, decimal? MBMAvailableLimit);
    private sealed record Amount(decimal? Value);

    private sealed class Person
    {
        public required string CoverId { get; init; }
        public required CoveredPersonRequest Request { get; init; }
        public DateTime Effective { get; init; }
        public DateTime Expiry { get; init; }
        public required string AgeCode { get; init; }
        public required string AdultChild { get; init; }
        public decimal BasicPremium { get; set; }
        public decimal Premium { get; set; }
        public decimal BasicMco { get; set; }
        public decimal Mco { get; set; }
        public decimal AnnualLimit { get; set; }
        public decimal AvailableLimit { get; set; }
    }

    private sealed class Computation
    {
        public required Plan Plan { get; init; }
        public required string PayorRegStatus { get; init; }
        public Limit? Limit { get; init; }
        public bool Lifetime { get; init; }
        public required string AgeCode { get; init; }
        public required string AdultChild { get; init; }
        public required string MemberType { get; init; }
        public decimal BasicPremium { get; init; }
        public decimal Premium { get; init; }
        public decimal BasicMco { get; init; }
        public decimal Mco { get; init; }
        public decimal AnnualLimit { get; set; }
        public List<Person> Covered { get; } = [];
        public List<string> Warnings { get; } = [];
        public bool WriteLifetime { get; set; }
    }

    public async Task<RegistrationQuote> QuoteAsync(RegistrationRequest request)
    {
        Normalize(request);
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        return ToQuote(await ComputeAsync(conn, null, request));
    }

    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request, string userCode)
    {
        Normalize(request);
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var result = await SaveAsync(conn, tx, maintenanceDb, request, userCode);
            await tx.CommitAsync();
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    internal static void Normalize(RegistrationRequest r)
    {
        static string? Code(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
        static string? Text(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        r.HealthCode = Code(r.HealthCode); r.PayorCode = Code(r.PayorCode); r.InsuredType = Code(r.InsuredType);
        r.PlanCode = Code(r.PlanCode); r.Renewal = Code(r.Renewal) ?? "N"; r.Sex = Code(r.Sex);
        r.TakeOver = Code(r.TakeOver) ?? "N"; r.GuaranteeRenewal = Code(r.GuaranteeRenewal); r.InstallmentMode = Code(r.InstallmentMode);
        r.RaceCode = Code(r.RaceCode); r.NationalityCode = Code(r.NationalityCode); r.MaritalStatus = Code(r.MaritalStatus);
        r.Name = Text(r.Name); r.IcBcPp = Text(r.IcBcPp); r.OtherIc = Text(r.OtherIc); r.PolicyNo = Text(r.PolicyNo);
        r.PreviousMembershipNo = Text(r.PreviousMembershipNo); r.PreviousPolicyNo = Text(r.PreviousPolicyNo);
        r.Salutation = Text(r.Salutation); r.Address1 = Text(r.Address1); r.Address2 = Text(r.Address2); r.Address3 = Text(r.Address3);
        r.City = Text(r.City); r.PostCode = Text(r.PostCode); r.State = Text(r.State);
        r.TelHome = Text(r.TelHome); r.TelMobile = Text(r.TelMobile); r.TelOffice = Text(r.TelOffice); r.Email = Text(r.Email);
        r.GroupCompany = Text(r.GroupCompany); r.EmployeeNo = Text(r.EmployeeNo); r.Department = Text(r.Department);
        r.Branch = Text(r.Branch); r.AgentCode = Text(r.AgentCode);
        r.Exclusion = Text(r.Exclusion); r.Allergic = Text(r.Allergic); r.Remarks = Text(r.Remarks);
        foreach (var c in r.CoveredPersons)
        {
            c.Relationship = Code(c.Relationship); c.Sex = Code(c.Sex); c.Name = Text(c.Name); c.IcBcPp = Text(c.IcBcPp);
            c.Salutation = Text(c.Salutation); c.Occupation = Text(c.Occupation); c.Exclusion = Text(c.Exclusion); c.Allergic = Text(c.Allergic);
        }
    }

    private static string Flag(string? value, string fallback = "N") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
    private static string S(string? value) => value ?? "";

    private static async Task<Computation> ComputeAsync(SqlConnection conn, SqlTransaction? tx, RegistrationRequest r)
    {
        var hlt = r.HealthCode!; var pay = r.PayorCode!; var plan = r.PlanCode!; var ins = r.InsuredType!;
        var eff = r.PayorEffectiveDate!.Value.Date; var exp = r.PayorExpiryDate!.Value.Date;

        var payor = await conn.QueryFirstOrDefaultAsync<Payor>(
            "SELECT TOP 1 RTRIM(PAYMBMRegStatus) AS RegStatus FROM dbo.PAY WHERE PAYCode = @pay", new { pay }, tx)
            ?? throw new RegistrationException($"Payor {pay} was not found.");

        var p = await conn.QueryFirstOrDefaultAsync<Plan>(
            """
            SELECT TOP 1 GRPCompany, PRMInd, MCOInd, AnnLmtIND, PLNLifeTimeStatus, PLNProRate, PLNRoundUpStatus, PLNPremGenderStatus
            FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @hlt AND (@hlt = 'S' OR PAYCode = @pay)
            ORDER BY PLNEffDate DESC
            """, new { plan, hlt, pay }, tx)
            ?? throw new RegistrationException($"Plan {plan} is not set up for payor {pay} and health code {hlt}.");

        var lifetime = Flag(p.PLNLifeTimeStatus) == "Y";
        if (lifetime && r.GuaranteeRenewal is null)
            throw new RegistrationException($"Plan {plan} is a lifetime plan. Please select Guarantee Renewal.");

        var bands = (await conn.QueryAsync<LegacyRules.AgeBand>(
            """
            SELECT RTRIM(AGECode) AS AgeCode, RTRIM(AGEDescription) AS Description, RTRIM(AGEFrom) AS AgeFrom, RTRIM(AGETo) AS AgeTo, RTRIM(PLNCode) AS PlanCode
            FROM dbo.AGE WHERE PLNCode = @plan OR PLNCode IS NULL
            """, new { plan }, tx)).ToList();

        var limit = await conn.QueryFirstOrDefaultAsync<Limit>(
            """
            SELECT TOP 1 SuppLimitStatus, AnnLimit, SuppLimit, LifeTimeLimit, SuppLifeTimeLimit FROM dbo.AnnualLimit
            WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnualEffDate <= @eff ORDER BY AnnualEffDate DESC
            """, new { ins, plan, hlt, eff }, tx);

        const string mcoColumns = "SELECT TOP 1 SuppMcoStatus, CovIDChrg, MCOAmountAdult, MCOAmountChild, MCOSuppAdultAmt, MCOSuppChildAmt FROM dbo.MCO";
        var mco = await conn.QueryFirstOrDefaultAsync<Mco>(
                      $"{mcoColumns} WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND MCOEffDate <= @eff ORDER BY MCOEffDate DESC",
                      new { ins, hlt, plan, eff }, tx)
                  ?? await conn.QueryFirstOrDefaultAsync<Mco>(
                      $"{mcoColumns} WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode IS NULL AND MCOEffDate <= @eff ORDER BY MCOEffDate DESC",
                      new { ins, hlt, eff }, tx);

        Task<Premium?> PremiumFor(string ageCode) => conn.QueryFirstOrDefaultAsync<Premium>(
            """
            SELECT TOP 1 SuppPrmStatus, PRMAmount, PRMAmountFemale, SuppPrmAmt FROM dbo.PRM
            WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @ageCode AND PRMEffDate <= @eff
              AND (@hlt = 'S' OR PAYCode = @pay)
            ORDER BY PRMEffDate DESC
            """, new { ins, hlt, plan, ageCode, eff, pay }, tx);

        var proRate = Flag(p.PLNProRate, "Y") != "N";
        var roundUp = Flag(p.PLNRoundUpStatus, "Y") != "N";
        var genderRates = Flag(p.PLNPremGenderStatus) == "Y";
        var warnings = new List<string>();

        decimal Mco(decimal basic, DateTime from, DateTime to)
        {
            var value = proRate ? LegacyRules.Money(LegacyRules.ProRate(from, to, basic)) : basic;
            return roundUp ? LegacyRules.RoundWhole(value) : value;
        }

        var band = LegacyRules.PickAgeBand(bands, LegacyRules.Age(eff, r.DateOfBirth!.Value.Date), plan)
            ?? throw new RegistrationException("Age band not found for the member's age on the Payor Effective Date. Check the age band setup.");
        var ageCode = band.AgeCode.Trim();
        var adultChild = ageCode == "01" ? "C" : "A";

        decimal basicPremium = 0, premium = 0, basicMco = 0, mcoFee = 0, annual = 0;
        if (Flag(p.PRMInd) == "Y")
        {
            var row = await PremiumFor(ageCode);
            if (row is null) warnings.Add($"No premium rate found for age band {ageCode}; premium set to 0.");
            basicPremium = LegacyRules.Money((genderRates && r.Sex == "F" ? row?.PRMAmountFemale : row?.PRMAmount) ?? 0);
            premium = LegacyRules.Money(LegacyRules.ProRate(eff, exp, basicPremium));
        }
        if (Flag(p.MCOInd) == "Y")
        {
            if (mco is null) warnings.Add("No MCO fee found for this plan; MCO set to 0.");
            basicMco = LegacyRules.Money((adultChild == "C" ? mco?.MCOAmountChild : mco?.MCOAmountAdult) ?? 0);
            mcoFee = Mco(basicMco, eff, exp);
        }
        if (Flag(p.AnnLmtIND) == "Y")
        {
            if (limit is null) warnings.Add($"Annual limit is not set up for plan {plan}; limit set to 0.");
            annual = limit?.AnnLimit ?? 0;
        }

        var result = new Computation
        {
            Plan = p, PayorRegStatus = S(payor.RegStatus), Limit = limit, Lifetime = lifetime,
            AgeCode = ageCode, AdultChild = adultChild,
            MemberType = Flag(limit?.SuppLimitStatus, "A") != "A" ? "Q" : "P",
            BasicPremium = basicPremium, Premium = premium, BasicMco = basicMco, Mco = mcoFee, AnnualLimit = annual,
        };
        result.Warnings.AddRange(warnings);

        for (var i = 0; i < r.CoveredPersons.Count; i++)
        {
            var c = r.CoveredPersons[i];
            var coverId = (i + 1).ToString("00");
            var from = (c.EffectiveDate ?? eff).Date;
            var to = (c.ExpiryDate ?? exp).Date;
            var covBand = LegacyRules.PickAgeBand(bands, LegacyRules.Age(from, c.DateOfBirth!.Value.Date), plan)
                ?? throw new RegistrationException($"Covered person {i + 1}: age band not found for this age. Check the age band setup.");
            var person = new Person
            {
                CoverId = coverId, Request = c, Effective = from, Expiry = to,
                AgeCode = covBand.AgeCode.Trim(), AdultChild = covBand.AgeCode.Trim() == "01" ? "C" : "A",
            };

            if (Flag(p.PRMInd) == "Y" && await PremiumFor(person.AgeCode) is { } row
                && PremiumCharged(Flag(row.SuppPrmStatus, "A"), coverId))
            {
                person.BasicPremium = LegacyRules.Money((genderRates && c.Sex == "F" ? row.PRMAmountFemale : row.SuppPrmAmt) ?? 0);
                person.Premium = LegacyRules.Money(LegacyRules.ProRate(eff, exp, person.BasicPremium));
            }
            if (Flag(p.MCOInd) == "Y" && mco is not null && Charged(Flag(mco.SuppMcoStatus, "A"), coverId, mco.CovIDChrg))
            {
                person.BasicMco = LegacyRules.Money((person.AdultChild == "C" ? mco.MCOSuppChildAmt : mco.MCOSuppAdultAmt) ?? 0);
                person.Mco = Mco(person.BasicMco, from, to);
            }
            if (Flag(p.AnnLmtIND) == "Y" && limit is not null && Charged(Flag(limit.SuppLimitStatus, "A"), coverId, null))
                person.AnnualLimit = person.AvailableLimit = limit.SuppLimit ?? 0;

            result.Covered.Add(person);
        }
        return result;
    }

    /// <summary>Legacy supplementary status: A = principal only, B = first covered person only, C = everyone, D = from CovIDChrg onwards.</summary>
    /// <summary>Supplementary premium is only charged for status C, or B on the first covered person (no D rule, unlike MCO).</summary>
    internal static bool PremiumCharged(string status, string coverId) => status == "C" || (status == "B" && coverId == "01");

    internal static bool Charged(string status, string coverId, string? chargeFrom) => status switch
    {
        "A" => false,
        "B" => coverId == "01",
        "D" => string.CompareOrdinal(coverId, string.IsNullOrWhiteSpace(chargeFrom) ? "00" : chargeFrom.Trim()) >= 0,
        _ => true,
    };

    private static RegistrationQuote ToQuote(Computation c) => new(
        c.AgeCode, c.AdultChild, c.MemberType, c.Lifetime, c.BasicPremium, c.Premium, c.BasicMco, c.Mco, c.AnnualLimit,
        c.Covered.Select(p => new CoveredPersonQuote(p.CoverId, S(p.Request.Name), p.AgeCode, p.AdultChild,
            p.BasicPremium, p.Premium, p.BasicMco, p.Mco, p.AnnualLimit)).ToList(),
        c.Warnings);

    internal static async Task<RegistrationResult> SaveAsync(SqlConnection conn, SqlTransaction tx, string maintenanceDb,
        RegistrationRequest r, string userCode)
    {
        var c = await ComputeAsync(conn, tx, r);
        var hlt = r.HealthCode!; var pay = r.PayorCode!; var plan = r.PlanCode!; var ins = r.InsuredType!;
        var eff = r.PayorEffectiveDate!.Value.Date; var exp = r.PayorExpiryDate!.Value.Date;
        var guarantee3 = r.GuaranteeRenewal?.StartsWith('3') == true;

        var number = await MembershipNumbers.GenerateAsync(conn, tx, maintenanceDb, pay, ins, r.DateOfBirth!.Value.Date,
            r.Name!, r.PolicyNo!, r.Renewal!, byPolicy: c.PayorRegStatus == "P");
        var today = await conn.ExecuteScalarAsync<DateTime>("SELECT CAST(GETDATE() AS date)", transaction: tx);

        // Lifetime plans carry the limit forward from the member's previous active policy (PrincipalLifeTimeChecking).
        var topupNo = ""; var topupInd = "N"; var previousNo = "";
        var available = c.AnnualLimit;
        Benefit? benefit = null;
        if (c.Lifetime)
        {
            benefit = await conn.QueryFirstOrDefaultAsync<Benefit>(
                """
                SELECT TOP 1 RTRIM(SuppALBStatus) AS SuppBnfStatus, KidDialysisLmt, CancerLmt, HomeNursLmt FROM dbo.BnfAnnLmt
                WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND ALBEffDate <= @eff ORDER BY ALBEffDate DESC
                """, new { ins, plan, hlt, eff }, tx);
            await InsertBenefitLimitAsync(conn, tx, number, "00", benefit);

            topupNo = previousNo = number;
            var previous = await conn.QueryFirstOrDefaultAsync<PreviousLifetime>(
                """
                SELECT TOP 1 MBMNumber, MBMTopupNumber, MBMAvailableLimit FROM dbo.MBM
                WHERE MBMPolicyNo = @policy AND MBMStatus = 'A' AND PAYCode = @pay AND INSCode = @ins ORDER BY MBMPayorEffDate DESC
                """, new { policy = r.PolicyNo, pay, ins }, tx);
            if (previous is not null)
            {
                topupNo = previous.MBMTopupNumber?.Trim() ?? "";
                topupInd = "Y";
                previousNo = previous.MBMNumber.Trim();
                available = previous.MBMAvailableLimit ?? 0;
                if (!guarantee3) c.AnnualLimit = available;
            }
            (_, available) = await SaveLifetimeAsync(conn, tx, c, topupNo, "00", principal: true, c.AnnualLimit, available, guarantee3);
        }

        await conn.ExecuteAsync(
            """
            EXEC dbo.Upload_MBM_HIS @pay, @hlt, @number, @policy, '00', @ic, @name, @dob, @sex, @race, 'A', 'A', @memberType,
                @add1, @add2, @add3, @city, @postCode, @state, 'N', @today, @eff, @exp, @takeOver, @renewal, @bordx, @batch,
                @plan, @ins, @ageCode, @adultChild, @available, 'H', @topupNo, 'N'
            """,
            new
            {
                pay, hlt, number, policy = r.PolicyNo, ic = r.IcBcPp, name = r.Name, dob = r.DateOfBirth!.Value.Date, sex = S(r.Sex),
                race = S(r.RaceCode), memberType = c.MemberType, add1 = r.Address1, add2 = S(r.Address2), add3 = S(r.Address3),
                city = S(r.City), postCode = r.PostCode, state = r.State, today, eff, exp, takeOver = r.TakeOver, renewal = r.Renewal,
                bordx = r.BordxDate!.Value.Date, batch = r.BatchNo, plan, ins, ageCode = c.AgeCode, adultChild = c.AdultChild,
                available, topupNo,
            }, tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.MBMTwo (MBMNumber, MBMSalutation, MBMTelnoH, MBMTelNoM, MBMTelNoO, MBMEmail, MBMIcBcPp2nd,
                NATCode, SRVCodeList, LGNCode, MBMRegUser, MBMLastUpdateUser, MBMUploadStatus)
            VALUES (@number, @salutation, @telHome, @telMobile, @telOffice, @email, @otherIc, @nat, 'ME', '', @user, @user, 'M')
            """,
            new
            {
                number, salutation = S(r.Salutation), telHome = S(r.TelHome), telMobile = S(r.TelMobile), telOffice = S(r.TelOffice),
                email = S(r.Email), otherIc = r.OtherIc, nat = S(r.NationalityCode), user = userCode,
            }, tx);

        await conn.ExecuteAsync(
            """
            EXEC dbo.Upload_MBMXref_HIS @number, '00', @policy, @ic, @name, @eff, @exp, @ageCode, @ins, @plan, @bordx,
                @topupNo, @topupInd, 'A', 'A', @dob, @hlt, @pay, @batch
            """,
            new
            {
                number, policy = r.PolicyNo, ic = r.IcBcPp, name = r.Name, eff, exp, ageCode = c.AgeCode, ins, plan,
                bordx = r.BordxDate!.Value.Date, topupNo, topupInd, dob = r.DateOfBirth!.Value.Date, hlt, pay, batch = r.BatchNo,
            }, tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.MBMOthers (MBMNumber, RELCode, MBMGroupCompany, MBMEmployeeNo, MBMAgentCode, MBMPOLSubNo1, MBMBranch,
                MBMPremium, MBMMCOFee, MBMBasicMCO, MBMBasicPrem, MBMDepartment, MBMInstallment, MBMUndExcess,
                MBMFirstMemNo, MBMFirstPolNo, MBMPrevPolNo, MBMPrevMemNo, MBMFirstJoinedDate, MBMPolicyDisc, MBMRenewalDisc,
                MBMPolCondition, MBMGuaRenewal, MBMENDtRefNo, MCOVoidStatus, MBMInvoiceNo, MBMPosition, MBMGroupCategory,
                MBMDivision, MBMSubDivision, MBMCostCenter)
            VALUES (@number, 'P', @groupCompany, @employeeNo, @agentCode, '', @branch,
                @premium, @mco, @basicMco, @basicPremium, @department, @installment, 0,
                '', '', @prevPolicy, @prevMember, @joined, 0, 0,
                '', @guarantee, '', '', '', '', '', '', '', '')
            """,
            new
            {
                number, groupCompany = r.GroupCompany ?? c.Plan.GRPCompany?.Trim() ?? "", employeeNo = S(r.EmployeeNo),
                agentCode = S(r.AgentCode), branch = S(r.Branch), premium = c.Premium, mco = c.Mco, basicMco = c.BasicMco,
                basicPremium = c.BasicPremium, department = S(r.Department), installment = S(r.InstallmentMode),
                prevPolicy = S(r.PreviousPolicyNo), prevMember = S(r.PreviousMembershipNo), joined = r.DateJoined?.Date,
                guarantee = S(r.GuaranteeRenewal),
            }, tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.MBMOthersTwo (MBMNumber, MBMWeight, MBMHeight, MBMOccupation, MBMWorkNature, MBMBlood, MBMSmoking, MBMAlcohol,
                MBMPrevPLNCode, MBMPAYRemarks, MBMPrevInsCom, MBMMaritalStatus, MBMCoPayRB, MBMStaffDec,
                MBMPayorPremGross, MBMPayorPremNet, MBMPayorMCOGross, MBMPayorMCONet, MBMRBAmt, MBMFamDiscAmt, MBMRenewDiscAmt,
                MBMLoadingPrem, MBMExclusion, MBMBankACNo, MBMAllergic, MBMRemarks)
            VALUES (@number, '', '', '', '', '', '', '', '', '', '', @marital, '', '', 0, 0, 0, 0, 0, 0, 0, 0,
                @exclusion, '', @allergic, @remarks)
            """,
            new { number, marital = S(r.MaritalStatus), exclusion = S(r.Exclusion), allergic = S(r.Allergic), remarks = S(r.Remarks) }, tx);

        var generatedCode = await MembershipNumbers.NextSecurityCodeAsync(conn, tx);
        var baseNumber = number.Split('*')[0];
        var securityCode = await conn.ExecuteScalarAsync<string>(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.MBMSCSecurity WITH (UPDLOCK, HOLDLOCK) WHERE MBMNumber = @baseNumber)
                INSERT INTO dbo.MBMSCSecurity (MBMNumber, MBMSecurityCode, MBMCardType, MBMClientType) VALUES (@baseNumber, @generatedCode, 'M', 'MED');
            SELECT TOP 1 RTRIM(MBMSecurityCode) FROM dbo.MBMSCSecurity WHERE MBMNumber = @baseNumber;
            """, new { baseNumber, generatedCode }, tx);

        foreach (var person in c.Covered)
        {
            var cr = person.Request;
            if (c.Lifetime && r.GuaranteeRenewal is not null)
            {
                var status = benefit is null ? "A" : Flag(benefit.SuppBnfStatus, "A");
                if ((status == "B" && person.CoverId == "01") || status == "C")
                {
                    await InsertBenefitLimitAsync(conn, tx, number, person.CoverId, benefit);
                    var carried = await conn.QueryFirstOrDefaultAsync<Amount>(
                        "SELECT TOP 1 MBMCAvailableLimit AS Value FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @previousNo AND MBMCCoverID = @coverId",
                        new { previousNo, coverId = person.CoverId }, tx);
                    var next = 0m;
                    if (carried is not null)
                    {
                        next = carried.Value ?? 0;
                        if (!guarantee3) person.AnnualLimit = next;
                    }
                    (person.AnnualLimit, _) = await SaveLifetimeAsync(conn, tx, c, topupNo, person.CoverId, principal: false,
                        person.AnnualLimit, next, guarantee3);
                    person.AvailableLimit = person.AnnualLimit;
                }
            }

            await conn.ExecuteAsync(
                """
                EXEC dbo.Upload_MBMCovPersonsNew @rel, @number, @coverId, @dob, 'C', @name, 'A', @bordx, @batch, @ic, @sex,
                    @plan, @ageCode, @adultChild, @annual, @available, @basicMco, @mco, @basicPremium, @premium, @today, @eff, @exp
                """,
                new
                {
                    rel = cr.Relationship, number, coverId = person.CoverId, dob = cr.DateOfBirth!.Value.Date, name = cr.Name,
                    bordx = r.BordxDate!.Value.Date, batch = r.BatchNo, ic = S(cr.IcBcPp), sex = S(cr.Sex), plan,
                    ageCode = person.AgeCode, adultChild = person.AdultChild, annual = person.AnnualLimit, available = person.AvailableLimit,
                    basicMco = person.BasicMco, mco = person.Mco, basicPremium = person.BasicPremium, premium = person.Premium,
                    today, eff = person.Effective, exp = person.Expiry,
                }, tx);

            // Upload_MBMCovPersonsNew takes a char(1) relationship, which would store SP (spouse) as S (son).
            if (cr.Relationship!.Length > 1)
                await conn.ExecuteAsync(
                    "UPDATE dbo.MBMCoveredPersons SET MBMCRELCode = @rel WHERE MBMCNumber = @number AND MBMCCoverID = @coverId",
                    new { rel = cr.Relationship, number, coverId = person.CoverId }, tx);

            await conn.ExecuteAsync(
                """
                EXEC dbo.Upload_MBMCovPersonsTwo @number, @coverId, @salutation, @occupation, '', '', @allergic, '', '', '', '', '',
                    @exclusion, '', '', '', '', 0, 0, 0, 0, '', 0, 0, 0, 0, 0, NULL, NULL, 0, 0
                """,
                new
                {
                    number, coverId = person.CoverId, salutation = S(cr.Salutation), occupation = S(cr.Occupation),
                    allergic = S(cr.Allergic), exclusion = S(cr.Exclusion),
                }, tx);
        }

        await conn.ExecuteAsync(
            """
            UPDATE dbo.BAT WITH (UPDLOCK, HOLDLOCK) SET BATSequence = @batch
            WHERE PAYCode = @pay AND BATBordxDate = @bordx AND BordxType = 'N';
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.BAT (PAYCode, BATBordxDate, BATSequence, BordxType) VALUES (@pay, @bordx, @batch, 'N');
            """, new { pay, bordx = r.BordxDate!.Value.Date, batch = r.BatchNo }, tx);

        return new RegistrationResult(number, securityCode ?? generatedCode, ToQuote(c));
    }

    private static Task InsertBenefitLimitAsync(SqlConnection conn, SqlTransaction tx, string number, string coverId, Benefit? b) =>
        conn.ExecuteAsync(
            """
            INSERT INTO dbo.MBMBnfLmt (MBMNumber, MBMCoveredID, MBMKidneyAnnLmt, MBMKidneyBalLmt, MBMCancerAnnLmt, MBMCancerBalLmt,
                MBMHomeNCAnnLmt, MBMHomeNCBalLmt)
            VALUES (@number, @coverId, @kidney, @kidney, @cancer, @cancer, @homeNursing, @homeNursing)
            """,
            new { number, coverId, kidney = b?.KidDialysisLmt ?? 0, cancer = b?.CancerLmt ?? 0, homeNursing = b?.HomeNursLmt ?? 0 }, tx);

    /// <summary>SaveMBMLifeTimeLmt: caps the limit by the remaining lifetime balance, or opens a new lifetime balance.</summary>
    private static async Task<(decimal Actual, decimal Next)> SaveLifetimeAsync(SqlConnection conn, SqlTransaction tx, Computation c,
        string topupNo, string coverId, bool principal, decimal actual, decimal next, bool guarantee3)
    {
        var existing = await conn.QueryFirstOrDefaultAsync<Amount>(
            "SELECT TOP 1 MBMLifeTimeLmt AS Value FROM dbo.MBMLifeTime WHERE MBMNumber = @topupNo AND MBMCovID = @coverId",
            new { topupNo, coverId }, tx);
        if (existing is null)
        {
            if (guarantee3) c.WriteLifetime = true;
        }
        else
        {
            next = actual;
            if (actual > (existing.Value ?? 0)) actual = existing.Value ?? 0;
        }

        if (c.WriteLifetime && existing is null)
        {
            var lifetimeLimit = (principal ? c.Limit?.LifeTimeLimit : c.Limit?.SuppLifeTimeLimit) ?? 0;
            await conn.ExecuteAsync(
                "INSERT INTO dbo.MBMLifeTime (MBMNumber, MBMCovID, MBMLifeTimeLmt) VALUES (@topupNo, @coverId, @balance)",
                new { topupNo, coverId, balance = lifetimeLimit - (actual - next) }, tx);
            next = c.AnnualLimit;
        }
        return (actual, next);
    }
}
