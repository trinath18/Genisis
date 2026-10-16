using Genisis.Api.Adjustment;
using Genisis.Api.Membership;
using Microsoft.Extensions.Options;

namespace Genisis.Api.Tests;

/// <summary>Unit tests of the shared membership rules and the policy wording file store (no database).</summary>
public class MembershipRulesTests
{
    [Theory]
    [InlineData(true, "A", "05", null, true)]
    [InlineData(false, "", "00", null, true)]
    [InlineData(false, "A", "01", null, false)]
    [InlineData(false, "B", "01", null, true)]
    [InlineData(false, "B", "02", null, false)]
    [InlineData(false, "C", "07", null, true)]
    [InlineData(false, "D", "02", "03", false)]
    [InlineData(false, "D", "03", "03", true)]
    public void Supplementary_charge_rule_matches_the_desktop(bool principal, string status, string coverId, string? chargeFrom, bool charged) =>
        Assert.Equal(charged, FeeRules.IsCharged(principal, status, coverId, chargeFrom));

    [Fact]
    public void Supplementary_expiry_ends_on_the_birthday_that_reaches_the_age_limit()
    {
        var expiry = new DateTime(2022, 8, 31);
        Assert.Equal(new DateTime(2022, 6, 15), FeeRules.SupplementaryExpiry(new DateTime(1999, 6, 15), expiry, 23));
        Assert.Equal(expiry, FeeRules.SupplementaryExpiry(new DateTime(1999, 10, 15), expiry, 23));
        Assert.Equal(expiry, FeeRules.SupplementaryExpiry(new DateTime(2005, 6, 15), expiry, 23));
        Assert.Equal(expiry, FeeRules.SupplementaryExpiry(new DateTime(1999, 6, 15), expiry, null));
        Assert.Equal(new DateTime(2023, 2, 28), FeeRules.BirthdayIn(2023, new DateTime(2000, 2, 29)));
    }

    [Fact]
    public void Adjustment_rules_cover_ids_and_policy_length()
    {
        Assert.Equal("01", AdjustmentRules.CoverId(" 1 "));
        Assert.Equal("AB", AdjustmentRules.CoverId("AB"));
        Assert.Equal("Policy more than 365 days.", AdjustmentRules.PolicyLengthWarning(new DateTime(2021, 1, 1), new DateTime(2022, 1, 2)));
        Assert.Equal("Policy less than 30 days.", AdjustmentRules.PolicyLengthWarning(new DateTime(2021, 1, 1), new DateTime(2021, 1, 31)));
        Assert.Null(AdjustmentRules.PolicyLengthWarning(new DateTime(2021, 1, 1), new DateTime(2021, 12, 31)));
        Assert.True(AdjustmentRules.DisabilityLimitApplies(null, CoverIds.Principal));
        Assert.False(AdjustmentRules.DisabilityLimitApplies("B", CoverIds.Principal));
        Assert.Equal("XI*H*19800101*JOHN", AdjustmentRules.RenewalKey(" xi ", "h", new DateTime(1980, 1, 1), " JOHN "));
    }

    [Fact]
    public void Adjustment_request_check_runs_before_loading()
    {
        Assert.Equal("Please Choose An Endorsement Type", AdjustmentValidator.ValidateRequest(new AdjustmentRequest()));
        Assert.Equal("Unknown endorsement type.", AdjustmentValidator.ValidateRequest(new AdjustmentRequest { EndorsementType = "Z" }));
        Assert.Null(AdjustmentValidator.ValidateRequest(new AdjustmentRequest { EndorsementType = " a " }));
    }

    [Fact]
    public void Policy_wording_store_only_serves_allowed_files_inside_the_folder()
    {
        var root = Directory.CreateTempSubdirectory("genisis-wording-");
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(root.FullName, "MEDICALIFE"));
            File.WriteAllText(Path.Combine(folder.FullName, "wording.pdf"), "pdf");
            File.WriteAllText(Path.Combine(folder.FullName, "notes.txt"), "txt");
            File.WriteAllText(Path.Combine(root.FullName, "secret.pdf"), "secret");
            var store = new PolicyWordingStore(Options.Create(new DocumentOptions { PolicyWordingPath = root.FullName }));

            var (files, folderFound) = store.ListFiles("MEDICALIFE");
            Assert.Equal(["wording.pdf"], files);
            Assert.True(folderFound);
            Assert.Equal("application/pdf", store.Find("MEDICALIFE", "wording.pdf")!.ContentType);
            Assert.Null(store.Find("MEDICALIFE", "notes.txt"));
            Assert.Null(store.Find("MEDICALIFE", "../secret.pdf"));
            Assert.Null(store.Find("..", "secret.pdf"));
            Assert.False(store.ListFiles("MISSING").FolderFound);
            Assert.False(new PolicyWordingStore(Options.Create(new DocumentOptions())).ListFiles("MEDICALIFE").FolderFound);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
