namespace MotionControlConsole.Services;

public sealed class ConsoleUi
{
    public async Task StartAsync(AppController app, CancellationToken cancellationToken)
    {
        Console.WriteLine("Concurrent Motion Control Console");
        Console.WriteLine("Commands:");
        Console.WriteLine("  list");
        Console.WriteLine("  send <deviceId> <command>");
        Console.WriteLine("  exit");
        Console.WriteLine();

        using var uiCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var displayTask = DisplayEventsAsync(app, uiCancellation.Token);

        try
        {
            while (!uiCancellation.Token.IsCancellationRequested)
            {
                Console.Write("> ");
                var input = await Task.Run(Console.ReadLine, uiCancellation.Token);

                if (input is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                var shouldContinue = await app.HandleInputAsync(input, uiCancellation.Token);
                if (!shouldContinue)
                {
                    break;
                }
            }
        }
        finally
        {
            uiCancellation.Cancel();

            try
            {
                await displayTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task DisplayEventsAsync(AppController app, CancellationToken cancellationToken)
    {
        await foreach (var deviceEvent in app.ReadEventsAsync(cancellationToken))
        {
            Console.WriteLine();
            Console.WriteLine($"[{deviceEvent.TimestampUtc:HH:mm:ss}] {deviceEvent.DeviceId}: {deviceEvent.Message}");
        }
    }
}
