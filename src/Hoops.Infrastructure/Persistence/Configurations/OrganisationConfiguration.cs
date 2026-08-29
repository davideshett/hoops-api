using System.Text.Json;
using Hoops.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hoops.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="Organisation"/>.</summary>
public sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    private static readonly JsonSerializerOptions SettingsJsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).IsRequired();

        builder.Property(o => o.Slug).HasColumnType("citext").IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();

        builder.Property(o => o.CountryCode).HasColumnType("char(2)");
        builder.Property(o => o.DefaultTimezone).IsRequired().HasDefaultValue("UTC");

        // settings: jsonb, serialised deterministically so change tracking stays honest.
        var settingsComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => JsonSerializer.Serialize(a, SettingsJsonOptions) == JsonSerializer.Serialize(b, SettingsJsonOptions),
            v => JsonSerializer.Serialize(v, SettingsJsonOptions).GetHashCode(),
            v => new Dictionary<string, string>(v));

        builder.Property(o => o.Settings)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, SettingsJsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, SettingsJsonOptions)
                     ?? new Dictionary<string, string>())
            .Metadata.SetValueComparer(settingsComparer);

        builder.HasQueryFilter(o => o.DeletedAt == null);
    }
}
