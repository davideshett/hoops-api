using System.Text.Json;
using FluentAssertions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.SharedKernel;

public sealed class StronglyTypedIdTests
{
    private sealed record Holder(OrganisationId OrganisationId, UserId? UserId);

    [Fact]
    public void New_ids_are_version_7_and_distinct()
    {
        var a = OrganisationId.New();
        var b = OrganisationId.New();

        a.Should().NotBe(b);
        a.Value.Version.Should().Be(7);
    }

    [Fact]
    public void Ids_serialise_as_a_bare_guid_string()
    {
        var id = OrganisationId.New();

        var json = JsonSerializer.Serialize(id);

        json.Should().Be($"\"{id.Value}\"");
    }

    [Fact]
    public void Ids_round_trip_through_json_inside_an_object()
    {
        var original = new Holder(OrganisationId.New(), UserId.New());

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Holder>(json);

        restored.Should().Be(original);
    }

    [Fact]
    public void Distinct_id_types_are_not_interchangeable()
    {
        // Compile-time guarantee documented as a test: these are different types wrapping a Guid.
        typeof(OrganisationId).Should().NotBe(typeof(UserId));
    }
}
