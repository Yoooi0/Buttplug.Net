namespace Buttplug;

[AttributeUsage(AttributeTargets.Class)]
public class ButtplugMessageNameAttribute : Attribute
{
    public string Name { get; }
    public ButtplugMessageNameAttribute(string name) => Name = name;
}
