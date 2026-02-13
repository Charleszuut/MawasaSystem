using Microsoft.Extensions.DependencyInjection;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.Services.Messaging;
using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<AppStateStore>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BillingViewModel>();
        services.AddTransient<PaymentsViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<PrinterViewModel>();
        services.AddTransient<PrintQueueViewModel>();
        services.AddTransient<ReceiptViewModel>();
        services.AddTransient<InvoiceViewModel>();

        return services;
    }
}
