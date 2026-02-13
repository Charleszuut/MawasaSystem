using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Validation;
using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Services;

public sealed class CustomerService(ICustomerRepository customerRepository) : ICustomerService
{
    public async Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        EntityValidator.ValidateObject(customer);
        await customerRepository.AddAsync(customer, cancellationToken);
        return customer;
    }

    public Task<IReadOnlyList<Customer>> SearchCustomersAsync(string? query, CancellationToken cancellationToken = default)
    {
        return customerRepository.SearchAsync(query, cancellationToken);
    }

    public async Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        EntityValidator.ValidateObject(customer);
        customer.Touch();
        await customerRepository.UpdateAsync(customer, cancellationToken);
    }
}
