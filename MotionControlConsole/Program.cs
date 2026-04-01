using MotionControlConsole.Connections;
using MotionControlConsole.Services;

var bridge = new ControlBridge();
var deviceManager = new DeviceManager(bridge);

await deviceManager.AddDeviceAsync("axis-x", new SimulatedConnection("axis-x"));
await deviceManager.AddDeviceAsync("axis-y", new SimulatedConnection("axis-y"));

using var appCancellation = new CancellationTokenSource();

var dispatcherTask = bridge.RunCommandDispatcherAsync(deviceManager, appCancellation.Token);
var ui = new ConsoleUi();
var displayTask = ui.DisplayEventsAsync(bridge.EventReader, appCancellation.Token);

await ui.RunInputLoopAsync(deviceManager, bridge, appCancellation.Token);

bridge.CompleteCommands();
await dispatcherTask;
bridge.CompleteEvents();
await displayTask;

appCancellation.Cancel();

await deviceManager.DisposeAsync();
