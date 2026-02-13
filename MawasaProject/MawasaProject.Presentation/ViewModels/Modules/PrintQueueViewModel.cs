using System.Collections.ObjectModel;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class PrintQueueViewModel : BaseViewModel
{
    public ObservableCollection<string> QueueItems { get; } = [];

    public RelayCommand RefreshCommand => new(() =>
    {
        QueueItems.Clear();
        QueueItems.Add("Queue processing handled by PrintQueueService.");
        QueueItems.Add("Use Printer module to enqueue print jobs.");
    });
}
