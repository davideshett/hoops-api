using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }
}
