# AI Prompts And Tools

## Prompt Design Principles

- backend-prepared context only
- structured output first
- explicit evidence sections
- bounded task scope
- prompt version ids for all production features

## Core Prompt Families

## v2

- recruiter search intent extraction
- candidate-job fit analysis
- explainable ranking summary
- shortlist recommendation
- interview question generation
- HR email drafting

## v3

- planner prompt
- agent specialist prompts
- approval summary prompt
- run final-summary prompt

## v4

- workflow action summarization
- workflow branch explanation
- MCP tool response normalization

## v5

- evaluation judge prompts
- risk-flag prompts
- market intelligence narrative prompts

## Example Structured Schemas

- `SearchIntentSchema`
- `FitAnalysisSchema`
- `ShortlistSchema`
- `InterviewQuestionSchema`
- `EmailDraftSchema`
- `AgentPlanSchema`
- `WorkflowActionOutputSchema`
- `AiRiskFlagSchema`

## Tool Catalog Direction

All AI tools should map to application-service capabilities:

- search talent pool
- get job context
- get candidate profile
- get application summary
- get interview schedule
- get offer state
- draft email
- create shortlist suggestion
- notify user
- get analytics summary

## Guardrails

- block protected or discriminatory criteria
- force unknown when evidence is missing
- cap output lengths for chat and email artifacts
- store prompt and tool metadata for audit

## Fallback Strategy

- AI parser failure -> deterministic query/filter rules
- explanation failure -> computed score breakdown only
- draft failure -> static template
- agent plan failure -> single-step copilot response
