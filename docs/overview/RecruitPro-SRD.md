# RecruitPro SRD

## 1. Mục tiêu tài liệu

Tài liệu này mô tả đặc tả hệ thống của RecruitPro ở mức kỹ thuật và triển khai.

Mục đích:

- Mô tả kiến trúc, module và trách nhiệm từng lớp
- Chuẩn hóa luồng xử lý chính của backend và frontend
- Liệt kê API, dữ liệu, RBAC và các luồng AI
- Làm tài liệu nền cho phát triển, review và bảo trì

## 2. Tổng quan hệ thống

RecruitPro gồm 2 phần chính:

- Backend `ASP.NET Core`:
  - `RecruitPro.API`
  - `RecruitPro.Application`
  - `RecruitPro.Infrastructure`
  - `RecruitPro.Domain`
- Frontend `React + TypeScript`:
  - `recruit-pro-fe`

Hệ thống dùng:

- PostgreSQL
- JWT authentication
- EF Core
- MinIO/file storage
- SignalR notification
- AI provider abstraction

## 3. Kiến trúc logic

### 3.1 Layer responsibilities

#### API layer

- Nhận HTTP request
- Xử lý auth, authorization, middleware, filters
- Gọi application services
- Trả response envelope

#### Application layer

- Chứa business logic
- Điều phối các workflow
- Validation, mapping, scoring, orchestration

#### Infrastructure layer

- Implement repository
- File storage
- Email service
- JWT service
- AI providers
- EF Core database access

#### Domain layer

- Entity, enum, workflow rules và business constants

#### Frontend

- Hiển thị public flow, candidate flow và internal flow
- Gọi API
- Render menu, guard, actions theo permission

## 4. Module chức năng

### 4.1 Auth

- Candidate login
- Internal login
- Token refresh
- Logout
- Role-based user session

### 4.2 Candidate module

- Register candidate
- View/update own profile
- Update skills
- Manage experience
- Upload resume
- View dashboard
- View own applications

### 4.3 Job module

- Public job listing
- Public job detail
- Internal create/update/delete/approve job
- Job statistics and funnel
- Job filters

### 4.4 Application module

- Apply job
- View candidate applications
- Withdraw application
- Accept offer
- Internal review, status update, CV view, email send

### 4.5 Interview module

- Schedule interview
- Update interview status
- View interview list
- Schedule data

### 4.6 Notification module

- In-app notification
- Notification count and list
- SignalR realtime push

### 4.7 AI / Semantic module

- Resume parsing
- Embedding generation
- Semantic scoring
- Candidate/job recommendation
- Copilot ranking and chat

### 4.8 Manager analytics

- Recruitment analytics
- Job approval analytics
- Dashboard metrics

## 5. System roles and RBAC

### 5.1 Roles

- `Candidate`
- `HR`
- `Manager`
- `SystemAdmin`

### 5.2 Inheritance

- `Manager` inherits all `HR` permissions
- `SystemAdmin` inherits all internal permissions and has full override

### 5.3 Permission model

The system uses permission-based authorization, not only role-based authorization.

Permission groups:

- `auth`
- `dashboard`
- `job`
- `candidate`
- `application`
- `interview`
- `lookup`
- `system`

Reference:

- [ai-rules/rbac-matrix.md](./ai-rules/rbac-matrix.md)

## 6. Core data model

### 6.1 Main entities

- `User`
- `Role`
- `Permission`
- `UserRole`
- `RolePermission`
- `Job`
- `JobSkill`
- `Skill`
- `Department`
- `Application`
- `Interview`
- `OfferTemplate`
- `ApplicationOffer`
- `ApplicationOfferBenefit`
- `CandidateProfile`
- `CandidateSkill`
- `CandidateProject`
- `CandidateResume`
- `CandidateProfileSection`
- `CandidateProfileSectionItem`
- `Notification`
- `CopilotConversation`
- `CopilotMessage`
- `CopilotRankingSession`
- `CopilotRankingResult`
- `CopilotSavedRule`
- `CopilotCandidateTag`
- `SystemLog`

### 6.2 Candidate profile model

Hệ thống giữ cả legacy model và flexible section model:

- Legacy:
  - skills
  - projects
  - JSON-based records
- New flexible model:
  - profile sections
  - section items

Nguyên tắc:

