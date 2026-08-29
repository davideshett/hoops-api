using FluentAssertions;
using Hoops.Infrastructure.Persistence;
using Hoops.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Hoops.ArchitectureTests;

/// <summary>
/// The only thing standing between a forgotten <c>.Where()</c> and a cross-tenant data leak. Every
/// mapped entity must either implement <see cref="ITenantScoped"/> (and thus receive a global query
/// filter) or appear on the explicit unscoped whitelist. A new unfiltered entity added by mistake
/// fails the build here — the test is asserted against a named whitelist, never a silent skip (§13,
/// ADR-003).
/// </summary>
public sealed class TenantFilterCoverageTests
{
    private static AppDbContext BuildContext() =>
        new DesignTimeDbContextFactory().CreateDbContext([]);

    [Fact]
    public void Every_mapped_entity_is_tenant_scoped_or_explicitly_whitelisted()
    {
        using var context = BuildContext();

        var offenders = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .Select(e => e.ClrType)
            .Distinct()
            .Where(clr => !typeof(ITenantScoped).IsAssignableFrom(clr)
                && !AppDbContext.UnscopedEntityWhitelist.Contains(clr))
            .Select(clr => clr.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "every mapped entity must be ITenantScoped or added to AppDbContext.UnscopedEntityWhitelist "
            + "with justification; unaccounted-for entities: {0}", string.Join(", ", offenders));
    }

    [Fact]
    public void Every_tenant_scoped_entity_has_a_global_query_filter()
    {
        using var context = BuildContext();

        var unfiltered = context.Model.GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .Where(e => !e.GetDeclaredQueryFilters().Any())
            .Select(e => e.ClrType.Name)
            .ToList();

        unfiltered.Should().BeEmpty(
            "every ITenantScoped entity must carry a global query filter; unfiltered: {0}",
            string.Join(", ", unfiltered));
    }

    [Fact]
    public void Whitelist_contains_only_entities_that_are_actually_mapped_and_unscoped()
    {
        using var context = BuildContext();

        var mappedUnscoped = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && !typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .Select(e => e.ClrType)
            .ToHashSet();

        // Nothing stale on the whitelist: each entry must be a real, mapped, non-tenant-scoped entity.
        AppDbContext.UnscopedEntityWhitelist.Should().BeSubsetOf(mappedUnscoped);
    }
}
