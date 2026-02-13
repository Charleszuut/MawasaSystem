namespace MawasaProject.Presentation.Services.Messaging;

public sealed class MessageBus : IMessageBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly object _lock = new();

    public IDisposable Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = [];
                _handlers[typeof(T)] = list;
            }

            list.Add(handler);
        }

        return new Subscription(() => Unsubscribe(handler));
    }

    public void Publish<T>(T message)
    {
        List<Delegate> snapshot;

        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                return;
            }

            snapshot = [.. list];
        }

        foreach (var handler in snapshot.Cast<Action<T>>())
        {
            handler(message);
        }
    }

    private void Unsubscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                _handlers.Remove(typeof(T));
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            dispose();
            _isDisposed = true;
        }
    }
}
