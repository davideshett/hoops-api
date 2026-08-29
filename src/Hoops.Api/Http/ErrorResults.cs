using System.Diagnostics;
using Hoops.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Http;

/// <summary>
/// Maps a domain <see cref="Error"/> to an RFC 9457 <see cref="ProblemDetails"/> response, carrying the
/// machine-readable <c>code</c> and a <c>traceId</c> so every failure is greppable in logs.
/// </summary>
public static class ErrorResults
{
    /// <summary>Builds a problem-details result for <paramref name="error"/>.</summary>
    public static ObjectResult ToProblem(Error error, HttpContext httpContext)
    {
        var status = StatusFor(error.Type);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Detail = error.Message,
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>The HTTP status code for an error category.</summary>
    public static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Error",
    };
}
