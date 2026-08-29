namespace Hoops.Api.Auth;

/// <summary>Custom JWT claim types used by the platform.</summary>
public static class HoopsClaims
{
    /// <summary>
    /// One claim per organisation the user belongs to, valued <c>{organisationId}:{role}</c>. Read by
    /// the organisation-membership authorization handler to permit or forbid org-scoped requests.
    /// </summary>
    public const string Membership = "membership";

    /// <summary>Platform-administrator flag, valued <c>true</c> or <c>false</c>.</summary>
    public const string SystemAdmin = "sys_admin";
}
