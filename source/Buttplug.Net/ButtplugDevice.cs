using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Buttplug;

public interface IUnsafeButtplugDevice
{
    IReadOnlyList<string> ReadEndpoints { get; }
    IReadOnlyList<string> WriteEndpoints { get; }
    IReadOnlyList<string> SubscribeEndpoints { get; }
    IReadOnlyList<ButtplugDeviceEndpointSubscription> EndpointSubscriptions { get; }

    Task EndpointWriteAsync(string endpoint, IEnumerable<byte> data, bool writeWithResponse, CancellationToken cancellationToken);
    Task<ImmutableArray<byte>> EndpointReadAsync(string endpoint, uint expectedLength, bool waitForData, CancellationToken cancellationToken);
    Task<ButtplugDeviceEndpointSubscription> EndpointSubscribeAsync(string endpoint, ButtplugDeviceEndpointSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken);
    Task EndpointUnsubscribeAsync(string endpoint, CancellationToken cancellationToken);
}

public class ButtplugDevice : IUnsafeButtplugDevice, IEquatable<ButtplugDevice>, IDisposable
{
    private readonly ImmutableArray<ButtplugDeviceLinearActuator> _linearActuators;
    private readonly ImmutableArray<ButtplugDeviceRotateActuator> _rotateActuators;
    private readonly ImmutableArray<ButtplugDeviceScalarActuator> _scalarActuators;
    private readonly ImmutableArray<ButtplugDeviceReadSensor> _readSensors;
    private readonly ImmutableArray<ButtplugDeviceSubscribeSensor> _subscribeSensors;
    private readonly ImmutableArray<string> _readEndpoints;
    private readonly ImmutableArray<string> _writeEndpoints;
    private readonly ImmutableArray<string> _subscribeEndpoints;

    private readonly ConcurrentDictionary<ButtplugDeviceSubscribeSensor, ButtplugDeviceSensorSubscription> _sensorSubscriptions;
    private readonly ConcurrentDictionary<string, ButtplugDeviceEndpointSubscription> _endpointSubscriptions;

    private IButtplugSender? _sender;

