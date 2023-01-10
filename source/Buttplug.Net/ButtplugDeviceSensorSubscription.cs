using System.Collections.Immutable;

namespace Buttplug;

public delegate void SensorSubscriptionReadingCallback(ButtplugDevice device, uint sensorIndex, SensorType sensorType, ImmutableList<int> data);
public delegate Task ButtplugDeviceSensorSubscriptionUnsubscribe(SensorAttributeIdentifier indentifier, CancellationToken cancellationToken);

public record class ButtplugDeviceSensorSubscription
{
    private readonly SensorSubscriptionReadingCallback _readingCallback;
    private readonly ButtplugDeviceSensorSubscriptionUnsubscribe _unsubscribe;

    public ButtplugDevice Device { get; }
    public uint SensorIndex { get; }
    public SensorType SensorType { get; }

    internal ButtplugDeviceSensorSubscription(ButtplugDevice device, uint sensorIndex, SensorType sensorType,
        SensorSubscriptionReadingCallback readingCallback, ButtplugDeviceSensorSubscriptionUnsubscribe unsubscribeCallback)
    {
        _readingCallback = readingCallback;
        _unsubscribe = unsubscribeCallback;

        Device = device;
        SensorIndex = sensorIndex;
        SensorType = sensorType;
    }

    internal void HandleReadingData(ImmutableList<int> data)
        => _readingCallback(Device, SensorIndex, SensorType, data);

    public async Task UnsubscribeAsync(CancellationToken cancellationToken)
        => await _unsubscribe.Invoke(new(SensorIndex, SensorType), cancellationToken).ConfigureAwait(false);
}
