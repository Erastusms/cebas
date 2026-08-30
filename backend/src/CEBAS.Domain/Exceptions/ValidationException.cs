namespace CEBAS.Domain.Exceptions;

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message = "One or more validation failures occurred.")
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors, string message = "One or more validation failures occurred.")
        : base(message)
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage)
        : base("One or more validation failures occurred.")
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, new[] { errorMessage } }
        };
    }
}
