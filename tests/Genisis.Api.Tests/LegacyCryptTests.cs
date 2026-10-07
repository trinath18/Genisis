using Genisis.Api.Auth;

namespace Genisis.Api.Tests;

public class LegacyCryptTests
{
    [Theory]
    [InlineData("abc", "bfl")]
    [InlineData("medix", "nimy\u2018")]
    public void Encrypt_matches_vb6_crypt(string plain, string expected) =>
        Assert.Equal(expected, LegacyCrypt.Encrypt(plain));

    [Fact]
    public void Encrypt_wraps_values_above_255()
    {
        var encrypted = LegacyCrypt.Encrypt(new string('z', 12));
        Assert.Equal((char)('z' + 144 - 255), encrypted[11]);
    }

    [Fact]
    public void Matches_is_case_insensitive_and_ignores_padding() =>
        Assert.True(LegacyCrypt.Matches("medix", "NIMY\u2018   "));

    [Fact]
    public void Matches_rejects_wrong_password() =>
        Assert.False(LegacyCrypt.Matches("medix1", "NIMY\u2018"));
}

public class AccessRightTests
{
    [Theory]
    [InlineData("111", AccessRight.MembershipEnquiry, true)]
    [InlineData("110", AccessRight.MembershipEnquiry, false)]
    [InlineData("11", AccessRight.MembershipEnquiry, false)]
    [InlineData(null, AccessRight.MembershipRegistration, false)]
    public void Has_reads_position(string? access, int position, bool expected) =>
        Assert.Equal(expected, AccessRight.Has(access, position));
}
