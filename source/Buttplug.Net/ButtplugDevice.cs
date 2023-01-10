using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Buttplug;

public class ButtplugDevice : IEquatable<ButtplugDevice>, IDisposable
{
    private readonly ButtplugDeviceAttributes _attributes;
    private readonly ConcurrentDictionary<SensorAttributeIdentifier, ButtplugDeviceSensorSubscription> _sensorSubscriptions;
    private IButtplugSender? _sender;

    public uint Index { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public uint MessageTimingGap { get; }

    public IEnumerable<ActuatorType> SupportedActuatorTypes
    {
        get
        {
            var actuatorTypes = ScalarActuators.Select(c => c.ActuatorType);
            if (LinearActuators.Count > 0)
                actuatorTypes = actuatorTypes.Append(ActuatorType.Position);
            if (RotateActuators.Count > 0)
                actuatorTypes = actuatorTypes.Append(ActuatorType.Rotate);

            return actuatorTypes.Distinct();
        }
    }

    public IEnumerable<ButtplugDeviceGenericAttribute> Actuators => LinearActuators.Concat(RotateActuators).Concat(ScalarActuators);
    public IReadOnlyList<ButtplugDeviceGenericAttribute> LinearActuators => _attributes?.LinearCmd ?? ImmutableList.Create<ButtplugDeviceGenericAttribute>();
    public IReadOnlyList<ButtplugDeviceGenericAttribute> RotateActuators => _attributes?.RotateCmd ?? ImmutableList.Create<ButtplugDeviceGenericAttribute>();
    public IReadOnlyList<ButtplugDeviceGenericAttribute> ScalarActuators => _attributes?.ScalarCmd ?? ImmutableList.Create<ButtplugDeviceGenericAttribute>();

    public IEnumerable<SensorType> SupportedSensorTypes
        => _attributes.SensorReadCmd?.Select(c => c.SensorType).Distinct() ?? Enumerable.Empty<SensorType>();
    public IEnumerable<SensorType> SupportedSensorSubscribeTypes
        => _attributes.SensorReadCmd?.Select(c => c.SensorType).Distinct() ?? Enumerable.Empty<SensorType>();

    public IReadOnlyList<ButtplugDeviceSensorAttribute> Sensors => _attributes?.SensorReadCmd ?? ImmutableList.Create<ButtplugDeviceSensorAttribute>();
    public IReadOnlyList<ButtplugDeviceSensorAttribute> SubscribeSensors => _attributes?.SensorSubscribeCmd ?? ImmutableList.Create<ButtplugDeviceSensorAttribute>();
    public ICollection<ButtplugDeviceSensorSubscription> SensorSubscriptions => _sensorSubscriptions.Values;

    internal ButtplugDevice(IButtplugSender sender, ButtplugMessageDeviceInfo info)
    {
        _sender = sender;
        _attributes = info.DeviceMessages;
        _sensorSubscriptions = new ConcurrentDictionary<SensorAttributeIdentifier, ButtplugDeviceSensorSubscription>();

        //workaround untill buttplug sends attribute index in the message
        for (var i = 0; i < _attributes.ScalarCmd?.Count; i++) _attributes.ScalarCmd[i].Index = (uint)i;
        for (var i = 0; i < _attributes.RotateCmd?.Count; i++) _attributes.RotateCmd[i].Index = (uint)i;
        for (var i = 0; i < _attributes.LinearCmd?.Count; i++) _attributes.LinearCmd[i].Index = (uint)i;
        for (var i = 0; i < _attributes.SensorReadCmd?.Count; i++) _attributes.SensorReadCmd[i].Index = (uint)i;
        for (var i = 0; i < _attributes.SensorSubscribeCmd?.Count; i++) _attributes.SensorSubscribeCmd[i].Index = (uint)i;

        Index = info.DeviceIndex;
        Name = info.DeviceName;
        DisplayName = info.DeviceDisplayName;
        MessageTimingGap = info.DeviceMessageTimingGap;
    }

    public IEnumerable<ButtplugDeviceGenericAttribute> GetActuators(ActuatorType actuatorType)
        => actuatorType switch
        {
            ActuatorType.Position => LinearActuators,
            ActuatorType.Rotate => RotateActuators,
            _ => ScalarActuators.Where(c => c.ActuatorType == actuatorType)
        };

    public IEnumerable<ButtplugDeviceSensorAttribute> GetSensors(SensorType sensorType)
        => Sensors.Where(c => c.SensorType == sensorType);

    public IEnumerable<ButtplugDeviceSensorAttribute> GetSubscribeSensors(SensorType sensorType)
        => SubscribeSensors.Where(c => c.SensorType == sensorType);

    public ButtplugDeviceGenericAttribute? GetActuator(uint actuatorIndex, ActuatorType actuatorType)
        => GetActuators(actuatorType).SingleOrDefault(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType);
    public ButtplugDeviceGenericAttribute? GetActuator(ActuatorAttributeIdentifier identifier)
        => GetActuator(identifier.Index, identifier.ActuatorType);

    public ButtplugDeviceSensorAttribute? GetSensor(uint sensorIndex, SensorType sensorType)
        => Sensors.SingleOrDefault(s => s.Index == sensorIndex && s.SensorType == sensorType);
    public ButtplugDeviceSensorAttribute? GetSensor(SensorAttributeIdentifier identifier)
        => GetSensor(identifier.Index, identifier.SensorType);

    public ButtplugDeviceSensorAttribute? GetSubscribeSensor(uint sensorIndex, SensorType sensorType)
        => SubscribeSensors.SingleOrDefault(s => s.Index == sensorIndex && s.SensorType == sensorType);
    public ButtplugDeviceSensorAttribute? GetSubscribeSensor(SensorAttributeIdentifier identifier)
        => GetSubscribeSensor(identifier.Index, identifier.SensorType);

    public bool TryGetActuator(uint actuatorIndex, ActuatorType actuatorType, [MaybeNullWhen(false)] out ButtplugDeviceGenericAttribute actuator)
        => (actuator = GetActuator(actuatorIndex, actuatorType)) != null;
    public bool TryGetActuator(ActuatorAttributeIdentifier identifier, [MaybeNullWhen(false)] out ButtplugDeviceGenericAttribute actuator)
        => TryGetActuator(identifier.Index, identifier.ActuatorType, out actuator);

    public bool TryGetSensor(uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out ButtplugDeviceSensorAttribute sensor)
        => (sensor = GetSensor(sensorIndex, sensorType)) != null;
    public bool TryGetSensor(SensorAttributeIdentifier identifier, [MaybeNullWhen(false)] out ButtplugDeviceSensorAttribute sensor)
        => TryGetSensor(identifier.Index, identifier.SensorType, out sensor);

    public bool TryGetSubscribeSensor(uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out ButtplugDeviceSensorAttribute sensor)
        => (sensor = GetSubscribeSensor(sensorIndex, sensorType)) != null;
    public bool TryGetSubscribeSensor(SensorAttributeIdentifier identifier, [MaybeNullWhen(false)] out ButtplugDeviceSensorAttribute sensor)
        => TryGetSubscribeSensor(identifier.Index, identifier.SensorType, out sensor);

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

    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(uint sensorIndex, SensorType sensorType, SensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
        => await SubscribeSensorAsync(new(sensorIndex, sensorType), readingCallback, cancellationToken).ConfigureAwait(false);
    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(SensorAttributeIdentifier identifier, SensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
    {
        if (_sensorSubscriptions.ContainsKey(identifier))
            throw new ButtplugException("Cannot subscribe to the same sensor multiple times");

        await SendMessageExpectTAsync<OkButtplugMessage>(new SensorSubscribeCommandButtplugMessage(Index, identifier.Index, identifier.SensorType), cancellationToken).ConfigureAwait(false);

        var subscription = new ButtplugDeviceSensorSubscription(this, identifier.Index, identifier.SensorType, readingCallback, UnsubscribeSensorAsync);
        return !_sensorSubscriptions.TryAdd(identifier, subscription)
            ? throw new ButtplugException("Cannot subscribe to the same sensor multiple times")
            : subscription;
    }

    public async Task UnsubscribeSensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
        => await UnsubscribeSensorAsync(new SensorAttributeIdentifier(sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
    public async Task UnsubscribeSensorAsync(SensorAttributeIdentifier identifier, CancellationToken cancellationToken)
    {
        if (!_sensorSubscriptions.TryRemove(identifier, out var _))
            throw new ButtplugException("Cannot find sensor to unsubscribe");

        await SendMessageExpectTAsync<OkButtplugMessage>(new SensorUnsubscribeCommandButtplugMessage(Index, identifier.Index, identifier.SensorType), cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopDeviceCommandButtplugMessage(Index), cancellationToken).ConfigureAwait(false);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _sender == null ? throw new ObjectDisposedException(nameof(_sender))
                           : await _sender.SendMessageExpectTAsync<T>(message, cancellationToken).ConfigureAwait(false);

    public override bool Equals(object? obj) => Equals(obj as ButtplugDevice);
    public virtual bool Equals(ButtplugDevice? other) => other != null && (ReferenceEquals(this, other) || Index == other.Index);
    public override int GetHashCode() => HashCode.Combine(Index);

    internal void HandleSubscribeSensorReading(uint sensorIndex, SensorType sensorType, ImmutableList<int> data)
    {
        if (!_sensorSubscriptions.TryGetValue(new SensorAttributeIdentifier(sensorIndex, sensorType), out var subscription))
            throw new ButtplugException("Could not find sensor subscription for sensor reading");

        subscription.HandleReadingData(data);
    }

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