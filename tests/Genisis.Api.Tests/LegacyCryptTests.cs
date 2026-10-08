using Genisis.Api.Auth;

namespace Genisis.Api.Tests;

public class LegacyCryptTests
{
    [Theory]
    [InlineData("abc", "bfl")]
    [InlineData("genis", "hiwy\u0152")]
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
        Assert.True(LegacyCrypt.Matches("genis", "HIWY\u0152   "));

    [Fact]
    public void Matches_rejects_wrong_password() =>
        Assert.False(LegacyCrypt.Matches("genis1", "HIWY\u0152"));
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
