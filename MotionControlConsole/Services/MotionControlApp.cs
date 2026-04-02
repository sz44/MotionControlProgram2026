using MotionControlConsole.Domain;

namespace MotionControlConsole.Services;

public sealed class MotionControlApp
{
    private readonly DeviceManager _deviceManager;

    public MotionControlApp(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    public IAsyncEnumerable<DeviceEvent> ReadEventsAsync(CancellationToken cancellationToken)
    {
        return _deviceManager.Events.ReadAllAsync(cancellationToken);
    }

    public async Task<bool> HandleInputAsync(string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return true;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "exit":
                return false;

            case "list":
                foreach (var device in _deviceManager.GetDevices())
                {
                    Console.WriteLine($"  {device.Id} [{device.Connection.ConnectionType}] Connected={device.IsConnected}");
                }

                return true;

            case "send":
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: send <deviceId> <command>");
                    return true;
                }

                try
                {
                    await _deviceManager.SendCommandAsync(parts[1], parts[2], cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Command failed: {ex.Message}");
                }

                return true;

            default:
                Console.WriteLine("Unknown command. Use 'list', 'send <deviceId> <command>', or 'exit'.");
                return true;
        }
    }
}
