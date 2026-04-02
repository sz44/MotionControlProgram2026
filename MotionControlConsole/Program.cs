using MotionControlConsole.Connections;
using MotionControlConsole.Services;

await using var deviceManager = new DeviceManager();

await deviceManager.AddDeviceAsync("axis-x", new SimulatedConnection("axis-x"));
await deviceManager.AddDeviceAsync("axis-y", new SimulatedConnection("axis-y"));

using var appCancellation = new CancellationTokenSource();

var ui = new ConsoleUi();
var router = new CommandRouter(deviceManager);
var displayTask = ui.DisplayEventsAsync(deviceManager.Events, appCancellation.Token);

await ui.RunInputLoopAsync(router, appCancellation.Token);

appCancellation.Cancel();

try
{
    await displayTask;
}
catch (OperationCanceledException)
{
}
