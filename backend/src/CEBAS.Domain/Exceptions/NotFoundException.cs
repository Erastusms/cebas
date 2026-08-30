namespace CEBAS.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public string ResourceName { get; }
    public object? ResourceKey { get; }

    public NotFoundException(string message) : base(message)
    {
        ResourceName = "Resource";
    }

    public NotFoundException(string resourceName, object resourceKey)
        : base($"{resourceName} with identifier '{resourceKey}' was not found.")
    {
        ResourceName = resourceName;
        ResourceKey = resourceKey;
    }
}
