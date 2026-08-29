using System.IdentityModel.Tokens.Jwt;
using Hoops.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Hoops.Api.Auth;

/// <summary>Resolves <see cref="ICurrentUser"/> from the authenticated request's JWT claims.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    /// <summary>Creates the accessor.</summary>
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var sub = _accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public bool IsSystemAdmin =>
        string.Equals(
            _accessor.HttpContext?.User.FindFirst(HoopsClaims.SystemAdmin)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);
}
