# 08. Search and Ranking

## Keyword Search

- Public job list supports query and filters
- Candidate-facing job browsing uses search/filter UI
- Internal candidate and application lists also use filters

## Filtering

Observed filter dimensions:

- department
- work mode
- employment type
- job status
- application status
- candidate/job identifiers

## Ranking

### Application ranking

- Rule score exists in workflow
- Semantic score is added asynchronously
- Final score can combine both

### Copilot ranking

- Candidate pool is evaluated for a selected job
- Prompt is normalized into screening rules
- Candidates are ranked and stored per session

## Recommendation

- Candidate dashboard can suggest jobs
- Similarity-based matching uses candidate and job normalized data

## Similarity

- Candidate similarity is based on structured profile and embedding text
- Job similarity is based on job normalized data

## Scoring

Observed or described scoring inputs:

- skill match
- experience
- education
- project relevance
- semantic similarity

## Search Pipeline

1. Search/filter candidate or job list
2. Build normalized text or structured query
3. Run similarity or ranking
4. Sort by score
5. Return paginated results

## Current Algorithms

Confirmed by source:

- cosine similarity is used for semantic scoring
- deterministic ranking exists before AI refinement in copilot flow

## Performance Notes

- For larger pools, background processing is preferred over blocking request-time scoring
- In-memory queue is present for semantic scoring flow

