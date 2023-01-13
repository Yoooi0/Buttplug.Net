using System.Collections.Immutable;

namespace Buttplug;

public delegate void ButtplugDeviceSensorSubscriptionReadingCallback(ButtplugDeviceSubscribeSensor sensor, ImmutableArray<int> data);
public delegate Task ButtplugDeviceSensorSubscriptionUnsubscribe(ButtplugDeviceSubscribeSensor sensor, CancellationToken cancellationToken);

public record class ButtplugDeviceSensorSubscription
{
    private readonly ButtplugDeviceSensorSubscriptionReadingCallback _readingCallback;
    private readonly ButtplugDeviceSensorSubscriptionUnsubscribe _unsubscribe;

    public ButtplugDeviceSubscribeSensor Sensor { get; }

    public ButtplugDevice Device => Sensor.Device;
    public uint SensorIndex => Sensor.Index;
    public SensorType SensorType => Sensor.SensorType;

    internal ButtplugDeviceSensorSubscription(ButtplugDeviceSubscribeSensor sensor,
        ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, ButtplugDeviceSensorSubscriptionUnsubscribe unsubscribe)
    {
        _readingCallback = readingCallback;
        _unsubscribe = unsubscribe;

        Sensor = sensor;
    }

    internal void HandleReadingData(ImmutableArray<int> data)
        => _readingCallback(Sensor, data);
    public async Task UnsubscribeAsync(CancellationToken cancellationToken)
        => await _unsubscribe.Invoke(Sensor, cancellationToken).ConfigureAwait(false);
}
