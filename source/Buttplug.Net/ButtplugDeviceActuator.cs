namespace Buttplug;

public enum ActuatorType
{
    Unknown,
    Vibrate,
    Rotate,
    Oscillate,
    Constrict,
    Inflate,
    Position
}

public readonly record struct ScalarCommand(uint Index, double Scalar, ActuatorType ActuatorType);
public readonly record struct RotateCommand(uint Index, double Speed, bool Clockwise);
public readonly record struct LinearCommand(uint Index, uint Duration, double Position);

public record class ButtplugDeviceActuator
{
    public ButtplugDevice Device { get; }
    public uint Index { get; }
    public ActuatorType ActuatorType { get; }
    public string FeatureDescriptor { get; }
    public uint StepCount { get; }

    internal ButtplugDeviceActuator(ButtplugDevice device, uint index, ButtplugDeviceActuatorAttribute attribute)
    {
        Device = device;
        Index = index;
        ActuatorType = attribute.ActuatorType;
        FeatureDescriptor = attribute.FeatureDescriptor;
        StepCount = attribute.StepCount;
    }
}

public record class ButtplugDeviceScalarActuator : ButtplugDeviceActuator
{
    internal ButtplugDeviceScalarActuator(ButtplugDevice Device, uint Index, ButtplugDeviceActuatorAttribute attribute)
        : base(Device, Index, attribute) { }

    public async Task ScalarAsync(double scalar, CancellationToken cancellationToken)
        => await Device.ScalarAsync(new ScalarCommand(Index, scalar, ActuatorType), cancellationToken).ConfigureAwait(false);
}

public record class ButtplugDeviceLinearActuator : ButtplugDeviceActuator
{
    internal ButtplugDeviceLinearActuator(ButtplugDevice Device, uint Index, ButtplugDeviceActuatorAttribute attribute) : base(Device, Index, attribute)
    {
        if (attribute.ActuatorType != ActuatorType.Position)
            throw new ButtplugException($"Cannot create linear actuator from \"{attribute.ActuatorType}\" attribute type");
    }

    public async Task LinearAsync(uint duration, double position, CancellationToken cancellationToken)
        => await Device.LinearAsync(new LinearCommand(Index, duration, position), cancellationToken).ConfigureAwait(false);
}

public record class ButtplugDeviceRotateActuator : ButtplugDeviceActuator
{
    internal ButtplugDeviceRotateActuator(ButtplugDevice Device, uint Index, ButtplugDeviceActuatorAttribute attribute) : base(Device, Index, attribute)
    {
        if (attribute.ActuatorType != ActuatorType.Rotate)
            throw new ButtplugException($"Cannot create linear actuator from \"{attribute.ActuatorType}\" attribute type");
    }

    public async Task RotateAsync(double speed, bool clockwise, CancellationToken cancellationToken)
        => await Device.RotateAsync(new RotateCommand(Index, speed, clockwise), cancellationToken).ConfigureAwait(false);
}