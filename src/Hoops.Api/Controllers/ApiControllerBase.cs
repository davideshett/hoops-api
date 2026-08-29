using Hoops.Api.Http;
using Hoops.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers;

/// <summary>
/// Base for all API controllers. Provides the single mapping from <see cref="Result{T}"/> to an
/// <see cref="ActionResult{T}"/>, so failures always render as RFC 9457 problem-details and no
/// controller hand-rolls error responses.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Maps a result to <c>200 OK</c> on success, or a problem-details response on failure.</summary>
    protected ActionResult<T> Ok<T>(Result<T> result)
        => result.IsSuccess ? base.Ok(result.Value) : ErrorResults.ToProblem(result.Error, HttpContext);

    /// <summary>Maps a valueless result to <c>204 No Content</c> on success, or a problem on failure.</summary>
    protected ActionResult NoContent(Result result)
        => result.IsSuccess ? base.NoContent() : ErrorResults.ToProblem(result.Error, HttpContext);

    /// <summary>Maps a result to <c>201 Created</c> at <paramref name="location"/>, or a problem on failure.</summary>
    protected ActionResult<T> Created<T>(Result<T> result, string location)
        => result.IsSuccess ? base.Created(location, result.Value) : ErrorResults.ToProblem(result.Error, HttpContext);

    /// <summary>Maps a result to <c>201 Created</c> with no location header, or a problem on failure.</summary>
    protected ActionResult<T> Created<T>(Result<T> result)
        => result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResults.ToProblem(result.Error, HttpContext);
}
