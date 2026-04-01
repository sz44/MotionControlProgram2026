namespace MotionControlConsole.Domain;

public sealed record DeviceCommand(string DeviceId, string CommandText, DateTimeOffset CreatedAtUtc);
