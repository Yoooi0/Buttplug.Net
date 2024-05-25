using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Buttplug.SystemTextJson;

public class ButtplugSystemTextJsonConverter : ButtplugMessageJsonConverter
{
    private readonly JsonSerializerOptions? _serializerOptions;
    private readonly JsonNodeOptions? _nodeOptions;
    private readonly JsonDocumentOptions? _documentOptions;

    public ButtplugSystemTextJsonConverter(JsonSerializerOptions? serializerOptions = null, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions? documentOptions = null)
    {
        _serializerOptions = serializerOptions ?? new(JsonSerializerOptions.Default);
        _nodeOptions = nodeOptions;
        _documentOptions = documentOptions;

        if (!_serializerOptions.Converters.OfType<JsonStringEnumConverter>().Any())
            _serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public override string Serialize(IButtplugMessage message) => Serialize([message]);
    public override string Serialize(IEnumerable<IButtplugMessage> messages)
    {
        var array = new JsonArray(_nodeOptions);
        foreach (var messageObject in messages.Select(ToJsonObject))
            array.Add(messageObject);

        return JsonSerializer.Serialize(array, _serializerOptions);

        JsonObject ToJsonObject(IButtplugMessage message)
            => new() { [GetMessageName(message)] = JsonSerializer.SerializeToNode(message, message.GetType(), _serializerOptions)! };
    }

    public override IEnumerable<IButtplugMessage> Deserialize(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var array = JsonNode.Parse(stream, _nodeOptions, _documentOptions ?? default)?.AsArray();
        if (array == null)
            yield break;

        foreach(var o in array.Select(o => o?.AsObject()).Where(o => o != null))
        {
            var (messageName, messageNode) = o!.FirstOrDefault();

            var messageObject = messageNode?.AsObject();
            if (messageNode == null)
                continue;

            var messageType = GetMessageType(messageName);
            if (messageObject.Deserialize(messageType, _serializerOptions) is not IButtplugMessage message)
                continue;

            yield return message;
        }
    }
}
