namespace MotionControlConsole.Domain;

public sealed record DeviceEvent(string DeviceId, string Message, DateTimeOffset TimestampUtc);
