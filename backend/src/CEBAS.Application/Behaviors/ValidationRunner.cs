using FluentValidation;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Application.Behaviors;

public static class ValidationRunner
{
    public static async Task ValidateAndThrowAsync<T>(IValidator<T>? validator, T instance, CancellationToken cancellationToken = default)
    {
        if (validator is null) return;

        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            throw new CEBAS.Domain.Exceptions.ValidationException(errors);
        }
    }
}
