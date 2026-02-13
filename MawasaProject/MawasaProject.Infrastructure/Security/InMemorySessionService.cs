using MawasaProject.Application.Abstractions.Security;

namespace MawasaProject.Infrastructure.Security;

public sealed class InMemorySessionService : ISessionService
{
    private SessionContext? _current;

    public SessionContext? CurrentSession => _current;

    public void Set(SessionContext context)
    {
        _current = context;
    }

    public void Clear()
    {
        _current = null;
    }

    public void Touch()
    {
        if (_current is null)
        {
            return;
        }

        _current = _current with { LastActivityAtUtc = DateTime.UtcNow };
    }
}
