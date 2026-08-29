namespace Hoops.SharedKernel.Identifiers;

/// <summary>
/// Non-generic marker for strongly-typed id structs, so infrastructure (JSON, EF) can discover them
/// by a single interface without knowing the concrete types.
/// </summary>
public interface IStronglyTypedId
{
    /// <summary>The underlying value.</summary>
    Guid Value { get; }
}

/// <summary>
/// A strongly-typed id wrapping a <see cref="Guid"/>. Using distinct id types per entity makes it a
/// compile error to pass a <c>UserId</c> where an <c>OrganisationId</c> is expected.
/// </summary>
/// <typeparam name="TSelf">The concrete id type.</typeparam>
public interface IStronglyTypedId<TSelf> : IStronglyTypedId
    where TSelf : IStronglyTypedId<TSelf>
{
    /// <summary>Rehydrates an id from its underlying <see cref="Guid"/>.</summary>
    static abstract TSelf FromGuid(Guid value);
}
