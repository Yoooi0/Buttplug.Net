using System.Collections.Immutable;

namespace Buttplug;

internal record class ButtplugDeviceInfo(string DeviceName, uint DeviceIndex, string DeviceDisplayName, uint DeviceMessageTimingGap, ButtplugDeviceAttributes DeviceMessages);

internal record class ButtplugDeviceActuatorAttribute(string FeatureDescriptor, ActuatorType ActuatorType, uint StepCount);
internal record class ButtplugDeviceSensorAttribute(string FeatureDescriptor, SensorType SensorType, ImmutableArray<ImmutableArray<uint>> SensorRange);
internal record class ButtplugDeviceRawAttribute(ImmutableArray<string> Endpoints);
internal record class ButtplugDeviceVoidAttribute();

internal record class ButtplugDeviceAttributes
{
    public ImmutableArray<ButtplugDeviceActuatorAttribute> ScalarCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceActuatorAttribute> RotateCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceActuatorAttribute> LinearCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceSensorAttribute> SensorReadCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceSensorAttribute> SensorSubscribeCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceRawAttribute> RawReadCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceRawAttribute> RawWriteCmd { get; init; } = [];
    public ImmutableArray<ButtplugDeviceRawAttribute> RawSubscribeCmd { get; init; } = [];
    public ButtplugDeviceVoidAttribute? StopDeviceCmd { get; init; }
}