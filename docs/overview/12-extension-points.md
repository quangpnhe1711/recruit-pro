# 12. AI Extension Points

## Principle

This section identifies places where AI can be integrated naturally without redesigning the core architecture.

## Confirmed Extension Points

### Resume Parsing

Why suitable:

- Resume upload pipeline already exists
- Structured candidate profile is already a target output

### Candidate Matching

Why suitable:

- Application scoring already includes semantic processing
- Candidate profile and job profile are both normalized

### Semantic Search

Why suitable:

- Candidate and job embeddings are already part of the flow
- Search can reuse normalized profile data

### Recommendation

Why suitable:

- Candidate dashboard already exposes recommended jobs
- Job and candidate vectors are already derived from system data

### Recruiter Copilot

Why suitable:

- Copilot ranking and prompt workflow already exist
- Backend already prepares candidate pool data

### Candidate Copilot

Why suitable:

- Candidate profile and recommendation surfaces exist
- Candidate-side assistant can reuse the same structured data

### Interview Assistant

Why suitable:

- Interview entities and scheduling data already exist

### Email Generation

Why suitable:

- Application email workflow already exists
- Offer and interview communication can reuse structured records

### Screening and Ranking

Why suitable:

- Applications already have score fields and lifecycle states
- Rules can be normalized before execution

## Unknown or Not Yet Confirmed

- Document QA
- Knowledge base assistant
- Advanced generative offer drafting

These may be possible conceptually, but they are not directly confirmed by the current source as implemented features.

