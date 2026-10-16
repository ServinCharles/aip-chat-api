# Model evaluation

`test-set.json` is the **held-out evaluation set** for the AIP chat intent classifier: customer-style questions, each labelled with the intent a person would expect. None of them appear in the training phrases (`src/AipChatApi/Data/intents.json`), so the score shows how the bot handles questions it hasn't seen.

`fallback` means "the bot should say it can't help", used for off-topic questions.

## Running the evaluation

The evaluation is an ordinary test, so it runs with every build:

```bash
EVAL_REPORT_DIR=TestResults/model-evaluation dotnet test tests/AipChatApi.Tests --filter ModelEvaluationTests
```

It writes:

- `model-evaluation.md`: accuracy, per-intent precision / recall / F1, and every misclassified question
- `model-evaluation.json`: the same data for tracking scores across model versions

The test fails if the model misses its acceptance criteria (see `MODEL_CARD.md`).

## Adding cases

Add realistic questions, especially ones the bot got wrong in testing or that customers actually ask. Keep this set separate from training: if a question here is copied into `intents.json`, the score stops meaning anything.
