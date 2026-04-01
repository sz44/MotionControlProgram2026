using System.Collections.Concurrent;
using MotionControlConsole.Abstractions;
using MotionControlConsole.Domain;

namespace MotionControlConsole.Services;

public sealed class DeviceManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Device> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ControlBridge _bridge;

    public DeviceManager(ControlBridge bridge)
    {
        _bridge = bridge;
    }

    public async Task<Device> AddDeviceAsync(string id, IConnection connection, CancellationToken cancellationToken = default)
    {
        var device = new Device(id, connection, _bridge.EventWriter);

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

    public Task SendCommandAsync(DeviceCommand command, CancellationToken cancellationToken)
    {
        if (!_devices.TryGetValue(command.DeviceId, out var device))
        {
            throw new KeyNotFoundException($"Unknown device '{command.DeviceId}'.");
        }

        return device.SendCommandAsync(command.CommandText, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var device in _devices.Values)
        {
            await device.DisposeAsync();
        }
    }
}
