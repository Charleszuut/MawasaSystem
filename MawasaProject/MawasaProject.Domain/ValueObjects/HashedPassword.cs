namespace MawasaProject.Domain.ValueObjects;

public sealed record HashedPassword
{
    public required string Hash { get; init; }
    public required string Salt { get; init; }
}
