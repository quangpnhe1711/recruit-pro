# API Changes

## Design Rules

- keep current `/api/...` routing style
- use existing API envelope conventions
- protect all AI routes with JWT + permission checks
- return structured AI metadata: `auditId`, optional `artifactId`, `fallbackUsed`, `provider`, `model`, `warnings`

## v2 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/copilot/candidate-search` | recruiter natural language search |
| `POST` | `/api/copilot/jobs/{jobId}/fit-analysis` | candidate-job fit analysis |
| `POST` | `/api/copilot/jobs/{jobId}/interview-questions` | generate interview question pack |
| `POST` | `/api/copilot/jobs/{jobId}/shortlists` | suggest shortlist |
| `POST` | `/api/copilot/applications/{applicationId}/emails/draft` | draft HR email |
| `GET` | `/api/copilot/prompt-templates` | list templates |
| `POST` | `/api/copilot/prompt-templates` | create template |
| `GET` | `/api/copilot/applications/{applicationId}/fit-analysis/latest` | latest persisted fit-analysis snapshot |
| `GET` | `/api/copilot/artifacts` | current user's generated artifacts |

## v3 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/agents/runs` | start agent run |
| `GET` | `/api/agents/runs/{runId}` | get run summary |
| `GET` | `/api/agents/runs/{runId}/events` | get run timeline |
| `POST` | `/api/agents/runs/{runId}/approve` | approve checkpoint |
| `POST` | `/api/agents/runs/{runId}/reject` | reject checkpoint |
| `GET` | `/api/agents/definitions` | list agents |

## v4 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/workflows` | list workflows |
| `POST` | `/api/workflows` | create workflow |
| `PATCH` | `/api/workflows/{id}` | update workflow draft |
| `POST` | `/api/workflows/{id}/publish` | publish workflow version |
| `GET` | `/api/workflows/{id}/executions` | list executions |
| `GET` | `/api/workflows/executions/{executionId}` | get execution detail |

## v5 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/admin/ai/metrics` | AI ops dashboard |
| `GET` | `/api/admin/ai/prompts` | prompt version list |
| `POST` | `/api/admin/ai/prompts` | create prompt version |
| `GET` | `/api/admin/ai/evaluations` | evaluation results |
| `POST` | `/api/admin/ai/provider-routing/test` | test routing policy |
| `GET` | `/api/manager/talent-intelligence` | talent intelligence |

## Response Pattern

Every AI-heavy response should include:

- business payload
- `auditId`
- `fallbackUsed`
- `providerName`
- `modelName`
- `warnings[]`
- `usage` when available:
  - `promptTokens`
  - `completionTokens`
  - `estimatedCost`
  - `latencyMs`

## Streaming / Realtime

Consider SignalR or SSE for:

- agent run progress
- workflow execution progress
- long-running copilot tasks

Do not require streaming for initial delivery; polling endpoints are sufficient if easier to ship.
