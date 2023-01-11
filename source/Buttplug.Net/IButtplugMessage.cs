using System.Collections.Immutable;

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

internal record class ButtplugDeviceActuatorAttribute(string FeatureDescriptor, ActuatorType ActuatorType, uint StepCount)
{
    public uint Index { get; internal set; }
}

internal record class ButtplugDeviceSensorAttribute(string FeatureDescriptor, SensorType SensorType, ImmutableArray<ImmutableArray<uint>> SensorRange)
{
    public uint Index { get; internal set; }
}

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

internal record class ButtplugMessageDeviceInfo(string DeviceName, uint DeviceIndex, string DeviceDisplayName, uint DeviceMessageTimingGap, ButtplugDeviceAttributes DeviceMessages);

[ButtplugMessageName("DeviceList")]
internal record class DeviceListButtplugMessage(uint Id, ImmutableArray<ButtplugMessageDeviceInfo> Devices) : IButtplugMessage;

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