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

foreach (var device in client.Devices)
    await device.ScalarAsync(1, ActuatorType.Vibrate, cancellationToken);

await Task.Delay(1000);

foreach (var actuator in client.Devices.SelectMany(d => d.GetActuators<ButtplugDeviceScalarActuator>(ActuatorType.Vibrate)))
    await actuator.ScalarAsync(0.5, cancellationToken);

await Task.Delay(1000);
await client.StopAllDevicesAsync(cancellationToken);