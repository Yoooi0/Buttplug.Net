using System.Collections.Immutable;

namespace Buttplug;

internal record class ButtplugDeviceInfo(string DeviceName, uint DeviceIndex, string DeviceDisplayName, uint DeviceMessageTimingGap, ButtplugDeviceAttributes DeviceMessages);

internal record class ButtplugDeviceActuatorAttribute(string FeatureDescriptor, ActuatorType ActuatorType, uint StepCount);
internal record class ButtplugDeviceSensorAttribute(string FeatureDescriptor, SensorType SensorType, ImmutableArray<ImmutableArray<uint>> SensorRange);
internal record class ButtplugDeviceRawAttribute(ImmutableArray<string> Endpoints);
internal record class ButtplugDeviceVoidAttribute();

internal record class ButtplugDeviceAttributes
{
    public ImmutableArray<ButtplugDeviceActuatorAttribute> ScalarCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceActuatorAttribute>();
    public ImmutableArray<ButtplugDeviceActuatorAttribute> RotateCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceActuatorAttribute>();
    public ImmutableArray<ButtplugDeviceActuatorAttribute> LinearCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceActuatorAttribute>();
    public ImmutableArray<ButtplugDeviceSensorAttribute> SensorReadCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceSensorAttribute>();
    public ImmutableArray<ButtplugDeviceSensorAttribute> SensorSubscribeCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceSensorAttribute>();
    public ImmutableArray<ButtplugDeviceRawAttribute> RawReadCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceRawAttribute>();
    public ImmutableArray<ButtplugDeviceRawAttribute> RawWriteCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceRawAttribute>();
    public ImmutableArray<ButtplugDeviceRawAttribute> RawSubscribeCmd { get; init; } = ImmutableArray.Create<ButtplugDeviceRawAttribute>();
    public ButtplugDeviceVoidAttribute? StopDeviceCmd { get; init; }
}