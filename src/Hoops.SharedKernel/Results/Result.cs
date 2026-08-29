namespace Hoops.SharedKernel.Results;

/// <summary>
/// The outcome of an operation that can fail in an expected way. Prefer this over exceptions for
/// domain and application failures; reserve exceptions for genuinely exceptional cases.
/// </summary>
public readonly struct Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure. <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>A successful result.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>A failed result carrying <paramref name="error"/>.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>A successful result carrying a value.</summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>A failed typed result.</summary>
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    /// <summary>Implicitly lifts an <see cref="Error"/> into a failed result.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// The outcome of an operation that yields a value on success or an <see cref="Error"/> on failure.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public readonly struct Result<T>
{
    private readonly T _value;

    private Result(bool isSuccess, T value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure. <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>The success value. Throws if accessed on a failed result.</summary>
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(true, value, Error.None);

    /// <summary>A failed result carrying <paramref name="error"/>.</summary>
    public static Result<T> Failure(Error error) => new(false, default!, error);

    /// <summary>Implicitly lifts a value into a successful result.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Implicitly lifts an <see cref="Error"/> into a failed result.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}
