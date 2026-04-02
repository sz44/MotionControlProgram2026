using MotionControlConsole.Connections;
using MotionControlConsole.Services;

await using var deviceManager = new DeviceManager();
var app = new AppController(deviceManager);

await deviceManager.AddDeviceAsync("axis-x", new SimulatedConnection("axis-x"));
await deviceManager.AddDeviceAsync("axis-y", new SimulatedConnection("axis-y"));

using var appCancellation = new CancellationTokenSource();

var ui = new ConsoleUi();
await ui.StartAsync(app, appCancellation.Token);
