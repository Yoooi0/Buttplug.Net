using System.Collections.Immutable;

namespace Buttplug;

public delegate void ButtplugDeviceSensorSubscriptionReadingCallback(ButtplugDeviceSubscribeSensor sensor, ImmutableArray<int> data);

public record class ButtplugDeviceSensorSubscription
{
    private readonly ButtplugDeviceSensorSubscriptionReadingCallback _readingCallback;

    public ButtplugDeviceSubscribeSensor Sensor { get; }

    public ButtplugDevice Device => Sensor.Device;
    public uint SensorIndex => Sensor.Index;
    public SensorType SensorType => Sensor.SensorType;

    internal ButtplugDeviceSensorSubscription(ButtplugDeviceSubscribeSensor sensor, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback)
    {
        _readingCallback = readingCallback;
        Sensor = sensor;
    }

    internal void HandleReadingData(ImmutableArray<int> data)
        => _readingCallback(Sensor, data);
    public async Task UnsubscribeAsync(CancellationToken cancellationToken)
        => await Device.UnsubscribeSensorAsync(Sensor, cancellationToken).ConfigureAwait(false);
}
