using System.Runtime.CompilerServices;

namespace Hoops.SharedKernel;

/// <summary>
/// Argument guards for domain invariants. These throw — they protect constructors and factory
/// methods from ever producing an invalid entity. Expected, user-facing failures use
/// <see cref="Results.Result{T}"/> instead.
/// </summary>
public static class Guard
{
    /// <summary>Throws if <paramref name="value"/> is null.</summary>
    public static T AgainstNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
        => value ?? throw new ArgumentNullException(name);

    /// <summary>Throws if <paramref name="value"/> is null, empty, or whitespace.</summary>
    public static string AgainstNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null or whitespace.", name);
        }

        return value;
    }

    /// <summary>Throws if <paramref name="value"/> equals the default of its type (e.g. an empty Guid).</summary>
    public static T AgainstDefault<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException("Value must not be the default.", name);
        }

        return value;
    }

    /// <summary>Throws if <paramref name="value"/> exceeds <paramref name="maxLength"/> characters.</summary>
    public static string AgainstTooLong(
        string value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"Value must be at most {maxLength} characters.", name);
        }

        return value;
    }
}
