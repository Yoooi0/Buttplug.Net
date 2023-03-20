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

internal enum ErrorButtplugMessageCode
{
    Unknown,
    Init,
    Ping,
    Message,
    Device,
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