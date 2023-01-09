using Buttplug.SystemTextJson;
using Buttplug;

var cancellationToken = CancellationToken.None;
var converter = new ButtplugSystemTextJsonConverter();
await using var client = new ButtplugClient("Buttplug.Net", converter);

client.DeviceAdded += (_, e) => Console.WriteLine($"Device added: {e}");
client.DeviceRemoved += (_, e) => Console.WriteLine($"Device removed: {e}");
client.ScanningFinished += (_, e) => Console.WriteLine("Scanning finished");
client.UnhandledException += (_, e) => Console.WriteLine($"Error received: {e}");
client.Disconnected += (_, e) => Console.WriteLine("Disconnected");

await client.ConnectAsync(new Uri("ws://127.0.0.1:12345/"), cancellationToken);

foreach (var device in client.Devices)
    await device.ScalarAsync(1, ActuatorType.Vibrate, cancellationToken);

await Task.Delay(1000);
await client.StopAllDevicesAsync(cancellationToken);