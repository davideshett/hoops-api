using Hoops.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hoops.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="OrganisationMembership"/>.</summary>
public sealed class OrganisationMembershipConfiguration : IEntityTypeConfiguration<OrganisationMembership>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganisationMembership> builder)
    {
        builder.ToTable("organisation_memberships");
        builder.HasKey(m => m.Id);

        // Role stored as its enum name (text), never an ordinal.
        builder.Property(m => m.Role)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(m => new { m.OrganisationId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(m => m.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
