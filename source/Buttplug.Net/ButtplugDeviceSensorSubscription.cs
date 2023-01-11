using System.Collections.Immutable;

namespace Buttplug;

public delegate void ButtplugDeviceSensorSubscriptionReadingCallback(ButtplugDevice device, SensorAttributeIdentifier sensorIdentifier, ImmutableArray<int> data);
public delegate Task ButtplugDeviceSensorSubscriptionUnsubscribe(SensorAttributeIdentifier sensorIdentifier, CancellationToken cancellationToken);

public record class ButtplugDeviceSensorSubscription
{
    private readonly ButtplugDeviceSensorSubscriptionReadingCallback _readingCallback;
    private readonly ButtplugDeviceSensorSubscriptionUnsubscribe _unsubscribe;

    public ButtplugDevice Device { get; }
    public SensorAttributeIdentifier SensorIdentifier { get; }

    public uint SensorIndex => SensorIdentifier.Index;
    public SensorType SensorType => SensorIdentifier.SensorType;

    internal ButtplugDeviceSensorSubscription(ButtplugDevice device, SensorAttributeIdentifier sensorIdentifier,
        ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, ButtplugDeviceSensorSubscriptionUnsubscribe unsubscribe)
    {
        _readingCallback = readingCallback;
        _unsubscribe = unsubscribe;

        Device = device;
        SensorIdentifier = sensorIdentifier;
    }

    internal void HandleReadingData(ImmutableArray<int> data)
        => _readingCallback(Device, SensorIdentifier, data);

    public async Task UnsubscribeAsync(CancellationToken cancellationToken)
        => await _unsubscribe.Invoke(SensorIdentifier, cancellationToken).ConfigureAwait(false);
}
