using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class CustomersViewModel(
    ICustomerService customerService,
    IDialogService dialogService) : BaseViewModel
{
    private string _searchQuery = string.Empty;
    private string _name = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;

    public CustomersViewModel() : this(
        App.Services.GetRequiredService<ICustomerService>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

    public ObservableCollection<Customer> Customers { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public AsyncCommand SearchCommand => new(async () => await RunBusyAsync(async () =>
    {
        var items = await customerService.SearchCustomersAsync(SearchQuery);
        Customers.Clear();
        foreach (var item in items)
        {
            Customers.Add(item);
        }
    }));

    public AsyncCommand AddCustomerCommand => new(async () => await RunBusyAsync(async () =>
    {
        var customer = new Customer
        {
            Name = Name,
            PhoneNumber = Phone,
            Email = Email,
            CreatedAtUtc = DateTime.UtcNow
        };

        await customerService.CreateCustomerAsync(customer);
        var items = await customerService.SearchCustomersAsync(SearchQuery);
        Customers.Clear();
        foreach (var item in items)
        {
            Customers.Add(item);
        }

        await dialogService.AlertAsync("Customer", "Customer created successfully.");
    }));
}
