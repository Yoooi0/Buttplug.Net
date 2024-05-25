namespace Buttplug;

[AttributeUsage(AttributeTargets.Class)]
public class ButtplugMessageNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
