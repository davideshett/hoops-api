namespace Hoops.SharedKernel.Results;

/// <summary>
/// The category of an expected failure. Maps to an HTTP status code at the API boundary.
/// </summary>
public enum ErrorType
{
    /// <summary>An unclassified failure. Maps to 400.</summary>
    Failure = 0,

    /// <summary>Request shape or value violated a rule. Maps to 400.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist. Maps to 404.</summary>
    NotFound = 2,

    /// <summary>The operation conflicts with existing state. Maps to 409.</summary>
    Conflict = 3,

    /// <summary>The caller is not authenticated. Maps to 401.</summary>
    Unauthorized = 4,

    /// <summary>The caller is authenticated but not permitted. Maps to 403.</summary>
    Forbidden = 5,
}

/// <summary>
/// A machine-readable failure. <see cref="Code"/> is a stable SCREAMING_SNAKE_CASE token the
/// client can switch on; <see cref="Message"/> is human-facing detail.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    /// <summary>The absence of an error. Never surfaced on a failed result.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Creates a <see cref="ErrorType.Validation"/> error.</summary>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>Creates a <see cref="ErrorType.NotFound"/> error.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates a <see cref="ErrorType.Conflict"/> error.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates a <see cref="ErrorType.Unauthorized"/> error.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Creates a <see cref="ErrorType.Forbidden"/> error.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Creates an unclassified <see cref="ErrorType.Failure"/> error.</summary>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
