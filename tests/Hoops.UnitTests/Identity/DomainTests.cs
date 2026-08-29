using FluentAssertions;
using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.Identity;

public sealed class DomainTests
{
    [Fact]
    public void User_register_trims_fields()
    {
        var user = User.Register("  a@b.com ", "hash", "  Ada Lovelace  ");

        user.Email.Should().Be("a@b.com");
        user.FullName.Should().Be("Ada Lovelace");
        user.PasswordHash.Should().Be("hash");
        user.IsSystemAdmin.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void User_register_rejects_blank_email(string email)
    {
        var act = () => User.Register(email, "hash", "Name");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Organisation_create_lowercases_slug_and_defaults_timezone()
    {
        var org = Organisation.Create("Anambra BBA", "Anambra-BBA", "NG", defaultTimezone: null);

        org.Slug.Should().Be("anambra-bba");
        org.DefaultTimezone.Should().Be("UTC");
        org.CountryCode.Should().Be("NG");
        org.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Organisation_update_leaves_omitted_fields_unchanged()
    {
        var org = Organisation.Create("Old", "slug", "NG", "Africa/Lagos");

        org.UpdateProfile(name: "New", countryCode: null, defaultTimezone: null, logoUrl: null);

        org.Name.Should().Be("New");
        org.CountryCode.Should().Be("NG");
        org.DefaultTimezone.Should().Be("Africa/Lagos");
    }

    [Fact]
    public void Membership_change_role_updates_role()
    {
        var membership = OrganisationMembership.CreateActive(
            OrganisationId.New(), UserId.New(), OrganisationRole.Viewer, DateTimeOffset.UnixEpoch);

        membership.ChangeRole(OrganisationRole.Admin);

        membership.Role.Should().Be(OrganisationRole.Admin);
        membership.AcceptedAt.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void RefreshToken_is_active_until_expired_or_revoked()
    {
        var now = DateTimeOffset.UnixEpoch;
        var token = RefreshToken.Issue(UserId.New(), "hash", now.AddDays(1));

        token.IsActive(now).Should().BeTrue();
        token.IsActive(now.AddDays(2)).Should().BeFalse("it is expired");

        token.Revoke(now, RefreshTokenId.New());
        token.IsActive(now).Should().BeFalse("it is revoked");
        token.ReplacedByTokenId.Should().NotBeNull();
    }
}
