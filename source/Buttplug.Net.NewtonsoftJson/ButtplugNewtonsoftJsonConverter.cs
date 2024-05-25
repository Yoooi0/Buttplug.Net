using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;

namespace Buttplug.NewtonsoftJson;

public class ButtplugNewtonsoftJsonConverter : ButtplugMessageJsonConverter
{
    private readonly JsonSerializer _serializer;

    public ButtplugNewtonsoftJsonConverter(JsonSerializerSettings? settings = null)
    {
        settings ??= new JsonSerializerSettings()
        {
            Formatting = Formatting.None
        };

        if (!settings.Converters.OfType<StringEnumConverter>().Any())
            settings.Converters.Add(new StringEnumConverter());

        _serializer = JsonSerializer.CreateDefault(settings);
    }

    public override string Serialize(IButtplugMessage message) => Serialize([message]);
    public override string Serialize(IEnumerable<IButtplugMessage> messages)
    {
        var token = CreateJToken(messages);

        using var stringWriter = new StringWriter(new StringBuilder(256), CultureInfo.InvariantCulture);
        using var jsonTextWriter = new JsonTextWriter(stringWriter)
        {
            Formatting = _serializer.Formatting
        };

        _serializer.Serialize(jsonTextWriter, token, null);
        return stringWriter.ToString();
    }

    internal JToken CreateJToken(IButtplugMessage message) => CreateJToken([message]);
    internal JToken CreateJToken(IEnumerable<IButtplugMessage> messages)
    {
        return new JArray(messages.Select(ToJObject).ToArray());

        JObject ToJObject(IButtplugMessage message)
            => new() { [GetMessageName(message)] = JObject.FromObject(message, _serializer)! };
    }

    public override IEnumerable<IButtplugMessage> Deserialize(string json)
    {
        using var stream = new StringReader(json);
        using var reader = new JsonTextReader(stream);

        var array = JArray.Load(reader);
        foreach (var o in array.Children<JObject>())
        {
            var property = o.Properties().FirstOrDefault();
            if (property == null)
                continue;

            if (property.Value is not JObject messageObject)
                continue;

            var messageType = GetMessageType(property.Name);
            if (messageObject.ToObject(messageType, _serializer) is not IButtplugMessage message)
                continue;

            yield return message;
        }
    }
}
