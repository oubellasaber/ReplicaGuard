using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Behaviors;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ValidationException = ReplicaGuard.Application.Exceptions.ValidationException;

namespace ReplicaGuard.Application.Tests;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task pipeline_invokes_next_when_no_validators_are_registered()
    {
        var behavior = new ValidationBehavior<TestCommand, Result<TestResponse>>(
            Enumerable.Empty<IValidator<TestCommand>>());
        var next = Substitute.For<RequestHandlerDelegate<Result<TestResponse>>>();
        next().Returns(Result.Success(new TestResponse()));

        var result = await behavior.Handle(new TestCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await next.Received(1)();
    }

    [Fact]
    public async Task pipeline_throws_validation_exception_when_validation_fails()
    {
        var validator = Substitute.For<IValidator<TestCommand>>();
        validator.Validate(Arg.Any<ValidationContext<TestCommand>>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Name is required")
            }));

        var behavior = new ValidationBehavior<TestCommand, Result<TestResponse>>(new[] { validator });
        var act = async () => await behavior.Handle(
            new TestCommand(),
            Substitute.For<RequestHandlerDelegate<Result<TestResponse>>>(),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Name" && e.ErrorMessage == "Name is required");
    }

    [Fact]
    public async Task pipeline_invokes_next_when_validation_passes()
    {
        var validator = Substitute.For<IValidator<TestCommand>>();
        validator.Validate(
            Arg.Any<ValidationContext<TestCommand>>())
            .Returns(new ValidationResult());

        var behavior = new ValidationBehavior<TestCommand, Result<TestResponse>>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<Result<TestResponse>>>();
        next().Returns(Result.Success(new TestResponse()));

        var result = await behavior.Handle(new TestCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await next.Received(1)();
    }

    public sealed record TestCommand : ICommand<TestResponse>;
    public sealed record TestResponse;
}
