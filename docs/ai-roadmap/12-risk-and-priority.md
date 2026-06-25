# Risk And Priority

## Priority Recommendation

### Highest Product Value

1. v2 recruiter copilot depth
2. v3 recruiter/search/email agents
3. v4 workflow automation for top triggers
4. v5 production AI observability

### Lower Priority For Now

- candidate-side autonomous behaviors
- advanced market intelligence narratives
- broad MCP connector ecosystem

## Risks That Can Break Production

### Technical

- AI calls inside synchronous request paths causing timeouts
- in-memory queue saturation or process restarts losing non-durable work
- insufficient idempotency in workflows causing duplicate notifications/emails
- agent actions mutating ATS state without strict approval boundaries

### Product

- black-box ranking that recruiters cannot trust
- over-automation leading to spam or bad candidate experience
- agent autonomy exceeding user expectation

### Cost

- per-candidate LLM calls at scale
- missing token telemetry leading to uncontrolled spend

### Data / Compliance

- prompts including excessive PII
- discriminatory or non-compliant criteria leaking into ranking or search
- missing audit trail for AI-assisted decisions

## Mitigations

- deterministic-first design
- approval checkpoints for outbound/state-changing actions
- structured outputs with validation
- async enrichment where possible
- telemetry from day one for new AI features

## Complexity Ranking

| Version | Complexity | Why |
|---|---|---|
| v2 | Medium | extends existing services and UI |
| v3 | High | introduces orchestration, memory, approvals |
| v4 | High | adds event/workflow engine and MCP surface |
| v5 | High | requires production telemetry and intelligence modeling |
