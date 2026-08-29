using FluentAssertions;
using Hoops.SharedKernel.Results;

namespace Hoops.UnitTests.SharedKernel;

public sealed class ResultTests
{
    [Fact]
    public void Success_result_carries_value_and_no_error()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_result_carries_error_and_throws_on_value_access()
    {
        var error = Error.NotFound("X_NOT_FOUND", "missing");
        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        var access = () => result.Value;
        access.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_factories_set_the_expected_type()
    {
        Error.Validation("C", "m").Type.Should().Be(ErrorType.Validation);
        Error.NotFound("C", "m").Type.Should().Be(ErrorType.NotFound);
        Error.Conflict("C", "m").Type.Should().Be(ErrorType.Conflict);
        Error.Unauthorized("C", "m").Type.Should().Be(ErrorType.Unauthorized);
        Error.Forbidden("C", "m").Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Valueless_result_round_trips_success_and_failure()
    {
        Result.Success().IsSuccess.Should().BeTrue();

        Result failure = Error.Conflict("DUP", "duplicate");
        failure.IsFailure.Should().BeTrue();
        failure.Error.Code.Should().Be("DUP");
    }
}
