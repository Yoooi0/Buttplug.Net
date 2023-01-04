using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Buttplug;

internal static class Extensions
{
    public static void Throw(this Exception e) => ExceptionDispatchInfo.Capture(e).Throw();

    public static void ThrowIfFaulted(this Task task)
    {
        var e = task.Exception;
        if (e == null)
            return;

        if (e.InnerExceptions.Count == 1)
            e.InnerExceptions[0].Throw();
        else
            e.Throw();
    }

    public static async Task<string> ReceiveStringAsync(this ClientWebSocket client, Encoding encoding, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(1024);

        var result = default(ValueWebSocketReceiveResult);
        do
        {
            result = await client.ReceiveAsync(memoryOwner.Memory, cancellationToken);
            await stream.WriteAsync(memoryOwner.Memory[..result.Count], cancellationToken);
        } while (!cancellationToken.IsCancellationRequested && !result.EndOfMessage);

        return encoding.GetString(stream.ToArray());
    }
}
