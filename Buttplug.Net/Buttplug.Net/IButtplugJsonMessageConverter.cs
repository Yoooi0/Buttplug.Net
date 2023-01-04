using System.Collections.Immutable;
using System.Reflection;

namespace Buttplug;

public interface IButtplugJsonMessageConverter
{
    string Serialize<T>(T message) where T : IButtplugMessage;
    string Serialize<T>(IEnumerable<T> message) where T : IButtplugMessage;
    public IEnumerable<IButtplugMessage> Deserialize(string json);
}

public abstract class ButtplugJsonMessageConverter : IButtplugJsonMessageConverter
{
    private readonly ILookup<string, Type> _messageTypeLookup;
    private readonly ILookup<Type, string> _messageNameLookup;

    protected ButtplugJsonMessageConverter()
    {
        var messageTypes = ImmutableList.CreateRange(Assembly.GetAssembly(typeof(IButtplugMessage))!
                                                             .GetTypes()
                                                             .Where(t => t.IsClass && !t.IsAbstract &&
                                                                         t.IsAssignableTo(typeof(IButtplugMessage))));

        var messageNames = messageTypes.ToDictionary(t => t, t => t.GetCustomAttribute<ButtplugMessageNameAttribute>()!.Name);
        _messageTypeLookup = messageTypes.ToLookup(t => messageNames[t]);
        _messageNameLookup = messageTypes.ToLookup(t => t, t => messageNames[t]);
    }

    protected string GetMessageName(IButtplugMessage message) => _messageNameLookup[message.GetType()].Single();
    protected Type GetMessageType(string messageName) => _messageTypeLookup[messageName].Single();

    public abstract IEnumerable<IButtplugMessage> Deserialize(string json);
    public abstract string Serialize<T>(T message) where T : IButtplugMessage;
    public abstract string Serialize<T>(IEnumerable<T> message) where T : IButtplugMessage;
}