# 07. AI Current State

## Implemented AI Capabilities

### Resume Parsing

- Resume text is extracted first
- AI parser converts resume content to structured candidate data
- Heuristic fallback exists if AI parsing fails

### Embedding

- Candidate embedding text is built from structured profile data
- Job embedding text is built from job data
- Embedding is used for semantic comparison

### Semantic Scoring

- Application semantic scoring runs asynchronously
- Rule score is available first
- Semantic score updates later

### Recommendation

- Candidate dashboard can receive recommended jobs
- Job and candidate similarity are derived from normalized system data

### Copilot Ranking

- HR can rank candidates for a job using prompt-based screening
- Candidate pool is prepared by backend
- Results are persisted in copilot ranking records

## AI Providers

Confirmed from source:

- `AiProviderSettings`
- `AiResumeParserProvider`
- `AiEmbeddingProvider`
- `AiCopilotProvider`
- `AiCompatibleApiHelper`

## AI Data Sources

- Candidate profile
- Candidate skills
- Candidate projects
- Candidate profile sections
- Job title, description, requirements, skills, location, work mode, experience
- Application data for scoring context

## AI Fallback Principles

- AI failure should not break core recruitment flow
- Resume parsing can fall back to heuristic parsing
- Semantic scoring can fail without blocking application creation

## AI Extension Points

Confirmed current extension points include:

- Resume parsing
- Candidate matching
- Semantic scoring
- Job recommendation
- Copilot ranking

## Unknowns

- Exact model name and provider endpoint are configuration-driven and not hardcoded in the analyzed source
- Exact vector storage strategy is runtime/json-based rather than a specialized vector engine in the current source

