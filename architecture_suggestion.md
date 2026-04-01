A clean way to structure this is:

**UI → application bridge → device manager → device → connection**

And for messages coming back:

**connection/device → device event loop → shared message bus → UI**

The “bridge” you are unsure about is usually an **application service / command router**. It sits between the console UI and the device layer.

## Recommended architecture

### Core pieces

`IDeviceConnection`

* Abstraction for physical/simulated transport.
* `SerialPortConnection` and `SimulatedConnection` implement it.

`Device`

* Owns one connection.
* Has its own async receive loop.
* Can send commands.
* Publishes incoming messages.

`DeviceManager`

* Creates devices.
* Tracks them by device id.
* Connects/disconnects devices.
* Sends commands to a specific device.

`CommandRouter` or `ApplicationController`

* This is your bridge.
* Accepts UI commands like `send motor1 MOVE 100`.
* Parses/routs them to `DeviceManager`.
* Keeps UI logic out of the device layer.

`MessageBus`

* A shared `Channel<DeviceMessage>` is perfect here.
* Devices write messages into it.
* UI reads from it and displays messages.

## Why this works well

It keeps responsibilities separate:

* **UI** only deals with user input and display.
* **CommandRouter** interprets user intent.
* **DeviceManager** knows which device exists and where to send commands.
* **Device** handles per-device concurrency.
* **Connection** handles transport details.

That separation will make it much easier later if you replace console with WPF, WinForms, web UI, or add more connection types.

---

# Suggested flow

## Outbound commands

1. User types:
   `send dev1 MOVE 100`
2. Console UI passes raw text to `CommandRouter`.
3. `CommandRouter` parses command and calls:
   `DeviceManager.SendCommandAsync("dev1", "MOVE 100")`
4. `DeviceManager` finds `Device dev1`
5. `Device` writes the command to its connection

## Inbound messages

1. `SerialPortConnection` or `SimulatedConnection` receives data
2. `Device` wraps it in a `DeviceMessage`
3. `Device` writes message into `Channel<DeviceMessage>`
4. Console UI has a background reader task that prints messages

---

# Best concurrency model

Use:

* `async/await`
* one `Channel<DeviceMessage>` for device-to-UI messages
* optional one `Channel<UiCommand>` for UI-to-app commands if you want the console input loop fully decoupled

For a simple app, you do **not** need a channel for UI commands yet. A direct call from UI to `CommandRouter` is simpler.

So:

* **messages from devices**: use `Channel<DeviceMessage>`
* **commands from UI**: direct method calls into `CommandRouter`

Later, if you want more decoupling, add a `Channel<UiCommand>` too.

---

# Minimal domain model

## Messages

```csharp
public record DeviceMessage(
    string DeviceId,
    DateTime Timestamp,
    string Text);
```

## UI commands

```csharp
public abstract record UiCommand;

public record SendDeviceCommand(string DeviceId, string CommandText) : UiCommand;
public record ConnectDeviceCommand(string DeviceId) : UiCommand;
public record ListDevicesCommand() : UiCommand;
public record ExitCommand() : UiCommand;
```

---

# Connection abstraction

```csharp
public interface IDeviceConnection : IAsyncDisposable
{
    Task OpenAsync(CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
    Task SendAsync(string command, CancellationToken ct);

    IAsyncEnumerable<string> ReadMessagesAsync(CancellationToken ct);

    bool IsOpen { get; }
}
```

This is better than exposing events because it fits naturally with async streams.

---

# Simulated connection

```csharp
using System.Threading.Channels;

public sealed class SimulatedConnection : IDeviceConnection
{
    private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
    private bool _isOpen;
    private Task? _simulationTask;
    private CancellationTokenSource? _internalCts;

    public bool IsOpen => _isOpen;

    public Task OpenAsync(CancellationToken ct)
    {
        if (_isOpen) return Task.CompletedTask;

        _isOpen = true;
        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _simulationTask = Task.Run(async () =>
        {
            var token = _internalCts.Token;
            var counter = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);
                    await _incoming.Writer.WriteAsync($"SIM STATUS {counter++}", token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _incoming.Writer.TryComplete();
            }
        }, _internalCts.Token);

        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken ct)
    {
        if (!_isOpen) return Task.CompletedTask;

        _isOpen = false;
        _internalCts?.Cancel();
        return Task.CompletedTask;
    }

    public async Task SendAsync(string command, CancellationToken ct)
    {
        if (!_isOpen) throw new InvalidOperationException("Connection not open.");

        await Task.Delay(100, ct);
        await _incoming.Writer.WriteAsync($"SIM ACK {command}", ct);
    }

    public async IAsyncEnumerable<string> ReadMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (await _incoming.Reader.WaitToReadAsync(ct))
        {
            while (_incoming.Reader.TryRead(out var msg))
            {
                yield return msg;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CloseAsync(CancellationToken.None);
        }
        catch
        {
        }
    }
}
```

---

# Device

A `Device` should own its receive loop.

```csharp
using System.Threading.Channels;

public sealed class Device : IAsyncDisposable
{
    private readonly IDeviceConnection _connection;
    private readonly ChannelWriter<DeviceMessage> _messageWriter;
    private Task? _receiveTask;
    private CancellationTokenSource? _cts;

    public string Id { get; }

    public Device(string id, IDeviceConnection connection, ChannelWriter<DeviceMessage> messageWriter)
    {
        Id = id;
        _connection = connection;
        _messageWriter = messageWriter;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_cts != null) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await _connection.OpenAsync(_cts.Token);

        _receiveTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var text in _connection.ReadMessagesAsync(_cts.Token))
                {
                    var msg = new DeviceMessage(Id, DateTime.UtcNow, text);
                    await _messageWriter.WriteAsync(msg, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await _messageWriter.WriteAsync(
                    new DeviceMessage(Id, DateTime.UtcNow, $"ERROR: {ex.Message}"),
                    CancellationToken.None);
            }
        }, _cts.Token);
    }

    public Task SendCommandAsync(string command, CancellationToken ct)
    {
        return _connection.SendAsync(command, ct);
    }

    public async Task DisconnectAsync()
    {
        if (_cts == null) return;

        _cts.Cancel();
        await _connection.CloseAsync(CancellationToken.None);

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
        }

        _cts.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        await _connection.DisposeAsync();
    }
}
```

