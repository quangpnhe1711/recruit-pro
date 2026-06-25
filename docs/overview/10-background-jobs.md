# 10. Background Jobs

## Confirmed Background Processing

### SemanticScoringBackgroundService

- Runs in the API project
- Consumes application semantic processing queue
- Updates semantic score and related fields asynchronously

### In-Memory Queue

- `InMemoryApplicationSemanticProcessingQueue`
- Used for semantic scoring workflow

## Async Tasks

Observed async work includes:

- resume parsing workflow
- embedding refresh after profile/job changes
- semantic application scoring
- notification sending

## Queue Characteristics

- In-process
- Not a distributed broker in the current source

## Operational Implication

- Good for simple deployment and development
- May be a scaling bottleneck for high throughput workloads

