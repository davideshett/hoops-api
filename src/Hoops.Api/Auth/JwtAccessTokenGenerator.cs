using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hoops.Api.Auth;

/// <summary>
/// Mints signed JWT access tokens. Lives in the host because it owns the signing key and JWT
/// configuration; satisfies the Identity module's <see cref="IAccessTokenGenerator"/> abstraction.
/// </summary>
public sealed class JwtAccessTokenGenerator : IAccessTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    /// <summary>Creates the generator.</summary>
    public JwtAccessTokenGenerator(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc />
    public AccessToken Generate(
        UserId userId,
        string email,
        bool isSystemAdmin,
        IReadOnlyCollection<MembershipClaim> memberships)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(HoopsClaims.SystemAdmin, isSystemAdmin ? "true" : "false"),
        };

        foreach (var membership in memberships)
        {
            claims.Add(new Claim(HoopsClaims.Membership, $"{membership.OrganisationId.Value}:{membership.Role}"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(value, expiresAt);
    }
}
