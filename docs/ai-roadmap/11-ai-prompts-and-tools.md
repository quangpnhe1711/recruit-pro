# AI Prompts And Tools

## Prompt Design Principles

- backend-prepared context only
- structured output first
- explicit evidence sections
- bounded task scope
- prompt version ids for all production features

## Core Prompt Families

## v2

> **Final scope:** the only AI prompts actually used in shipped v2 are the **explainable ranking summary**
> (Vietnamese, one structured provider call per ranking run, deterministic fallback) and **interview
> question generation** (cached after first use). Search-intent, shortlist-recommendation and HR
> email-drafting prompts are **not** used: search/email are deprecated (deterministic only) and shortlist
> was removed. All human-facing prose is Vietnamese; JSON keys and enum/code values stay unchanged.

- recruiter search intent extraction *(unused — deprecated)*
- candidate-job fit analysis *(folded into the ranking summary at ranking time)*
- explainable ranking summary
- shortlist recommendation *(removed)*
- interview question generation
- HR email drafting *(unused — deprecated)*

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

## v2 Structured JSON Contracts

Provider-backed v2 generation must return only JSON. `CopilotService` parses, normalizes, and validates these shapes before replacing deterministic fallback output. If provider config is disabled, the provider fails, JSON parsing fails, or required fields are missing, the deterministic output is returned and persisted with fallback metadata.

### `candidate_search`

```json
{
  "normalizedIntent": "candidate_search",
  "query": "string",
  "extractedFilters": {
    "requiredSkills": [],
    "preferredSkills": [],
    "minExperienceYears": null,
    "autoRejectRules": [],
    "minTotalScore": null,
    "priorityCriteria": [],
    "negativeCriteria": []
  },
  "results": [
    {
      "candidateUserId": "uuid",
      "applicationId": "uuid",
      "fullName": "string",
      "matchScore": 0,
      "matchedSkills": [],
      "missingSkills": [],
      "evidence": "string"
    }
  ]
}
```

Required validation: at least one result; each result must map to a candidate/application in the current job pool and include `fullName` plus `evidence`.

### `fit_analysis`

```json
{
  "jobId": "uuid",
  "analyses": [
    {
      "candidateUserId": "uuid",
      "applicationId": "uuid",
      "fullName": "string",
      "fitLabel": "StrongFit",
      "confidenceScore": 0,
      "totalScore": 0,
      "strengths": [],
      "gaps": [],
      "evidence": [],
      "summary": "string"
    }
  ]
}
```

Required validation: `jobId`, at least one analysis, current candidate/application ids, `fullName`, `fitLabel`, and `summary`.

### `interview_questions`

```json
{
  "jobId": "uuid",
  "candidateUserId": "uuid or null",
  "focus": "string",
  "questions": [
    {
      "category": "string",
      "question": "string",
      "evidence": "string"
    }
  ]
}
```

Required validation: `jobId`, at least one question, and every question must include `category`, `question`, and `evidence`.

### `shortlist_suggestion`

```json
{
  "jobId": "uuid",
  "suggestions": [
    {
      "candidateUserId": "uuid",
      "applicationId": "uuid",
      "fullName": "string",
      "rankPosition": 1,
      "score": 0,
      "recommendation": "string",
      "rationale": []
    }
  ]
}
```

Required validation: `jobId`, at least one suggestion, current candidate/application ids, `fullName`, and `recommendation`.

### `email_draft`

```json
{
  "applicationId": "uuid",
  "templateType": "string",
  "subject": "string",
  "body": "string",
  "evidence": []
}
```

Required validation: `applicationId`, `templateType`, `subject`, and `body`.

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
- v2 provider disabled/missing API key/exception/timeout/invalid JSON/validation failure -> deterministic v2 response with fallback warning persisted in artifact or fit-analysis metadata
