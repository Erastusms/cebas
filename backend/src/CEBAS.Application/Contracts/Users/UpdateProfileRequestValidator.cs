using FluentValidation;

namespace CEBAS.Application.Contracts.Users;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name cannot be empty.")
            .MaximumLength(50).WithMessage("Display name cannot exceed 50 characters.");

        RuleFor(x => x.Bio)
            .MaximumLength(160).WithMessage("Biography cannot exceed 160 characters.")
            .When(x => !string.IsNullOrEmpty(x.Bio));
    }
}
