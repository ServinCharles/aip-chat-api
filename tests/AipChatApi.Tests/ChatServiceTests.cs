using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AipChatApi.Tests;

public class ChatServiceTests
{
    private readonly ChatService _chat = new(IntentClassifier.LoadEmbedded(), NullLogger<ChatService>.Instance);

    [Fact]
    public void BorrowingQuestion_WithIncome_GivesAnEstimate()
    {
        var reply = _chat.Reply("I earn £45,000, how much can I borrow?");

        Assert.Equal("how_much_can_i_borrow", reply.Intent);
        Assert.Contains("£45,000", reply.Reply);
        Assert.Contains("£202,500", reply.Reply);
        Assert.Contains("not advice", reply.Reply);
    }

    [Fact]
    public void BorrowingQuestion_WithoutIncome_AsksForIt()
    {
        var reply = _chat.Reply("how much can I borrow");

        Assert.Equal("how_much_can_i_borrow", reply.Intent);
        Assert.Contains("annual income", reply.Reply);
    }

    [Theory]
    [InlineData("I earn £45,000", 45000)]
    [InlineData("salary 52000 a year", 52000)]
    [InlineData("about 45k", 45000)]
    [InlineData("I have 2 kids and earn 45.5k", 45500)]
    public void TryReadIncome_ReadsCommonFormats(string message, double expected)
    {
        Assert.True(ChatService.TryReadIncome(message, out var income));
        Assert.Equal(expected, income);
    }

    [Theory]
    [InlineData("how much can I borrow")]
    [InlineData("I have 2 children")]
    [InlineData("I earn 900 a week")]
    public void TryReadIncome_IgnoresNonIncomeNumbers(string message)
    {
        Assert.False(ChatService.TryReadIncome(message, out _));
    }

    [Theory]
    [InlineData("I want to make a complaint", "complaint")]
    [InlineData("can I speak to an advisor", "speak_to_advisor")]
    public void HandoverIntents_AreFlaggedForAColleague(string message, string intent)
    {
        var reply = _chat.Reply(message);

        Assert.Equal(intent, reply.Intent);
        Assert.True(reply.Handover);
    }

    [Theory]
    [InlineData("my card is 4111 1111 1111 1111")]
    [InlineData("sort code 12-34-56")]
    [InlineData("account 12345678")]
    [InlineData("my NI number is AB 12 34 56 C")]
    public void PersonalData_IsBlockedAndNeverEchoed(string message)
    {
        var reply = _chat.Reply(message);

        Assert.Equal(ChatService.PersonalDataIntent, reply.Intent);
        Assert.DoesNotContain("1111", reply.Reply);
        Assert.DoesNotContain("12-34-56", reply.Reply);
        Assert.DoesNotContain("12345678", reply.Reply);
        Assert.DoesNotContain("AB 12", reply.Reply);
    }

    [Fact]
    public void OffTopic_GetsTheFallbackAnswer()
    {
        var reply = _chat.Reply("should I buy bitcoin");

        Assert.Equal(IntentClassifier.Fallback, reply.Intent);
        Assert.False(reply.Handover);
        Assert.Contains("Agreement in Principle", reply.Reply);
    }

    [Fact]
    public void EveryReply_CarriesSuggestionsAndModelVersion()
    {
        var reply = _chat.Reply("hello");

        Assert.NotEmpty(reply.Suggestions);
        Assert.False(string.IsNullOrWhiteSpace(reply.ModelVersion));
    }
}
