# Architecture Rules

## Dependency Direction

API
-> Application
-> Domain

Infrastructure implements interfaces from Application.

Domain must not reference:

- EF Core
- ASP.NET
- DbContext

---

## Repository Rules

Repositories are organized by Aggregate Root.

Allowed:

- JobRepository
- ApplicationRepository
- InterviewRepository
- UserRepository

Not allowed:

- HrRepository
- AdminRepository
- DashboardRepository

---

## Service Rules

Services represent business use cases.

Examples:

- JobService
- CandidateService
- InterviewService

Services may call multiple repositories.

Repositories must not call other repositories.