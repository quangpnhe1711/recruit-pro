# v2 - AI Recruitment Copilot

## 1. Goal

### Business

- reduce recruiter time spent searching, screening, summarizing, and drafting messages
- increase confidence in AI outputs through explainability and structured fit analysis

### Technical

- extend the existing copilot and semantic discovery modules without changing the core ATS workflow
- keep AI calls optional and recoverable with deterministic behavior

## 2. Features

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
  - fit analysis
  - question sets
  - email drafts

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

| Method | Route | Purpose | Authorization |
|---|---|---|---|
| `POST` | `/api/copilot/candidate-search` | natural language recruiter search | HR, Manager |
| `POST` | `/api/copilot/jobs/{jobId}/fit-analysis` | analyze fit for one or many candidates | HR, Manager |
| `POST` | `/api/copilot/jobs/{jobId}/interview-questions` | generate structured interview questions | HR, Interviewer |
| `POST` | `/api/copilot/jobs/{jobId}/shortlists` | create shortlist suggestions | HR |
| `POST` | `/api/copilot/applications/{applicationId}/emails/draft` | generate recruiter email draft | HR |
| `GET` | `/api/copilot/prompt-templates` | list saved prompt templates | HR, Admin |
| `POST` | `/api/copilot/prompt-templates` | save prompt template | HR, Admin |

Request/response should follow the current API envelope style and include:

- normalized AI intent
- fallback flag
- audit identifiers
- model/provider metadata when available

## 7. Frontend Changes

### HR Side

- upgrade [AiCopilotScreen](/D:/FPT_HocTap/Semester%208/PRN232/recuit-pro/recruit-pro-fe/recruit-pro-fe/src/pages/hr/AiCopilotScreen.tsx)
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
