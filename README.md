Buttplug.Net is a buttplug client library written in modern .Net

[![Release](https://img.shields.io/nuget/v/Buttplug.Net?label=release&logo=nuget)](https://www.nuget.org/packages/Buttplug.Net)
[![Pre-Release](https://img.shields.io/nuget/vpre/Buttplug.Net?label=pre-release&logo=nuget)](https://www.nuget.org/packages/Buttplug.Net)
[![Build](https://img.shields.io/github/actions/workflow/status/Yoooi0/Buttplug.Net/ci.yml?logo=github)](https://github.com/Yoooi0/Buttplug.Net/actions)


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

await client.ConnectAsync(new Uri("ws://127.0.0.1:12345/"), cancellationToken);
Console.WriteLine("Connected");

await client.StartScanningAsync(cancellationToken);
await Task.Delay(2500);

foreach (var device in client.Devices)
    await device.ScalarAsync(1, ActuatorType.Vibrate, cancellationToken);

await Task.Delay(1000);
await client.StopAllDevicesAsync(cancellationToken);
```

> More samples available **[here](https://github.com/Yoooi0/Buttplug.Net/tree/master/samples)**