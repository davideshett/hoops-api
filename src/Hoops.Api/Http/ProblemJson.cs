using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Http;

/// <summary>Writes an RFC 9457 problem-details body directly to a response, for pipeline points that
/// sit outside MVC (JWT bearer challenge/forbidden events).</summary>
public static class ProblemJson
{
    /// <summary>Writes <paramref name="status"/> as <c>application/problem+json</c> with a code and trace id.</summary>
    public static async Task WriteAsync(HttpContext context, int status, string code, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json");
    }
}
