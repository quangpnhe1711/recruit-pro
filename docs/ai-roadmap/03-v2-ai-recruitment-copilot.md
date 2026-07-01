# v2 - AI Recruitment Copilot

> **Phase 2 is CLOSED.** This document is the source of truth for what actually shipped. Sections 3–14
> below are the original planning notes kept for history; where they disagree with the "Current State
> (final)" section, the current state wins.

## Current State (final — matches shipped code)

The v2 Copilot is refactored so that **candidate ranking is the single source of truth for CV-screening
evaluation**. The end-to-end flow is:

1. HR opens a job → the Copilot candidate pool contains **only applications in `Screening`** (the job
   picker badge and the empty-state message also reflect the Screening count).
2. HR runs AI-assisted ranking once → each ranked candidate already includes a detailed **Vietnamese**
   fit explanation (fit label, confidence, strengths, gaps, evidence, summary).
3. HR selects candidate(s) and clicks **"Chuyển sang Head Review" (Pass CV)** → the selected
   applications move `Screening → ManagerReview` (the Head/Department-Head review stage) via the existing
   application status-transition service. **AI never mutates ATS state** — this is an explicit HR action.

Key implemented behaviors:

- **Screening-only pool:** `CopilotRepository.GetCandidatePoolAsync` filters `Status == Screening`;
  `GetJobOptionsAsync` counts only Screening applications. New applications are created as
  `Applied` (there is no `Pending` status); the CV-screening stage is `Screening`; Head Review is
  `ManagerReview`.
- **Fit explanation lives in ranking:** deterministic Vietnamese fit evaluation is generated at ranking
  time and persisted (in `copilot_ranking_results.explanation_json` and as `candidate_fit_analyses`
  snapshots). Machine values (`StrongFit/PotentialFit/RiskFit/NotRecommended`, `Interview/Consider/
  Hold/Reject`) stay in English; all human-facing prose is Vietnamese.
- **Duplicate-ranking prevention:** `copilot_ranking_sessions.input_hash` (SHA-256 of the effective
  merged rules + screening-pool evidence). An unchanged re-click returns the latest matching session
  with warning `ranking-session:reused` and message "Tiêu chí chưa thay đổi…" — no new AI call, no
  duplicate session.
- **Provider ordering locked:** deterministic ranking is the source of truth for order/scores. The
  provider may only enrich Vietnamese prose for validated candidate ids; unknown ids are ignored
  (`provider-candidate:unknown`) and a differing order is ignored (`provider-order:ignored`) unless
  `AiProviderSettings.AllowProviderReordering` is true (default false).
- **Chatbot scope guard:** unrelated prompts are refused in Vietnamese **before** loading the pool or
  calling AI (`IsRecruitmentCopilotQuery`), with no ranking session / artifact created.
- **Interview questions:** generated once per candidate and cached (artifact reuse, warning
  `interview-questions:cached`); repeated clicks do not call the provider again.
- **Deprecated (no AI, no new artifacts):** natural-language **candidate search** and **AI email draft**
  are removed from the active flow. Their endpoints still exist but return deterministic output with a
  `candidate-search:deprecated` / `email-draft:deprecated` warning. Old historical artifacts remain
  readable.
- **Removed entirely:** the **shortlist** feature (endpoint `POST /api/copilot/jobs/{id}/shortlists`,
  service method, DTOs, and UI) has been deleted.
- **Fit-analysis endpoint** (`.../fit-analysis`, `.../fit-analysis/latest`) is read/derive-only — it
  reads the latest ranking session and never re-ranks or calls the provider.
- **UI:** `AiCopilotScreen` is a table-first screen; ranking is the primary action; per-row tools are
  fit + interview-questions only (candidate-search and email-draft tools removed). Popups
  (`CriteriaBuilderModal`, `ToolResultModal`) are rendered via `createPortal(document.body)` at
  `z-[100]` so they float above the sticky header/nav. The artifact-history browser and prompt-template
  manager panels are **not** present in the UI (their backend endpoints still exist but are unused by
  the screen).

Deployment note (no EF migrations by project policy): the `input_hash` column ships in `init.sql`
(fresh DB) and the idempotent patch `db/patches/20260701-add-copilot-ranking-input-hash.sql` (existing
DB). The v2 artifact tables ship in `init.sql` + `db/patches/20260630-add-copilot-v2-artifacts.sql`.

Tests: `RecruitPro.Tests` covers the Copilot service (unit) and API/RBAC (integration, Testcontainers);
`recruit-pro-internal/e2e/ai-copilot-ranking.e2e.ts` covers the ranking table + Pass CV + deprecated-tool
absence. `dotnet test --filter "FullyQualifiedName~Copilot"` is green.

## 1. Goal

### Business

- reduce recruiter time spent searching, screening, summarizing, and drafting messages
- increase confidence in AI outputs through explainability and structured fit analysis

### Technical

