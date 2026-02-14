namespace MawasaProject.Infrastructure.Repositories;

public sealed class RepositoryException(string message, Exception innerException) : Exception(message, innerException)
{
}
