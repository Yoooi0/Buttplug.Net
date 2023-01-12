using System.Reflection;

namespace Buttplug;

public interface IButtplugMessageJsonConverter
{
    string Serialize(IButtplugMessage message);
    string Serialize(IEnumerable<IButtplugMessage> message);
    public IEnumerable<IButtplugMessage> Deserialize(string json);
}

public abstract class ButtplugMessageJsonConverter : IButtplugMessageJsonConverter
{
    private readonly ILookup<string, Type> _messageTypeLookup;
    private readonly ILookup<Type, string> _messageNameLookup;

    protected ButtplugMessageJsonConverter()
    {
        var messageTypes = Assembly.GetAssembly(typeof(IButtplugMessage))!
                                   .GetTypes()
                                   .Where(t => t.IsClass && !t.IsAbstract &&
                                               t.IsAssignableTo(typeof(IButtplugMessage)))
                                   .ToList();

        var messageNames = messageTypes.ToDictionary(t => t, t => t.GetCustomAttribute<ButtplugMessageNameAttribute>()!.Name);
        _messageTypeLookup = messageTypes.ToLookup(t => messageNames[t]);
        _messageNameLookup = messageTypes.ToLookup(t => t, t => messageNames[t]);
    }

    protected string GetMessageName(IButtplugMessage message) => _messageNameLookup[message.GetType()].Single();
    protected Type GetMessageType(string messageName) => _messageTypeLookup[messageName].Single();

    public abstract IEnumerable<IButtplugMessage> Deserialize(string json);
    public abstract string Serialize(IButtplugMessage message);
    public abstract string Serialize(IEnumerable<IButtplugMessage> message);
}