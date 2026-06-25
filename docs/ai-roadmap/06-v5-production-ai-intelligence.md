# v5 - Production AI + Talent Intelligence

## 1. Goal

### Business

- make RecruitPro production-safe as an AI platform
- give admins and managers visibility into AI quality, cost, latency, and hiring intelligence

### Technical

- add observability, governance, and evaluation around all prior AI features
- build talent intelligence datasets and dashboards from accumulated recruitment activity

## 2. Features

### P0

- AI evaluation dashboard
- prompt versioning
- token usage and cost tracking
- latency monitoring
- hallucination/risk flags
- model/provider switching

### P1

- talent market intelligence
- skill gap analytics
- hiring funnel AI analytics
- alerting for degraded quality/cost

### P2

- automated offline evaluation suites
- provider routing by use case and SLO

## 3. Architecture

Add a production AI control plane:

- prompt registry
- AI run telemetry pipeline
- evaluation service
- provider routing service
- intelligence mart builders

These should remain decoupled from recruiter-facing features so v2-v4 can keep operating if observability components are degraded.

## 4. Backend Changes

### New Services

- `IPromptRegistryService`
- `IAiTelemetryService`
- `IAiEvaluationService`
- `IAiProviderRoutingService`
- `ITalentIntelligenceService`

### Modified Services

- all AI-capable services emit telemetry events
- workflow and agent services report run metrics and risk flags

### Entities

- `PromptTemplateVersion`
- `AiRunTelemetry`
- `AiEvaluationCase`
- `AiEvaluationResult`
- `ProviderRoutingPolicy`
- `TalentIntelligenceSnapshot`

### Background Workers

- telemetry aggregation
- evaluation batch runner
- intelligence snapshot builder
- alerting dispatcher

## 5. Data Model

### New Tables

- `prompt_template_versions`
- `ai_run_telemetry`
- `ai_usage_daily_rollups`
- `ai_evaluation_cases`
- `ai_evaluation_results`
- `provider_routing_policies`
- `talent_intelligence_snapshots`

### Important Metrics

- request count
- token usage
- estimated cost
- p50/p95 latency
- success/fallback rate
- schema validation failure rate
- approval override rate
- recruiter acceptance/edit rate

## 6. Dashboard Design

### Admin AI Operations Dashboard

- request volume by feature
- provider/model split
- token/cost trend
- latency and error trend
- fallback rate
- prompt/version performance
- risk-flag trend

### Manager Talent Intelligence Dashboard

- top demanded skills by open jobs
- candidate supply vs demand gap
- funnel conversion by role/department
- time-to-fill risk predictions
- source quality insights

## 7. Monitoring And Alerting

### Monitoring

- endpoint latency
- provider latency
- AI schema-validation failures
- workflow/agent failure counts
- queue depth
- fallback frequency

### Alerting

- cost spike beyond threshold
- provider failure surge
- prompt version regression
- latency SLO breach
- sudden bias/risk flag spike

## 8. API Changes

| Method | Route | Purpose | Authorization |
|---|---|---|---|
| `GET` | `/api/admin/ai/metrics` | AI ops dashboard data | Admin |
| `GET` | `/api/admin/ai/prompts` | list prompt versions | Admin |
| `POST` | `/api/admin/ai/prompts` | create prompt version | Admin |
| `POST` | `/api/admin/ai/provider-routing/test` | simulate routing | Admin |
| `GET` | `/api/manager/talent-intelligence` | talent intelligence dashboard | Manager, Admin |
| `GET` | `/api/admin/ai/evaluations` | evaluation results | Admin |

## 9. Frontend Changes

### Admin Side

- AI operations dashboard
- prompt registry/version compare screen
- provider routing policy screen
- evaluation report screen

### Manager Side

- talent intelligence dashboard
- skill-gap and funnel AI widgets

### HR Side

- visible confidence/risk banners on AI artifacts

## 10. AI Design

### Prompt Versioning

- each feature references a prompt version id
- staged rollout by percentage or role
- rollback supported without code deploy

### Provider Switching

- per-feature default provider/model
- policy by cost ceiling, latency, and quality target
- fallback chain retained

### Hallucination/Risk Flags

Start with heuristic flags:

- unsupported claim count
- missing evidence section
- prohibited criteria detection
- confidence too high with low evidence

## 11. Production Rollout Strategy

1. instrument existing v2-v4 features first
2. shadow-record telemetry before showing dashboards broadly
3. enable prompt registry for a limited set of features
4. gate provider routing behind admin-only controls
5. roll out intelligence dashboards after data backfill validates quality

## 12. RBAC

New permissions:

- `ai.metrics.view`
- `ai.prompts.manage`
- `ai.evaluations.view`
- `ai.provider_routing.manage`
- `talent_intelligence.view`

## 13. Testing

- unit tests for telemetry aggregation and routing decisions
- integration tests for prompt version resolution
- dashboard contract tests
- offline AI evaluation tests
- regression tests for provider fallback and prompt rollback

## 14. Acceptance Criteria

- admins can inspect AI traffic, cost, latency, and fallback by feature
- prompt versions are deployable and reversible without code changes
- provider/model routing is configurable per feature
- managers can view talent intelligence and skill-gap metrics

## 15. Risks

- inaccurate cost estimation if providers omit usage metadata
- misleading intelligence if source data quality is poor
- excessive admin complexity too early
- dashboard trust erosion if evaluation methodology is weak

## 16. Estimated Complexity

`High`
