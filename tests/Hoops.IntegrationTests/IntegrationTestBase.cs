using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hoops.Infrastructure.Persistence;
using Hoops.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.IntegrationTests;

/// <summary>Shared helpers for integration tests: unique identities and the register/login flow.</summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase
{
    /// <summary>The factory under test.</summary>
    protected ApiFactory Factory { get; }

    /// <summary>Creates the fixture.</summary>
    protected IntegrationTestBase(ApiFactory factory) => Factory = factory;

    /// <summary>A fresh, unauthenticated client.</summary>
    protected HttpClient NewClient() => Factory.CreateClient();

    /// <summary>A unique email so tests never collide within a shared container.</summary>
    protected static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>A unique slug.</summary>
    protected static string UniqueSlug() => $"org-{Guid.NewGuid():N}";

    /// <summary>Registers a new user and returns their access token.</summary>
    protected async Task<string> RegisterAsync(HttpClient client, string email, string password = "password123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, fullName = "Test User" });
        response.EnsureSuccessStatusCode();
        return await ReadAccessTokenAsync(response);
    }

    /// <summary>Logs in and returns a fresh access token (which reflects current memberships).</summary>
    protected async Task<string> LoginAsync(HttpClient client, string email, string password = "password123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return await ReadAccessTokenAsync(response);
    }

    /// <summary>Attaches a bearer token to a new client.</summary>
    protected HttpClient AuthenticatedClient(string accessToken)
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>
    /// Registers a fresh user and creates an organisation they own, returning the id and the fresh
    /// access token the create-org call hands back — which already carries the Owner membership, so no
    /// re-login is needed. Owner satisfies every competition-management policy.
    /// </summary>
    protected async Task<(string Token, Guid OrgId)> NewOrgWithOwnerAsync()
    {
        var client = NewClient();
        var token = await RegisterAsync(client, UniqueEmail());

        var created = await AuthenticatedClient(token).PostAsJsonAsync("/api/v1/organisations",
            new { name = "Org", slug = UniqueSlug(), countryCode = "NG", defaultTimezone = "Africa/Lagos" });
        created.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orgId = Guid.Parse(doc.RootElement.GetProperty("id").GetString()!);
        var freshToken = doc.RootElement.GetProperty("accessToken").GetString()!;
        return (freshToken, orgId);
    }

    /// <summary>Flips a user to platform admin directly in the database (there is no endpoint for it).</summary>
    protected async Task PromoteToSystemAdminAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        db.Entry(user).Property(u => u.IsSystemAdmin).CurrentValue = true;
        await db.SaveChangesAsync();
    }

    /// <summary>Runs an action against the application database (for setup or tampering).</summary>
    protected async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    /// <summary>Registers a user, promotes them to platform admin, and returns a token with the admin claim.</summary>
    protected async Task<string> NewPlatformAdminTokenAsync()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);
        await PromoteToSystemAdminAsync(email);
        return await LoginAsync(client, email);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }
}
