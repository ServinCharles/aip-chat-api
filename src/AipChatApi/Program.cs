using AipChatApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ => IntentClassifier.LoadEmbedded());
builder.Services.AddSingleton<ChatService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .WithMethods("GET", "POST", "OPTIONS")));

var app = builder.Build();

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "aip-chat-api" }));

app.MapPost("/api/chat", (ChatRequest request, ChatService chat) =>
{
    var message = request.Message?.Trim() ?? "";
    if (message.Length == 0)
    {
        return Results.BadRequest(new { error = "Message is required" });
    }

    if (message.Length > ChatService.MaxMessageLength)
    {
        return Results.BadRequest(new { error = $"Message must be {ChatService.MaxMessageLength} characters or fewer" });
    }

    return Results.Ok(chat.Reply(message));
});

// Model card summary: which model version is live and what it can answer
app.MapGet("/api/chat/model", (IntentClassifier classifier) => Results.Ok(new
{
    version = classifier.Data.Version,
    confidenceThreshold = classifier.Data.ConfidenceThreshold,
    intents = classifier.Data.Intents.Select(i => new { i.Name, examples = i.Examples.Count, i.Handover })
}));

app.MapFallback(() => Results.NotFound(new { error = "Not Found" }));

app.Run();

// Exposes Program to WebApplicationFactory in the test project
public partial class Program { }
