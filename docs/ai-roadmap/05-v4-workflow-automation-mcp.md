# v4 - Workflow Automation + MCP

## 1. Goal

### Business

- automate repeatable recruitment actions triggered by ATS events
- let HR teams configure lightweight recruitment automations without custom code

### Technical

- introduce a workflow engine aligned to Clean Architecture
- expose RecruitPro capabilities through an MCP server with strict permission checks

## 2. Features

### P0

- trigger-condition-action workflow engine
- workflow execution log
- workflow templates for top recruitment scenarios
- MCP server with read and approved-action tools

### P1

- workflow builder UI
- delayed actions and retries
- workflow versioning

### P2

- advanced branching and reusable subflows
- cross-system connector expansion

## 3. Workflow Model

### Trigger Model

Supported initial triggers:

- candidate applied
- interview completed
- offer sent
- offer not responded after N days
- candidate profile updated
- job approved

Trigger source should come from application services and domain events, not direct table polling where avoidable.

### Condition Model

Examples:

- semantic score >= threshold
- recommendation in allowed set
- job department matches
- days since offer > X
- interview feedback present

### Action Model

Examples:

- run fit analysis
- notify HR via SignalR/notification
- draft follow-up email
- refresh embedding
- create shortlist suggestion
- create manager review task

## 4. Architecture

Recommended components:

- `WorkflowDefinitionService`
- `WorkflowExecutionService`
- `WorkflowTriggerDispatcher`
- `WorkflowActionRegistry`
- `RecruitProEventPublisher`
- `McpServerHostService`

Execution style:

- synchronous validation at trigger time
- async action execution for AI-heavy work
- idempotent execution keys to prevent duplicate runs

## 5. Backend Changes

### New Services

- `IWorkflowDefinitionService`
- `IWorkflowExecutionService`
- `IWorkflowActionHandler`
- `IRecruitProEventBus`
- `IMcpToolService`

### Modified Services

- application, interview, offer, candidate, and job services publish internal events
- notification services consume workflow-generated tasks

### Entities

- `WorkflowDefinition`
- `WorkflowTrigger`
- `WorkflowCondition`
- `WorkflowAction`
- `WorkflowExecution`
- `WorkflowExecutionStep`
- `PublishedDomainEvent`

### Background Workers

- workflow dispatcher
- delayed action scheduler
- dead-letter/retry processor

## 6. Database Changes

### New Tables

- `workflow_definitions`
- `workflow_definition_versions`
- `workflow_executions`
- `workflow_execution_steps`
- `published_domain_events`
- `workflow_action_dead_letters`

### Indexes

- `workflow_executions(workflow_definition_id, created_at desc)`
- `published_domain_events(event_type, occurred_at)`
- `workflow_executions(status, next_retry_at)`

## 7. API Changes

| Method | Route | Purpose | Authorization |
|---|---|---|---|
| `GET` | `/api/workflows` | list workflows | HR Admin, Admin |
| `POST` | `/api/workflows` | create workflow | HR Admin, Admin |
| `PATCH` | `/api/workflows/{id}` | update workflow | HR Admin, Admin |
| `POST` | `/api/workflows/{id}/publish` | publish version | HR Admin, Admin |
| `GET` | `/api/workflows/{id}/executions` | execution history | HR Admin, Admin |
| `GET` | `/api/workflows/executions/{executionId}` | execution detail | HR Admin, Admin |

## 8. Frontend Changes

### HR/Admin Side

- workflow list
- workflow editor with trigger, condition, action builder
- execution history and failure inspector
- MCP tool catalog and access policy screen

### Manager Side

- read-only workflow outcomes affecting approvals

### Candidate Side

- none required directly

## 9. MCP Server Design

### Server Scope

Start with an internal RecruitPro MCP server exposing safe tools over application services, not direct repository access.

### MCP Tools List

- `jobs.search`
- `jobs.get`
- `candidates.search`
- `candidates.get_profile`
- `applications.get`
- `applications.get_fit_analysis`
- `interviews.get_schedule`
- `offers.get_status`
- `notifications.create`
- `emails.create_draft`
- `copilot.generate_shortlist`
- `analytics.get_funnel_summary`

### Security And Permission Model

- every MCP tool request resolves user/service identity
- RBAC checked inside application service boundaries
- tool-level scopes mapped to RecruitPro permissions
- sensitive tools require explicit approval token or service account policy
- tool input/output audit persisted

## 10. AI Design

- workflows may call AI actions but should receive structured outputs only
- conditions should branch on structured values, not free-form text
- workflow steps should record fallback path taken

## 11. Background Jobs

- event ingestion
- workflow execution
- delayed follow-up scheduling
- embedding refresh workflow
- dead-letter retry with capped attempts

## 12. RBAC

New permissions:

- `workflows.view`
- `workflows.manage`
- `workflows.publish`
- `workflows.executions.view`
- `mcp.tools.use.internal`
- `mcp.tools.manage`

## 13. Testing

- unit tests for trigger/condition/action evaluation
- integration tests for event-to-workflow execution
- API tests for workflow CRUD and execution views
- AI output tests for structured action payloads
- idempotency and retry regression tests

## 14. Acceptance Criteria

- admin can configure at least 4 demo workflows
- workflows execute from actual ATS events and log every step
- MCP server exposes a first set of RecruitPro tools with RBAC enforcement
- failed AI steps do not break primary ATS transactions

## 15. Risks

- duplicated event delivery
- uncontrolled automation spamming recruiters/candidates
- overly broad MCP access
- workflow-debugging complexity

## 16. Estimated Complexity

`High`
