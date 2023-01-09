﻿using System.Collections.Immutable;

namespace Buttplug;

internal interface IButtplugDeviceMessage : IButtplugMessage
{
    public uint DeviceIndex { get; }
}

[ButtplugMessageName("DeviceAdded")]
internal record class DeviceAddedButtplugMessage(uint DeviceIndex, string DeviceName, string DeviceDisplayName, ButtplugDeviceAttributes DeviceMessages, uint DeviceMessageTimingGap) : IButtplugDeviceMessage { public uint Id => 0; }

[ButtplugMessageName("DeviceRemoved")]
internal record class DeviceRemovedButtplugMessage(uint DeviceIndex) : IButtplugDeviceMessage { public uint Id => 0; }

public readonly record struct ScalarCommand(uint Index, double Scalar, ActuatorType ActuatorType);

[ButtplugMessageName("ScalarCmd")]
internal record class ScalarCommandButtplugMessage(uint DeviceIndex, IEnumerable<ScalarCommand> Scalars) : AutoIncrementingButtplugMessage;

public readonly record struct RotateCommand(uint Index, double Speed, bool Clockwise);

[ButtplugMessageName("RotateCmd")]
internal record class RotateCommandButtplugMessage(uint DeviceIndex, IEnumerable<RotateCommand> Rotations) : AutoIncrementingButtplugMessage;

public readonly record struct LinearCommand(uint Index, double Duration, double Position);

[ButtplugMessageName("LinearCmd")]
internal record class LinearCommandButtplugMessage(uint DeviceIndex, IEnumerable<LinearCommand> Vectors) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("StopDeviceCmd")]
internal record class StopDeviceCommandButtplugMessage(uint DeviceIndex) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorReadCmd")]
internal record class SensorReadCommandButtplugMessage(uint DeviceIndex, uint SensorIndex, SensorType SensorType) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("SensorReading")]
internal record class SensorReadingButtplugMessage(uint Id, uint DeviceIndex, uint SensorIndex, SensorType SensorType, ImmutableList<int> Data) : IButtplugDeviceMessage;