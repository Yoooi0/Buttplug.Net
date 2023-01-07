﻿using Buttplug.SystemTextJson;
using System.Text.Json;

namespace Buttplug.NewtonsoftJson.Tests;

public class ButtplugSystemTextJsonConverterTests
{
    [Theory]
    [InlineData(@"[{ ""__Invalid__"": {} }]")]
    public void DeserializeThrowsOnInvalidMessage(string messageJson)
    {
        var converter = new ButtplugSystemTextJsonConverter();
        Assert.ThrowsAny<Exception>(() => converter.Deserialize(messageJson).ToList());
    }

    [Theory]
    [InlineData(@"[{ ""Ok"": { ""Id"": 0 } }]", 1)]
    [InlineData(@"[{ ""Ok"": { ""Id"": 0 } }, { ""Ok"": { ""Id"": 1 } }]", 2)]
    public void DeserializeMessageCountEquals(string messageJson, int messageCount)
    {
        var converter = new ButtplugSystemTextJsonConverter();
        var messages = converter.Deserialize(messageJson);

        Assert.Equal(messageCount, messages.Count());
    }
}
