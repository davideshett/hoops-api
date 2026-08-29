using System.Linq.Expressions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hoops.Infrastructure.Persistence;

/// <summary>Converts a strongly-typed id to and from its underlying <see cref="Guid"/> for storage.</summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class StronglyTypedIdValueConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, IStronglyTypedId<TId>
{
    /// <summary>Creates the converter.</summary>
    public StronglyTypedIdValueConverter()
        : base(id => id.Value, BuildFromProvider())
    {
    }

    // A static abstract (FromGuid) cannot appear in an expression tree, so rehydrate via the id's
    // (Guid) constructor, which every strongly-typed id record struct exposes.
    private static Expression<Func<Guid, TId>> BuildFromProvider()
    {
        var ctor = typeof(TId).GetConstructor([typeof(Guid)])
            ?? throw new InvalidOperationException($"{typeof(TId)} must expose a (Guid) constructor.");
        var value = Expression.Parameter(typeof(Guid), "value");
        return Expression.Lambda<Func<Guid, TId>>(Expression.New(ctor, value), value);
    }
}

/// <summary>
/// Discovers every strongly-typed id defined in the SharedKernel so <c>AppDbContext</c> can register
/// one value converter per type by convention, rather than listing them by hand.
/// </summary>
public static class StronglyTypedIdRegistry
{
    /// <summary>All concrete strongly-typed id value types.</summary>
    public static readonly IReadOnlyList<Type> All = typeof(IStronglyTypedId).Assembly
        .GetTypes()
        .Where(t => t is { IsValueType: true, IsAbstract: false }
            && typeof(IStronglyTypedId).IsAssignableFrom(t))
        .ToList();
}
