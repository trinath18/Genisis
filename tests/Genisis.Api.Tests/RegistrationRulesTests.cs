using System.Reflection;
using System.Security.Claims;
using Genisis.Api.Auth;
using Genisis.Api.Controllers;
using Genisis.Api.Registration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Genisis.Api.Tests;

public class LegacyRulesTests
{
    [Theory]
    [InlineData(null, "A00001")]
    [InlineData("", "A00001")]
    [InlineData("A00001", "A00002")]
    [InlineData("C90075    ", "C90076")]
    [InlineData("A99999", "B00001")]
    public void NextPayorSequence_matches_legacy(string? current, string expected) =>
        Assert.Equal(expected, LegacyRules.NextPayorSequence(current));

    [Theory]
    [InlineData("01", "02")]
    [InlineData("61", "62")]
    [InlineData("99", "01")]
    public void NextCheckDigits_cycles_01_to_99(string current, string expected) =>
        Assert.Equal(expected, LegacyRules.NextCheckDigits(current));

    [Theory]
    [InlineData("1001", "1002")]
    [InlineData("2721", "2722")]
    [InlineData("9999", "1001")]
    public void NextSecurityCode_cycles_1001_to_9999(string current, string expected) =>
        Assert.Equal(expected, LegacyRules.NextSecurityCode(current));

    [Fact]
    public void Age_under_one_calendar_year_is_in_days() =>
        Assert.Equal((-1, true), LegacyRules.Age(new DateTime(2021, 8, 1), new DateTime(2021, 2, 1)));

    [Theory]
    [InlineData("2021-08-01", "2010-09-01", 10)]
    [InlineData("2021-08-01", "2010-07-01", 11)]
    [InlineData("2021-08-01", "1980-12-31", 41)]
    public void Age_in_years(string on, string dob, int expected) =>
        Assert.Equal((expected, false), LegacyRules.Age(DateTime.Parse(on), DateTime.Parse(dob)));

    private static readonly LegacyRules.AgeBand[] Bands =
    [
        new("01", "CHILD", "-100D", "17Y", null),
        new("02", "YOUNG", "18Y", "35Y", null),
        new("03", "MID", "36Y", "45Y", null),
        new("01", "INFANT", "0D", "5Y", "PX01"),
        new("02", "KID", "6Y", "17Y", "PX01"),
    ];

    [Theory]
    [InlineData(30, false, null, "02")]
    [InlineData(-1, true, null, "01")]
    [InlineData(10, false, null, "01")]
    [InlineData(10, false, "PX01", "02")]
    [InlineData(40, false, "PX01", "03")]
    public void PickAgeBand_prefers_plan_bands_then_generic(int age, bool inDays, string? plan, string expected) =>
        Assert.Equal(expected, LegacyRules.PickAgeBand(Bands, (age, inDays), plan)?.AgeCode);

    [Fact]
    public void PickAgeBand_returns_null_when_out_of_range() =>
        Assert.Null(LegacyRules.PickAgeBand(Bands, (90, false), null));

    [Fact]
    public void ProRate_counts_both_end_days() =>
        Assert.Equal(17m, LegacyRules.ProRate(new DateTime(2021, 1, 1), new DateTime(2021, 12, 31), 17m));

    [Theory]
    [InlineData(8.5, 8)]
    [InlineData(9.5, 10)]
    [InlineData(8.495, 8)]
    [InlineData(8.504, 8)]
    [InlineData(8.51, 9)]
    public void RoundWhole_uses_bankers_rounding_after_two_decimals(decimal value, decimal expected) =>
        Assert.Equal(expected, LegacyRules.RoundWhole(value));
}

public class RegistrationValidatorTests
{
    internal static RegistrationRequest Valid() => new()
    {
        HealthCode = "N", PayorCode = "SU", BordxDate = new DateTime(2021, 8, 6), BatchNo = 1, Renewal = "N",
        Name = "TEST MEMBER", IcBcPp = "800101-01-0001", DateOfBirth = new DateTime(1980, 1, 1), Sex = "M",
        Address1 = "1 JALAN TEST", PostCode = "50000", State = "KUALA LUMPUR",
        InsuredType = "H", PlanCode = "AN76", PolicyNo = "POL-1",
        PayorEffectiveDate = new DateTime(2021, 8, 1), PayorExpiryDate = new DateTime(2022, 7, 31),
    };

    [Fact]
    public void Valid_request_passes() => Assert.Null(RegistrationValidator.Validate(Valid()));

    public static TheoryData<Action<RegistrationRequest>, string> Failures => new()
    {
        { r => r.HealthCode = " ", "Health Code" },
        { r => r.PayorCode = null, "Payor Code" },
        { r => r.BordxDate = null, "Bordx Date" },
        { r => r.BatchNo = 0, "Batch No" },
        { r => r.Renewal = "R", "Previous Membership No" },
        { r => r.Name = "", "Membership Name" },
        { r => r.IcBcPp = "", "IC/BC/PP" },
        { r => r.Address1 = "", "Address" },
        { r => r.PostCode = "", "Post Code" },
        { r => r.State = "", "State" },
        { r => r.DateOfBirth = null, "Birthdate" },
        { r => r.PlanCode = "", "Plan Code" },
        { r => r.PolicyNo = "", "Policy No" },
        { r => r.PayorEffectiveDate = null, "Payor Effective Date" },
        { r => r.PayorExpiryDate = new DateTime(2021, 7, 1), "Payor Expiry Date" },
        { r => r.InsuredType = "", "Insured Code" },
        { r => { r.InsuredType = "I"; r.CoveredPersons.Add(new CoveredPersonRequest { Name = "A", Relationship = "C", DateOfBirth = DateTime.Today.AddYears(-5) }); }, "family" },
        { r => r.CoveredPersons.Add(new CoveredPersonRequest { Name = "A", DateOfBirth = DateTime.Today.AddYears(-5) }), "Relationship" },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Missing_required_field_is_reported(Action<RegistrationRequest> change, string expected)
    {
        var request = Valid();
        change(request);
        Assert.Contains(expected, RegistrationValidator.Validate(request));
    }

    [Fact]
    public void Renewal_with_previous_policy_passes()
    {
        var request = Valid();
        request.Renewal = "R";
        request.PreviousPolicyNo = "OLD-1";
        Assert.Null(RegistrationValidator.Validate(request));
    }
}

public class RegistrationAccessTests
{
    private static AuthorizationFilterContext Context(string access)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(TokenService.AccessClaim, access)], "test")),
        };
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }

    [Fact]
    public void Controller_requires_registration_access_position() =>
        Assert.Equal(AccessRight.MembershipRegistration,
            typeof(RegistrationController).GetCustomAttribute<RequireAccessAttribute>()?.Position);

    [Fact]
    public void User_without_registration_access_gets_403()
    {
        var context = Context("011");
        new RequireAccessAttribute(AccessRight.MembershipRegistration).OnAuthorization(context);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }

    [Fact]
    public void User_with_registration_access_passes()
    {
        var context = Context("100");
        new RequireAccessAttribute(AccessRight.MembershipRegistration).OnAuthorization(context);
        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData("A", "01", null, false)]
    [InlineData("B", "01", null, true)]
    [InlineData("B", "02", null, false)]
    [InlineData("C", "03", null, true)]
    [InlineData("D", "01", "02", false)]
    [InlineData("D", "02", "02", true)]
    public void Supplementary_status_controls_covered_person_charges(string status, string coverId, string? from, bool expected) =>
        Assert.Equal(expected, RegistrationService.Charged(status, coverId, from));
}
