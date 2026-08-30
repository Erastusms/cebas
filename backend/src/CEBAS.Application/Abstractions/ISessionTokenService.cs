namespace CEBAS.Application.Abstractions;

public interface ISessionTokenService
{
    string GenerateRawToken();
    string ComputeTokenHash(string rawToken);
}
