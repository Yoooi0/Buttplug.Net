﻿using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Buttplug;

public interface IButtplugMessage
{
    public uint Id { get; }
}

internal abstract record class AutoIncrementingButtplugMessage : IButtplugMessage
{
    private static uint _idCounter;
    public uint Id { get; private set; }
    protected AutoIncrementingButtplugMessage() => Id = Interlocked.Increment(ref _idCounter);
}

[ButtplugMessageName("Ok")]
internal record class OkButtplugMessage(uint Id) : IButtplugMessage;

[ButtplugMessageName("Test")]
internal record class TestButtplugMessage(string TestString) : AutoIncrementingButtplugMessage;

internal enum ErrorButtplugMessageCode
{
    ERROR_UNKNOWN,
    ERROR_INIT,
    ERROR_PING,
    ERROR_MSG,
    ERROR_DEVICE,
}

[ButtplugMessageName("Error")]
internal record class ErrorButtplugMessage(uint Id, string ErrorMessage, ErrorButtplugMessageCode ErrorCode) : IButtplugMessage;

public enum ActuatorType
{
    Vibrate,
    Rotate,
    Oscillate,
    Constrict,
    Inflate,
    Position
}

public enum SensorType
{
    Battery,
    RSSI,
    Button,
    Pressure
}

public record class ButtplugDeviceGenericAttribute(string FeatureDescriptor, ActuatorType ActuatorType, uint StepCount)
{
    public uint Index { get; internal set; }
}

public record class ButtplugDeviceSensorAttribute(string FeatureDescriptor, SensorType SensorType, ImmutableArray<ImmutableArray<uint>> SensorRange)
{
    public uint Index { get; internal set; }
}

public record class ButtplugDeviceRawAttribute(ImmutableList<string> Endpoints);
public record class ButtplugDeviceVoidAttribute();
public record class ButtplugDeviceAttributes(ImmutableList<ButtplugDeviceGenericAttribute> ScalarCmd,
                                             ImmutableList<ButtplugDeviceGenericAttribute> RotateCmd,
                                             ImmutableList<ButtplugDeviceGenericAttribute> LinearCmd,
                                             ImmutableList<ButtplugDeviceSensorAttribute> SensorReadCmd,
                                             ImmutableList<ButtplugDeviceSensorAttribute> SensorSubscribeCmd,
                                             ImmutableList<ButtplugDeviceRawAttribute> RawReadCmd,
                                             ImmutableList<ButtplugDeviceRawAttribute> RawWriteCmd,
                                             ImmutableList<ButtplugDeviceRawAttribute> RawSubscribeCmd,
                                             ButtplugDeviceVoidAttribute StopDeviceCmd)
{
    [OnDeserialized]
    internal void OnDeserializedMethod(StreamingContext _)
    {
        for (var i = 0; i < ScalarCmd?.Count; i++) ScalarCmd[i].Index = (uint)i;
        for (var i = 0; i < RotateCmd?.Count; i++) RotateCmd[i].Index = (uint)i;
        for (var i = 0; i < LinearCmd?.Count; i++) LinearCmd[i].Index = (uint)i;
        for (var i = 0; i < SensorReadCmd?.Count; i++) SensorReadCmd[i].Index = (uint)i;
        for (var i = 0; i < SensorSubscribeCmd?.Count; i++) SensorSubscribeCmd[i].Index = (uint)i;
    }
}

internal record class ButtplugMessageDeviceInfo(string DeviceName, uint DeviceIndex, ButtplugDeviceAttributes DeviceMessages, string DeviceDisplayName, uint DeviceMessageTimingGap);

[ButtplugMessageName("DeviceList")]
internal record class DeviceListButtplugMessage(uint Id, ImmutableList<ButtplugMessageDeviceInfo> Devices) : IButtplugMessage;

[ButtplugMessageName("RequestDeviceList")]
internal record class RequestDeviceListButtplugMessage() : AutoIncrementingButtplugMessage;

[ButtplugMessageName("StartScanning")]
internal record class StartScanningButtplugMessage() : AutoIncrementingButtplugMessage;

[ButtplugMessageName("StopScanning")]
internal record class StopScanningButtplugMessage() : AutoIncrementingButtplugMessage;

[ButtplugMessageName("ScanningFinished")]
internal record class ScanningFinishedButtplugMessage() : IButtplugMessage { public uint Id => 0; }

[ButtplugMessageName("RequestServerInfo")]
internal record class RequestServerInfoButtplugMessage(string ClientName, uint MessageVersion = ButtplugClient.MessageVersion) : AutoIncrementingButtplugMessage;

[ButtplugMessageName("ServerInfo")]
internal record class ServerInfoButtplugMessage(uint Id, uint MessageVersion, uint MaxPingTime, string ServerName) : IButtplugMessage;

[ButtplugMessageName("Ping")]
internal record class PingButtplugMessage() : AutoIncrementingButtplugMessage;

[ButtplugMessageName("StopAllDevices")]
internal record class StopAllDevicesButtplugMessage() : AutoIncrementingButtplugMessage;