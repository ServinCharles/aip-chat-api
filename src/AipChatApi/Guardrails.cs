using System.Text.RegularExpressions;

namespace AipChatApi;

/// <summary>Detects personal financial data the chatbot must never process.</summary>
public static partial class Guardrails
{
    public static bool ContainsPersonalData(string message) =>
        CardNumber().IsMatch(message)
        || SortCode().IsMatch(message)
        || AccountNumber().IsMatch(message)
        || NationalInsuranceNumber().IsMatch(message);

    // 13 to 19 digits, optionally grouped with spaces or dashes
    [GeneratedRegex(@"\b(?:\d[ -]?){12,18}\d\b")]
    private static partial Regex CardNumber();

    // UK sort code, e.g. 12-34-56
    [GeneratedRegex(@"\b\d{2}-\d{2}-\d{2}\b")]
    private static partial Regex SortCode();

    // UK bank account number: exactly 8 digits
    [GeneratedRegex(@"\b\d{8}\b")]
    private static partial Regex AccountNumber();

    // National Insurance number, e.g. QQ 12 34 56 C
    [GeneratedRegex(@"\b[A-CEGHJ-PR-TW-Z]{2}\s?\d{2}\s?\d{2}\s?\d{2}\s?[A-D]\b", RegexOptions.IgnoreCase)]
    private static partial Regex NationalInsuranceNumber();
}
