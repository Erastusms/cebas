using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;
using CEBAS.Api.Common.Behaviors;
using CEBAS.Domain.Exceptions;

namespace CEBAS.UnitTests;

public class ValidationBehaviorTests
{
    public sealed record SampleCommand(string Name, int Age) : IRequest<string>;

    public sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name cannot be empty.");
            RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithMessage("Must be at least 18.");
        }
    }

    [Fact]
    public async Task ValidationBehavior_WithValidRequest_ShouldCallNextDelegate()
    {
        var validators = new IValidator<SampleCommand>[] { new SampleCommandValidator() };
        var behavior = new ValidationBehavior<SampleCommand, string>(validators);

        var validCommand = new SampleCommand("Valid Name", 25);
        var result = await behavior.Handle(validCommand, (ct) => Task.FromResult("SUCCESS"), CancellationToken.None);

        result.Should().Be("SUCCESS");
    }

    [Fact]
    public async Task ValidationBehavior_WithInvalidRequest_ShouldThrowValidationException_WithGroupedErrors()
    {
        var validators = new IValidator<SampleCommand>[] { new SampleCommandValidator() };
        var behavior = new ValidationBehavior<SampleCommand, string>(validators);

        var invalidCommand = new SampleCommand("", 15);
        var act = () => behavior.Handle(invalidCommand, (ct) => Task.FromResult("SUCCESS"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<CEBAS.Domain.Exceptions.ValidationException>();
        exception.Which.Errors.Should().ContainKey("Name");
        exception.Which.Errors.Should().ContainKey("Age");
    }
}
