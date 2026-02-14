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
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required.");
        }

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
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
        {
            return false;
        }

        byte[] saltBytes;
        byte[] hashBytes;

        try
        {
            saltBytes = Convert.FromBase64String(salt);
            hashBytes = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (hashBytes.Length != KeySize)
        {
            return false;
        }

        var keyBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);

        return CryptographicOperations.FixedTimeEquals(keyBytes, hashBytes);
    }
}
