using MotionControlConsole.Domain;

namespace MotionControlConsole.Services;

public sealed class ConsoleUi
{
    public async Task RunInputLoopAsync(DeviceManager deviceManager, ControlBridge bridge, CancellationToken cancellationToken)
    {
        Console.WriteLine("Concurrent Motion Control Console");
        Console.WriteLine("Commands:");
        Console.WriteLine("  list");
        Console.WriteLine("  send <deviceId> <command>");
        Console.WriteLine("  exit");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = await Task.Run(Console.ReadLine, cancellationToken);

            if (input is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var device in deviceManager.GetDevices())
                {
                    Console.WriteLine($"  {device.Id} [{device.Connection.ConnectionType}] Connected={device.IsConnected}");
                }

                continue;
            }

            if (TryParseSend(input, out var deviceId, out var commandText))
            {
                await bridge.QueueCommandAsync(deviceId, commandText, cancellationToken);
                continue;
            }

            Console.WriteLine("Unknown command. Use 'list', 'send <deviceId> <command>', or 'exit'.");
        }
    }

    public async Task DisplayEventsAsync(System.Threading.Channels.ChannelReader<DeviceEvent> eventReader, CancellationToken cancellationToken)
    {
        await foreach (var deviceEvent in eventReader.ReadAllAsync(cancellationToken))
        {
            Console.WriteLine();
            Console.WriteLine($"[{deviceEvent.TimestampUtc:HH:mm:ss}] {deviceEvent.DeviceId}: {deviceEvent.Message}");
        }
    }

    private static bool TryParseSend(string input, out string deviceId, out string commandText)
    {
        const string prefix = "send ";

        if (!input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            deviceId = string.Empty;
            commandText = string.Empty;
            return false;
        }

        var remainder = input[prefix.Length..].Trim();
        var firstSpace = remainder.IndexOf(' ');

        if (firstSpace <= 0 || firstSpace == remainder.Length - 1)
        {
            deviceId = string.Empty;
            commandText = string.Empty;
            return false;
        }

        deviceId = remainder[..firstSpace];
        commandText = remainder[(firstSpace + 1)..].Trim();
        return true;
    }
}
