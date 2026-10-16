using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AipChatApi.Tests;

public class ChatEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Chat_ReturnsTheReplyContract()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new { message = "what is an agreement in principle" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("what_is_aip", body.GetProperty("intent").GetString());
        foreach (var field in new[] { "confidence", "reply", "handover", "suggestions", "modelVersion" })
        {
            Assert.True(body.TryGetProperty(field, out _), $"Missing field {field}");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Chat_EmptyMessage_Returns400(string message)
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new { message });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Chat_TooLongMessage_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new { message = new string('a', ChatService.MaxMessageLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Model_ReportsVersionAndIntents()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>("/api/chat/model");

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
        Assert.True(body.GetProperty("intents").GetArrayLength() >= 10);
    }
}
