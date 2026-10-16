using System.Globalization;
using System.Text.RegularExpressions;

namespace AipChatApi;

public sealed record ChatRequest(string? Message);

public sealed record ChatReply(
    string Intent,
    double Confidence,
    string Reply,
    bool Handover,
    IReadOnlyList<string> Suggestions,
    string ModelVersion);

/// <summary>Turns a customer message into an approved reply. Never echoes the message back.</summary>
public sealed partial class ChatService(IntentClassifier classifier, ILogger<ChatService> logger)
{
    public const int MaxMessageLength = 500;
    public const double IncomeMultiple = 4.5;
    public const string PersonalDataIntent = "personal_data_blocked";

    public static readonly IReadOnlyList<string> DefaultSuggestions =
    [
        "What is an AIP?",
        "Does it affect my credit score?",
        "How much can I borrow?",
        "Speak to an advisor"
    ];

    public ChatReply Reply(string message)
    {
        // Guardrail: never process (or log) account, card or National Insurance numbers
        if (Guardrails.ContainsPersonalData(message))
        {
            logger.LogInformation("Chat intent {Intent}", PersonalDataIntent);
            return Build(PersonalDataIntent, 1.0,
                "For your security, please don't share account, card or National Insurance numbers here. I haven't stored them.",
                handover: false);
        }

        var classification = classifier.Classify(message);
        // Only the intent and confidence are logged, never the customer's words
        logger.LogInformation("Chat intent {Intent} confidence {Confidence}", classification.Intent, classification.Confidence);

        if (classification.Intent == IntentClassifier.Fallback)
        {
            return Build(IntentClassifier.Fallback, classification.Confidence, classifier.Data.Fallback.Response, handover: false);
        }

        var intent = classifier.Data.Intents.First(i => i.Name == classification.Intent);
        var reply = intent.Response;

        if (intent.Name == "how_much_can_i_borrow" && TryReadIncome(message, out var income))
        {
            var estimate = Math.Round(income * IncomeMultiple);
            reply = $"Based on an income of £{income.ToString("N0", CultureInfo.InvariantCulture)}, " +
                    $"you could borrow up to about £{estimate.ToString("N0", CultureInfo.InvariantCulture)} " +
                    $"({IncomeMultiple} times income), before existing commitments. " +
                    "This is a guide, not advice: use the form above for your Agreement in Principle.";
        }

        return Build(intent.Name, classification.Confidence, reply, intent.Handover);
    }

    /// <summary>Reads an annual income such as "£45,000", "45000" or "45k" from the message.</summary>
    public static bool TryReadIncome(string message, out double income)
    {
        income = 0;
        // Take the first number that looks like an annual income ("I have 2 kids and earn 45k" -> 45,000)
        foreach (Match match in IncomePattern().Matches(message))
        {
            if (!double.TryParse(match.Groups["amount"].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (match.Groups["k"].Success)
            {
                value *= 1000;
            }

            if (value is >= 5_000 and <= 5_000_000)
            {
                income = value;
                return true;
            }
        }

        return false;
    }

    private ChatReply Build(string intent, double confidence, string reply, bool handover) =>
        new(intent, confidence, reply, handover, DefaultSuggestions, classifier.Data.Version);

    [GeneratedRegex(@"(?<amount>\d{1,3}(?:,\d{3})+|\d+(?:\.\d+)?)\s?(?<k>k)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex IncomePattern();
}