- extend the existing copilot and semantic discovery modules without changing the core ATS workflow
- keep AI calls optional and recoverable with deterministic behavior

## 2. Features

> Shipped scope (see "Current State" above): the active P0 features are the **job-scoped copilot chat**
> (scope-guarded), **explainable screening-only ranking with Vietnamese fit analysis**, the
> **interview-question generator** (cached once), and the **Pass CV → Head Review** action. The
> **shortlist assistant was removed**; **candidate search** and the **AI email assistant** were
> deprecated (no AI). The list below is the original P0 plan.

### P0

- Recruiter copilot chat with job-scoped context
- natural language candidate search over existing talent pool APIs
- explainable candidate ranking and candidate-job fit analysis
- interview question generator
- shortlist assistant
- AI email assistant for HR

### P1

- reusable recruiter prompt presets
- candidate comparison view
- fit analysis snapshots stored per application/job pair

### P2

- multilingual recruiter prompts
- bulk email draft generation
- suggested status next steps

## 3. Architecture

v2 should extend current modules:

- keep `CopilotService` as the main orchestration entry point for recruiter interaction
- keep `SemanticDiscoveryService` as the retrieval and recommendation engine
- add a new application-layer service for fit analysis and communication drafting
- reuse existing provider abstractions and add structured-output methods instead of replacing them

Recommended services:

- `RecruitmentCopilotService`
- `CandidateFitAnalysisService`
- `InterviewQuestionService`
- `HrEmailAssistantService`
- `CopilotPromptTemplateService`

## 4. Backend Changes

### New Services

- `IRecruitmentCopilotService`
- `ICandidateFitAnalysisService`
- `IInterviewQuestionService`
- `IHrEmailAssistantService`
- `IAiStructuredOutputProvider` or extension methods on `IAiCopilotProvider`

### Modified Services

- `CopilotService`
  - add non-ranking chat intents
  - add shortlist generation
  - add explainable ranking enrichment
- `SemanticDiscoveryService`
  - add NL-query translation and reusable search specs
- `ApplicationService`
  - expose fit analysis snapshots in review screens

### Repositories

- extend `ICopilotRepository`
- new repository methods for prompt templates, fit analyses, and email drafts

### Entities

- `CopilotPromptTemplate`
- `CandidateFitAnalysis`
- `CopilotGeneratedArtifact`

### DTOs

- `NaturalLanguageCandidateSearchRequest/Response`
- `CandidateFitAnalysisRequest/Response`
- `InterviewQuestionSetDto`
- `HrEmailDraftRequest/Response`
- `ShortlistSuggestionDto`

### Background Workers

- async explainability enrichment for top-ranked candidates
- optional cached resume text extraction refresh

### Providers

- structured JSON output contract for:
  - natural-language candidate search
  - fit analysis
  - question sets
  - shortlist suggestions
  - email drafts
- deterministic fallback remains the public contract whenever provider config is disabled, provider calls fail, JSON cannot be parsed, or required fields fail validation

### Interfaces

- keep AI provider interface segregated by use case instead of one giant provider API

## 5. Database Changes

### New Tables

- `copilot_prompt_templates`
- `candidate_fit_analyses`
- `copilot_generated_artifacts`

### New Columns

- `copilot_ranking_sessions`
  - `intent_type`
  - `status`
  - `error_message`
- `copilot_ranking_results`
  - `fit_label`
  - `confidence_score`
- `applications`
  - optional `latest_fit_analysis_id`

### Indexes

- `candidate_fit_analyses(job_id, candidate_user_id, created_at desc)`
- `copilot_generated_artifacts(owner_user_id, artifact_type, created_at desc)`

## 6. API Changes

Endpoints as shipped (all under `[Authorize(Roles = "HR,Manager")]`):

| Method | Route | Purpose | Status |
|---|---|---|---|
| `POST` | `/api/copilot/conversations/{conversationId}/rankings` | run ranking (screening-only, Vietnamese fit, idempotent) or chat reply (scope-guarded) | **active — primary** |
| `POST` | `/api/copilot/ranking-sessions/{rankingSessionId}/pass-cv` | HR moves selected Screening candidates to Head Review (`Screening → ManagerReview`) | **active (v2)** |
| `GET` | `/api/copilot/ranking-sessions/{rankingSessionId}` | reload a persisted ranking session | active |
| `GET` | `/api/copilot/jobs/{jobId}/candidates` | screening-only candidate pool | active |
| `POST` | `/api/copilot/jobs/{jobId}/fit-analysis` | fit analysis derived from latest ranking (no re-rank, no provider) | active (derived) |
| `GET` | `/api/copilot/applications/{applicationId}/fit-analysis/latest` | latest persisted fit-analysis snapshot | active |
| `POST` | `/api/copilot/jobs/{jobId}/interview-questions` | interview questions — generated once per candidate then cached | active |
| `POST` | `/api/copilot/candidate-search` | natural-language search | **deprecated** — no AI, no artifact, warning `candidate-search:deprecated` |
| `POST` | `/api/copilot/applications/{applicationId}/emails/draft` | HR email draft | **deprecated** — no AI, no artifact, warning `email-draft:deprecated` |
| `GET` | `/api/copilot/prompt-templates` · `POST` same | list/create prompt templates | backend only (no UI) |
| `GET` | `/api/copilot/artifacts` | generated-artifact history | backend only (no UI) |
| `POST` | `/api/copilot/jobs/{jobId}/shortlists` | shortlist | **REMOVED** |

