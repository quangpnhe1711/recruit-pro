# Database Changes

## Strategy

Do not replace the current PostgreSQL model. Extend it in layers that correspond to the roadmap versions.

Use these principles:

- additive schema first
- keep nullable fields for phased rollout
- capture audit and telemetry separately from transactional ATS records
- prefer JSONB for AI intermediate payloads, but keep key reporting dimensions as typed columns

## v2

### New Tables

- `copilot_prompt_templates`
- `candidate_fit_analyses`
- `copilot_generated_artifacts`

Implementation status: these tables are covered by migration `20260630000000_AddCopilotV2Artifacts`.

### Modified Tables

- `copilot_ranking_sessions`
  - `intent_type`
  - `status`
  - `error_message`
  - `provider_name`
- `copilot_ranking_results`
  - `fit_label`
  - `confidence_score`
- `applications`
  - `latest_fit_analysis_id`

## v3

### New Tables

- `agent_definitions`
- `agent_runs`
- `agent_step_executions`
- `agent_memory_snapshots`
- `agent_approval_requests`
- `agent_tool_invocation_logs`

## v4

### New Tables

- `workflow_definitions`
- `workflow_definition_versions`
- `workflow_executions`
- `workflow_execution_steps`
- `published_domain_events`
- `workflow_action_dead_letters`

## v5

### New Tables

- `prompt_template_versions`
- `ai_run_telemetry`
- `ai_usage_daily_rollups`
- `ai_evaluation_cases`
- `ai_evaluation_results`
- `provider_routing_policies`
- `talent_intelligence_snapshots`

## Recommended Shared Columns

For AI, agent, workflow, and telemetry tables, standardize:

- `id uuid`
- `created_at timestamp`
- `updated_at timestamp` where mutable
- `created_by_user_id` or `initiated_by_user_id` where user-originated
- `status varchar(...)`
- `provider_name varchar(...)`
- `model_name varchar(...)`
- `error_code varchar(...)`
- `error_message text`
- `metadata_json jsonb`

## Index Guidance

- favor `(foreign_key, created_at desc)` for timeline views
- add `(status, created_at)` for workers and dashboards
- add unique idempotency keys where duplicate runs are possible
- index event type and retry timestamps for workflow dispatch

## Migration Order

1. v2 copilot additions
2. v3 agent runtime tables
3. v4 workflow/event tables
4. v5 telemetry/evaluation/intelligence tables

## What Not To Migrate Yet

- dedicated vector database
- high-cardinality prompt payload search indexes
- hard foreign keys from every ATS table to AI telemetry tables

Keep AI telemetry loosely coupled so transactional writes stay fast.
