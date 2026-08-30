namespace CEBAS.Domain.Exceptions;

public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Authentication is required to access this resource.") : base(message) { }
}
