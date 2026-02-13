using MawasaProject.Application.Abstractions.Security;

namespace MawasaProject.Presentation.ViewModels.Core;

public sealed class AppStateStore : ObservableObject
{
    private SessionContext? _session;

    public SessionContext? Session
    {
        get => _session;
        set => SetProperty(ref _session, value);
    }
}
