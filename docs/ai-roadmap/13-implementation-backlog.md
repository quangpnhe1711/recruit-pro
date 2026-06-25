# Implementation Backlog

## Recommended Order

1. v2 P0
2. v2 hardening
3. v3 recruiter-focused agent runtime
4. v4 workflow automation for top triggers
5. v5 production AI baseline

## v2 Backlog

### P0

- extend copilot endpoints for NL search, fit analysis, interview questions, shortlist, email draft
- add `candidate_fit_analyses` and prompt-template storage
- upgrade HR copilot UI
- add audit metadata and fallback flags
- add tests for structured outputs and RBAC

### P1

- prompt presets
- candidate comparison view
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
