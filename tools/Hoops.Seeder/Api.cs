using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hoops.Seeder;

/// <summary>A thin client over the HTTP API. Every call fails loudly with the problem-details body.</summary>
public sealed class Api
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public Api(Uri baseAddress)
    {
        _http = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) };
    }

    public string? Token
    {
        set => _http.DefaultRequestHeaders.Authorization = value is null ? null : new AuthenticationHeaderValue("Bearer", value);
    }

    public async Task<JsonElement> PostAsync(string url, object? body, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = body is null ? null : JsonContent.Create(body, options: Json),
        };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await SendAsync(request);
    }

    public async Task<JsonElement> PatchAsync(string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body, options: Json) };
        return await SendAsync(request);
    }

    public async Task<JsonElement> GetAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync(request);
    }

    /// <summary>Posts and returns the created resource's id.</summary>
    public async Task<string> CreateAsync(string url, object body)
        => (await PostAsync(url, body)).GetProperty("id").GetString()!;

    private async Task<JsonElement> SendAsync(HttpRequestMessage request)
    {
        using var response = await _http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new SeedException($"{request.Method} {request.RequestUri} → {(int)response.StatusCode}\n{text}");
        }

        // Health probes return plain text; everything else is JSON.
        if (string.IsNullOrWhiteSpace(text) || response.Content.Headers.ContentType?.MediaType?.Contains("json") != true)
        {
            return default;
        }

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }
}

public sealed class SeedException(string message) : Exception(message);
