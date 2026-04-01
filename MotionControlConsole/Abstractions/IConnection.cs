namespace MotionControlConsole.Abstractions;

public interface IConnection : IAsyncDisposable
{
    string ConnectionType { get; }
    bool IsConnected { get; }

    Task ConnectAsync(Func<string, Task> onMessage, CancellationToken cancellationToken);
    Task SendAsync(string command, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}
