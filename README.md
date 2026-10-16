# AIP Chat API

.NET 8 chatbot microservice for the mortgage **Agreement in Principle (AIP)** page. It answers common customer questions with **pre-approved answers**, hands over to a colleague when asked, and blocks personal financial data.

It's an **intent-based** bot (the same approach as contact-centre virtual assistants): a classifier matches each question to one of 12 intents. It never generates free text, so every answer is predictable, approved and testable.

```
aip-webapp (chat bubble) ──/api/chat──▶ aip-chat-api ──▶ intent + approved answer
```

| Repo | Role |
| --- | --- |
| [aip-webapp](https://github.com/ServinCharles/aip-webapp) | React chat widget on the AIP page |
| **aip-chat-api** (this repo) | Chatbot service, model and validation artifacts |
| [aip-deployment-pipeline](https://github.com/ServinCharles/aip-deployment-pipeline) | Deploys it to `<env>-api` on EKS |
| [Agentic-qa-demo](https://github.com/ServinCharles/Agentic-qa-demo) | End-to-end tests |

## API

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/health` | Kubernetes probe |
| POST | `/api/chat` | `{"message": "..."}` → intent, confidence, reply, handover flag, suggestions, model version |
| GET | `/api/chat/model` | Live model version and intents |

```bash
curl -X POST http://localhost:5081/api/chat -H "Content-Type: application/json" \
  -d '{"message": "I earn £45,000, how much can I borrow?"}'
```

```json
{
  "intent": "how_much_can_i_borrow",
  "confidence": 0.791,
  "reply": "Based on an income of £45,000, you could borrow up to about £202,500 (4.5 times income), before existing commitments. This is a guide, not advice: use the form above for your Agreement in Principle.",
  "handover": false,
  "suggestions": ["What is an AIP?", "Does it affect my credit score?", "How much can I borrow?", "Speak to an advisor"],
  "modelVersion": "1.0.0"
}
```

## Intents

`greeting`, `what_is_aip`, `credit_score_impact`, `how_much_can_i_borrow`, `documents_needed`, `aip_validity`, `referred_meaning`, `declined_next_steps`, `deposit_ltv`, `speak_to_advisor` (handover), `complaint` (handover), `thanks_goodbye`. Anything else gets the fallback answer.

## AI model validation artifacts

| Artifact | Where |
| --- | --- |
| Model card (intended use, limits, acceptance criteria, results) | [`MODEL_CARD.md`](MODEL_CARD.md) |
| Versioned training data and approved answers | `src/AipChatApi/Data/intents.json` |
| Held-out evaluation set | [`evals/test-set.json`](evals/test-set.json) |
| Evaluation test with acceptance gates | `tests/AipChatApi.Tests/ModelEvaluationTests.cs` |
| Evaluation report per build (accuracy, per-intent precision/recall/F1, misclassified) | `TestResults/model-evaluation/` (Jenkins artifact, GitHub run summary) |
| Guardrail tests (personal data blocked and never echoed, handover, off-topic) | `tests/AipChatApi.Tests/ChatServiceTests.cs` |

Version 1.0.0 scores **98% accuracy** on the held-out set. The build fails below 90%, if any request for an advisor or a complaint is missed, or if off-topic questions start getting mortgage answers.

## Run locally

Port **5081** locally (8080 is Jenkins, 5080 is aip-api):

```bash
dotnet run --project src/AipChatApi --urls http://localhost:5081
dotnet test tests/AipChatApi.Tests
```

Or in Docker: `docker build -t aip-chat-api . && docker run -p 5081:8080 aip-chat-api`

## Jenkins build pipeline

Same structure as aip-mortgage-api: **Checkout → Build, Test & Model Evaluation → SonarQube → Snyk → Docker Build → Image Scan → Push to ECR → Trigger Deploy**. The model evaluation report is archived on every build.

Create a Pipeline job `aip-chat-api-build` from this repo (`*/main`, script path `Jenkinsfile`). Credentials are the same as the other build jobs.
