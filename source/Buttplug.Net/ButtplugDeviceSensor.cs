using System.Collections.Immutable;

namespace Buttplug;
public enum SensorType
{
    Unknown,
    Battery,
    RSSI,
    Button,
    Pressure
}

public record class ButtplugDeviceSensor
{
    public ButtplugDevice Device { get; }
    public uint Index { get; }
    public SensorType SensorType { get; }
    public string FeatureDescriptor { get; }
    public ImmutableArray<ImmutableArray<uint>> SensorRange { get; }

    internal ButtplugDeviceSensor(ButtplugDevice device, uint index, ButtplugDeviceSensorAttribute attribute)
    {
        Device = device;
        Index = index;
        SensorType = attribute.SensorType;
        FeatureDescriptor = attribute.FeatureDescriptor;
        SensorRange = attribute.SensorRange;
    }
}

public record class ButtplugDeviceReadSensor : ButtplugDeviceSensor
{
    internal ButtplugDeviceReadSensor(ButtplugDevice device, uint index, ButtplugDeviceSensorAttribute attribute)
        : base(device, index, attribute) { }

    public async Task<ImmutableArray<int>> ReadAsync(CancellationToken cancellationToken)
        => await Device.ReadSensorAsync(Index, SensorType, cancellationToken).ConfigureAwait(false);
}

public record class ButtplugDeviceSubscribeSensor : ButtplugDeviceSensor
{
    internal ButtplugDeviceSubscribeSensor(ButtplugDevice device, uint index, ButtplugDeviceSensorAttribute attribute)
        : base(device, index, attribute) { }

    public async Task<ButtplugDeviceSensorSubscription> SubscribeAsync(ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
        => await Device.SubscribeSensorAsync(Index, SensorType, readingCallback, cancellationToken).ConfigureAwait(false);
}