- CV file là input
- Profile trong DB là source of truth sau cùng
- Profile có thể mở rộng theo section tự do

### 6.3 Application status model

Trạng thái application hiện có:

- `PENDING`
- `REVIEWING`
- `INTERVIEWING`
- `MANAGER_REVIEW`
- `ACCEPTED`
- `REJECTED`

### 6.4 Job status model

- `DRAFT`
- `PENDING_APPROVAL`
- `APPROVED`
- `CLOSED`
- `REJECTED`

### 6.5 Interview status model

- `SCHEDULED`
- `COMPLETED`
- `CANCELLED`

## 7. API surface

### 7.1 Auth

- `POST /api/auth/login`
- `POST /api/auth/candidate/login`
- `POST /api/auth/internal/login`
- `POST /api/auth/refresh-token`
- `GET /api/auth/me`
- `POST /api/auth/logout`

### 7.2 Public job endpoints

- `GET /api/jobs`
- `GET /api/jobs/filters`
- `GET /api/jobs/{jobId}`
- `GET /api/jobs/{jobId}/applications`
- `GET /api/jobs/{jobId}/applications/recent`
- `GET /api/jobs/{jobId}/statistics`
- `PATCH /api/jobs/{jobId}/status`

### 7.3 Candidate endpoints

- `POST /api/candidates/register`
- `GET /api/candidate/profile`
- `PUT /api/candidate/profile`
- `PUT /api/candidate/profile/skills`
- `POST /api/candidate/profile/experience`
- `PUT /api/candidate/profile/experience/{experienceId}`
- `DELETE /api/candidate/profile/experience/{experienceId}`
- `POST /api/candidate/profile/resume`
- `GET /api/candidate/dashboard`
- `GET /api/candidate/applications`
- `POST /api/candidate/applications/{applicationId}/withdraw`
- `POST /api/candidate/applications/{applicationId}/accept-offer`

### 7.4 Internal job endpoints

- `GET /api/hr/jobs`
- `POST /api/hr/jobs`
- `PATCH /api/hr/jobs/{jobId}`
- `PATCH /api/hr/jobs/{jobId}/status`
- `DELETE /api/hr/jobs/{jobId}`

### 7.5 Internal candidate/application endpoints

- `GET /api/hr/candidates`
- `GET /api/hr/applications`
- `GET /api/hr/applications/{applicationId}/cv`
- `POST /api/hr/applications/{applicationId}/send-email`

### 7.6 Interview endpoints

- `GET /api/hr/interviews`
- `GET /api/hr/interviews/schedule-data`
- `POST /api/hr/interviews`
- `PATCH /api/hr/interviews/{interviewId}/status`
- `DELETE /api/hr/interviews/{interviewId}`

### 7.7 Lookup and misc

- `GET /api/departments`
- `GET /api/skills`
- `GET /api/notifications`
- `GET /api/hr/dashboard`

### 7.8 AI/Copilot endpoints

- `GET /api/copilot/jobs`
- `POST /api/copilot/conversations`
- `GET /api/copilot/jobs/{jobId}/candidates`
- `POST /api/copilot/conversations/{conversationId}/rankings`
- `GET /api/copilot/conversations/{conversationId}`
- `GET /api/copilot/ranking-sessions/{rankingSessionId}`
- `POST /api/copilot/jobs/{jobId}/rules`
- `GET /api/copilot/jobs/{jobId}/rules`
- `PATCH /api/copilot/rules/{ruleId}`
- `POST /api/copilot/candidates/{candidateUserId}/tags`

## 8. Frontend routes

### 8.1 Public

- `/home`
- `/login`
- `/register`
- `/internal/login`
- `/jobs`
- `/jobs/:jobId`

### 8.2 Candidate

- `/candidate/dashboard`
- `/candidate/my-applications`
- `/candidate/profile/*`

### 8.3 Internal

- `/hr/dashboard`
- `/hr/jobs/create`
- `/hr/candidates`
- `/hr/applications`
- `/hr/interviews`
- `/hr/interviews/schedule`

## 9. Frontend architecture

### 9.1 Main folders

- `src/pages`
- `src/routes`
- `src/services`
- `src/guards`
- `src/store`
- `src/common`
- `src/modules`
- `src/permissions`

### 9.2 Route guards

