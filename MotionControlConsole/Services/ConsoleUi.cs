using MotionControlConsole.Domain;

namespace MotionControlConsole.Services;

public sealed class ConsoleUi
{
    public async Task RunInputLoopAsync(CommandRouter commandRouter, CancellationToken cancellationToken)
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

            var shouldContinue = await commandRouter.HandleAsync(input, cancellationToken);
            if (!shouldContinue)
            {
                break;
            }
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
}