---

# DeviceManager

This is responsible for lookup and routing to the correct device.

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;

public sealed class DeviceManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Device> _devices = new();
    private readonly Channel<DeviceMessage> _messageChannel =
        Channel.CreateUnbounded<DeviceMessage>();

    public ChannelReader<DeviceMessage> Messages => _messageChannel.Reader;

    public async Task AddDeviceAsync(string deviceId, IDeviceConnection connection, CancellationToken ct)
    {
        var device = new Device(deviceId, connection, _messageChannel.Writer);

        if (!_devices.TryAdd(deviceId, device))
            throw new InvalidOperationException($"Device '{deviceId}' already exists.");

        await device.ConnectAsync(ct);
    }

    public Task SendCommandAsync(string deviceId, string command, CancellationToken ct)
    {
        if (!_devices.TryGetValue(deviceId, out var device))
            throw new KeyNotFoundException($"Device '{deviceId}' not found.");

        return device.SendCommandAsync(command, ct);
    }

    public IReadOnlyCollection<string> GetDeviceIds() => _devices.Keys.ToList();

    public async Task RemoveDeviceAsync(string deviceId)
    {
        if (_devices.TryRemove(deviceId, out var device))
        {
            await device.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var pair in _devices)
        {
            await pair.Value.DisposeAsync();
        }

        _messageChannel.Writer.TryComplete();
    }
}
```

---

# The bridge: CommandRouter

This is the missing piece you were describing.

```csharp
public sealed class CommandRouter
{
    private readonly DeviceManager _deviceManager;

    public CommandRouter(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    public async Task<bool> HandleAsync(string input, CancellationToken ct)
    {
        var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        switch (parts[0].ToLowerInvariant())
        {
            case "list":
                foreach (var id in _deviceManager.GetDeviceIds())
                {
                    Console.WriteLine($"- {id}");
                }
                return true;

            case "send":
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: send <deviceId> <command>");
                    return true;
                }

                await _deviceManager.SendCommandAsync(parts[1], parts[2], ct);
                return true;

            case "exit":
                return false;

            default:
                Console.WriteLine("Commands: list, send <deviceId> <command>, exit");
                return true;
        }
    }
}
```

This keeps parsing and app-level behavior out of `DeviceManager`.

---

# Console UI

Two loops:

* one reads user input
* one reads device messages and prints them

```csharp
class Program
{
    static async Task Main()
    {
        using var cts = new CancellationTokenSource();

        await using var deviceManager = new DeviceManager();

        await deviceManager.AddDeviceAsync("dev1", new SimulatedConnection(), cts.Token);
        await deviceManager.AddDeviceAsync("dev2", new SimulatedConnection(), cts.Token);

        var router = new CommandRouter(deviceManager);

        var displayTask = Task.Run(async () =>
        {
            await foreach (var msg in deviceManager.Messages.ReadAllAsync(cts.Token))
            {
                Console.WriteLine($"[{msg.Timestamp:HH:mm:ss}] [{msg.DeviceId}] {msg.Text}");
            }
        }, cts.Token);

        Console.WriteLine("Commands: list, send <deviceId> <command>, exit");

        try
        {
            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (input is null) break;

                bool shouldContinue = await router.HandleAsync(input, cts.Token);
                if (!shouldContinue)
                    break;
            }
        }
        finally
        {
            cts.Cancel();
            try { await displayTask; } catch { }
        }
    }
}
```

---

# Important design choices

## 1. One shared message channel is enough

You do not need one queue per device unless you have special requirements.

A single:

```csharp
Channel<DeviceMessage>
```

is enough for the UI to observe all device traffic.

## 2. DeviceManager should not parse console commands

That belongs in the bridge/application layer.

## 3. Device should not know about the console

It should only publish messages.

## 4. Connection should not know about device ids

It just sends and receives raw text.

---

# Suggested project structure

```text
MotionControl/
  Program.cs

  Application/
    CommandRouter.cs
    UiCommand.cs

  Devices/
    Device.cs
    DeviceManager.cs
    DeviceMessage.cs

  Connections/
    IDeviceConnection.cs
    SerialPortConnection.cs
    SimulatedConnection.cs
```

---

# What to do when you add SerialPort later

Your `SerialPortConnection` should implement the same interface:

```csharp
public sealed class SerialPortConnection : IDeviceConnection
{
    // wrap System.IO.Ports.SerialPort here
}
```

So the rest of the app does not change.

That is the main value of the abstraction.

---

# A slightly better next step

Once this basic version works, add a **typed command** instead of raw strings.

For example:

```csharp
public record MotionCommand(string Name, string[] Args);
```

Then UI parses text into `MotionCommand`, and the device/connection formats it into wire protocol.

That avoids spreading string parsing everywhere.

---

# My recommendation for your current version

Keep it simple:

* `Channel<DeviceMessage>` for device-to-UI messages
* direct call from console to `CommandRouter`
* `CommandRouter` calls `DeviceManager`
* `DeviceManager` routes by device id
* `Device` owns connection and receive loop
* `SimulatedConnection` first, `SerialPortConnection` later

That is a solid starting architecture without overengineering.

I can also turn this into a small runnable `.NET` console project with all files filled in.
