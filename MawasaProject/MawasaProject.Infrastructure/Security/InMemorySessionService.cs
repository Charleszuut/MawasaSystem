using MawasaProject.Application.Abstractions.Security;

namespace MawasaProject.Infrastructure.Security;

public sealed class InMemorySessionService : ISessionService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(8);
    private readonly object _sync = new();
    private SessionContext? _current;

    public SessionContext? CurrentSession
    {
        get
        {
            lock (_sync)
            {
                if (_current is null)
                {
                    return null;
                }

                if (DateTime.UtcNow - _current.LastActivityAtUtc > IdleTimeout)
                {
                    _current = null;
                    return null;
                }

                return _current;
            }
        }
    }

    public void Set(SessionContext context)
    {
        lock (_sync)
        {
            _current = context;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _current = null;
        }
    }

    public void Touch()
    {
        lock (_sync)
        {
            if (_current is null)
            {
                return;
            }

            _current = _current with { LastActivityAtUtc = DateTime.UtcNow };
        }
    }
}
