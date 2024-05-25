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
        await using var stream = new MemoryStream();
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(1024);

        var readMemory = memoryOwner.Memory;
        var result = default(ValueWebSocketReceiveResult);
        do
        {
            result = await client.ReceiveAsync(readMemory, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(memoryOwner.Memory[..result.Count], cancellationToken).ConfigureAwait(false);
        } while (!cancellationToken.IsCancellationRequested && !result.EndOfMessage);

        return encoding.GetString(stream.ToArray());
    }
}
