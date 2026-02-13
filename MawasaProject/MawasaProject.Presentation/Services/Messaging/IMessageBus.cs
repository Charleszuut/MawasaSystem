namespace MawasaProject.Presentation.Services.Messaging;

public interface IMessageBus
{
    IDisposable Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
}
