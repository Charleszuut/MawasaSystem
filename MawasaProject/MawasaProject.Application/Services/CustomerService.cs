using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Validation;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class CustomerService(
    ICustomerRepository customerRepository,
    IAuditInterceptor auditInterceptor) : ICustomerService
{
    public async Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        EntityValidator.ValidateObject(customer);
        await customerRepository.AddAsync(customer, cancellationToken);

        await auditInterceptor.TrackAsync(
            AuditActionType.Create,
            nameof(Customer),
            customer.Id.ToString(),
            oldValue: null,
            newValue: new
            {
                customer.Name,
                customer.PhoneNumber,
                customer.Email,
                customer.Address
            },
            context: "Customer created",
            username: null,
            cancellationToken);

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

        await auditInterceptor.TrackAsync(
            AuditActionType.Update,
            nameof(Customer),
            customer.Id.ToString(),
            oldValue: null,
            newValue: new
            {
                customer.Name,
                customer.PhoneNumber,
                customer.Email,
                customer.Address
            },
            context: "Customer updated",
            username: null,
            cancellationToken);
    }

    public async Task DisconnectCustomerAsync(Guid customerId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Disconnection reason is required.", nameof(reason));

        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
            throw new KeyNotFoundException($"Customer {customerId} not found.");

        customer.Disconnect(reason.Trim());
        await customerRepository.UpdateAsync(customer, cancellationToken);

        await auditInterceptor.TrackAsync(
            AuditActionType.Update,
            nameof(Customer),
            customer.Id.ToString(),
            oldValue: new { Status = "Connected" },
            newValue: new { Status = "Disconnected", Reason = customer.DisconnectionReason },
            context: "Customer disconnected",
            username: null,
            cancellationToken);
    }

    public async Task ReconnectCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
            throw new KeyNotFoundException($"Customer {customerId} not found.");

        customer.Reconnect();
        await customerRepository.UpdateAsync(customer, cancellationToken);

        await auditInterceptor.TrackAsync(
            AuditActionType.Update,
            nameof(Customer),
            customer.Id.ToString(),
            oldValue: new { Status = "Disconnected" },
            newValue: new { Status = "Connected" },
            context: "Customer reconnected",
            username: null,
            cancellationToken);
    }
}
