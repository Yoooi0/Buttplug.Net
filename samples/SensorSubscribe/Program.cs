using Buttplug.SystemTextJson;
using Buttplug;

var cancellationToken = CancellationToken.None;
var converter = new ButtplugSystemTextJsonConverter();
await using var client = new ButtplugClient("Buttplug.Net", converter);

client.DeviceAdded += (_, e) => Console.WriteLine($"Device added: {e}");
client.DeviceRemoved += (_, e) => Console.WriteLine($"Device removed: {e}");
client.UnhandledException += (_, e) => Console.WriteLine($"Error received: {e}");
client.Disconnected += (_, e) => Console.WriteLine("Disconnected");

await client.ConnectAsync(new Uri("ws://127.0.0.1:12345/"), cancellationToken);
Console.WriteLine("Connected");

await client.StartScanningAsync(cancellationToken);
await Task.Delay(2500);
await client.StopScanningAsync(cancellationToken);

var sensor = client.Devices.SelectMany(d => d.SubscribeSensors).FirstOrDefault();
if (sensor != null)
{
    var subscription = await sensor.SubscribeAsync((s, d) => Console.WriteLine($"Sensor {s} data: \"{string.Join(", ", d)}\""), cancellationToken);
    await Task.Delay(10000);
    await subscription.UnsubscribeAsync(cancellationToken);
}

await Task.Delay(1000);
await client.StopAllDevicesAsync(cancellationToken);