    public uint Index { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public uint MessageTimingGap { get; }
    public bool SupportsStopCommand { get; }

    public IEnumerable<ButtplugDeviceActuator> Actuators => LinearActuators.Concat<ButtplugDeviceActuator>(RotateActuators).Concat(ScalarActuators);
    public IReadOnlyList<ButtplugDeviceLinearActuator> LinearActuators => _linearActuators;
    public IReadOnlyList<ButtplugDeviceRotateActuator> RotateActuators => _rotateActuators;
    public IReadOnlyList<ButtplugDeviceScalarActuator> ScalarActuators => _scalarActuators;

    public IEnumerable<ButtplugDeviceSensor> Sensors => ReadSensors.Concat<ButtplugDeviceSensor>(SubscribeSensors);
    public IReadOnlyList<ButtplugDeviceReadSensor> ReadSensors => _readSensors;
    public IReadOnlyList<ButtplugDeviceSubscribeSensor> SubscribeSensors => _subscribeSensors;
    public IReadOnlyList<ButtplugDeviceSensorSubscription> SensorSubscriptions => (IReadOnlyList<ButtplugDeviceSensorSubscription>)_sensorSubscriptions.Values;

    IReadOnlyList<string> IUnsafeButtplugDevice.ReadEndpoints => _readEndpoints;
    IReadOnlyList<string> IUnsafeButtplugDevice.WriteEndpoints => _writeEndpoints;
    IReadOnlyList<string> IUnsafeButtplugDevice.SubscribeEndpoints => _subscribeEndpoints;
    IReadOnlyList<ButtplugDeviceEndpointSubscription> IUnsafeButtplugDevice.EndpointSubscriptions => (IReadOnlyList<ButtplugDeviceEndpointSubscription>)_endpointSubscriptions.Values;

    internal ButtplugDevice(IButtplugSender sender, ButtplugDeviceInfo info)
    {
        _sender = sender;
        _sensorSubscriptions = new ConcurrentDictionary<ButtplugDeviceSubscribeSensor, ButtplugDeviceSensorSubscription>();
        _endpointSubscriptions = new ConcurrentDictionary<string, ButtplugDeviceEndpointSubscription>();

        Index = info.DeviceIndex;
        Name = info.DeviceName;
        DisplayName = info.DeviceDisplayName;
        MessageTimingGap = info.DeviceMessageTimingGap;

        var attributes = info.DeviceMessages;

        //use enumeration index as workaround until buttplug sends correct index in the message
        _linearActuators = ImmutableArray.CreateRange(attributes.LinearCmd.Select((a, i) => new ButtplugDeviceLinearActuator(this, (uint)i, a)));
        _rotateActuators = ImmutableArray.CreateRange(attributes.RotateCmd.Select((a, i) => new ButtplugDeviceRotateActuator(this, (uint)i, a)));
        _scalarActuators = ImmutableArray.CreateRange(attributes.ScalarCmd.Select((a, i) => new ButtplugDeviceScalarActuator(this, (uint)i, a)));

        _readSensors = ImmutableArray.CreateRange(attributes.SensorReadCmd.Select((s, i) => new ButtplugDeviceReadSensor(this, (uint)i, s)));
        _subscribeSensors = ImmutableArray.CreateRange(attributes.SensorSubscribeCmd.Select((s, i) => new ButtplugDeviceSubscribeSensor(this, (uint)i, s)));

        _readEndpoints = ImmutableArray.CreateRange(attributes.RawReadCmd.SelectMany(r => r.Endpoints));
        _writeEndpoints = ImmutableArray.CreateRange(attributes.RawWriteCmd.SelectMany(r => r.Endpoints));
        _subscribeEndpoints = ImmutableArray.CreateRange(attributes.RawSubscribeCmd.SelectMany(r => r.Endpoints));

        SupportsStopCommand = attributes.StopDeviceCmd != null;
    }

    public IEnumerable<ButtplugDeviceActuator> GetActuators(ActuatorType actuatorType)
        => Actuators.Where(a => a.ActuatorType == actuatorType);
    public IEnumerable<TActuator> GetActuators<TActuator>(ActuatorType actuatorType) where TActuator : ButtplugDeviceActuator
        => GetActuators(actuatorType).OfType<TActuator>();
    public IEnumerable<ButtplugDeviceActuator> GetActuators(Type type, ActuatorType actuatorType)
        => GetActuators(actuatorType).Where(a => a.GetType().IsAssignableTo(type));

    public IEnumerable<ButtplugDeviceSensor> GetSensors(SensorType sensorType)
        => Sensors.Where(s => s.SensorType == sensorType);
    public IEnumerable<TSensor> GetSensors<TSensor>(SensorType sensorType) where TSensor : ButtplugDeviceSensor
        => GetSensors(sensorType).OfType<TSensor>();
    public IEnumerable<ButtplugDeviceSensor> GetSensors(Type type, SensorType sensorType)
        => GetSensors(sensorType).Where(s => s.GetType().IsAssignableTo(type));

    public ButtplugDeviceActuator GetActuator(uint actuatorIndex, ActuatorType actuatorType)
        => GetActuators(actuatorType).Single(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType);
    public TActuator GetActuator<TActuator>(uint actuatorIndex, ActuatorType actuatorType) where TActuator : ButtplugDeviceActuator
        => GetActuators<TActuator>(actuatorType).Single(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType);
    public ButtplugDeviceActuator GetActuator(Type type, uint actuatorIndex, ActuatorType actuatorType)
        => GetActuators(type, actuatorType).Single(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType);

    public ButtplugDeviceSensor GetSensor(uint sensorIndex, SensorType sensorType)
        => GetSensors(sensorType).Single(s => s.Index == sensorIndex && s.SensorType == sensorType);
    public TSensor GetSensor<TSensor>(uint sensorIndex, SensorType sensorType) where TSensor : ButtplugDeviceSensor
        => GetSensors<TSensor>(sensorType).Single(s => s.Index == sensorIndex && s.SensorType == sensorType);
    public ButtplugDeviceSensor GetSensor(Type type, uint sensorIndex, SensorType sensorType)
        => GetSensors(type, sensorType).Single(s => s.Index == sensorIndex && s.SensorType == sensorType);

    public bool TryGetActuator(uint actuatorIndex, ActuatorType actuatorType, [MaybeNullWhen(false)] out ButtplugDeviceActuator actuator)
        => (actuator = GetActuators(actuatorType).FirstOrDefault(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType)) != null;
    public bool TryGetActuator<TActuator>(uint actuatorIndex, ActuatorType actuatorType, [MaybeNullWhen(false)] out TActuator actuator) where TActuator : ButtplugDeviceActuator
        => (actuator = GetActuators<TActuator>(actuatorType).FirstOrDefault(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType)) != null;
    public bool TryGetActuator(Type type, uint actuatorIndex, ActuatorType actuatorType, [MaybeNullWhen(false)] out ButtplugDeviceActuator actuator)
        => (actuator = GetActuators(type, actuatorType).FirstOrDefault(a => a.Index == actuatorIndex && a.ActuatorType == actuatorType)) != null;

    public bool TryGetSensor(uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out ButtplugDeviceSensor sensor)
        => (sensor = GetSensors(sensorType).FirstOrDefault(s => s.Index == sensorIndex && s.SensorType == sensorType)) != null;
    public bool TryGetSensor<TSensor>(uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out TSensor sensor) where TSensor : ButtplugDeviceSensor
        => (sensor = GetSensors<TSensor>(sensorType).FirstOrDefault(s => s.Index == sensorIndex && s.SensorType == sensorType)) != null;
    public bool TryGetSensor(Type type, uint sensorIndex, SensorType sensorType, [MaybeNullWhen(false)] out ButtplugDeviceSensor sensor)
        => (sensor = GetSensors(type, sensorType).FirstOrDefault(s => s.Index == sensorIndex && s.SensorType == sensorType)) != null;

    public async Task ScalarAsync(double scalar, ActuatorType actuatorType, CancellationToken cancellationToken)
        => await ScalarAsync(ScalarActuators.Select(a => new ScalarCommand(a.Index, scalar, actuatorType)), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(double scalar, uint actuatorIndex, ActuatorType actuatorType, CancellationToken cancellationToken)
        => await ScalarAsync(new ScalarCommand(actuatorIndex, scalar, actuatorType), cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(ScalarCommand scalarCommand, CancellationToken cancellationToken)
        => await ScalarAsync([scalarCommand], cancellationToken).ConfigureAwait(false);
    public async Task ScalarAsync(IEnumerable<ScalarCommand> scalarCommands, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new ScalarCommandButtplugMessage(Index, scalarCommands), cancellationToken).ConfigureAwait(false);

    public async Task RotateAsync(double speed, bool clockwise, CancellationToken cancellationToken)
        => await RotateAsync(RotateActuators.Select(a => new RotateCommand(a.Index, speed, clockwise)), cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(double speed, bool clockwise, uint actuatorIndex, CancellationToken cancellationToken)
        => await RotateAsync(new RotateCommand(actuatorIndex, speed, clockwise), cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(RotateCommand rotateCommand, CancellationToken cancellationToken)
        => await RotateAsync([rotateCommand], cancellationToken).ConfigureAwait(false);
    public async Task RotateAsync(IEnumerable<RotateCommand> rotateCommands, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new RotateCommandButtplugMessage(Index, rotateCommands), cancellationToken).ConfigureAwait(false);

    public async Task LinearAsync(uint duration, double position, CancellationToken cancellationToken)
        => await LinearAsync(LinearActuators.Select(a => new LinearCommand(a.Index, duration, position)), cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(uint duration, double position, uint actuatorIndex, CancellationToken cancellationToken)
        => await LinearAsync(new LinearCommand(actuatorIndex, duration, position), cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(LinearCommand linearCommand, CancellationToken cancellationToken)
        => await LinearAsync([linearCommand], cancellationToken).ConfigureAwait(false);
    public async Task LinearAsync(IEnumerable<LinearCommand> linearCommands, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new LinearCommandButtplugMessage(Index, linearCommands), cancellationToken).ConfigureAwait(false);

    public async Task<ImmutableArray<int>> ReadSensorAsync(SensorType sensorType, CancellationToken cancellationToken)
        => await ReadSensorAsync(GetSensors<ButtplugDeviceReadSensor>(sensorType).First().Index, sensorType, cancellationToken).ConfigureAwait(false);
    public async Task<ImmutableArray<int>> ReadSensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
    {
        var response = await SendMessageExpectTAsync<SensorReadingButtplugMessage>(new SensorReadCommandButtplugMessage(Index, sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(SensorType sensorType, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
        => await SubscribeSensorAsync(GetSensors<ButtplugDeviceSubscribeSensor>(sensorType).First(), readingCallback, cancellationToken).ConfigureAwait(false);
    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(uint sensorIndex, SensorType sensorType, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
        => await SubscribeSensorAsync(GetSensor<ButtplugDeviceSubscribeSensor>(sensorIndex, sensorType), readingCallback, cancellationToken).ConfigureAwait(false);
    public async Task<ButtplugDeviceSensorSubscription> SubscribeSensorAsync(ButtplugDeviceSubscribeSensor sensor, ButtplugDeviceSensorSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
    {
        if (_sensorSubscriptions.ContainsKey(sensor))
            throw new ButtplugException("Cannot subscribe to the same sensor multiple times");

        await SendMessageExpectTAsync<OkButtplugMessage>(new SensorSubscribeCommandButtplugMessage(Index, sensor.Index, sensor.SensorType), cancellationToken).ConfigureAwait(false);

        var subscription = new ButtplugDeviceSensorSubscription(sensor, readingCallback);
        return !_sensorSubscriptions.TryAdd(sensor, subscription)
            ? throw new ButtplugException("Cannot subscribe to the same sensor multiple times")
            : subscription;
    }

    public async Task UnsubscribeSensorAsync(SensorType sensorType, CancellationToken cancellationToken)
        => await UnsubscribeSensorAsync(GetSensors<ButtplugDeviceSubscribeSensor>(sensorType).First(), cancellationToken).ConfigureAwait(false);
    public async Task UnsubscribeSensorAsync(uint sensorIndex, SensorType sensorType, CancellationToken cancellationToken)
        => await UnsubscribeSensorAsync(GetSensor<ButtplugDeviceSubscribeSensor>(sensorIndex, sensorType), cancellationToken).ConfigureAwait(false);
    public async Task UnsubscribeSensorAsync(ButtplugDeviceSubscribeSensor sensor, CancellationToken cancellationToken)
    {
        if (!_sensorSubscriptions.TryRemove(sensor, out var _))
            throw new ButtplugException("Cannot find sensor to unsubscribe");

        await SendMessageExpectTAsync<OkButtplugMessage>(new SensorUnsubscribeCommandButtplugMessage(Index, sensor.Index, sensor.SensorType), cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new StopDeviceCommandButtplugMessage(Index), cancellationToken).ConfigureAwait(false);

    public IUnsafeButtplugDevice AsUnsafe() => this;
    async Task IUnsafeButtplugDevice.EndpointWriteAsync(string endpoint, IEnumerable<byte> data, bool writeWithResponse, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new EndpointWriteCommandButtplugMessage(Index, endpoint, data, writeWithResponse), cancellationToken).ConfigureAwait(false);
    async Task<ImmutableArray<byte>> IUnsafeButtplugDevice.EndpointReadAsync(string endpoint, uint expectedLength, bool waitForData, CancellationToken cancellationToken)
    {
        var response = await SendMessageExpectTAsync<EndpointReadingButtplugMessage>(new EndpointReadCommandButtplugMessage(Index, endpoint, expectedLength, waitForData), cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    async Task<ButtplugDeviceEndpointSubscription> IUnsafeButtplugDevice.EndpointSubscribeAsync(string endpoint, ButtplugDeviceEndpointSubscriptionReadingCallback readingCallback, CancellationToken cancellationToken)
    {
        if (_endpointSubscriptions.ContainsKey(endpoint))
            throw new ButtplugException("Cannot subscribe to the same endpoint multiple times");

        await SendMessageExpectTAsync<OkButtplugMessage>(new EndpointSubscribeCommandButtplugMessage(Index, endpoint), cancellationToken).ConfigureAwait(false);

        var subscription = new ButtplugDeviceEndpointSubscription(this, endpoint, readingCallback);
        return !_endpointSubscriptions.TryAdd(endpoint, subscription)
            ? throw new ButtplugException("Cannot subscribe to the same endpoint multiple times")
            : subscription;
    }

    async Task IUnsafeButtplugDevice.EndpointUnsubscribeAsync(string endpoint, CancellationToken cancellationToken)
        => await SendMessageExpectTAsync<OkButtplugMessage>(new EndpointUnsubscribeCommandButtplugMessage(Index, endpoint), cancellationToken).ConfigureAwait(false);

    private async Task<T> SendMessageExpectTAsync<T>(IButtplugMessage message, CancellationToken cancellationToken) where T : IButtplugMessage
        => _sender == null ? throw new ObjectDisposedException(nameof(_sender))
                           : await _sender.SendMessageExpectTAsync<T>(message, cancellationToken).ConfigureAwait(false);

    public override bool Equals(object? obj) => Equals(obj as ButtplugDevice);
    public virtual bool Equals(ButtplugDevice? other) => other != null && (ReferenceEquals(this, other) || Index == other.Index);
    public override int GetHashCode() => HashCode.Combine(Index);

    internal void HandleSubscribeSensorReading(uint sensorIndex, SensorType sensorType, ImmutableArray<int> data)
    {
        var sensor = GetSensor<ButtplugDeviceSubscribeSensor>(sensorIndex, sensorType);
        if (!_sensorSubscriptions.TryGetValue(sensor, out var subscription))
            throw new ButtplugException("Could not find sensor subscription for sensor reading");

        subscription.HandleReadingData(data);
    }

    internal void HandleSubscribeEndpointReading(string endpoint, ImmutableArray<byte> data)
    {
        if (!_endpointSubscriptions.TryGetValue(endpoint, out var subscription))
            throw new ButtplugException("Could not find endpoint subscription for endpoint reading");

        subscription.HandleReadingData(data);
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(nameof(ButtplugDevice));
        stringBuilder.Append(" { ");
        if (PrintMembers(stringBuilder))
            stringBuilder.Append(' ');

        stringBuilder.Append('}');
        return stringBuilder.ToString();
    }

    protected virtual bool PrintMembers(StringBuilder builder)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        builder.Append($"{nameof(Index)} = ");
        builder.Append(Index);
        builder.Append($", {nameof(Name)} = ");
        builder.Append(Name);
        builder.Append($", {nameof(DisplayName)} = ");
        builder.Append(DisplayName);
        builder.Append($", {nameof(MessageTimingGap)} = ");
        builder.Append(MessageTimingGap);
        builder.Append($", {nameof(Actuators)} = ");
        builder.Append(_linearActuators.Length + _rotateActuators.Length + _scalarActuators.Length);
        builder.Append($", {nameof(Sensors)} = ");
        builder.Append(_readSensors.Length + _subscribeSensors.Length);
        return true;
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