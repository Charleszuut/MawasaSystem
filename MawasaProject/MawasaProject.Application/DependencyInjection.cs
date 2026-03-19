using Microsoft.Extensions.DependencyInjection;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Rules;
using MawasaProject.Application.Services;

namespace MawasaProject.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<BusinessRuleEngine>();

        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IBillingService, BillingService>();
        services.AddTransient<IPaymentService, PaymentService>();
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IDashboardService, DashboardService>();
        services.AddTransient<IReportService, ReportService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IUserService, UserService>();

        return services;
    }
}
