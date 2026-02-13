using MawasaProject.Domain.ValueObjects;

namespace MawasaProject.Application.Abstractions.Security;

public interface IPasswordHasher
{
    HashedPassword Hash(string password);
    bool Verify(string password, string hash, string salt);
}
