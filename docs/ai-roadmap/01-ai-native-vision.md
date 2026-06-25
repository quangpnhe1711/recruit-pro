# AI-Native RecruitPro Vision

## Purpose

RecruitPro should evolve from an ATS with AI-assisted features into an AI-native recruitment platform where AI helps users search, decide, automate, evaluate, and monitor recruitment work without replacing the core transactional ATS flow.

The target state is not "LLM everywhere". The target state is:

- AI available at the point of work for HR, candidate, manager, and admin users
- deterministic recruitment workflow remains the source of truth
- AI outputs are explainable, permission-aware, logged, and failure-tolerant
- every AI feature reuses the existing Clean Architecture, semantic discovery, copilot, queue, and provider abstractions

## Current Baseline Confirmed From Source

- ASP.NET Core API with Clean Architecture
- PostgreSQL + EF Core
- React + TypeScript frontend
- MinIO-backed file storage
- SignalR notifications
- JWT + RBAC
- resume parsing provider with fallback
- embedding provider and in-memory embedding cache
- semantic discovery and recommendation services
- async semantic scoring via `SemanticScoringBackgroundService`
- recruiter copilot foundation with conversations, ranking sessions, saved rules, and structured results

## AI-Native Product Principles

1. AI is optional.
Core ATS actions like apply, schedule interview, update status, send offer, and notify users must still work when AI providers are slow, unavailable, or inaccurate.

2. AI must be scoped.
AI should operate on backend-prepared context, not unrestricted database access. That matches the current copilot design and reduces privacy, leakage, and hallucination risk.

3. Deterministic first, generative second.
For ranking, filtering, workflow branching, and approval decisions, backend rules and structured scoring should lead. LLMs should add explanation, extraction, summarization, and orchestration.

4. Human approval stays in the loop.
AI can recommend shortlist, emails, interview plans, next steps, and workflow actions, but sensitive actions should still require explicit approval or policy-controlled auto-run.

5. Auditability is a feature.
Every meaningful AI run should capture prompt version, tool inputs, model/provider, outputs, token/cost data when available, and the resulting business action.

6. Incremental delivery wins.
Each version should extend current modules and produce a demoable business increment instead of a platform rewrite.

## End-State Capability Map

### Assist

- recruiter copilot chat
- candidate assistant
- interview preparation and summaries
- email drafting and follow-up suggestions

### Decide

- explainable ranking
- fit analysis
- skill gap and talent intelligence
- funnel insights and risk flags

### Automate

- trigger-condition-action workflows
- event-driven AI enrichments
- MCP tools for secure platform operations

### Operate

- prompt/version registry
- evaluation dashboard
- model/provider switching
- latency, cost, and quality monitoring

## Architecture Direction

RecruitPro should keep the existing layer boundaries and add an AI orchestration slice:

- `Domain`: AI audit, workflow, agent run, tool registry, evaluation entities
- `Application`: orchestration services, agent/workflow services, tool contracts, evaluation services
- `Infrastructure`: provider adapters, workflow executors, MCP server adapter, persistent queue later if needed
- `API`: HTTP endpoints, SignalR updates, admin dashboards, workflow triggers

The present in-memory queue is acceptable for v2. v3-v5 should introduce abstractions so execution can move to durable workers without breaking application services.

## Success Criteria

RecruitPro becomes AI-native when:

- recruiters can complete materially faster screening and communication tasks
- candidates receive better guidance and transparency
- managers receive auditable AI-assisted recommendations rather than black-box outputs
- admins can observe cost, quality, risk, and provider behavior in production
- the platform still behaves like a reliable ATS if AI is degraded
