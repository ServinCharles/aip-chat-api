# Model card: AIP chat intent classifier

| | |
| --- | --- |
| **Model** | AIP chat intent classifier |
| **Version** | 1.0.0 (`version` in `src/AipChatApi/Data/intents.json`) |
| **Type** | Deterministic intent classifier: TF-IDF over words and word pairs, nearest training phrase by cosine similarity |
| **Owner** | Servin Charles |
| **Status** | Demo / SIT only. Not a production banking service |

## Intended use

Answer common customer questions on the mortgage **Agreement in Principle (AIP)** page with **pre-approved answers**, and hand the customer over to a colleague when they ask for one or want to complain.

## Out of scope

- Financial advice or product recommendations. Borrowing estimates are labelled as a guide, not advice.
- Account servicing, payments or anything needing the customer's identity.
- Free-text generation: the bot never writes new answers, it only selects approved ones.

## How it decides

1. **Guardrail:** messages containing a card number, sort code, account number or National Insurance number are blocked before classification. They are never logged or echoed back.
2. The message is normalised (synonyms, stop words, light stemming) and compared with every training phrase.
3. The best match above the **confidence threshold (0.3)** decides the intent. Below it, the bot gives the fallback answer.
4. `speak_to_advisor` and `complaint` set `handover: true` so the page offers a colleague.
5. "How much can I borrow" with an income in the message returns an estimate at 4.5x income.

Only the intent and confidence are logged, never the customer's words.

## Data

| Dataset | Location | Size | Purpose |
| --- | --- | --- | --- |
| Training phrases | `src/AipChatApi/Data/intents.json` | 12 intents, ~90 phrases | Defines what each intent sounds like |
| Held-out evaluation set | `evals/test-set.json` | 49 questions, incl. 6 off-topic | Measures real-world accuracy; never used for training |

All data is synthetic. No customer data was used.

## Acceptance criteria (enforced in CI and Jenkins)

`tests/AipChatApi.Tests/ModelEvaluationTests.cs` fails the build unless:

| Metric | Threshold |
| --- | --- |
| Accuracy on the held-out set | ≥ 90% |
| Recall for `speak_to_advisor` and `complaint` | 100%: a customer asking for help must always be handed over |
| Recall for off-topic questions (`fallback`) | ≥ 80%: off-topic questions must not get a mortgage answer |

Every training phrase must also classify as its own intent, and the same input must always give the same answer.

## Results (version 1.0.0)

- Accuracy on the held-out set: **98.0%** (48 of 49)
- Handover intents: 100% recall
- Off-topic questions: 100% fall back

The full per-intent report (precision, recall, F1, misclassified questions) is written on every build to `TestResults/model-evaluation/model-evaluation.md` and `.json`, and archived by Jenkins.

## Known limitations

- **Overlapping topics:** "Is referred the same as being declined?" is classified as `declined_next_steps` rather than `referred_meaning`. Both answers are reasonable, but the referral answer is better.
- **Vocabulary:** words absent from the training phrases and synonyms are ignored. New phrasings need new training phrases.
- **English only.** No spelling correction.
- **Income reading** takes the first number between £5,000 and £5,000,000 and assumes it's annual.

## Changing the model

1. Add or edit phrases, synonyms or answers in `intents.json`, and **bump `version`**.
2. Don't copy questions from `evals/test-set.json` into training: that would hide real performance.
3. Run the tests. The evaluation report shows the effect on every intent.
4. Answers are customer-facing: have them approved before merging.
