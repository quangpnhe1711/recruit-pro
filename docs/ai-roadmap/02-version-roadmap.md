# Version Roadmap

## Summary

Recommended build order:

1. `v2` AI Recruitment Copilot
2. `v3` Multi-Agent Recruitment System
3. `v4` Workflow Automation + MCP
4. `v5` Production AI + Talent Intelligence

This order follows the current codebase maturity:

- `v2` extends existing copilot, semantic discovery, ranking, and chat
- `v3` reuses v2 tools/endpoints as agent tools
- `v4` turns v2/v3 actions into configurable workflows and MCP operations
- `v5` adds observability, governance, analytics, and intelligence after enough real AI traffic exists

## Version Matrix

| Version | Theme | Business Outcome | Main Technical Addition | Complexity |
|---|---|---|---|---|
| v2 | AI Recruitment Copilot | Faster screening and communication | richer copilot services and explainable AI endpoints | Medium |
| v3 | Multi-Agent System | Delegated AI task execution with approvals | agent runtime, memory, orchestration, audit | High |
| v4 | Workflow Automation + MCP | Event-driven automation across recruitment lifecycle | workflow engine, event bus abstraction, MCP server | High |
| v5 | Production AI + Intelligence | Operable, measurable, trustworthy AI at scale | evaluation, prompt registry, telemetry, intelligence marts | High |

## Recommended Milestones

### v2

- deepen recruiter copilot chat
- add NL candidate search and fit analysis
- add interview question and email drafting
- keep deterministic scoring as the default fallback

### v3

- introduce agent runtime behind application services
- add Recruiter, Candidate, Interview, Search, Email, Analytics agents
- require human approval for outbound or state-changing actions

### v4

- introduce workflow designer and execution engine
- move AI enrichments and follow-ups to configurable workflows
- expose RecruitPro tools over an MCP server with RBAC checks

### v5

- instrument every AI run
- add prompt versioning, provider switching, and evaluation dashboards
- add talent intelligence and funnel analytics based on accumulated data

## What Should Be Built First

`v2` should be built first.

Why:

- it delivers immediate recruiter value
- much of the data model already exists
- it proves AI adoption patterns before investing in multi-agent or automation complexity
- its APIs and tools become the foundation for v3 and v4

## MVP Scope Across The Whole Roadmap

The overall MVP should stop at:

- v2 P0
- v3 limited orchestration for recruiter workflows only
- v4 event-driven automations for 3 to 4 high-value triggers
- v5 observability basics: prompt versions, token/cost/latency tracking, provider switching

This creates a strong AI-native story without overbuilding enterprise governance too early.

## What Not To Build Yet

- autonomous offer approval
- autonomous rejection sending without HR approval
- complex graph memory for every agent from day one
- custom vector database migration before current PostgreSQL-based approach is saturated
- low-level microservice decomposition
- a full n8n clone with arbitrary scripting

## 10-Week Execution Plan

### Weeks 1-3

- build `v2` P0 endpoints and schemas
- upgrade AI copilot UI
- add explainable fit analysis, shortlist assistant, interview questions, email draft

### Weeks 4-5

- stabilize `v2`
- add audit and evaluation hooks needed by later versions
- ship tests and usage instrumentation basics

### Weeks 6-7

- build `v3` agent runtime and Recruiter/Search/Email agents first
- add approval checkpoints and audit logs

### Weeks 8-9

- build `v4` workflow engine for selected triggers
- add workflow execution log and MCP server prototype

### Week 10

- add `v5` production AI baseline
- token/cost/latency tracking
- prompt registry
- admin dashboard with failure/risk signals

## Best Portfolio Story

The strongest AI Engineer interview narrative is:

"I evolved a working Clean Architecture ATS into an AI-native recruitment platform in stages: first explainable recruiter copilot, then multi-agent orchestration, then workflow automation and MCP tooling, and finally production AI observability and talent intelligence. I kept the main ATS reliable when AI failed, used deterministic fallbacks, and added auditability, RBAC, and cost control instead of shipping a black-box demo."
