# v3 - Multi-Agent Recruitment System

## 1. Goal

### Business

- let users delegate multi-step recruitment tasks instead of issuing one prompt at a time
- improve responsiveness across recruiter, candidate, interview, and analytics workflows

### Technical

- add an agent runtime that orchestrates existing RecruitPro services and APIs
- preserve human approval for outbound or state-changing actions

## 2. Features

### P0

- Recruiter Agent
- Search Agent
- Email Agent
- Interview Agent
- approval checkpoints
- shared audit log and run timeline

### P1

- Candidate Agent
- Analytics Agent
- agent memory snapshots
- reusable playbooks

### P2

- multi-agent negotiation and delegation policies
- proactive agent suggestions via SignalR

## 3. Architecture

Recommended architecture: LangGraph-style stateful orchestration, but implemented behind RecruitPro abstractions so the runtime can be swapped later.

Core concepts:

- `AgentDefinition`
- `AgentRun`
- `AgentStep`
- `AgentToolBinding`
- `AgentMemorySnapshot`
- `ApprovalCheckpoint`

Use the application layer to own:

- graph state
- tool invocation policies
- approval gates
- final action dispatch

Infrastructure should only implement:

- provider/model calls
- persistent storage
- optional background execution

## 4. Agent Design

### Recruiter Agent

Responsibility:

- handle recruiter tasks spanning search, shortlist, outreach, and follow-up

Tools:

- candidate search
- fit analysis
- shortlist generation
- email drafting
- get application status

Memory:

- active job context
- recent shortlist decisions
- saved recruiter preferences

Approval points:

- sending emails
- bulk shortlist updates

### Candidate Agent

Responsibility:

- answer candidate-side questions and help interpret job fit, applications, interview schedule, and offer steps

Tools:

- recommended jobs
- candidate profile summary
- application status lookup
- interview schedule lookup

Memory:

- candidate profile preferences
- recent viewed jobs

Approval points:

- none for read-only features

### Interview Agent

Responsibility:

- prepare interview kits, summarize completed interviews, recommend next steps

Tools:

- job context
- candidate fit analysis
- interview feedback templates

Memory:

- last interview packet
- role-specific question packs

Approval points:

- publishing final interview summary to workflow

### Search Agent

Responsibility:

- turn ambiguous recruiter intent into candidate retrieval strategies

Tools:

- semantic discovery
- deterministic filters
- similar candidate lookup

Memory:

- prior successful search strategies per recruiter or job family

Approval points:

- none for search

### Email Agent

Responsibility:

- draft outreach, follow-ups, reminders, and offer nudges

Tools:

- templates
- candidate/contact context
- workflow history

Memory:

- approved messaging tone and previous drafts

Approval points:

- any send action

### Analytics Agent

Responsibility:

- answer funnel and hiring performance questions with structured evidence

Tools:

- dashboard aggregates
- manager analytics
- workflow logs

Memory:

- saved metrics questions

Approval points:

- none for read-only analysis

## 5. Backend Changes

### New Services

- `IAgentOrchestratorService`
- `IAgentToolRegistry`
- `IAgentMemoryService`
- `IAgentApprovalService`
- `IAgentAuditService`

### Modified Services

- `CopilotService` becomes a tool/source for Recruiter Agent
- `NotificationService` supports agent-run progress updates
- analytics services expose queryable summaries for Analytics Agent

### Entities

- `AgentDefinition`
- `AgentRun`
- `AgentStepExecution`
- `AgentMemorySnapshot`
- `AgentApprovalRequest`
- `AgentToolInvocationLog`

### Repositories

- agent run repository
- agent memory repository
- approval repository

### Background Workers

- async agent execution worker
- retry worker for provider/tool failure

## 6. Database Changes

### New Tables

- `agent_definitions`
- `agent_runs`
- `agent_step_executions`
- `agent_memory_snapshots`
- `agent_approval_requests`
- `agent_tool_invocation_logs`

### Important Columns

- run status, parent run id, initiating user id
- graph state JSON
- failure category
- provider/model info
- human decision metadata

### Indexes

- `agent_runs(initiating_user_id, created_at desc)`
- `agent_runs(status, created_at)`
- `agent_step_executions(agent_run_id, step_order)`

## 7. API Changes

| Method | Route | Purpose | Authorization |
|---|---|---|---|
| `POST` | `/api/agents/runs` | start an agent task | authenticated by role |
| `GET` | `/api/agents/runs/{runId}` | inspect run status/timeline | owner, manager, admin |
| `POST` | `/api/agents/runs/{runId}/approve` | approve checkpoint | approver role |
| `POST` | `/api/agents/runs/{runId}/reject` | reject checkpoint | approver role |
| `GET` | `/api/agents/definitions` | list available agents | authenticated |
| `GET` | `/api/agents/runs/{runId}/events` | step-by-step event stream | owner, approver |

## 8. Frontend Changes

### HR Side

- agent task composer
- run timeline panel
- approval inbox
- reusable playbook launcher

### Candidate Side

- candidate assistant chat
- job-fit Q&A using only candidate-visible context

### Manager Side

- approval queue for sensitive agent actions
- analytics question panel

### Admin Side

- agent definitions, policies, and feature-flag management

## 9. AI Design

### Agent Orchestration

- graph state per run
- planner node
- tool execution nodes
- approval node
- summarizer node

### Failure Handling

- provider timeout -> retry limited times -> fallback summary
- tool auth failure -> stop run and surface approval/error
- invalid structured output -> schema repair attempt -> deterministic failure result

### Audit Logging

Store:

- user prompt
- selected agent
- tools invoked
- step outputs
- approval decisions
- final outcome

## 10. RBAC

New permissions:

- `agents.run.recruiter`
- `agents.run.candidate`
- `agents.run.analytics`
- `agents.approve.email_send`
- `agents.approve.status_change`
- `agents.definitions.manage`

## 11. Testing

- unit tests for graph transitions and approval gates
- integration tests for agent-tool orchestration
- AI output tests for planner and summarizer schemas
- regression tests ensuring agent failure does not mutate ATS state accidentally

## 12. Acceptance Criteria

- at least three agent types can complete multi-step tasks using existing RecruitPro tools
- approval is required before any email send or workflow mutation
- each run is replayable through audit logs
- failed runs surface actionable reason codes

## 13. Risks

- orchestration complexity and debugging overhead
- user confusion about autonomous vs assisted behavior
- approval bottlenecks
- prompt drift across planner and specialist prompts

## 14. Estimated Complexity

`High`
