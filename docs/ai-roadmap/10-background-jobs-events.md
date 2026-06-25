# Background Jobs And Events

## Current Baseline

Confirmed current async pattern:

- `SemanticScoringBackgroundService`
- in-memory application semantic processing queue
- post-transaction enrichment model

This should remain the default delivery model for new AI features.

## Recommended Event Categories

- `CandidateApplied`
- `CandidateProfileUpdated`
- `ResumeUploaded`
- `JobApproved`
- `JobUpdated`
- `InterviewCompleted`
- `OfferSent`
- `OfferResponseOverdue`
- `AgentRunRequested`
- `WorkflowTriggered`

## v2 Jobs

- top-candidate explanation enrichment
- fit-analysis refresh
- resume text extraction cache refresh
- AI telemetry write-behind

## v3 Jobs

- agent run executor
- agent retry processor
- approval timeout notifier

## v4 Jobs

- event dispatcher
- workflow execution worker
- delayed follow-up scheduler
- dead-letter processor

## v5 Jobs

- telemetry aggregation
- daily usage rollup
- evaluation batch run
- intelligence snapshot builder
- alert dispatcher

## Queue Evolution

### Near Term

- keep in-process queue for low-volume features
- wrap queue behavior behind interfaces

### Later

- switch to durable queue for workflow and agent execution when concurrency or reliability demands it

## Operational Rules

- ATS transaction first, enrichment second
- workers must be idempotent
- store failure reason and retry count
- state-changing jobs must respect permission and approval policy
