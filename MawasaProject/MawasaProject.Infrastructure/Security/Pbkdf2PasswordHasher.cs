using System.Security.Cryptography;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Domain.ValueObjects;

namespace MawasaProject.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 120000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public HashedPassword Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var keyBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);

        return new HashedPassword
        {
            Hash = Convert.ToBase64String(keyBytes),
            Salt = Convert.ToBase64String(saltBytes)
        };
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var keyBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);
        var hashBytes = Convert.FromBase64String(hash);

        return CryptographicOperations.FixedTimeEquals(keyBytes, hashBytes);
    }
}
