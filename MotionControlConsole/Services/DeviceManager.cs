using System.Collections.Concurrent;
using System.Threading.Channels;
using MotionControlConsole.Abstractions;
using MotionControlConsole.Domain;

namespace MotionControlConsole.Services;

public sealed class DeviceManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Device> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<DeviceEvent> _events = Channel.CreateUnbounded<DeviceEvent>();

    public ChannelReader<DeviceEvent> Events => _events.Reader;

    public async Task<Device> AddDeviceAsync(string id, IConnection connection, CancellationToken cancellationToken = default)
    {
        var device = new Device(id, connection, _events.Writer);

        if (!_devices.TryAdd(id, device))
        {
            await device.DisposeAsync();
            throw new InvalidOperationException($"A device named '{id}' already exists.");
        }

        await device.ConnectAsync(cancellationToken);
        return device;
    }

    public IReadOnlyCollection<Device> GetDevices()
    {
        return _devices.Values.OrderBy(device => device.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Task SendCommandAsync(string deviceId, string commandText, CancellationToken cancellationToken)
    {
        if (!_devices.TryGetValue(deviceId, out var device))
        {
            throw new KeyNotFoundException($"Unknown device '{deviceId}'.");
        }

        return device.SendCommandAsync(commandText, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var device in _devices.Values)
        {
            await device.DisposeAsync();
        }

        _events.Writer.TryComplete();
    }
}
