using System.Collections.Immutable;

namespace Buttplug;

internal interface IButtplugDeviceMessage : IButtplugMessage
{
    public uint DeviceIndex { get; }
}

[ButtplugMessageName("DeviceAdded")]
internal record class DeviceAddedButtplugMessage(uint DeviceIndex, string DeviceName, string DeviceDisplayName, ButtplugDeviceAttributes DeviceMessages, uint DeviceMessageTimingGap) : IButtplugDeviceMessage { public uint Id => 0; }

[ButtplugMessageName("DeviceRemoved")]
internal record class DeviceRemovedButtplugMessage(uint DeviceIndex) : IButtplugDeviceMessage { public uint Id => 0; }

public record class ScalarCmd(uint Index, double Scalar, ActuatorType ActuatorType);

[ButtplugMessageName("ScalarCmd")]
internal record class ScalarCmdButtplugMessage(uint DeviceIndex, IEnumerable<ScalarCmd> Scalars) : AutoIncrementingButtplugMessage;

public record class RotateCmd(uint Index, double Speed, bool Clockwise);

[ButtplugMessageName("RotateCmd")]
internal record class RotateCmdButtplugMessage(uint DeviceIndex, IEnumerable<RotateCmd> Rotations) : AutoIncrementingButtplugMessage;

public record class LinearCmd(uint Index, double Duration, double Position);

[ButtplugMessageName("LinearCmd")]
internal record class LinearCmdButtplugMessage(uint DeviceIndex, IEnumerable<LinearCmd> Vectors) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("StopDeviceCmd")]
internal record class StopDeviceCmdButtplugMessage(uint DeviceIndex) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorReadCmd")]
internal record class SensorReadCmdButtplugMessage(uint DeviceIndex, uint SensorIndex, SensorType SensorType) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorReading")]
internal record class SensorReadingButtplugMessage(uint Id, uint DeviceIndex, uint SensorIndex, SensorType SensorType, ImmutableList<int> Data) : IButtplugDeviceMessage;