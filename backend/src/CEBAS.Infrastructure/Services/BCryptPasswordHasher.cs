using BCrypt.Net;
using CEBAS.Application.Abstractions;

namespace CEBAS.Infrastructure.Services;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor, HashType.SHA384);
    }

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash, HashType.SHA384);
        }
        catch
        {
            return false;
        }
    }
}
