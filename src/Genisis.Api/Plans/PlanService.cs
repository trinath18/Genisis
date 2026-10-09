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
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var created = await SaveAsync(conn, tx, request, userCode);
        await tx.CommitAsync();
        return created;
    }

    internal static async Task<List<CreatedPlan>> SaveAsync(SqlConnection conn, SqlTransaction tx, PlanCreateRequest r, string userCode)
    {
        static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
        var health = U(r.HealthCode)!;
        var payor = U(r.PayorCode) ?? "";
        var productCategory = U(r.ProductCategory)!;

        var refs = await conn.ProcSingleAsync<(bool Health, bool Payor, bool ProductCategory)>(
            "genisis.Plan_ReferencesExist", new { health, payor, productCategory }, tx);
        if (!refs.Health) throw new PlanException("Health Type was not found.");
        if (payor != "" && !refs.Payor) throw new PlanException("Payor Code was not found.");
        if (!refs.ProductCategory) throw new PlanException("Product Category was not found.");

        var lines = health == "N"
            ? Enumerable.Range(0, r.NumberOfPlans!.Value).Select(i => (Code: (string?)null, Description: i < r.Plans.Count ? r.Plans[i].Description : null)).ToList()
            : r.Plans.Where(p => !string.IsNullOrWhiteSpace(p.Code)).Select(p => (Code: U(p.Code), p.Description)).ToList();

        // ET/EC and TK plans get fixed LG/co-pay defaults in the desktop.
        var etEc = payor.StartsWith("ET") || payor.StartsWith("EC");
        var created = new List<CreatedPlan>();
        foreach (var line in lines)
        {
            var code = line.Code ?? await NextPlanCodeAsync(conn, tx, U(r.GroupCompany)![..1]);
            var description = U(line.Description);
            var index = await conn.ProcScalarAsync<int>("genisis.Plan_Insert",
                new
                {
                    code, description, health, payor, groupCompany = U(r.GroupCompany),
                    topUp = U(r.TopUpStatus), annualLimit = U(r.AnnualLimitInd), premium = U(r.PremiumInd), mco = U(r.McoInd),
                    coPayment = etEc ? "Y" : U(r.CoPayment), meal = U(r.Meal), nursing = U(r.Nursing), tax = U(r.Tax), mri = U(r.Mri),
                    sof = U(r.Sof) ?? "", smPlan = U(r.SmPlan) ?? "", gracePeriod = U(r.SpecialGracePeriod), lifetime = U(r.LifetimeStatus),
                    productCategory, graceDays = (decimal?)r.SpecialGracePeriodDays, effective = r.EffectiveDate!.Value.Date,
                    managementFee = U(r.ManagementFee), userCode, disIndicator = U(r.DisIndicator),
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
        var next = await conn.ProcScalarAsync<int>("genisis.Plan_NextCode", new { letter }, tx);
        if (next > 999)
            throw new PlanException($"Plan codes for group companies starting with '{letter}' are used up ({letter}999). Use a group company name that starts with another letter.");
        return letter + next.ToString("000");
    }

    public async Task<(List<PlanRow> Rows, bool Truncated)> SearchAsync(PlanSearch s)
    {
        static string? V(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
        static string? Like(string? v) => V(v) is { } x ? SqlText.EscapeLike(x) : null;
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<PlanRow>("genisis.Plan_Search",
            new
            {
                top = SearchLimit + 1,
                health = V(s.HealthCode), payor = V(s.PayorCode), group = Like(s.GroupCompany), plan = Like(s.PlanCode),
                productCategory = V(s.ProductCategory), topUp = V(s.TopUpStatus), coPayment = V(s.CoPayment), sof = V(s.Sof),
                gracePeriod = V(s.SpecialGracePeriod), meal = V(s.Meal), nursing = V(s.Nursing), tax = V(s.Tax), mri = V(s.Mri),
                disIndicator = V(s.DisIndicator),
            });
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }

    /// <summary>Deletes one plan row; refused while any member is on that plan code.</summary>
    public async Task DeleteAsync(int index)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        switch (await conn.ProcScalarAsync<int>("genisis.Plan_Delete", new { index }, tx))
        {
            case StoredProcedures.NotFound: throw new PlanException("Record not found");
            case StoredProcedures.Conflict: throw new PlanException("This record cannot be deleted because it is used in Membership table!");
        }
        await tx.CommitAsync();
    }
}
