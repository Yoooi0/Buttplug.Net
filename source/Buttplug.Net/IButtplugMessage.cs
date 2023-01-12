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

[ButtplugMessageName("DeviceList")]
internal record class DeviceListButtplugMessage(uint Id, ImmutableArray<ButtplugDeviceInfo> Devices) : IButtplugMessage;

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