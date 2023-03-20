using System.Collections.Immutable;

namespace Buttplug;

internal interface IButtplugDeviceMessage : IButtplugMessage
{
    public uint DeviceIndex { get; }
}

[ButtplugMessageName("DeviceAdded")]
internal record class DeviceAddedButtplugMessage : ButtplugDeviceInfo, IButtplugDeviceMessage
{
    public uint Id => 0;

    public DeviceAddedButtplugMessage(string DeviceName, uint DeviceIndex, string DeviceDisplayName, uint DeviceMessageTimingGap, ButtplugDeviceAttributes DeviceMessages)
        : base(DeviceName, DeviceIndex, DeviceDisplayName, DeviceMessageTimingGap, DeviceMessages) { }
}

[ButtplugMessageName("DeviceRemoved")]
internal record class DeviceRemovedButtplugMessage(uint DeviceIndex) : IButtplugDeviceMessage { public uint Id => 0; }

[ButtplugMessageName("ScalarCmd")]
internal record class ScalarCommandButtplugMessage(uint DeviceIndex, IEnumerable<ScalarCommand> Scalars) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("RotateCmd")]
internal record class RotateCommandButtplugMessage(uint DeviceIndex, IEnumerable<RotateCommand> Rotations) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("LinearCmd")]
internal record class LinearCommandButtplugMessage(uint DeviceIndex, IEnumerable<LinearCommand> Vectors) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("StopDeviceCmd")]
internal record class StopDeviceCommandButtplugMessage(uint DeviceIndex) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorReadCmd")]
internal record class SensorReadCommandButtplugMessage(uint DeviceIndex, uint SensorIndex, SensorType SensorType) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorSubscribeCmd")]
internal record class SensorSubscribeCommandButtplugMessage(uint DeviceIndex, uint SensorIndex, SensorType SensorType) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorUnsubscribeCmd")]
internal record class SensorUnsubscribeCommandButtplugMessage(uint DeviceIndex, uint SensorIndex, SensorType SensorType) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorReading")]
internal record class SensorReadingButtplugMessage(uint Id, uint DeviceIndex, uint SensorIndex, SensorType SensorType, ImmutableArray<int> Data) : IButtplugDeviceMessage;

[ButtplugMessageName("RawWriteCmd")]
internal record class EndpointWriteCommandButtplugMessage(uint DeviceIndex, string Endpoint, IEnumerable<byte> Data, bool WriteWithResponse) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("RawReadCmd")]
internal record class EndpointReadCommandButtplugMessage(uint DeviceIndex, string Endpoint, uint Length, bool WaitForData) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("RawReading")]
internal record class EndpointReadingButtplugMessage(uint Id, uint DeviceIndex, string Endpoint, ImmutableArray<byte> Data) : IButtplugDeviceMessage;

[ButtplugMessageName("RawSubscribeCmd")]
internal record class EndpointSubscribeCommandButtplugMessage(uint DeviceIndex, string Endpoint) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("RawUnsubscribeCmd")]
internal record class EndpointUnsubscribeCommandButtplugMessage(uint DeviceIndex, string Endpoint) : AutoIncrementingButtplugMessage;