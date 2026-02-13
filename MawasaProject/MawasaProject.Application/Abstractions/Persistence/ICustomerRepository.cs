using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Persistence;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<IReadOnlyList<Customer>> SearchAsync(string? searchText, CancellationToken cancellationToken = default);
}
