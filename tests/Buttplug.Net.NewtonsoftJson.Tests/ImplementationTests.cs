namespace Buttplug.NewtonsoftJson.Tests;

public class ImplementationTests
{
    [Theory]
    [InlineData(@"[{ ""__Invalid__"": {} }]")]
    public void DeserializeThrowsOnInvalidMessage(string messageJson)
    {
        var converter = new ButtplugNewtonsoftJsonConverter();
        Assert.ThrowsAny<Exception>(() => converter.Deserialize(messageJson).ToList());
    }

    [Theory]
    [InlineData(@"[{ ""Ok"": { ""Id"": 0 } }]", 1)]
    [InlineData(@"[{ ""Ok"": { ""Id"": 0 } }, { ""Ok"": { ""Id"": 1 } }]", 2)]
    public void DeserializeMessageCountEquals(string messageJson, int messageCount)
    {
        var converter = new ButtplugNewtonsoftJsonConverter();
        var messages = converter.Deserialize(messageJson);

        Assert.Equal(messageCount, messages.Count());
    }
}
