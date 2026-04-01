using System.Threading.Channels;
using MotionControlConsole.Domain;

namespace MotionControlConsole.Services;

public sealed class ControlBridge
{
    private readonly Channel<DeviceCommand> _commands = Channel.CreateUnbounded<DeviceCommand>();
    private readonly Channel<DeviceEvent> _events = Channel.CreateUnbounded<DeviceEvent>();

    public ChannelWriter<DeviceCommand> CommandWriter => _commands.Writer;
    public ChannelReader<DeviceCommand> CommandReader => _commands.Reader;
    public ChannelWriter<DeviceEvent> EventWriter => _events.Writer;
    public ChannelReader<DeviceEvent> EventReader => _events.Reader;

    public ValueTask QueueCommandAsync(string deviceId, string commandText, CancellationToken cancellationToken)
    {
        var command = new DeviceCommand(deviceId, commandText, DateTimeOffset.UtcNow);
        return CommandWriter.WriteAsync(command, cancellationToken);
    }

    public void CompleteCommands()
    {
        _commands.Writer.TryComplete();
    }

    public void CompleteEvents()
    {
        _events.Writer.TryComplete();
    }

    public async Task RunCommandDispatcherAsync(DeviceManager deviceManager, CancellationToken cancellationToken)
    {
        await foreach (var command in CommandReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await deviceManager.SendCommandAsync(command, cancellationToken);
            }
            catch (Exception ex)
            {
                await EventWriter.WriteAsync(
                    new DeviceEvent(command.DeviceId, $"Dispatch error: {ex.Message}", DateTimeOffset.UtcNow),
                    cancellationToken);
            }
        }
    }
}
