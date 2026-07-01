# API Changes

## Design Rules

- keep current `/api/...` routing style
- use existing API envelope conventions
- protect all AI routes with JWT + permission checks
- return structured AI metadata: `auditId`, optional `artifactId`, `fallbackUsed`, `provider`, `model`, `warnings`

## v2 Endpoints (as shipped)

| Method | Route | Purpose | Status |
|---|---|---|---|
| `POST` | `/api/copilot/conversations/{conversationId}/rankings` | run ranking (screening-only, Vietnamese fit, idempotent via `input_hash`) or scope-guarded chat reply | active — primary |
| `POST` | `/api/copilot/ranking-sessions/{rankingSessionId}/pass-cv` | HR moves selected Screening candidates to Head Review (`Screening → ManagerReview`); AI never mutates state | active (v2) |
| `GET` | `/api/copilot/ranking-sessions/{rankingSessionId}` | reload persisted ranking session | active |
| `GET` | `/api/copilot/jobs/{jobId}/candidates` | screening-only candidate pool | active |
| `POST` | `/api/copilot/jobs/{jobId}/fit-analysis` | fit analysis derived from latest ranking (no re-rank/provider) | active (derived) |
| `GET` | `/api/copilot/applications/{applicationId}/fit-analysis/latest` | latest persisted fit-analysis snapshot | active |
| `POST` | `/api/copilot/jobs/{jobId}/interview-questions` | interview question pack — generated once per candidate then cached | active |
| `POST` | `/api/copilot/candidate-search` | recruiter natural language search | deprecated (no AI/artifact) |
| `POST` | `/api/copilot/applications/{applicationId}/emails/draft` | draft HR email | deprecated (no AI/artifact) |
| `GET` · `POST` | `/api/copilot/prompt-templates` | list/create templates | backend only (no UI) |
| `GET` | `/api/copilot/artifacts` | current user's generated artifacts | backend only (no UI) |
| `POST` | `/api/copilot/jobs/{jobId}/shortlists` | suggest shortlist | REMOVED |

AI metadata note: responses carry `warnings[]` markers such as `ranking-session:reused`,
`provider-order:ignored`, `provider-candidate:unknown`, `interview-questions:cached`,
`candidate-search:deprecated`, `email-draft:deprecated`.

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
