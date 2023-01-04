namespace Buttplug;

public class ButtplugException : Exception
{
    internal ButtplugException() { }
    internal ButtplugException(string? message) : base(message) { }
    internal ButtplugException(string? message, Exception? innerException) : base(message, innerException) { }
    internal ButtplugException(ErrorButtplugMessage message) : this($"{message.ErrorCode}({message.Id}): {message.ErrorMessage}") { }
}