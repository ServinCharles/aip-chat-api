using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AipChatApi.Tests;

/// <summary>
/// Model validation: scores the classifier on the held-out evaluation set (evals/test-set.json),
/// writes the evaluation report, and fails the build if the model falls below its acceptance criteria.
/// </summary>
public class ModelEvaluationTests(ITestOutputHelper output)
{
    // Acceptance criteria (also documented in MODEL_CARD.md)
    public const double MinimumAccuracy = 0.90;
    public const double MinimumFallbackRecall = 0.80;
    private static readonly string[] MustNeverMiss = ["speak_to_advisor", "complaint"];

    private sealed record TestCase(string Text, string Intent);
    private sealed record TestSet(string Description, List<TestCase> Cases);
    private sealed record IntentScore(string Intent, int Support, int TruePositives, double? Precision, double? Recall, double? F1);
    private sealed record Miss(string Text, string Expected, string Predicted, double Confidence);

    [Fact]
    public void Model_MeetsAcceptanceCriteriaOnHeldOutSet()
    {
        var classifier = IntentClassifier.LoadEmbedded();
        var testSet = JsonSerializer.Deserialize<TestSet>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "evals", "test-set.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var results = testSet.Cases
            .Select(c => (Case: c, Result: classifier.Classify(c.Text)))
            .ToList();

        var accuracy = results.Count(r => r.Result.Intent == r.Case.Intent) / (double)results.Count;

        var labels = results.SelectMany(r => new[] { r.Case.Intent, r.Result.Intent }).Distinct().OrderBy(l => l).ToList();
        var scores = labels.Select(label =>
        {
            var tp = results.Count(r => r.Case.Intent == label && r.Result.Intent == label);
            var fp = results.Count(r => r.Case.Intent != label && r.Result.Intent == label);
            var fn = results.Count(r => r.Case.Intent == label && r.Result.Intent != label);
            double? precision = tp + fp == 0 ? null : tp / (double)(tp + fp);
            double? recall = tp + fn == 0 ? null : tp / (double)(tp + fn);
            double? f1 = precision is > 0 && recall is > 0 ? 2 * precision * recall / (precision + recall) : null;
            return new IntentScore(label, tp + fn, tp, precision, recall, f1);
        }).ToList();

        var misses = results
            .Where(r => r.Result.Intent != r.Case.Intent)
            .Select(r => new Miss(r.Case.Text, r.Case.Intent, r.Result.Intent, r.Result.Confidence))
            .ToList();

        WriteReports(classifier.Data, testSet.Cases.Count, accuracy, scores, misses);

        Assert.True(accuracy >= MinimumAccuracy,
            $"Accuracy {accuracy:P1} is below the {MinimumAccuracy:P0} acceptance threshold");

        foreach (var intent in MustNeverMiss)
        {
            var recall = scores.Single(s => s.Intent == intent).Recall;
            Assert.True(recall == 1.0, $"{intent} must always be recognised (recall {recall:P0})");
        }

        var fallbackRecall = scores.Single(s => s.Intent == IntentClassifier.Fallback).Recall;
        Assert.True(fallbackRecall >= MinimumFallbackRecall,
            $"Off-topic questions must fall back at least {MinimumFallbackRecall:P0} of the time (was {fallbackRecall:P0})");
    }

    private void WriteReports(IntentModelData model, int total, double accuracy, List<IntentScore> scores, List<Miss> misses)
    {
        var directory = Environment.GetEnvironmentVariable("EVAL_REPORT_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "model-evaluation");
        Directory.CreateDirectory(directory);

        static string Pct(double? value) => value is null ? "n/a" : value.Value.ToString("P1", CultureInfo.InvariantCulture);

        var md = new StringBuilder()
            .AppendLine("# AIP chat model evaluation")
            .AppendLine()
            .AppendLine($"- Model version: **{model.Version}**")
            .AppendLine($"- Evaluated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
            .AppendLine($"- Held-out cases: {total}")
            .AppendLine($"- **Accuracy: {Pct(accuracy)}** (acceptance threshold {Pct(MinimumAccuracy)})")
            .AppendLine()
            .AppendLine("| Intent | Support | Precision | Recall | F1 |")
            .AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var s in scores)
        {
            md.AppendLine($"| {s.Intent} | {s.Support} | {Pct(s.Precision)} | {Pct(s.Recall)} | {Pct(s.F1)} |");
        }

        md.AppendLine().AppendLine("## Misclassified").AppendLine();
        if (misses.Count == 0)
        {
            md.AppendLine("None.");
        }
        else
        {
            md.AppendLine("| Question | Expected | Predicted | Confidence |").AppendLine("| --- | --- | --- | --- |");
            foreach (var m in misses)
            {
                md.AppendLine($"| {m.Text} | {m.Expected} | {m.Predicted} | {m.Confidence.ToString("0.000", CultureInfo.InvariantCulture)} |");
            }
        }

        File.WriteAllText(Path.Combine(directory, "model-evaluation.md"), md.ToString());
        File.WriteAllText(Path.Combine(directory, "model-evaluation.json"), JsonSerializer.Serialize(new
        {
            modelVersion = model.Version,
            evaluatedUtc = DateTime.UtcNow,
            cases = total,
            accuracy,
            thresholds = new { accuracy = MinimumAccuracy, fallbackRecall = MinimumFallbackRecall, mustNeverMiss = MustNeverMiss },
            intents = scores,
            misclassified = misses
        }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        output.WriteLine(md.ToString());
    }
}
