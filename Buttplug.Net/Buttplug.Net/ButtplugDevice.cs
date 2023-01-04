using System.Collections.Immutable;

namespace Buttplug;

public class ButtplugDevice : IEquatable<ButtplugDevice>, IDisposable
{
    private IButtplugSender? _sender;

    public required uint Index { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required uint MessageTimingGap { get; init; }
    public required ButtplugDeviceAttributes MessageAttributes { get; init; }

    internal ButtplugDevice(IButtplugSender sender) => _sender = sender;

    public async Task ScalarAsync(double scalar, ActuatorType actuatorType, CancellationToken cancellationToken)
        => await ScalarAsync(Enumerable.Range(0, GetActuatorAttributes(actuatorType).Count()).Select(i => new ScalarCmd((uint)i, scalar, actuatorType)), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(ScalarCmd scalarCmd, CancellationToken cancellationToken)
        => await ScalarAsync(new[] { scalarCmd }, cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(IEnumerable<(double Scalar, ActuatorType ActuatorType)> scalarCmds, CancellationToken cancellationToken)
        => await ScalarAsync(scalarCmds.Select((c, i) => new ScalarCmd((uint)i, c.Scalar, c.ActuatorType)), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(IEnumerable<ScalarCmd> scalarCmds, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new ScalarCmdButtplugMessage(Index, scalarCmds), cancellationToken).ConfigureAwait(false);

    public async Task RotateAsync(double speed, bool clockwise, CancellationToken cancellationToken)
        => await RotateAsync(Enumerable.Range(0, MessageAttributes.RotateCmd.Count).Select(i => new RotateCmd((uint)i, speed, clockwise)), cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(RotateCmd rotateCmd, CancellationToken cancellationToken)
        => await RotateAsync(new[] { rotateCmd }, cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(IEnumerable<(double Speed, bool Clockwise)> rotateCmds, CancellationToken cancellationToken)
        => await RotateAsync(rotateCmds.Select((c, i) => new RotateCmd((uint)i, c.Speed, c.Clockwise)), cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(IEnumerable<RotateCmd> rotateCmds, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new RotateCmdButtplugMessage(Index, rotateCmds), cancellationToken).ConfigureAwait(false);

    public async Task LinearAsync(uint duration, double position, CancellationToken cancellationToken)
        => await LinearAsync(Enumerable.Range(0, MessageAttributes.LinearCmd.Count).Select(i => new LinearCmd((uint)i, duration, position)), cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(LinearCmd linearCmd, CancellationToken cancellationToken)
        => await LinearAsync(new[] { linearCmd }, cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(IEnumerable<(uint Duration, double Position)> linearCmds, CancellationToken cancellationToken)
        => await LinearAsync(linearCmds.Select((c, i) => new LinearCmd((uint)i, c.Duration, c.Position)), cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(IEnumerable<LinearCmd> linearCmds, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new LinearCmdButtplugMessage(Index, linearCmds), cancellationToken).ConfigureAwait(false);

    public async Task<ImmutableList<int>> SensorAsync(SensorType sensorType, CancellationToken cancellationToken)
        => await SensorAsync(GetSensorAttributes(sensorType).First().Index, sensorType, cancellationToken).ConfigureAwait(false);
    public async Task<ImmutableList<int>> SensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
    {
        var response = await SendMessageExpectTAsync<SensorReadingButtplugMessage>(new SensorReadCmdButtplugMessage(Index, sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopDeviceCmdButtplugMessage(Index), cancellationToken).ConfigureAwait(false);

    public IEnumerable<ButtplugDeviceGenericAttribute> GetActuatorAttributes(ActuatorType actuatorType)
        => MessageAttributes.ScalarCmd.Where(c => c.ActuatorType == actuatorType);

    public IEnumerable<ButtplugDeviceSensorAttribute> GetSensorAttributes(SensorType sensorType)
        => MessageAttributes.SensorReadCmd.Where(c => c.SensorType == sensorType);

    public IEnumerable<ButtplugDeviceGenericAttribute> GetLinearAttributes() => MessageAttributes.LinearCmd;
    public IEnumerable<ButtplugDeviceGenericAttribute> GetRotateAttributes() => MessageAttributes.RotateCmd;

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