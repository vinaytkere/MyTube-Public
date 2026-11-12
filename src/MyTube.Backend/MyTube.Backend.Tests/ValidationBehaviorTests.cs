using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Common.Behaviors;
using Xunit;

namespace MyTube.Backend.Tests;

public class ValidationBehaviorTests
{
    public class SampleCommand : IRequest<string>
    {
        public string Title { get; set; } = string.Empty;
    }

    public class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required.");
        }
    }

    [Fact]
    public async Task Handle_ShouldCallNext_WhenValidationPasses()
    {
        // Arrange
        var validators = new List<IValidator<SampleCommand>> { new SampleCommandValidator() };
        var behavior = new ValidationBehavior<SampleCommand, string>(validators);
        var command = new SampleCommand { Title = "Valid Title" };

        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("Success");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var validators = new List<IValidator<SampleCommand>> { new SampleCommandValidator() };
        var behavior = new ValidationBehavior<SampleCommand, string>(validators);
        var command = new SampleCommand { Title = "" };

        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("Success");

        // Act
        var act = async () => await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(e => e.PropertyName == "Title" && e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public async Task Handle_ShouldCallNext_WhenNoValidatorsRegistered()
    {
        // Arrange
        var validators = new List<IValidator<SampleCommand>>();
        var behavior = new ValidationBehavior<SampleCommand, string>(validators);
        var command = new SampleCommand { Title = "" };

        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("SuccessWithoutValidation");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be("SuccessWithoutValidation");
    }
}
