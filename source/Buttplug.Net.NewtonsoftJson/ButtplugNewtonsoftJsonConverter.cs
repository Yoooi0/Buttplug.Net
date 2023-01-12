using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Buttplug.NewtonsoftJson;

public class ButtplugNewtonsoftJsonConverter : ButtplugMessageJsonConverter
{
    private readonly JsonSerializerSettings? _settings;

    public ButtplugNewtonsoftJsonConverter(JsonSerializerSettings? settings = null)
    {
        _settings = settings ?? new JsonSerializerSettings();

        if (!_settings.Converters.OfType<StringEnumConverter>().Any())
            _settings.Converters.Add(new StringEnumConverter());
    }

    public override string Serialize(IButtplugMessage message) => Serialize(new[] { message });
    public override string Serialize(IEnumerable<IButtplugMessage> messages)
    {
        var serializer = JsonSerializer.CreateDefault(_settings);
        var array = new JArray(messages.Select(ToJObject).ToArray());
        return JsonConvert.SerializeObject(array, Formatting.None, _settings);

        JObject ToJObject(IButtplugMessage message)
            => new() { [GetMessageName(message)] = JObject.FromObject(message, serializer)! };
    }

    public override IEnumerable<IButtplugMessage> Deserialize(string json)
    {
        using var stream = new StringReader(json);
        using var reader = new JsonTextReader(stream);

        var serializer = JsonSerializer.CreateDefault(_settings);
        var array = JArray.Load(reader);
        foreach (var o in array.Children<JObject>())
        {
            var property = o.Properties().FirstOrDefault();
            if (property == null)
                continue;

            if (property.Value is not JObject messageObject)
                continue;

            var messageType = GetMessageType(property.Name);
            if (messageObject.ToObject(messageType, serializer) is not IButtplugMessage message)
                continue;

            yield return message;
        }
    }
}
