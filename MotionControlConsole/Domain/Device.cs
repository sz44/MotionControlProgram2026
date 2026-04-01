using System.Threading.Channels;
using MotionControlConsole.Abstractions;

namespace MotionControlConsole.Domain;

public sealed class Device : IAsyncDisposable
{
    private readonly ChannelWriter<DeviceEvent> _eventWriter;

    public Device(string id, IConnection connection, ChannelWriter<DeviceEvent> eventWriter)
    {
        Id = id;
        Connection = connection;
        _eventWriter = eventWriter;
    }

    public string Id { get; }
    public IConnection Connection { get; }
    public bool IsConnected => Connection.IsConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Connection.ConnectAsync(HandleConnectionMessageAsync, cancellationToken);
        await PublishEventAsync($"Connected via {Connection.ConnectionType}.", cancellationToken);
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        await PublishEventAsync($"> {command}", cancellationToken);
        await Connection.SendAsync(command, cancellationToken);
    }

    private async Task HandleConnectionMessageAsync(string message)
    {
        await PublishEventAsync(message, CancellationToken.None);
    }

    private ValueTask PublishEventAsync(string message, CancellationToken cancellationToken)
    {
        return _eventWriter.WriteAsync(
            new DeviceEvent(Id, message, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}
