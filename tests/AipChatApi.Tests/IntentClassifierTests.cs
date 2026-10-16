using Xunit;

namespace AipChatApi.Tests;

public class IntentClassifierTests
{
    private static readonly IntentClassifier Classifier = IntentClassifier.LoadEmbedded();

    [Fact]
    public void EmbeddedModel_LoadsWithVersionAndIntents()
    {
        Assert.False(string.IsNullOrWhiteSpace(Classifier.Data.Version));
        Assert.True(Classifier.Data.Intents.Count >= 10);
        Assert.All(Classifier.Data.Intents, intent =>
        {
            Assert.NotEmpty(intent.Examples);
            Assert.False(string.IsNullOrWhiteSpace(intent.Response));
        });
    }

    [Fact]
    public void IntentNames_AreUnique()
    {
        var names = Classifier.Data.Intents.Select(i => i.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void EveryTrainingExample_ClassifiesAsItsOwnIntent()
    {
        foreach (var intent in Classifier.Data.Intents)
        {
            foreach (var example in intent.Examples)
            {
                Assert.Equal(intent.Name, Classifier.Classify(example).Intent);
            }
        }
    }

    [Theory]
    [InlineData("tell me a joke")]
    [InlineData("what's the weather like")]
    [InlineData("")]
    public void OffTopicMessages_FallBack(string message)
    {
        Assert.Equal(IntentClassifier.Fallback, Classifier.Classify(message).Intent);
    }

    [Theory]
    [InlineData("payslips", "payslip")]
    [InlineData("searching", "search")]
    [InlineData("referred", "referr")]
    [InlineData("policies", "policy")]
    [InlineData("address", "address")]
    public void Stem_AppliesLightSuffixRules(string word, string expected)
    {
        Assert.Equal(expected, IntentClassifier.Stem(word));
    }

    [Fact]
    public void Features_ApplySynonymsStopWordsAndWordPairs()
    {
        var features = Classifier.Features("Can I speak to an adviser?");

        Assert.Contains("speak", features);
        Assert.Contains("advisor", features);
        Assert.Contains("speak_advisor", features);
        Assert.DoesNotContain("can", features);
    }

    [Fact]
    public void Classification_IsDeterministic()
    {
        var first = Classifier.Classify("does this affect my credit score");
        var second = Classifier.Classify("does this affect my credit score");

        Assert.Equal(first, second);
    }
}