- `RequireAuth`
- `PublicOnly`
- `PermissionGuard`
- `RouteGuard`
- `AdaptiveLayout`

### 9.3 Main UI groups

- Public landing and job browsing
- Candidate dashboard and profile management
- HR dashboard and operations screens
- Shared job detail with permission-gated actions

## 10. Key business workflows

### 10.1 Candidate onboarding

1. Candidate register
2. Candidate login
3. Create profile
4. Upload CV
5. Parse CV
6. Save structured profile

### 10.2 Job application

1. Candidate browses jobs
2. Candidate views job detail
3. Candidate applies
4. Application record is created
5. Rule score is calculated
6. Semantic score is processed asynchronously

### 10.3 Internal recruitment

1. HR creates job
2. Job enters approval flow
3. HR receives applications
4. HR reviews candidate profile and CV
5. HR schedules interview
6. HR sends offer or reject

### 10.4 AI matching

1. Candidate profile or job changes
2. Embedding refresh is triggered
3. Similarity scoring runs
4. Recommendation lists update

### 10.5 Copilot ranking

1. HR selects job
2. HR opens copilot conversation
3. Backend builds candidate pool
4. AI parses prompt into structured rules
5. Backend and AI produce ranked candidates
6. Results are persisted

## 11. AI and semantic design

### 11.1 AI providers

Current abstraction uses generic AI provider names:

- `AiProviderSettings`
- `AiResumeParserProvider`
- `AiEmbeddingProvider`
- `AiCopilotProvider`
- `AiCompatibleApiHelper`

### 11.2 Resume parsing flow

1. Extract text from CV
2. Call AI parser
3. Fallback to heuristic parser if needed
4. Save parsed data to profile

### 11.3 Semantic scoring flow

1. Build candidate embedding text from structured profile
2. Build job embedding text from job data
3. Generate embeddings
4. Calculate similarity
5. Update score fields

### 11.4 Copilot ranking flow

1. HR enters prompt
2. Backend prepares candidate pool
3. AI normalizes rules
4. Backend scores and orders candidates
5. AI returns explanation and recommendation

## 12. Storage and persistence

### 12.1 Database

- PostgreSQL
- EF Core migrations
- Legacy schema compatibility retained

### 12.2 File storage

- Resume files stored via `IFileStorageService`
- Storage implementation uses MinIO in infrastructure layer

### 12.3 Notifications

- Notification persistence in DB
- Realtime delivery via SignalR hub

## 13. Integration points

### 13.1 Email

- Candidate communication
- Application review email
- Offer-related email

### 13.2 Realtime

- Notification push
- Potential future live updates for workflow actions

### 13.3 AI provider

- Resume parser
- Embedding provider
- Copilot provider

## 14. Non-functional requirements

### 14.1 Availability

- Business flow must remain usable if AI provider fails
- Apply flow should not be blocked by semantic processing

### 14.2 Performance

- Public browsing and internal lists must stay paginated
- Background scoring should not block HTTP response

### 14.3 Maintainability

- Clear separation between API, application, infrastructure, and domain
- Permission-based authorization should remain centralized

### 14.4 Backward compatibility

- Legacy candidate profile data must remain readable
- New flexible profile sections must coexist with old data until migration is complete

## 15. Current system strengths

- Clear separation of public, candidate, and internal flows
- Candidate profile can be normalized from CV
- AI is additive, not a hard dependency
- RBAC is explicit and already mapped
- Backend-first orchestration makes the system easier to debug

## 16. Known technical gaps

- Some public endpoints still need stricter authorization alignment
- Some UI action visibility should be tightened further
- Semantic ranking is backend-driven and can be optimized further
- Queueing is currently in-process for some workflows

## 17. Deployment context

- Frontend is Vite-based and can be deployed via Vercel
- Backend is designed to run as an ASP.NET Core API
- Environment variables drive API base URL and AI provider settings

## 18. Implementation references

- [current-system-processing-flow.md](./current-system-processing-flow.md)
- [ats-ai-workflow-overview.md](./ats-ai-workflow-overview.md)
- [ai-recruitment-copilot-design.md](./ai-recruitment-copilot-design.md)
- [ai-rules/rbac-matrix.md](./ai-rules/rbac-matrix.md)
- [recruitpro-api-contract.json](../recruit-pro-fe/recruit-pro-fe/docs/api/recruitpro-api-contract.json)

