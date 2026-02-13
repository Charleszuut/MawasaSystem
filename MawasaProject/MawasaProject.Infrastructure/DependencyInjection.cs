using Microsoft.Extensions.DependencyInjection;
using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Infrastructure.Data;
using MawasaProject.Infrastructure.Data.SQLite;
using MawasaProject.Infrastructure.Logging;
using MawasaProject.Infrastructure.Repositories;
using MawasaProject.Infrastructure.Security;
using MawasaProject.Infrastructure.Services.Audit;
using MawasaProject.Infrastructure.Services.Backup;
using MawasaProject.Infrastructure.Services.Documents;
using MawasaProject.Infrastructure.Services.Printing;

namespace MawasaProject.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton(new SqliteDatabaseOptions
        {
            DatabasePath = databasePath,
            EnableForeignKeys = true,
            EnableWriteAheadLog = true,
            BusyTimeoutMs = 7000,
            DefaultCommandTimeoutSeconds = 45,
            SynchronousMode = "NORMAL",
            TempStore = "MEMORY",
            CacheSizeKiB = 65536,
            MaxRetryCount = 5,
            BaseRetryDelayMs = 80
        });

        services.AddSingleton<ISqliteConnectionManager, SqliteConnectionManager>();
        services.AddScoped<SqliteDatabaseService>();

        services.AddScoped<IDatabaseInitializer, SqliteDatabaseInitializer>();

        services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBillRepository, BillRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISessionService, InMemorySessionService>();
        services.AddSingleton<IRbacService, RoleBasedAccessService>();
        services.AddSingleton<RoleManager>();
        services.AddSingleton<PermissionService>();

        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IRestoreService, RestoreService>();
        services.AddSingleton<BackupScheduler>();

        services.AddSingleton<PrintQueueService>();
        services.AddScoped<IPrinterService, PrinterService>();

        services.AddSingleton<TemplateEngine>();
        services.AddSingleton<LayoutRenderer>();
        services.AddSingleton<PdfGenerator>();
        services.AddSingleton<ReceiptNumberGenerator>();
        services.AddSingleton<InvoiceNumberGenerator>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IInvoiceService, InvoiceService>();

        services.AddScoped<EntityDiffService>();
        services.AddScoped<AuditInterceptor>();

        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

        return services;
    }
}
