using System.Security.Cryptography;
using System.Text;
using CEBAS.Application.Abstractions;

namespace CEBAS.Infrastructure.Services;

public class SessionTokenService : ISessionTokenService
{
    public string GenerateRawToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    public string ComputeTokenHash(string rawToken)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
