using MotionControlConsole.Abstractions;

namespace MotionControlConsole.Connections;

public sealed class SimulatedConnection : IConnection
{
    private readonly string _deviceId;
    private Func<string, Task>? _onMessage;
    private CancellationTokenSource? _backgroundCancellation;
    private Task? _backgroundTask;
    private int _position;

    public SimulatedConnection(string deviceId)
    {
        _deviceId = deviceId;
    }

    public string ConnectionType => nameof(SimulatedConnection);
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(Func<string, Task> onMessage, CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return Task.CompletedTask;
        }

        _onMessage = onMessage;
        _backgroundCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunStatusLoopAsync(_backgroundCancellation.Token);
        IsConnected = true;

        return PublishAsync("Simulator ready.");
    }

    public async Task SendAsync(string command, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException($"Device '{_deviceId}' is not connected.");
        }

        var normalized = command.Trim();

        if (normalized.StartsWith("move ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized["move ".Length..], out var distance))
        {
            _position += distance;
            await PublishAsync($"Moved to position {_position}.");
            return;
        }

        if (normalized.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            _position = 0;
            await PublishAsync("Homed to position 0.");
            return;
        }

        if (normalized.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            await PublishAsync($"Status: position={_position}, state=Idle.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await PublishAsync($"Command '{command}' acknowledged.");
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return;
        }

        _backgroundCancellation?.Cancel();

        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        IsConnected = false;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task RunStatusLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await PublishAsync($"Heartbeat: position={_position}.");
        }
    }

    private Task PublishAsync(string message)
    {
        return _onMessage is null ? Task.CompletedTask : _onMessage(message);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
        _backgroundCancellation?.Dispose();
    }
}
