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

[ButtplugMessageName("SensorReading")]
internal record class SensorReadingButtplugMessage(uint Id, uint DeviceIndex, uint SensorIndex, SensorType SensorType, ImmutableArray<int> Data) : IButtplugDeviceMessage;