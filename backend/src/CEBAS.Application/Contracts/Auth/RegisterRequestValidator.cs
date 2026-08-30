using FluentValidation;
using CEBAS.Domain.Common;

namespace CEBAS.Application.Contracts.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(30).WithMessage("Username cannot exceed 30 characters.")
            .Must(IdentityNormalizers.IsValidUsername).WithMessage("Username may only contain letters, numbers, and underscores.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.")
            .Must(IdentityNormalizers.IsValidEmail).WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(50).WithMessage("Display name cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.DisplayName));
    }
}
