using System.Collections.Immutable;

namespace Buttplug;

public delegate void ButtplugDeviceEndpointSubscriptionReadingCallback(ButtplugDevice device, string endpoint, ImmutableArray<byte> data);

public record class ButtplugDeviceEndpointSubscription
{
    private readonly ButtplugDeviceEndpointSubscriptionReadingCallback _readingCallback;

    public ButtplugDevice Device { get; }
    public string Endpoint { get; }

    internal ButtplugDeviceEndpointSubscription(ButtplugDevice device, string endpoint, ButtplugDeviceEndpointSubscriptionReadingCallback readingCallback)
    {
        _readingCallback = readingCallback;
        Device = device;
        Endpoint = endpoint;
    }

    internal void HandleReadingData(ImmutableArray<byte> data)
        => _readingCallback(Device, Endpoint, data);
    public async Task UnsubscribeAsync(CancellationToken cancellationToken)
        => await Device.AsUnsafe().EndpointUnsubscribeAsync(Endpoint, cancellationToken).ConfigureAwait(false);
}
