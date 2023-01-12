using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Buttplug;

public class ButtplugDevice : IEquatable<ButtplugDevice>, IDisposable
{
    private readonly ImmutableArray<ButtplugDeviceLinearActuator> _linearActuators;
    private readonly ImmutableArray<ButtplugDeviceRotateActuator> _rotateActuators;
    private readonly ImmutableArray<ButtplugDeviceScalarActuator> _scalarActuators;
    private readonly ImmutableArray<ButtplugDeviceReadSensor> _readSensors;
    private readonly ImmutableArray<ButtplugDeviceSubscribeSensor> _subscribeSensors;
    private readonly ConcurrentDictionary<SensorIdentifier, ButtplugDeviceSensorSubscription> _sensorSubscriptions;

    private IButtplugSender? _sender;

    public uint Index { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public uint MessageTimingGap { get; }

    public IEnumerable<ActuatorType> SupportedActuatorTypes => Actuators.Select(a => a.ActuatorType).Distinct();
    public IEnumerable<SensorType> SupportedReadSensorTypes => ReadSensors.Select(c => c.SensorType).Distinct();
    public IEnumerable<SensorType> SupportedSubscribeSensorTypes => SubscribeSensors.Select(c => c.SensorType).Distinct();

    public IEnumerable<ButtplugDeviceActuator> Actuators => LinearActuators.Concat<ButtplugDeviceActuator>(RotateActuators).Concat(ScalarActuators);
    public IReadOnlyList<ButtplugDeviceLinearActuator> LinearActuators => _linearActuators;
    public IReadOnlyList<ButtplugDeviceRotateActuator> RotateActuators => _rotateActuators;
    public IReadOnlyList<ButtplugDeviceScalarActuator> ScalarActuators => _scalarActuators;

    public IEnumerable<ButtplugDeviceSensor> Sensors => ReadSensors.Concat<ButtplugDeviceSensor>(SubscribeSensors);
    public IReadOnlyList<ButtplugDeviceReadSensor> ReadSensors => _readSensors;
    public IReadOnlyList<ButtplugDeviceSubscribeSensor> SubscribeSensors => _subscribeSensors;
    public IReadOnlyList<ButtplugDeviceSensorSubscription> SensorSubscriptions => (IReadOnlyList<ButtplugDeviceSensorSubscription>)_sensorSubscriptions.Values;

    internal ButtplugDevice(IButtplugSender sender, ButtplugDeviceInfo info)
    {
        _sender = sender;
        _sensorSubscriptions = new ConcurrentDictionary<SensorIdentifier, ButtplugDeviceSensorSubscription>();

        var attributes = info.DeviceMessages;

        //use enumeration index as workaround until buttplug sends correct index in the message
        _linearActuators = ImmutableArray.CreateRange(attributes.LinearCmd.Select((a, i) => new ButtplugDeviceLinearActuator(this, (uint)i, a)));
        _rotateActuators = ImmutableArray.CreateRange(attributes.RotateCmd.Select((a, i) => new ButtplugDeviceRotateActuator(this, (uint)i, a)));
        _scalarActuators = ImmutableArray.CreateRange(attributes.ScalarCmd.Select((a, i) => new ButtplugDeviceScalarActuator(this, (uint)i, a)));

        _readSensors = ImmutableArray.CreateRange(attributes.SensorReadCmd.Select((s, i) => new ButtplugDeviceReadSensor(this, (uint)i, s)));
        _subscribeSensors = ImmutableArray.CreateRange(attributes.SensorSubscribeCmd.Select((s, i) => new ButtplugDeviceSubscribeSensor(this, (uint)i, s)));

        Index = info.DeviceIndex;
        Name = info.DeviceName;
        DisplayName = info.DeviceDisplayName;
        MessageTimingGap = info.DeviceMessageTimingGap;
    }

    public IEnumerable<ButtplugDeviceActuator> GetActuators(ActuatorType actuatorType)
        => Actuators.Where(a => a.ActuatorType == actuatorType);
    public IEnumerable<TActuator> GetActuators<TActuator>(ActuatorType actuatorType) where TActuator : ButtplugDeviceActuator
        => GetActuators(actuatorType).OfType<TActuator>();

    public IEnumerable<ButtplugDeviceSensor> GetSensors(SensorType sensorType)
        => Sensors.Where(c => c.SensorType == sensorType);
    public IEnumerable<TSensor> GetSensors<TSensor>(SensorType sensorType) where TSensor : ButtplugDeviceSensor
        => GetSensors(sensorType).OfType<TSensor>();

    public ButtplugDeviceActuator? GetActuator(uint actuatorIndex, ActuatorType actuatorType)
        => GetActuators(actuatorType).SingleOrDefault(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType);
    public ButtplugDeviceActuator? GetActuator(ActuatorIdentifier actuatorIdentifier)
        => GetActuator(actuatorIdentifier.Index, actuatorIdentifier.ActuatorType);
    public TActuator? GetActuator<TActuator>(uint actuatorIndex, ActuatorType actuatorType) where TActuator : ButtplugDeviceActuator
        => GetActuators<TActuator>(actuatorType).SingleOrDefault(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType);
    public TActuator? GetActuator<TActuator>(ActuatorIdentifier actuatorIdentifier) where TActuator : ButtplugDeviceActuator
        => GetActuator<TActuator>(actuatorIdentifier.Index, actuatorIdentifier.ActuatorType);

    public ButtplugDeviceSensor? GetSensor(uint sensorIndex, SensorType sensorType)
        => GetSensors(sensorType).SingleOrDefault(s => s.Index == sensorIndex && s.SensorType == sensorType);
    public ButtplugDeviceSensor? GetSensor(SensorIdentifier sensorIdentifier)
        => GetSensor(sensorIdentifier.Index, sensorIdentifier.SensorType);
    public TSensor? GetSensor<TSensor>(uint sensorIndex, SensorType sensorType) where TSensor : ButtplugDeviceSensor
        => GetSensors<TSensor>(sensorType).SingleOrDefault(a => a.Index == sensorIndex && a.SensorType == sensorType);
    public TSensor? GetSensor<TSensor>(SensorIdentifier sensorIdentifier) where TSensor : ButtplugDeviceSensor
        => GetSensor<TSensor>(sensorIdentifier.Index, sensorIdentifier.SensorType);

    public bool TryGetActuator(uint actuatorIndex, ActuatorType actuatorType, [MaybeNullWhen(false)] out ButtplugDeviceActuator actuator)
        => (actuator = GetActuator(actuatorIndex, actuatorType)) != null;
    public bool TryGetActuator(ActuatorIdentifier actuatorIdentifier, [MaybeNullWhen(false)] out ButtplugDeviceActuator actuator)
        => TryGetActuator(actuatorIdentifier.Index, actuatorIdentifier.ActuatorType, out actuator);
    public bool TryGetActuator<TActuator>(uint actuatorIndex, ActuatorType actuatorType, [MaybeNullWhen(false)] out TActuator actuator) where TActuator : ButtplugDeviceActuator
        => (actuator = GetActuator<TActuator>(actuatorIndex, actuatorType)) != null;
    public bool TryGetActuator<TActuator>(ActuatorIdentifier actuatorIdentifier, [MaybeNullWhen(false)] out TActuator actuator) where TActuator : ButtplugDeviceActuator
        => TryGetActuator(actuatorIdentifier.Index, actuatorIdentifier.ActuatorType, out actuator);

    public bool TryGetSensor(uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out ButtplugDeviceSensor sensor)
        => (sensor = GetSensor(sensorIndex, sensorType)) != null;
    public bool TryGetSensor(SensorIdentifier sensorIdentifier, [MaybeNullWhen(false)] out ButtplugDeviceSensor sensor)
        => TryGetSensor(sensorIdentifier.Index, sensorIdentifier.SensorType, out sensor);
    public bool TryGetSensor<TSensor>(uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out TSensor sensor) where TSensor : ButtplugDeviceSensor
        => (sensor = GetSensor<TSensor>(sensorIndex, sensorType)) != null;
    public bool TryGetSensor<TSensor>(SensorIdentifier sensorIdentifier, [MaybeNullWhen(false)] out TSensor sensor) where TSensor : ButtplugDeviceSensor
        => TryGetSensor(sensorIdentifier.Index, sensorIdentifier.SensorType, out sensor);

    public async Task ScalarAsync(double scalar, ActuatorType actuatorType, CancellationToken cancellationToken)
        => await ScalarAsync(Enumerable.Range(0, ScalarActuators.Count).Select(i => new ScalarCommand((uint)i, scalar, actuatorType)), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(ScalarCommand scalarCommand, CancellationToken cancellationToken)
        => await ScalarAsync(new[] { scalarCommand }, cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(double scalar, ActuatorIdentifier actuatorIdentifier, CancellationToken cancellationToken)
        => await ScalarAsync(new[] { new ScalarCommand(actuatorIdentifier.Index, scalar, actuatorIdentifier.ActuatorType) }, cancellationToken).ConfigureAwait(false);
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

    public async Task RotateAsync(double speed, bool clockwise, ActuatorIdentifier actuatorIdentifier, CancellationToken cancellationToken)
    {
        if (actuatorIdentifier.ActuatorType != ActuatorType.Rotate)
            throw new ButtplugException("Invalid actuator identifier type");

        await RotateAsync(new[] { new RotateCommand(actuatorIdentifier.Index, speed, clockwise) }, cancellationToken).ConfigureAwait(false);
    }

    public async Task LinearAsync(uint duration, double position, ActuatorIdentifier actuatorIdentifier, CancellationToken cancellationToken)
    {
        if (actuatorIdentifier.ActuatorType != ActuatorType.Position)
            throw new ButtplugException("Invalid actuator identifier type");

        await LinearAsync(new[] { new LinearCommand(actuatorIdentifier.Index, duration, position) }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImmutableArray<int>> ReadSensorAsync(SensorType sensorType, CancellationToken cancellationToken)
        => await ReadSensorAsync(GetSensors<ButtplugDeviceReadSensor>(sensorType).First().Index, sensorType, cancellationToken).ConfigureAwait(false);
    public async Task<ImmutableArray<int>> ReadSensorAsync(SensorIdentifier sensorIdentifier, CancellationToken cancellationToken)
        => await ReadSensorAsync(sensorIdentifier.Index, sensorIdentifier.SensorType, cancellationToken).ConfigureAwait(false);
    public async Task<ImmutableArray<int>> ReadSensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
    {
        var response = await SendMessageExpectTAsync<SensorReadingButtplugMessage>(new SensorReadCommandButtplugMessage(Index, sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(SensorType sensorType, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
        => await SubscribeSensorAsync(new SensorIdentifier(GetSensors<ButtplugDeviceSubscribeSensor>(sensorType).First().Index, sensorType), readingCallback, cancellationToken).ConfigureAwait(false);
    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(uint sensorIndex, SensorType sensorType, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
        => await SubscribeSensorAsync(new SensorIdentifier(sensorIndex, sensorType), readingCallback, cancellationToken).ConfigureAwait(false);
    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(SensorIdentifier sensorIdentifier, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
    {
        if (_sensorSubscriptions.ContainsKey(sensorIdentifier))
            throw new ButtplugException("Cannot subscribe to the same sensor multiple times");

        await SendMessageExpectTAsync<OkButtplugMessage>(new SensorSubscribeCommandButtplugMessage(Index, sensorIdentifier.Index, sensorIdentifier.SensorType), cancellationToken).ConfigureAwait(false);

        var subscription = new ButtplugDeviceSensorSubscription(this, sensorIdentifier, readingCallback, UnsubscribeSensorAsync);
        return !_sensorSubscriptions.TryAdd(sensorIdentifier, subscription)
            ? throw new ButtplugException("Cannot subscribe to the same sensor multiple times")
            : subscription;
    }

    public async Task UnsubscribeSensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
        => await UnsubscribeSensorAsync(new SensorIdentifier(sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
    public async Task UnsubscribeSensorAsync(SensorIdentifier sensorIdentifier, CancellationToken cancellationToken)
    {
        if (!_sensorSubscriptions.TryRemove(sensorIdentifier, out var _))
            throw new ButtplugException("Cannot find sensor to unsubscribe");

        await SendMessageExpectTAsync<OkButtplugMessage>(new SensorUnsubscribeCommandButtplugMessage(Index, sensorIdentifier.Index, sensorIdentifier.SensorType), cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopDeviceCommandButtplugMessage(Index), cancellationToken).ConfigureAwait(false);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _sender == null ? throw new ObjectDisposedException(nameof(_sender))
                           : await _sender.SendMessageExpectTAsync<T>(message, cancellationToken).ConfigureAwait(false);

    public override bool Equals(object? obj) => Equals(obj as ButtplugDevice);
    public virtual bool Equals(ButtplugDevice? other) => other != null && (ReferenceEquals(this, other) || Index == other.Index);
    public override int GetHashCode() => HashCode.Combine(Index);

    internal void HandleSubscribeSensorReading(uint sensorIndex, SensorType sensorType, ImmutableArray<int> data)
    {
        if (!_sensorSubscriptions.TryGetValue(new SensorIdentifier(sensorIndex, sensorType), out var subscription))
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