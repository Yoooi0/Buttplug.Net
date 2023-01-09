using System.Collections.Immutable;

namespace Buttplug;

public class ButtplugDevice : IEquatable<ButtplugDevice>, IDisposable
{
    private readonly ButtplugDeviceAttributes _attributes;
    private IButtplugSender? _sender;

    public required uint Index { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required uint MessageTimingGap { get; init; }

    public IEnumerable<ActuatorType> SupportedActuatorTypes
    {
        get
        {
            var actuatorTypes = _attributes.ScalarCmd.Select(c => c.ActuatorType);
            if (_attributes.LinearCmd.Any())
                actuatorTypes = actuatorTypes.Append(ActuatorType.Position);
            if (_attributes.RotateCmd.Any())
                actuatorTypes = actuatorTypes.Append(ActuatorType.Rotate);

            return actuatorTypes.Distinct();
        }
    }

    public IEnumerable<ButtplugDeviceGenericAttribute> Actuators => LinearActuators.Concat(RotateActuators).Concat(ScalarActuators);
    public IReadOnlyList<ButtplugDeviceGenericAttribute> LinearActuators => _attributes.LinearCmd;
    public IReadOnlyList<ButtplugDeviceGenericAttribute> RotateActuators => _attributes.RotateCmd;
    public IReadOnlyList<ButtplugDeviceGenericAttribute> ScalarActuators => _attributes.ScalarCmd;

    public IEnumerable<SensorType> SupportedSensorTypes
        => _attributes.SensorReadCmd.Select(c => c.SensorType);

    public IReadOnlyList<ButtplugDeviceSensorAttribute> Sensors => _attributes.SensorReadCmd;

    internal ButtplugDevice(IButtplugSender sender, ButtplugDeviceAttributes attributes)
    {
        _sender = sender;
        _attributes = attributes;
    }

    public IEnumerable<ButtplugDeviceGenericAttribute> GetActuators(ActuatorType actuatorType)
        => actuatorType switch
        {
            ActuatorType.Position => _attributes.LinearCmd,
            ActuatorType.Rotate => _attributes.RotateCmd,
            _ => _attributes.ScalarCmd.Where(c => c.ActuatorType == actuatorType)
        };

    public IEnumerable<ButtplugDeviceSensorAttribute> GetSensors(SensorType sensorType)
        => _attributes.SensorReadCmd.Where(c => c.SensorType == sensorType);

    public async Task ScalarAsync(double scalar, ActuatorType actuatorType, CancellationToken cancellationToken)
        => await ScalarAsync(Enumerable.Range(0, GetActuators(actuatorType).Count()).Select(i => new ScalarCommand((uint)i, scalar, actuatorType)), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(ScalarCommand scalarCommand, CancellationToken cancellationToken)
        => await ScalarAsync(new[] { scalarCommand }, cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(IEnumerable<(double Scalar, ActuatorType ActuatorType)> scalarCommands, CancellationToken cancellationToken)
        => await ScalarAsync(scalarCommands.Select((c, i) => new ScalarCommand((uint)i, c.Scalar, c.ActuatorType)), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(IEnumerable<ScalarCommand> scalarCommands, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new ScalarCommandButtplugMessage(Index, scalarCommands), cancellationToken).ConfigureAwait(false);

    public async Task RotateAsync(double speed, bool clockwise, CancellationToken cancellationToken)
        => await RotateAsync(Enumerable.Range(0, RotateActuators.Count).Select(i => new RotateCommand((uint)i, speed, clockwise)), cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(RotateCommand rotateCommand, CancellationToken cancellationToken)
        => await RotateAsync(new[] { rotateCommand }, cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(IEnumerable<(double Speed, bool Clockwise)> rotateCommands, CancellationToken cancellationToken)
        => await RotateAsync(rotateCommands.Select((c, i) => new RotateCommand((uint)i, c.Speed, c.Clockwise)), cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(IEnumerable<RotateCommand> rotateCommands, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new RotateCommandButtplugMessage(Index, rotateCommands), cancellationToken).ConfigureAwait(false);

    public async Task LinearAsync(uint duration, double position, CancellationToken cancellationToken)
        => await LinearAsync(Enumerable.Range(0, LinearActuators.Count).Select(i => new LinearCommand((uint)i, duration, position)), cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(LinearCommand linearCommand, CancellationToken cancellationToken)
        => await LinearAsync(new[] { linearCommand }, cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(IEnumerable<(uint Duration, double Position)> linearCommands, CancellationToken cancellationToken)
        => await LinearAsync(linearCommands.Select((c, i) => new LinearCommand((uint)i, c.Duration, c.Position)), cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(IEnumerable<LinearCommand> linearCommands, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new LinearCommandButtplugMessage(Index, linearCommands), cancellationToken).ConfigureAwait(false);

    public async Task<ImmutableList<int>> SensorAsync(SensorType sensorType, CancellationToken cancellationToken)
        => await SensorAsync(GetSensors(sensorType).First().Index, sensorType, cancellationToken).ConfigureAwait(false);
    public async Task<ImmutableList<int>> SensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
    {
        var response = await SendMessageExpectTAsync<SensorReadingButtplugMessage>(new SensorReadCommandButtplugMessage(Index, sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopDeviceCommandButtplugMessage(Index), cancellationToken).ConfigureAwait(false);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _sender == null ? throw new ObjectDisposedException(nameof(_sender))
                           : await _sender.SendMessageExpectTAsync<T>(message, cancellationToken).ConfigureAwait(false);

    public override bool Equals(object? obj) => Equals(obj as ButtplugDevice);
    public virtual bool Equals(ButtplugDevice? other) => other != null && (ReferenceEquals(this, other) || Index == other.Index);
    public override int GetHashCode() => HashCode.Combine(Index);

    protected virtual void Dispose(bool disposing)
    {
        _sender = null;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}