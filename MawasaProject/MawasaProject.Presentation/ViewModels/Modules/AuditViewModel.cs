using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class AuditViewModel(IAuditService auditService) : BaseViewModel
{
    public AuditViewModel() : this(App.Services.GetRequiredService<IAuditService>())
    {
    }

    public ObservableCollection<AuditLog> Logs { get; } = [];

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var items = await auditService.GetLogsAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        Logs.Clear();

        foreach (var item in items)
        {
            Logs.Add(item);
        }
    }));
}
