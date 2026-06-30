# Implementation Backlog

## Recommended Order

1. v2 P0
2. v2 hardening
3. v3 recruiter-focused agent runtime
4. v4 workflow automation for top triggers
5. v5 production AI baseline

## v2 Backlog

### P0

- extend copilot endpoints for NL search, fit analysis, interview questions, shortlist, email draft — **foundation slice implemented with deterministic fallback**
- add `candidate_fit_analyses`, `copilot_generated_artifacts`, and prompt-template storage — **implemented with repository + migration**
- upgrade HR copilot UI — **compact v2 tools panel implemented**
- add audit metadata and fallback flags — **implemented for foundation endpoints; generated artifacts expose `artifactId`**
- expose persisted fit snapshots/artifact history — **latest application fit snapshot + current-user artifacts API implemented**
- artifact history UI — **implemented in `AiCopilotScreen` with job/type filters**
- prompt-template management UI — **minimal list/create/detail implemented in `AiCopilotScreen`**
- add tests for structured outputs and RBAC — **unit + API integration/RBAC coverage expanded for v2 read/template endpoints**
- provider-backed structured JSON generation — **implemented for search, fit analysis, interview questions, shortlist, and email draft with deterministic fallback on disabled provider, errors, invalid JSON, or validation failure**
- frontend E2E coverage for AI Copilot artifact/template/provider metadata workflows — **implemented with deterministic Playwright route mocks**
- frontend performance hardening — **route-level lazy loading removes Vite chunk-size warning**
- critical package warning triage — **AutoMapper upgraded to 15.1.3; NU1903 resolved**

### P1

- prompt presets
- candidate comparison view
- richer artifact drill-down/editing after backend API expansion
- prompt-template edit/delete/versioning after backend API expansion
- cached explanation refresh job

## v3 Backlog

### P0

- add agent runtime tables and services
- implement Recruiter, Search, Email, Interview agents
- approval request flow
- run timeline UI

### P1

- Candidate Agent
- Analytics Agent
- memory snapshots

## v4 Backlog

### P0

- publish domain events from core services
- workflow definition and execution storage
- three workflow templates:
  - apply -> score -> notify HR
  - interview completed -> summarize -> recommend next step
  - candidate profile updated -> refresh embedding
- MCP server prototype

### P1

- workflow builder UI
- delayed actions
- dead-letter inspector

## v5 Backlog

### P0

- prompt registry
- AI telemetry capture
- admin AI metrics dashboard
- provider switching policy

### P1

- evaluation suite
- talent intelligence dashboard
- risk alerts

## Done Criteria For The Roadmap

- each version ships at least one demoable end-to-end flow
- AI failure never blocks the main ATS transaction
- new permissions are mapped and tested
- audit and telemetry coverage increases each version
