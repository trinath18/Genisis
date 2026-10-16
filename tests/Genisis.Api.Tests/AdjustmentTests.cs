using Genisis.Api.Membership;
using Dapper;
using Genisis.Api.Adjustment;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class AdjustmentTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    // An active family member with covered persons and every field the desktop insists on before saving.
    private const string FamilyMember = """
        SELECT TOP (1) m.MBMNumber FROM dbo.MBM m
        JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
        JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
        WHERE m.MBMStatus = 'A' AND m.INSCode IN ('F', 'H') AND m.PLNCode IS NOT NULL AND m.MBMAddress1 <> '' AND m.MBMPostCode <> ''
          AND m.STACode <> '' AND m.MBMIcBcPp <> '' AND m.MBMSex <> '' AND m.MBMBordxDate IS NOT NULL AND m.MBMPolicyNo <> ''
          AND m.MBMPayorEffDate IS NOT NULL AND m.MBMPayorExpDate IS NOT NULL
          AND (SELECT COUNT(*) FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = m.MBMNumber) >= 2
          AND NOT EXISTS (SELECT 1 FROM HISMaintenance.dbo.CASCrossReference x WHERE x.MBMNumber = m.MBMNumber AND x.CASStatus = 'O')
        ORDER BY m.MBMPayorEffDate DESC
        """;

    [Fact]
    public void Refund_formulas_follow_the_desktop()
    {
        var eff = new DateTime(2025, 1, 1); var exp = new DateTime(2025, 12, 31); var cancel = new DateTime(2025, 7, 1);
        // 184 days left including the cancel day, over 365 policy days.
        Assert.Equal(184m / 365m * 365m, FeeRules.McoRefund(exp, cancel, eff, 365));
        // The premium reduction does not count the cancel day.
        Assert.Equal(183m / 365m * 365m, FeeRules.PremiumReduction(exp, cancel, eff, 365));
        Assert.Equal(0, FeeRules.McoRefund(exp, exp.AddDays(5), eff, 100));
        Assert.Equal(0, FeeRules.PremiumReduction(exp, exp, eff, 100));
        Assert.Equal(2, FeeRules.VbRound(2.5m));
        Assert.Equal(4, FeeRules.VbRound(3.5m));
    }

    [Fact]
    public void Supplementary_expires_on_the_age_limit_birthday()
    {
        var eff = new DateTime(2025, 1, 1); var exp = new DateTime(2025, 12, 31);
        var turns23 = AdjustmentRules.SupplementaryExpiry(new DateTime(2002, 6, 15), 23, eff, exp);
        Assert.Equal((new DateTime(2025, 6, 15), false, 23), (turns23.ExpiryDate!.Value, turns23.OverAge, turns23.AgeLimit));
        Assert.True(AdjustmentRules.SupplementaryExpiry(new DateTime(2001, 6, 15), 23, eff, exp).OverAge);
        Assert.Equal(exp, AdjustmentRules.SupplementaryExpiry(new DateTime(2010, 6, 15), 23, eff, exp).ExpiryDate);
        Assert.Equal(exp, AdjustmentRules.SupplementaryExpiry(new DateTime(2001, 6, 15), 0, eff, exp).ExpiryDate);
    }

    private static LoadedMembership Sample(string status = "A", string ins = "H") => new(
        new AdjustmentMember
        {
            MembershipNo = "X*01", Status = status, InsuredType = ins, PlanCode = "BT48", PolicyNo = "P1", DateOfBirth = new DateTime(1980, 1, 1),
            PayorEffectiveDate = new DateTime(2021, 1, 1), PayorExpiryDate = new DateTime(2021, 12, 31), BordxDate = new DateTime(2021, 1, 5),
            Address1 = "A", PostCode = "1", State = "S", IcBcPp = "1", Sex = "M", HealthCode = "N", PayorCode = "AH",
        },
        [new AdjustmentCovered { CoverId = "01", Name = "C", IcBcPp = "2", Relationship = "W" }], null, null);

    private static readonly AdjustmentDateRanges AnyDate = new(null, null, null, null, null, null);

    [Fact]
    public void Messages_follow_desktop()
    {
        var l = Sample();
        AdjustmentRequest Req(string type) => new() { EndorsementType = type, Member = l.Member, Covered = l.Covered };

        Assert.Equal("Please Choose An Endorsement Type", AdjustmentValidator.Validate(l, Req(""), AnyDate));
        Assert.Equal("Please Enter Endt. Bordx Date!", AdjustmentValidator.Validate(l, Req("X"), AnyDate));
        var x = Req("X"); x.EndtBordxDate = DateTime.Today;
        Assert.Equal("Please Enter Endt. Effective Date!", AdjustmentValidator.Validate(l, x, AnyDate));
        x.EndtEffectiveDate = DateTime.Today;
        Assert.Null(AdjustmentValidator.Validate(l, x, AnyDate));

        Assert.Equal("Membership has been cancelled. No further changes are allowed!", AdjustmentValidator.Validate(Sample("C"), x, AnyDate));
        var single = Sample(ins: "I");
        var s = new AdjustmentRequest { EndorsementType = "S", EndtBordxDate = DateTime.Today, EndtEffectiveDate = DateTime.Today, Member = single.Member };
        Assert.Equal("This Plan does not have supplementary", AdjustmentValidator.Validate(single, s, AnyDate));

        var add = Req("S"); add.EndtBordxDate = add.EndtEffectiveDate = DateTime.Today;
        Assert.Equal("Please add at least one supplementary.", AdjustmentValidator.Validate(l, add, AnyDate));
        add.Covered = [.. l.Covered, new AdjustmentCovered { Action = "add", Name = "Kid" }];
        Assert.Equal("Please Enter Covered Person Identification!", AdjustmentValidator.Validate(l, add, AnyDate));

        var t = Req("T"); t.EndtBordxDate = t.EndtEffectiveDate = DateTime.Today;
        Assert.Equal("Please Choose a Record", AdjustmentValidator.Validate(l, t, AnyDate));

        var amend = Req("A"); amend.EndtBordxDate = amend.EndtEffectiveDate = DateTime.Today;
        amend.Member = new AdjustmentMember { PlanCode = "BT48", PolicyNo = "P1", InsuredType = "H", HealthCode = "N", PayorCode = "AH" };
        Assert.Equal("Please Enter Birth Date!", AdjustmentValidator.Validate(l, amend, AnyDate));

        var outOfRange = new AdjustmentDateRanges(null, null, new DateTime(1999, 1, 1), new DateTime(2021, 12, 31), null, null);
        var moved = Req("A"); moved.EndtBordxDate = moved.EndtEffectiveDate = DateTime.Today;
        moved.Member = new AdjustmentMember
        {
            MembershipNo = "X*01", Status = "A", InsuredType = "H", PlanCode = "BT48", PolicyNo = "P1", DateOfBirth = new DateTime(1980, 1, 1),
            PayorEffectiveDate = new DateTime(2025, 1, 1), PayorExpiryDate = new DateTime(2021, 12, 31), BordxDate = new DateTime(2021, 1, 5),
            Address1 = "A", PostCode = "1", State = "S", IcBcPp = "1", Sex = "M", HealthCode = "N", PayorCode = "AH",
        };
        Assert.Equal("Policy Effective Date is out of acceptable range! ", AdjustmentValidator.Validate(l, moved, outOfRange));
    }

    private static async Task<(SqlConnection Conn, SqlTransaction Tx, string Number, LoadedMembership Loaded)?> OpenAsync(string query = FamilyMember)
    {
        if (MainDb is null) return null;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        var tx = conn.BeginTransaction();
        var number = (await conn.ExecuteScalarAsync<string>(query, transaction: tx))!.Trim();
        var loaded = (await AdjustmentService.LoadAsync(conn, tx, number))!;
        return (conn, tx, number, loaded);
    }

    private static AdjustmentRequest Request(LoadedMembership l, string type) => new()
    {
        EndorsementType = type, EndtBordxDate = new DateTime(2021, 9, 1), EndtEffectiveDate = new DateTime(2021, 9, 1), Member = l.Member,
        Covered = l.Covered,
    };

    [Fact]
    public async Task Amendment_updates_principal_and_supplementary_and_writes_history()
    {
        if (await OpenAsync() is not var (conn, tx, number, l)) return;
        await using var _ = conn;
        var r = Request(l, "A");
        r.Member.Address1 = "1 jalan test";
        r.Member.MgmtNotes = "note from test";
        r.Member.Email = "a@b.c";
        r.Covered[0].Allergic = "PENICILLIN";

        var result = await AdjustmentService.SaveAsync(conn, tx, number, r, "TEST");
        Assert.Equal("Updated Sucessfully", result.Message);
        var after = (await AdjustmentService.LoadAsync(conn, tx, number))!;
        Assert.Equal(("1 JALAN TEST", "note from test", "A@B.C", "A"), (after.Member.Address1, after.Member.MgmtNotes, after.Member.Email, after.Member.Status));
        Assert.Equal("PENICILLIN", after.Covered[0].Allergic);
        var history = await conn.QueryFirstAsync<(string Type, string User)>(
            "SELECT TOP 1 MBMAmendEdtType, MBMAmendUser FROM dbo.MBMAmendHistory WHERE MBMNumber = @number ORDER BY MBMAmendID DESC", new { number }, tx);
        Assert.Equal(("A", "TEST"), history);
        Assert.Equal("A", await conn.ExecuteScalarAsync<string>("SELECT MBMLastEndorseStatus FROM dbo.MBMTwo WHERE MBMNumber = @number", new { number }, tx));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Add_cancel_and_delete_supplementary()
    {
        if (await OpenAsync() is not var (conn, tx, number, l)) return;
        await using var _ = conn;
        var add = Request(l, "S");
        add.Covered = [.. l.Covered, new AdjustmentCovered
        {
            Action = "add", Salutation = "MR", Name = "test child", IcBcPp = "150101-01-0001", DateOfBirth = new DateTime(2015, 1, 1), Sex = "M",
            Relationship = "S", Allergic = "NONE",
        }];
        Assert.Equal("Supplementary Added Sucessfully", (await AdjustmentService.SaveAsync(conn, tx, number, add, "TEST"))!.Message);
        var afterAdd = (await AdjustmentService.LoadAsync(conn, tx, number))!;
        var child = Assert.Single(afterAdd.Covered, c => c.Name == "TEST CHILD");
        Assert.Equal(afterAdd.Covered.Count, l.Covered.Count + 1);
        Assert.Equal(("C", "S", "C"), (child.AdultChild, child.Relationship, "C"));
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @number AND MBMCCoverID = @id",
            new { number, id = child.CoverId }, tx));

        var cancel = Request(afterAdd, "K");
        cancel.Covered = afterAdd.Covered.Select(c => { c.Action = c.CoverId == child.CoverId ? "cancel" : null; return c; }).ToList();
        Assert.Equal("Supplementary cancelled Sucessfully", (await AdjustmentService.SaveAsync(conn, tx, number, cancel, "TEST"))!.Message);
        var cancelled = Assert.Single((await AdjustmentService.LoadAsync(conn, tx, number))!.Covered, c => c.CoverId == child.CoverId);
        Assert.Equal(new DateTime(2021, 9, 1), cancelled.CancelDate);
        Assert.Equal("C", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMCStatus) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND MBMCCoverID = @id",
            new { number, id = child.CoverId }, tx));
        Assert.Equal(child.CoverId, await conn.ExecuteScalarAsync<string>("SELECT TOP 1 RTRIM(MBMCoverID) FROM dbo.MBMAmendHistory WHERE MBMNumber = @number AND MBMAmendEdtType = 'K' ORDER BY MBMAmendID DESC",
            new { number }, tx));
        Assert.Equal("A", (await AdjustmentService.LoadAsync(conn, tx, number))!.Member.Status);

        var delete = Request(afterAdd, "T");
        delete.Covered = afterAdd.Covered.Select(c => { c.Action = c.CoverId == child.CoverId ? "remove" : null; return c; }).ToList();
        Assert.Equal("Supplementary Deleted Sucessfully", (await AdjustmentService.SaveAsync(conn, tx, number, delete, "TEST"))!.Message);
        Assert.Equal(l.Covered.Count, (await AdjustmentService.LoadAsync(conn, tx, number))!.Covered.Count);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Cancellation_sets_status_final_expiry_and_refunds()
    {
        if (await OpenAsync() is not var (conn, tx, number, l)) return;
        await using var _ = conn;
        var before = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMRefund WHERE MBMNumber = @number", new { number }, tx);
        Assert.Equal("This Membership has been cancelled successfully", (await AdjustmentService.SaveAsync(conn, tx, number, Request(l, "C"), "TEST"))!.Message);

        var (status, final, cancelDate) = await conn.QueryFirstAsync<(string, DateTime?, DateTime?)>(
            "SELECT RTRIM(m.MBMStatus), m.MBMFinalExpiryDate, t.MBMCancelDate FROM dbo.MBM m JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber WHERE m.MBMNumber = @number",
            new { number }, tx);
        Assert.Equal(("C", new DateTime(2021, 9, 1), new DateTime(2021, 9, 1)), (status, final, cancelDate));
        Assert.Equal("C", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMStatus) FROM HISMaintenance.dbo.MBMCrossReference WHERE MBMNumber = @number", new { number }, tx));
        Assert.All((await AdjustmentService.LoadAsync(conn, tx, number))!.Covered, c => Assert.Equal(new DateTime(2021, 9, 1), c.CancelDate));
        Assert.True(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMRefund WHERE MBMNumber = @number", new { number }, tx) > before);
        var principal = await conn.QueryFirstAsync<(decimal Refund, decimal Effective)>(
            "SELECT TOP 1 MBMMCORefund, MBMMCOEffective FROM dbo.MBMRefund WHERE MBMNumber = @number AND MBMCovID = '00' ORDER BY MIndex DESC", new { number }, tx);
        var fee = FeeRules.VbRound(l.Member.McoFee ?? 0);
        Assert.Equal(fee, principal.Refund + principal.Effective);

        var again = await Assert.ThrowsAsync<AdjustmentException>(() => AdjustmentService.SaveAsync(conn, tx, number, Request(l, "X"), "TEST"));
        Assert.Equal("Membership has been cancelled. No further changes are allowed!", again.Message);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Suspend_and_delete_policy_change_every_status()
    {
        if (await OpenAsync() is not var (conn, tx, number, l)) return;
        await using var _ = conn;
        Assert.Equal("Record is Suspended", (await AdjustmentService.SaveAsync(conn, tx, number, Request(l, "X"), "TEST"))!.Message);
        Assert.Equal("S", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMStatus) FROM dbo.MBM WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND MBMCStatus <> 'S'", new { number }, tx));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Plan_change_marks_principal_and_supplementary_P()
    {
        if (await OpenAsync() is not var (conn, tx, number, l)) return;
        await using var _ = conn;
        var r = Request(l, "P");
        var calc = await AdjustmentService.RecalculateAsync(conn, tx, l, new RecalculateRequest
        {
            PlanCode = l.Member.PlanCode, PayorEffectiveDate = l.Member.PayorEffectiveDate, PayorExpiryDate = l.Member.PayorExpiryDate,
            DateOfBirth = l.Member.DateOfBirth, Sex = l.Member.Sex,
        });
        Assert.False(string.IsNullOrEmpty(calc.AgeCode));
        Assert.Equal("Updated Sucessfully", (await AdjustmentService.SaveAsync(conn, tx, number, r, "TEST"))!.Message);
        Assert.Equal("P", await conn.ExecuteScalarAsync<string>("SELECT RTRIM(MBMStatus) FROM dbo.MBM WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND RTRIM(MBMCStatus) NOT IN ('P', 'K', 'C')", new { number }, tx));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Clinical_member_supplementary_gets_a_clinical_row()
    {
        const string clinical = """
            SELECT TOP (1) m.MBMNumber FROM dbo.MBM m JOIN dbo.MBMCLNOthers x ON x.MBMNumber = m.MBMNumber JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
            JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
            WHERE m.MBMStatus = 'A' AND x.INSType IN ('F', 'H') AND x.MBMPolEffDate IS NOT NULL AND m.MBMAddress1 <> '' AND m.MBMPostCode <> ''
              AND m.STACode <> '' AND m.MBMIcBcPp <> '' AND m.MBMSex <> '' AND m.MBMBordxDate IS NOT NULL AND m.MBMDOB IS NOT NULL
              AND (ISNULL(m.PLNCode, '') = '' OR m.MBMPolicyNo <> '')
            ORDER BY m.MBMNumber DESC
            """;
        if (await OpenAsync(clinical) is not var (conn, tx, number, l)) return;
        await using var _ = conn;
        var add = Request(l, "S");
        add.Covered = [.. l.Covered, new AdjustmentCovered
        {
            Action = "add", Name = "clinic kid", IcBcPp = "150101-01-0002", DateOfBirth = new DateTime(2015, 1, 1), Sex = "F", Relationship = "D",
        }];
        await AdjustmentService.SaveAsync(conn, tx, number, add, "TEST");
        var id = (await AdjustmentService.LoadAsync(conn, tx, number))!.Covered.Single(c => c.Name == "CLINIC KID").CoverId;
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.MBMCoveredPersonsCLN WHERE MBMCNumber = @number AND MBMCCoverID = @id", new { number, id }, tx));
        await tx.RollbackAsync();
    }
}
