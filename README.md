Buttplug.Net is a buttplug client library written in modern .Net

## ⚙️ Installation

Stable and prerelease builds are available from Nuget:

- [Buttplug.Net](https://www.nuget.org/packages/Buttplug.Net/): Core buttplug client library
- [Buttplug.Net.NewtonsoftJson](https://www.nuget.org/packages/Buttplug.Net.NewtonsoftJson/): Buttplug JSON message converter based on Newtonsoft.Json
- [Buttplug.Net.SystemTextJson](https://www.nuget.org/packages/Buttplug.Net.SystemTextJson/): Buttplug JSON message converter based on System.Text.Json

## 📄 Sample

```csharp
var cancellationToken = CancellationToken.None;

//var converter = new ButtplugNewtonsoftJsonConverter();
var converter = new ButtplugSystemTextJsonConverter();
await using var client = new ButtplugClient("Buttplug.Net", converter);

client.DeviceAdded += (_, e) => Console.WriteLine($"Device added: {e}");
client.DeviceRemoved += (_, e) => Console.WriteLine($"Device removed: {e}");
client.ScanningFinished += (_, e) => Console.WriteLine("Scanning finished");
client.UnhandledException += (_, e) => Console.WriteLine($"Error received: {e}");
client.Disconnected += (_, e) => Console.WriteLine("Disconnected");

await client.ConnectAsync(new Uri("ws://127.0.0.1:12345/"), cancellationToken);
Console.WriteLine("Connected");

await client.StartScanningAsync(cancellationToken);
await Task.Delay(1000);
await client.StopScanningAsync(cancellationToken);

foreach (var device in client.Devices)
    await device.ScalarAsync(1, ActuatorType.Vibrate, cancellationToken);

await Task.Delay(1000);
await client.StopAllDevicesAsync(cancellationToken);
```