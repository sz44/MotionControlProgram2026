using MotionControlConsole.Abstractions;

namespace MotionControlConsole.Connections;

public sealed class SerialPortConnection : IConnection
{
    public string ConnectionType => nameof(SerialPortConnection);
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(Func<string, Task> onMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = true;

        return onMessage("Serial port connection placeholder attached.");
    }

    public Task SendAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsConnected)
        {
            throw new InvalidOperationException("Serial port is not connected.");
        }

        throw new NotImplementedException("Implement real serial I/O here.");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = false;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
    }
}