Plus the existing conversation / saved-rule endpoints (`/api/copilot/conversations`, `/api/copilot/jobs/{jobId}/rules`, …) are unchanged.

Request/response should follow the current API envelope style and include:

- normalized AI intent
- fallback flag
- audit identifiers
- model/provider metadata when available

## 7. Frontend Changes

### HR Side

> Shipped: `recruit-pro-internal/src/pages/hr/AiCopilotScreen.tsx` is a table-first screen. Ranking is
> the primary action; the ranking table shows Vietnamese summary, fit label, confidence, evidence,
> strengths/gaps and provider/fallback metadata, plus per-row checkboxes and a **"Chuyển sang Head
> Review"** button. Per-row AI tools are fit + interview-questions only. The assistant drawer holds the
> scope-guarded chat. The candidate-search panel, shortlist review modal, and email draft editor are
> **not** shipped (search/email deprecated, shortlist removed). Below is the original plan.

- upgrade `AiCopilotScreen`
- add candidate search panel with natural language query and chips for extracted filters
- add ranking explanation drawer
- add shortlist review modal
- add email draft editor and approval panel
- add interview question generation action on candidate review screens

### Manager Side

- fit analysis summary on review queue and candidate detail views
- read-only AI rationale for approvals/escalations

### Candidate Side

- no mandatory UI in v2
- optional "why this job matches me" explanation on recommendation cards

### Admin Side

- prompt template management
- provider status / feature-flag controls

## 8. AI Design

### Prompt Templates

- recruiter search intent extraction
- candidate fit analysis
- interview question generation
- shortlist rationale
- HR email drafting

### Tool Calling

Use backend-owned tools only:

- `search_candidate_pool`
- `get_job_context`
- `get_candidate_profile`
- `get_application_summary`
- `get_previous_ranking_context`

### Structured Output Schema

All new outputs should return typed JSON:

- `SearchIntentSchema`
- `FitAnalysisSchema`
- `InterviewQuestionSchema`
- `ShortlistSchema`
- `EmailDraftSchema`

### Guardrails

- reject unsupported actions like hidden discrimination rules
- mask unavailable fields instead of hallucinating
- force citations/evidence sections from ATS data fields
- never mutate ATS state from a pure analysis endpoint

### Evaluation Criteria

- precision of extracted search filters
- ranking explanation consistency with deterministic scores
- recruiter edit rate on email drafts
- acceptance rate of generated interview questions

### Fallback Behavior

- if AI fails, use deterministic search filters when possible
- ranking still uses current deterministic copilot scoring
- email draft falls back to approved templates
- question generation falls back to job-skill-based question bank

## 9. Background Jobs

- enrich top-ranked candidates with deeper explanation
- refresh resume-text cache when resumes change
- recompute fit analysis when job requirements change
- record AI telemetry asynchronously

Queue design should continue using the existing async enrichment pattern, but interfaces should allow a durable queue later.

## 10. RBAC

New permissions:

- `copilot.search`
- `copilot.fit_analysis`
- `copilot.shortlist.generate`
- `copilot.email.draft`
- `copilot.interview_questions.generate`
- `copilot.prompt_templates.manage`

Approval-sensitive actions:

- email send remains separate from email draft
- shortlist publish remains separate from shortlist generate

## 11. Testing

### Unit Tests

- prompt normalization
- search filter extraction mapping
- fit-analysis aggregation logic
- fallback selection

### Integration Tests

- copilot endpoints with repository and provider mocks
- persistence of analyses/artifacts

### API Tests

- auth and RBAC coverage
- structured response validation

### AI Output Tests

- golden test cases for fit labels, question format, and email schema
- adversarial prompts for prohibited criteria

### Regression Tests

- existing copilot ranking flow
- existing semantic discovery endpoints
- application creation and review flow

## 12. Acceptance Criteria

- recruiter can search candidates using natural language and receive extracted filters plus results
- recruiter can generate explainable ranking/fit analysis for a job
- recruiter can generate interview questions and an email draft from the same context
- all AI outputs have audit identifiers and structured payloads
- core ATS flow continues if provider calls fail

## 13. Risks

- prompt ambiguity causing surprising search filters
- hidden bias in ranking explanations
- latency spikes when resume text is fetched on demand
- recruiter overtrust in summaries
- provider cost growth if full-candidate explanations are generated too often

## 14. Estimated Complexity

`Medium`
