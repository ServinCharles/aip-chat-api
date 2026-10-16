using System.Text.Json;
using System.Text.RegularExpressions;

namespace AipChatApi;

public sealed class IntentDefinition
{
    public string Name { get; set; } = "";
    public List<string> Examples { get; set; } = [];
    public string Response { get; set; } = "";
    public bool Handover { get; set; }
}

public sealed class FallbackDefinition
{
    public string Response { get; set; } = "";
}

/// <summary>The model: versioned training phrases, synonyms and approved answers (Data/intents.json).</summary>
public sealed class IntentModelData
{
    public string Version { get; set; } = "";
    public double ConfidenceThreshold { get; set; } = 0.3;
    public Dictionary<string, string> Synonyms { get; set; } = new();
    public List<IntentDefinition> Intents { get; set; } = [];
    public FallbackDefinition Fallback { get; set; } = new();
}

public sealed record Classification(string Intent, double Confidence);

/// <summary>
/// Intent classifier: TF-IDF over words and word pairs, cosine similarity to each training phrase,
/// the best-matching phrase decides the intent. Below the confidence threshold the answer is "fallback".
/// Deliberately simple and deterministic, so every answer is explainable and testable.
/// </summary>
public sealed partial class IntentClassifier
{
    public const string Fallback = "fallback";

    private static readonly HashSet<string> StopWords = new(
        ("a an the is are was were be been am i me my you your we our it its this that to of in on for and or " +
         "do does did can could will would should what whats how why when which who please there any about with " +
         "have has had get got just so now ok if at by from as up out im id ive")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, double> _idf = new();
    private readonly List<(string Intent, Dictionary<string, double> Vector)> _examples = [];

    public IntentClassifier(IntentModelData data)
    {
        Data = data;

        var counted = data.Intents
            .SelectMany(intent => intent.Examples.Select(example => (intent.Name, Terms: Count(Features(example)))))
            .ToList();

        var documentFrequency = new Dictionary<string, int>();
        foreach (var (_, terms) in counted)
        {
            foreach (var term in terms.Keys)
            {
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
            }
        }

        var total = counted.Count;
        foreach (var (term, df) in documentFrequency)
        {
            _idf[term] = Math.Log((total + 1.0) / (df + 1.0)) + 1.0;
        }

        foreach (var (name, terms) in counted)
        {
            _examples.Add((name, Vectorise(terms)));
        }
    }

    public IntentModelData Data { get; }

    /// <summary>Loads the model embedded in this assembly.</summary>
    public static IntentClassifier LoadEmbedded()
    {
        using var stream = typeof(IntentClassifier).Assembly.GetManifestResourceStream("AipChatApi.intents.json")
            ?? throw new InvalidOperationException("Embedded model AipChatApi.intents.json not found");
        var data = JsonSerializer.Deserialize<IntentModelData>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Model file is empty");
        return new IntentClassifier(data);
    }

    public Classification Classify(string text)
    {
        var query = Vectorise(Count(Features(text)));

        var bestPerIntent = new Dictionary<string, double>();
        var order = new List<string>();
        foreach (var (intent, vector) in _examples)
        {
            double score = 0;
            foreach (var (term, weight) in query)
            {
                if (vector.TryGetValue(term, out var other))
                {
                    score += weight * other;
                }
            }

            if (!bestPerIntent.TryGetValue(intent, out var current))
            {
                bestPerIntent[intent] = score;
                order.Add(intent);
            }
            else if (score > current)
            {
                bestPerIntent[intent] = score;
            }
        }

        var bestIntent = Fallback;
        var bestScore = 0.0;
        var first = true;
        foreach (var intent in order)
        {
            if (first || bestPerIntent[intent] > bestScore)
            {
                bestIntent = intent;
                bestScore = bestPerIntent[intent];
                first = false;
            }
        }

        var confidence = Math.Round(bestScore, 3);
        return bestScore >= Data.ConfidenceThreshold
            ? new Classification(bestIntent, confidence)
            : new Classification(Fallback, confidence);
    }

    /// <summary>Normalised words (synonyms, stop words, light stemming) plus adjacent word pairs.</summary>
    public IReadOnlyList<string> Features(string text)
    {
        var cleaned = NonWord().Replace(text.ToLowerInvariant().Replace('’', '\''), " ").Replace("'", "");

        var words = new List<string>();
        foreach (var raw in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word = Data.Synonyms.GetValueOrDefault(raw, raw);
            if (StopWords.Contains(word))
            {
                continue;
            }

            word = Stem(word);
            words.Add(Data.Synonyms.GetValueOrDefault(word, word));
        }

        var features = new List<string>(words);
        for (var i = 0; i < words.Count - 1; i++)
        {
            features.Add(words[i] + "_" + words[i + 1]);
        }

        return features;
    }

    public static string Stem(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)) return word[..^3] + "y";
        if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal)) return word[..^3];
        if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal)) return word[..^2];
        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)) return word[..^1];
        return word;
    }

    private static Dictionary<string, int> Count(IEnumerable<string> features)
    {
        var counts = new Dictionary<string, int>();
        foreach (var feature in features)
        {
            counts[feature] = counts.GetValueOrDefault(feature) + 1;
        }

        return counts;
    }

    private Dictionary<string, double> Vectorise(Dictionary<string, int> counts)
    {
        var vector = new Dictionary<string, double>();
        foreach (var (term, tf) in counts)
        {
            if (_idf.TryGetValue(term, out var idf))
            {
                vector[term] = tf * idf;
            }
        }

        var norm = Math.Sqrt(vector.Values.Sum(v => v * v));
        if (norm == 0)
        {
            norm = 1;
        }

        foreach (var term in vector.Keys.ToList())
        {
            vector[term] /= norm;
        }

        return vector;
    }

    [GeneratedRegex("[^a-z0-9£%' ]+")]
    private static partial Regex NonWord();
}
