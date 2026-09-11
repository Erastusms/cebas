using FluentValidation;

namespace CEBAS.Application.Contracts.Users;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly HashSet<string> AllowedThemePreferences = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIGHT",
        "DARK",
        "SYSTEM"
    };

    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.DisplayName != null || x.Bio != null || x.BannerUrl != null || x.ThemePreference != null)
            .WithMessage("At least one profile field must be provided for update.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name cannot be empty.")
            .Length(1, 50).WithMessage("Display name must be between 1 and 50 characters.")
            .When(x => x.DisplayName != null);

        RuleFor(x => x.Bio)
            .MaximumLength(160).WithMessage("Biography cannot exceed 160 characters.")
            .When(x => !string.IsNullOrEmpty(x.Bio));

        RuleFor(x => x.BannerUrl)
            .MaximumLength(500).WithMessage("Banner URL cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.BannerUrl));

        RuleFor(x => x.ThemePreference)
            .Must(t => t != null && AllowedThemePreferences.Contains(t.Trim()))
            .WithMessage("Theme preference must be one of: LIGHT, DARK, SYSTEM.")
            .When(x => x.ThemePreference != null);
    }
}
