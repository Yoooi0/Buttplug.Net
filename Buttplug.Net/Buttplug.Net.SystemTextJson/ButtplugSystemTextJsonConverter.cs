﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Buttplug.SystemTextJson;

public class ButtplugSystemTextJsonConverter : ButtplugJsonMessageConverter
{
    private readonly JsonSerializerOptions? _serializerOptions;
    private readonly JsonNodeOptions? _nodeOptions;
    private readonly JsonDocumentOptions? _documentOptions;

    public ButtplugSystemTextJsonConverter(JsonSerializerOptions? serializerOptions = null, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions? documentOptions = null)
    {
        _serializerOptions = serializerOptions;
        _nodeOptions = nodeOptions;
        _documentOptions = documentOptions;
    }

    public override string Serialize<T>(T message) => Serialize(new[] { message });
    public override string Serialize<T>(IEnumerable<T> messages)
    {
        var array = new JsonArray(_nodeOptions);
        foreach (var messageObject in messages.Select(m => ButtplugMessageToJObject(m)))
            array.Add(messageObject);

        return JsonSerializer.Serialize(array, _serializerOptions);
    }

    private JsonObject ButtplugMessageToJObject(IButtplugMessage message)
        => new() { [GetMessageName(message)] = JsonSerializer.SerializeToNode(message, message.GetType(), _serializerOptions)! };

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
