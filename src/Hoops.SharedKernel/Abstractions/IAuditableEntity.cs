namespace Hoops.SharedKernel.Abstractions;

/// <summary>
/// An entity that carries creation and modification timestamps. <c>AppDbContext</c> stamps these on
/// save so application code never sets them by hand. All timestamps are stored as timestamptz in UTC.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>When the row was first persisted.</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the row was last modified.</summary>
    DateTimeOffset UpdatedAt { get; set; }
}
