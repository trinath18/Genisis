using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Plans;

/// <summary>Maintenance > Plan > Plan tab (legacy frmMaintenencePlan: SavePLN, SearchPLN, DeletePLN).</summary>
public class PlanService(DbConnections db)
{
    public const int SearchLimit = 500;

    public async Task<List<CreatedPlan>> CreateAsync(PlanCreateRequest request, string userCode)
    {
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var created = await SaveAsync(conn, tx, maintenanceDb, request, userCode);
        await tx.CommitAsync();
        return created;
    }

    internal static async Task<List<CreatedPlan>> SaveAsync(SqlConnection conn, SqlTransaction tx, string maintenanceDb,
        PlanCreateRequest r, string userCode)
    {
        static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
        var health = U(r.HealthCode)!;
        var payor = U(r.PayorCode) ?? "";
        var productCategory = U(r.ProductCategory)!;

        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.HLT WHERE HLTCode = @health", new { health }, tx) == 0)
            throw new PlanException("Health Type was not found.");
        if (payor != "" && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PAY WHERE PAYCode = @payor", new { payor }, tx) == 0)
            throw new PlanException("Payor Code was not found.");
        if (await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {maintenanceDb}.dbo.ProductCategory WHERE ProductCode = @productCategory",
                new { productCategory }, tx) == 0)
            throw new PlanException("Product Category was not found.");

        var lines = health == "N"
            ? Enumerable.Range(0, r.NumberOfPlans!.Value).Select(i => (Code: (string?)null, Description: i < r.Plans.Count ? r.Plans[i].Description : null)).ToList()
            : r.Plans.Where(p => !string.IsNullOrWhiteSpace(p.Code)).Select(p => (Code: U(p.Code), p.Description)).ToList();

        // ET/EC and TK plans get fixed LG/co-pay defaults in the desktop.
        var etEc = payor.StartsWith("ET") || payor.StartsWith("EC");
        var created = new List<CreatedPlan>();
        foreach (var line in lines)
        {
            var code = line.Code ?? await NextPlanCodeAsync(conn, tx, (r.GroupCompany ?? "")[..1].Trim().ToUpperInvariant());
            var description = U(line.Description);
            var index = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO dbo.PLN (PLNCode, PLNDescription, HLTCode, PAYCode, GRPCompany, PLNTopUPStatus, AnnLmtIND, PRMInd, MCOInd,
                    CoPaymentInd, PLNMeal, PLNNursing, PLNTax, PLNMRI, PLNSOF, NewSMPlan, SpeGPeriodStatus, PLNLifeTimeStatus, PLNProCat,
                    SpeGPeriodDays, PLNEffDate, PLNMgmtFee, PLNLastUpdateUser, PLNLastUpdateDate, PLNDisIndicator, PLNStartAge, PLNEndAge,
                    PLNPolicyWording, PLNPayorPlan, PLNPolNo, PLNCoPayPerc, PLNLGGov, PLNLGPrivate, PLNExcessPrivate)
                VALUES (@code, @description, @health, @payor, @groupCompany, @topUp, @annualLimit, @premium, @mco,
                    @coPayment, @meal, @nursing, @tax, @mri, @sof, @smPlan, @gracePeriod, @lifetime, @productCategory,
                    @graceDays, @effective, @managementFee, @userCode, @today, @disIndicator, @startAge, @endAge,
                    @policyWording, @clientPlan, @clientPolicyNo, @coPayPercent, @lgGov, @lgPrivate, @excessPrivate);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """,
                new
                {
                    code, description, health, payor, groupCompany = U(r.GroupCompany),
                    topUp = U(r.TopUpStatus), annualLimit = U(r.AnnualLimitInd), premium = U(r.PremiumInd), mco = U(r.McoInd),
                    coPayment = etEc ? "Y" : U(r.CoPayment), meal = U(r.Meal), nursing = U(r.Nursing), tax = U(r.Tax), mri = U(r.Mri),
                    sof = U(r.Sof) ?? "", smPlan = U(r.SmPlan) ?? "", gracePeriod = U(r.SpecialGracePeriod), lifetime = U(r.LifetimeStatus),
                    productCategory, graceDays = (decimal?)r.SpecialGracePeriodDays, effective = r.EffectiveDate!.Value.Date,
                    managementFee = U(r.ManagementFee), userCode, today = DateTime.Today, disIndicator = U(r.DisIndicator),
                    startAge = r.StartAge, endAge = r.EndAge, policyWording = r.PolicyWording!.Trim(),
                    clientPlan = U(r.ClientPlan), clientPolicyNo = string.IsNullOrWhiteSpace(r.ClientPolicyNo) ? null : r.ClientPolicyNo.Trim(),
                    coPayPercent = etEc ? 20 : r.CoPayPercent,
                    lgGov = etEc ? "5" : null,
                    lgPrivate = etEc ? 5 : payor.StartsWith("TK") ? 9 : (int?)null,
                    excessPrivate = etEc ? "2" : null,
                },
                tx);
            created.Add(new CreatedPlan(index, code, description));
        }
        return created;
    }

    /// <summary>Legacy GeneratePLNSeq: per-letter counter in PLNSeq, three digits ("A001".."A999").</summary>
    internal static async Task<string> NextPlanCodeAsync(SqlConnection conn, SqlTransaction tx, string letter)
    {
        var row = await conn.QuerySingleOrDefaultAsync<(int Index, string? Seq)>(
            "SELECT TOP 1 PLNIndex, PLNNoSeq FROM dbo.PLNSeq WITH (UPDLOCK, HOLDLOCK) WHERE PLNSeqCode = @letter ORDER BY PLNIndex",
            new { letter }, tx);
        if (row.Index == 0)
        {
            await conn.ExecuteAsync("INSERT INTO dbo.PLNSeq (PLNNoSeq, PLNSeqCode) VALUES ('001', @letter)", new { letter }, tx);
            return letter + "001";
        }
        var next = (int.TryParse(row.Seq?.Trim(), out var current) ? current : 0) + 1;
        if (next > 999)
            throw new PlanException($"Plan codes for group companies starting with '{letter}' are used up ({letter}999). Use a group company name that starts with another letter.");
        var seq = next.ToString("000");
        await conn.ExecuteAsync("UPDATE dbo.PLNSeq SET PLNNoSeq = @seq WHERE PLNIndex = @index", new { seq, index = row.Index }, tx);
        return letter + seq;
    }

    public async Task<(List<PlanRow> Rows, bool Truncated)> SearchAsync(PlanSearch s)
    {
        static string? V(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
        await using var conn = db.MainDb();
        var rows = (await conn.QueryAsync<PlanRow>(
            $"""
            SELECT TOP ({SearchLimit + 1}) PLNIndex AS [Index], RTRIM(HLTCode) AS HealthCode, RTRIM(PLNCode) AS Code, RTRIM(NewPLNCode) AS NewCode,
                PLNDescription AS Description, RTRIM(PAYCode) AS PayorCode, GRPCompany AS GroupCompany, PLNTopUPStatus AS TopUpStatus,
                AnnLmtIND AS AnnualLimitInd, PRMInd AS PremiumInd, MCOInd AS McoInd, CoPaymentInd AS CoPayment, PLNEffDate AS EffectiveDate,
                SpeGPeriodStatus AS SpecialGracePeriod, SpeGPeriodDays AS SpecialGracePeriodDays, NewSMPlan AS SmPlan,
                PLNLifeTimeStatus AS LifetimeStatus, PLNSOF AS Sof, RTRIM(PLNProCat) AS ProductCategory, PLNMeal AS Meal, PLNNursing AS Nursing,
                PLNTax AS Tax, PLNMRI AS Mri, PLNMgmtFee AS ManagementFee, PLNDisIndicator AS DisIndicator, PLNStartAge AS StartAge,
                PLNEndAge AS EndAge, PLNPolicyWording AS PolicyWording, PLNPayorPlan AS ClientPlan, PLNPolNo AS ClientPolicyNo,
                PLNCoPayPerc AS CoPayPercent
            FROM dbo.PLN
            WHERE (@health IS NULL OR HLTCode = @health)
              AND (@payor IS NULL OR PAYCode = @payor)
              AND (@group IS NULL OR GRPCompany LIKE '%' + @group + '%')
              AND (@plan IS NULL OR PLNCode LIKE '%' + @plan + '%')
              AND (@productCategory IS NULL OR PLNProCat = @productCategory)
              AND (@topUp IS NULL OR PLNTopUPStatus = @topUp)
              AND (@coPayment IS NULL OR CoPaymentInd = @coPayment)
              AND (@sof IS NULL OR PLNSOF = @sof)
              AND (@gracePeriod IS NULL OR SpeGPeriodStatus = @gracePeriod)
              AND (@meal IS NULL OR PLNMeal = @meal)
              AND (@nursing IS NULL OR PLNNursing = @nursing)
              AND (@tax IS NULL OR PLNTax = @tax)
              AND (@mri IS NULL OR PLNMRI = @mri)
              AND (@disIndicator IS NULL OR PLNDisIndicator = @disIndicator)
            ORDER BY PLNIndex DESC
            """,
            new
            {
                health = V(s.HealthCode), payor = V(s.PayorCode), group = V(s.GroupCompany), plan = V(s.PlanCode),
                productCategory = V(s.ProductCategory), topUp = V(s.TopUpStatus), coPayment = V(s.CoPayment), sof = V(s.Sof),
                gracePeriod = V(s.SpecialGracePeriod), meal = V(s.Meal), nursing = V(s.Nursing), tax = V(s.Tax), mri = V(s.Mri),
                disIndicator = V(s.DisIndicator),
            })).ToList();
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }

    /// <summary>Deletes one plan row; refused while any member is on that plan code.</summary>
    public async Task DeleteAsync(int index)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var code = await conn.ExecuteScalarAsync<string?>("SELECT PLNCode FROM dbo.PLN WITH (UPDLOCK) WHERE PLNIndex = @index", new { index }, tx)
            ?? throw new PlanException("Record not found");
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM (SELECT TOP 1 1 AS x FROM dbo.MBM WHERE PLNCode = @code) m", new { code }, tx) > 0)
            throw new PlanException("This record cannot be deleted because it is used in Membership table!");
        await conn.ExecuteAsync("DELETE FROM dbo.PLN WHERE PLNIndex = @index", new { index }, tx);
        await tx.CommitAsync();
    }
}
