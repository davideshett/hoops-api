using FluentAssertions;
using Hoops.SharedKernel;

namespace Hoops.UnitTests.SharedKernel;

public sealed class GuardTests
{
    [Fact]
    public void AgainstNull_throws_on_null_and_passes_otherwise()
    {
        var passNull = () => Guard.AgainstNull<string>(null);
        passNull.Should().Throw<ArgumentNullException>();

        Guard.AgainstNull("ok").Should().Be("ok");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AgainstNullOrWhiteSpace_throws_on_blank(string? value)
    {
        var act = () => Guard.AgainstNullOrWhiteSpace(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgainstDefault_throws_on_empty_guid()
    {
        var act = () => Guard.AgainstDefault(Guid.Empty);
        act.Should().Throw<ArgumentException>();

        var id = Guid.NewGuid();
        Guard.AgainstDefault(id).Should().Be(id);
    }

    [Fact]
    public void AgainstTooLong_enforces_max_length()
    {
        var act = () => Guard.AgainstTooLong("abcdef", 3);
        act.Should().Throw<ArgumentException>();

        Guard.AgainstTooLong("abc", 3).Should().Be("abc");
    }
}
