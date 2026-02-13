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

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
