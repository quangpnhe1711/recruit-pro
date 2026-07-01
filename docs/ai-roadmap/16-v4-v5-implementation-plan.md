# Kế hoạch triển khai v4 (Workflow Automation + MCP) và v5 (Production AI Ops + Talent Intelligence)

> Tài liệu lập kế hoạch, **không phải code**. Viết dựa trên audit code thực tế ngày 2026-07-01.
> Xây **trên nền v2** (AI Recruitment Copilot). Không rewrite, không tách microservice, giữ Clean Architecture,
> giữ nguyên tắc deterministic-first / AI-optional, AI không được phá vỡ transaction ATS chính.

---

## 1. Executive Summary

**v4 thêm gì:** Một *engine tự động hóa* dạng **trigger → condition → action** chạy khi có sự kiện ATS
(ứng viên apply, HR đẩy CV sang Head Review, Head Review quá hạn, phỏng vấn hoàn tất…). HR Admin cấu hình
workflow không cần code; hệ thống chạy action idempotent (thông báo, tạo task review, refresh ranking, gợi ý
bước tiếp theo). Kèm theo **MCP server nội bộ** (giai đoạn sau của v4) expose các tool đọc dữ liệu qua
application service, có RBAC + audit.

**v5 thêm gì:** *Control plane* vận hành AI production — telemetry (số lần chạy, token, cost ước tính, latency
p50/p95, fallback rate, tỉ lệ lỗi JSON/schema), prompt registry + versioning/rollback, provider routing, risk
flags, evaluation, và **Talent Intelligence dashboard** cho Manager. v5 chủ yếu thuộc **SysAdmin/SystemAdmin**;
Manager chỉ đọc Talent Intelligence.

**Vì sao v4 trước phần lớn dashboard v5:** v5 dashboard chỉ có giá trị khi đã có *dữ liệu telemetry*. Do đó thứ
tự đúng là: (1) instrument telemetry cho AI hiện có (v2) **và** cho action AI của v4 → (2) mới dựng dashboard.
Engine v4 vừa tạo thêm nguồn phát sinh AI-run (action AI), vừa là nơi cần telemetry, nên làm v4 trước giúp v5
có dữ liệu thật để đo. Ngoại lệ: **v5.1 (telemetry capture)** có thể chạy song song/rất sớm vì nó độc lập và
không có UI.

**Vì sao v5 thuộc SysAdmin:** governance AI (prompt/version/rollback, provider routing, cost, risk flags) là dữ
liệu *nền tảng/vận hành*, không phải dữ liệu nghiệp vụ tuyển dụng. Điều này nhất quán với thiết kế hiện tại
(Phase 2.2b/2.2c) đã **khóa SystemAdmin khỏi dữ liệu nghiệp vụ** ứng viên. SysAdmin quản trị hệ thống → sở hữu AI
Ops. Talent Intelligence (đọc dữ liệu nghiệp vụ tổng hợp) thuộc **Manager**.

**Phải làm trước tiên (Phase 0):**
1. Kích hoạt lớp kiểm tra **permission** thật (bảng `permissions`/`role_permissions` đã seed nhưng **runtime không
   dùng** — hiện chỉ `[Authorize(Roles=...)]`). v4/v5 sinh ra hàng loạt quyền mịn (`workflows.manage`,
   `ai.metrics.view`…) nên cần cơ chế `[HasPermission]`.
2. Quyết định **role sở hữu cấu hình v4** (đề xuất thêm role `HRAdmin`) và **outbox pattern** cho event.
3. Kỷ luật migration: repo dùng **cả EF migrations lẫn `init.sql` + `db/patches/`** (migration KHÔNG auto-apply
   lúc khởi động). Mọi bảng mới phải có đủ 2 nhánh.

---

## 2. Observed Current State

### 2.1 Confirmed from code

**Kiến trúc & stack**
- Clean Architecture 4 layer: `RecruitPro.Domain` / `.Application` / `.Infrastructure` / `.API`. .NET 8,
  PostgreSQL + EF Core (Npgsql). Frontend: React 19 + Vite + Redux Toolkit + React Router 7 + Axios + Tailwind.
- **Không có MediatR / IDomainEvent / event bus.** Side-effect (notification) được gọi **trực tiếp** qua
  `INotificationEventService` **sau commit**, bọc `try/catch` tại call site — thất bại không rollback nghiệp vụ.
  Call site: `ApplicationService.cs` (219, 409, 461, 509, 738–750, 880), `JobService.cs` (646, 800, 806),
  `InterviewService`, `OfferService`. ⇒ **Đây là điểm chèn tự nhiên để phát domain event cho v4.**
- `IUnitOfWork` (`Begin/Save/Commit/Rollback`, bọc `IDbContextTransaction`), repository **theo từng entity**
  (không có generic repo): `IApplicationRepository`, `IJobRepository`, `INotificationRepository`, `ICopilotRepository`…
- **Background:** đúng **1** `BackgroundService` = `SemanticScoringBackgroundService` (API project) tiêu thụ
  `IApplicationSemanticProcessingQueue` = `InMemoryApplicationSemanticProcessingQueue` (unbounded
  `Channel<Guid>`, singleton, single-reader). **Không** có Hangfire/RabbitMQ/durable queue. ⇒ Mẫu để tái dùng
  cho worker v4/v5, nhưng **in-memory không đủ tin cậy** cho retry/dead-letter/delayed → cần outbox DB.
- **SSE realtime:** `INotificationSseBroker` = `InMemoryNotificationSseBroker` (per-user/per-tab bounded channel,
  drop-oldest), endpoint `GET /api/notifications/stream` (heartbeat 25s), sender `SseNotificationSender`.
  Single-process; DB là source of truth, client REST re-sync khi miss.
- **`RecruitPro.Domain/Workflows/`** đã tồn tại nhưng là **state-machine tĩnh**, KHÔNG phải engine:
  `ApplicationStatusWorkflow.cs` (`AllowedTransitions`, `CanTransition`, `IsClosed`…), `InterviewWorkflow.cs`
  (`EvaluateCompletion`, `HasCompletedInterview`). ⇒ **Tên "Workflow" đã bị chiếm** → engine v4 nên đặt namespace
  riêng (`Automation`) để tránh nhầm lẫn.

**ATS state machine (nguồn trigger cho v4)**
- `ApplicationStatus`: `Applied, Screening, ManagerReview, Interview, Offer, Hired, Rejected, OfferDeclined,
  Withdrawn`. **`ManagerReview` = stage "Head Review"** (KHÔNG đổi tên enum). Transition hợp lệ:
  Applied→Screening→ManagerReview→Interview→Offer→(Hired/OfferDeclined); mọi active→Rejected.
- Offer/Reject **email-gated** (không đi qua endpoint decision); Interview→Offer/Reject cần interview
  `Completed`. `Screening→ManagerReview` stamp `Application.DepartmentHeadReviewRequestedAt`.
- **Notification event codes đã có (≈16)** = *catalog trigger sẵn có cho v4*: `application_applied`,
  `application_screening_started`, `application_department_head_review_requested`, `application_interview_requested`,
  `interview_scheduled`, `interview_completed`, `offer_email_sent`, `rejection_email_sent`, `offer_accepted`,
  `offer_declined`, `application_withdrawn`, `job_submitted_for_approval`, `job_approved`, `job_rejected`,
  `application_status_changed` (legacy), `candidate_score_ready` (legacy). File:
  `Domain/Constants/NotificationEventCodes.cs`.

**RBAC**
- Role seed (init.sql): **Candidate, HR, Manager, HeadDepartment, SystemAdmin** (`Domain/Constants/RoleNames.cs`).
  **Không có role "HRAdmin".**
- Enforcement backend = **role-based** `[Authorize(Roles="HR,Manager")]` + **ownership scoping** ở service
  (`OwnershipScope`, `ApplicationOwnershipResolver`, snapshot `AssignedRecruiterId`/`AssignedDepartmentHeadId`).
- **Bảng `permissions`/`role_permissions` đã seed (~25 quyền: `Job_VIEW`, `Application_REVIEW`…) nhưng RUNTIME
  KHÔNG kiểm tra** — không có policy/attribute/middleware permission nào. ⇒ Gap cần xử lý ở Phase 0.
- **SystemAdmin bị khóa khỏi dữ liệu nghiệp vụ** (Phase 2.2b/2.2c: 403 trên business endpoint, chỉ system admin).
  Copilot controller `[Authorize(Roles="HR,Manager")]` (SystemAdmin đã bị loại khỏi copilot).
- **Frontend LẠI có mô hình permission** (`src/permissions/permissions.ts` + `rolePermissions.ts`, hook
  `usePermissions`, guard `RouteGuard`/`PermissionGuard`) map role→permission string. ⇒ FE gating dễ mở rộng;
  BE mới là nơi thiếu permission-check.

**v2 Copilot (nền tảng)**
- Ranking = **single source of truth**, chỉ xếp hạng application ở **`Screening`** (`CopilotService.CreateRankingAsync`),
  scoring **deterministic** (Skill/Experience/Education/Project → Total 0–100 → Interview/Consider/Hold/Reject),
  idempotent qua `input_hash` (SHA-256). Gemini (`AiCopilotProvider`, endpoint OpenAI-compat) chỉ **enrich prose
  tiếng Việt** (fit summary/evidence); `AllowProviderReordering=false` ⇒ AI không đổi thứ hạng máy.
- **Vietnamese fit** lưu ở `candidate_fit_analyses` (FitLabel/Confidence/Summary/Evidence…). "Pass CV → Head
  Review" = `Screening→ManagerReview`.
- **candidate-search & AI email draft: deprecated** (không gọi provider, trả template/deterministic + cảnh báo).
  Chatbot **từ chối câu hỏi ngoài phạm vi ngay** (`IsRecruitmentCopilotQuery` allowlist trước khi gọi AI).
- **AI fail được cô lập:** mọi call bọc try/catch, luôn có fallback deterministic; không rollback transaction.
  Choke point AI = `IAiCopilotProvider` (mọi provider call đi qua `TryCreateStructuredJsonAsync` /
  `TryCreateChatReplyAsync`). Ngoài ra có `IEmbeddingProvider`, `IResumeParsingAiProvider`.

**Telemetry AI hiện có (PARTIAL)**
- `copilot_ranking_sessions`: `model_name`, `input_hash`, `prompt_tokens`/`completion_tokens` **(cột có nhưng
  KHÔNG bao giờ được set)**. `candidate_fit_analyses` & `copilot_generated_artifacts`: `provider_name`,
  `model_name`, `fallback_used`. **KHÔNG có** cost, latency, prompt version, bảng telemetry trung tâm.

**Database**
- Provisioning từ **`init.sql`** (+ `db/patches/*.sql`); **migration KHÔNG auto-apply** lúc khởi động (API không
  gọi `Migrate()`). Migration mới nhất: `20260630000000_AddCopilotV2Artifacts`.
- **Không có bảng lịch sử status** ứng dụng (chỉ cột `applications.status`). Có `system_logs` (đơn giản:
  user_id/action/description/created_at). Có `notification_events` (registry event) + `user_notification_settings`
  (prefs) ⇒ *tiền lệ cho catalog cấu hình động*.
- Embedding lưu **jsonb** (`job_embedding_vector`, `candidate_embedding_vector`) — **không có vector DB**.

**Frontend**
- HR screens (13): dashboard, candidates, jobs, applications, interviews, **AI Copilot** (`src/pages/hr/aiCopilot/`
  — ranking table, `AssistantDrawer`, `CriteriaBuilderModal`, `ToolResultModal`; search/email đã gỡ khỏi flow).
- Manager (≈7): `/manager/dashboard`, `/manager/applications` (Head Review), `/manager/jobs/:id/approval`,
  **`/manager/reports` = `ManagerRecruitmentAnalyticsScreen`** (nơi cắm Talent Intelligence).
- **SysAdmin: toàn stub** `FeaturePlaceholderScreen` ("Đang chờ hỗ trợ từ backend") tại `/system-admin/*`
  (dashboard, users, roles, permissions, audit-logs). ⇒ *Chỗ trống hoàn hảo cho dashboard v5.*
- **Không có route `/hr-admin/*`.**

**Testing**
- Backend: xUnit + FluentAssertions + Moq; integration bằng **Testcontainers PostgreSQL** +
  `WebApplicationFactory` (`RecruitProWebApplicationFactory`, fake AI/email/storage providers, tắt hosted
  service). Văn hóa test RBAC mạnh (`Phase22bSecurityTests`, `Phase22cSecurityTests`, `OwnershipGuardUnitTests`),
  notification routing (`NotificationRoutingTests` 23 fact), copilot unit tests, conformance tests.
- Frontend: **Playwright E2E** (mock `/api/**`, deterministic) — không có Jest/Vitest.

### 2.2 Confirmed from docs / roadmap

- `docs/ai-roadmap/05-v4-*.md`, `06-v5-*.md`, `07-database-changes.md`, `08-api-changes.md`,
  `10-background-jobs-events.md` đã phác thảo đúng các entity/tool/permission mà đề bài liệt kê (Workflow*,
  Prompt*, AiRunTelemetry…). `10-*` nêu rõ: giữ in-process queue cho feature nhẹ, **bọc queue sau interface**,
  chuyển durable khi cần; "ATS transaction first, enrichment second"; worker phải idempotent, lưu lý do lỗi + số
  lần retry; job đổi state phải tôn trọng permission/approval.
- `source-of-truth/STATE-MACHINE.md`, `NOTIFICATION-EVENT-MATRIX.md`: xác nhận transition, recipient routing,
  best-effort post-commit, "SystemAdmin không phải recipient tuyển dụng".

### 2.3 Unknown / cần quyết định (DECISION)

| # | Vấn đề | Lựa chọn | Khuyến nghị |
|---|---|---|---|
| D1 | **Ai sở hữu cấu hình workflow v4?** Chưa có role HR Admin | (a) Thêm role `HRAdmin` mới; (b) Dùng SystemAdmin cho config toàn cục + quyền `workflows.manage` gán cho một HR chỉ định; (c) Chỉ SystemAdmin | **(a)** Thêm `HRAdmin` (seed init.sql + patch). Workflow *definition* là cấu hình nghiệp vụ tuyển dụng → HRAdmin hợp lý hơn SystemAdmin (đang bị khóa khỏi dữ liệu nghiệp vụ). SystemAdmin có thể được cấp thêm để cấu hình toàn cục. |
| D2 | **Cơ chế permission backend** (hiện role-only, bảng permission ngủ) | (a) Kích hoạt `[HasPermission("...")]` dựa trên `role_permissions` (đánh thức RBAC đã seed); (b) Giữ `[Authorize(Roles=...)]` và map quyền mới → role | **(a) cho quyền mịn v4/v5**, fallback (b) cho gate thô. Bắt đầu ở Phase 0, ít rủi ro vì tái dùng bảng sẵn có. |
| D3 | **Độ tin cậy trigger v4** (in-memory channel mất khi restart) | (a) **Transactional outbox** `published_domain_events` ghi cùng transaction nghiệp vụ + worker polling; (b) giữ in-memory | **(a)**. Outbox = idempotency + durable + đúng roadmap. Đây là thay đổi kiến trúc quan trọng nhất của v4. |
| D4 | **Token/cost thật từ Gemini** | Provider OpenAI-compat có trả `usage` không? cần xác minh khi implement | Nếu có → lưu thật; nếu không → **cost ước tính** và **đánh dấu `is_estimated=true`** rõ ràng. |
| D5 | **Talent Intelligence lấy dữ liệu ở đâu** | Tổng hợp từ ATS (jobs/applications/interviews/skills) — nguồn "source quality" có thể yếu | Chỉ build widget khi dữ liệu đủ; widget thiếu dữ liệu phải ẩn/gắn nhãn "dữ liệu chưa đủ", không bịa. |
| D6 | **MCP transport** (stdio nội bộ vs HTTP) | (a) MCP tool nội bộ gọi thẳng application service (in-proc); (b) MCP server HTTP riêng | Bắt đầu **(a) in-proc, read-only, có audit** ở phase cuối v4; hoãn HTTP/external. |

---

## 3. Recommended Build Order

| Phase | Tên | Khối | Phụ thuộc | Complexity |
|---|---|---|---|---|
| **0** | Foundation: permission enforcement + HRAdmin role + kỷ luật migration/init.sql | Chung | — | **M** |
| **v4.1** | Event publishing foundation (outbox + `IRecruitProEventBus`) | v4 | 0 | **M** |
| **v4.2** | Workflow execution core (engine tối thiểu, 1 action `notify`) | v4 | v4.1 | **L** |
| **v4.3** | Workflow templates + HR Admin UI | v4 | v4.2 | **L** |
| **v4.4** | Execution logs, retry, dead-letter, delayed action | v4 | v4.2 | **M** |
| **v4.5** | MCP prototype (read tools, RBAC, audit) | v4 | v4.2, 0 | **L** |
| **v5.1** | AI telemetry capture (decorator, KHÔNG dashboard) | v5 | 0 (song song v4) | **M** |
| **v5.2** | SysAdmin AI metrics dashboard (rollup + API + UI) | v5 | v5.1 (+dữ liệu tích lũy) | **L** |
| **v5.3** | Prompt registry + versioning + rollback | v5 | v5.1 | **L** |
| **v5.4** | Provider routing + risk flags + evaluation | v5 | v5.1, v5.3 | **XL** |
| **v5.5** | Talent Intelligence dashboard (Manager) | v5 | dữ liệu ATS đủ | **L** |

Nguyên tắc: **v5.1 chạy sớm/song song** để tích lũy dữ liệu trong lúc làm v4; dashboard v5 chỉ bật khi có dữ
liệu. **Không** làm dashboard trước telemetry.

---

## 4. v4 Detailed Plan (5 phases)

### Phase 0 — Foundation (chung cho v4 & v5)

- **Goal:** Có lớp permission-check backend thật + role `HRAdmin` + quy ước migration/init.sql, để mọi endpoint
  v4/v5 gate được bằng quyền mịn mà không phá RBAC hiện tại.
- **User value:** Không lộ tính năng admin cho HR thường; kích hoạt RBAC đã seed; nền an toàn cho phần còn lại.
- **Backend:**
  - Thêm `IPermissionAuthorizationService` + attribute `[HasPermission("perm.code")]`
    (`IAuthorizationRequirement` + handler đọc quyền của user từ `role_permissions` theo role trong JWT claims;
    cache theo request). Giữ song song `[Authorize(Roles=...)]` (không gỡ gate cũ).
  - Nạp quyền vào JWT claims lúc login (`AuthService`/`IJwtService`) HOẶC lookup DB tại handler (chọn lookup +
    cache để tránh phình token). Reuse `IUserRepository.GetUsersInRolesAsync` pattern.
  - Seed permission mới (mục 8) + gán role.
- **Database:**
  - `init.sql` + patch `db/patches/20260701-add-v4v5-roles-permissions.sql`: thêm role `HRAdmin`; insert
    permission `workflows.*`, `mcp.tools.*`, `ai.*`, `talent_intelligence.view`; map role_permissions.
  - EF migration tương ứng (chỉ seed data; không đổi schema RBAC).
- **API:** không thêm endpoint; áp `[HasPermission]` dần.
- **Frontend:** mở rộng `permissions.ts` + `rolePermissions.ts` (thêm `HR_ADMIN`, `SYSTEM_ADMIN` map quyền mới);
  thêm role home path cho `HRAdmin` (đề xuất `/hr-admin/workflows`).
- **Tests:** unit cho `PermissionAuthorizationService`; integration RBAC kiểu `Phase22b` cho vài endpoint mẫu
  (HRAdmin pass / HR thường 403).
- **Dependencies:** —
- **Risks:** token phình nếu nhồi claims (→ dùng lookup+cache); regress RBAC hiện tại (→ chỉ *thêm*, test đủ).
- **Done:** HRAdmin đăng nhập nhận đúng quyền; 1 endpoint mẫu gate bằng `[HasPermission]`; test xanh; seed có ở
  cả init.sql lẫn migration.
- **Complexity:** **M**

### Phase v4.1 — Event publishing foundation (outbox)

- **Goal:** Sinh **domain event bền vững** từ các mốc ATS bằng transactional outbox, tách khỏi notification.
- **User value:** Nền cho mọi automation; đảm bảo event không mất khi restart, không double-fire.
- **Backend:**
  - `RecruitPro.Application/Automation/Events/`: `IRecruitProEventBus` (`PublishAsync(RecruitProEvent evt)`),
    record `RecruitProEvent { EventType, OccurredAt, AggregateType, AggregateId, PayloadJson, DedupKey }`.
  - Implementation ghi 1 row `published_domain_events` **trong cùng `IUnitOfWork` transaction nghiệp vụ**
    (không best-effort như notification). Chèn tại đúng call site đang gọi `INotificationEventService` trong
    `ApplicationService` (Applied→Screening, Screening→ManagerReview, ManagerReview→Interview, withdraw…),
    `InterviewService` (interview_completed), `OfferService`. **Event ban đầu:** `CandidateApplied`,
    `PassedToHeadReview` (Screening→ManagerReview), `InterviewCompleted`, `CandidateProfileUpdated`,
    `ResumeUploaded`, `JobApproved`. `DedupKey` = `{eventType}:{aggregateId}:{transitionId}`.
  - Chưa cần worker tiêu thụ ở phase này (chỉ ghi + đọc để verify) — nhưng thêm `IEventOutboxRepository`.
- **Database:** bảng `published_domain_events` (mục 6). Index `(event_type, occurred_at)`, `(status, next_attempt_at)`,
  unique `(dedup_key)`.
- **API:** (nội bộ) không public; có thể thêm `GET /api/sysadmin/events/recent` (debug, HRAdmin/SysAdmin) — optional.
- **Frontend:** không.
- **Tests:** unit: transition tạo đúng outbox row + dedup; integration: 2 lần cùng transition ⇒ 1 row
  (idempotency); notification cũ vẫn hoạt động không đổi.
- **Dependencies:** Phase 0 (không bắt buộc, nhưng nên).
- **Risks:** ghi outbox trong transaction làm transaction dài hơn (nhỏ, chấp nhận được); nhân đôi logic với
  notification (→ giữ 2 kênh độc lập: outbox=reliable trigger, notification=best-effort UX).
- **Done:** mỗi mốc ATS ở trên tạo đúng 1 outbox row bền vững, có dedup; không ảnh hưởng flow v2.
- **Complexity:** **M**

### Phase v4.2 — Workflow execution core

- **Goal:** Engine trigger→condition→action tối thiểu, chạy từ outbox, với **1 action built-in** `notify_hr` và
  1 template hard-code để demo end-to-end.
- **User value:** "Ứng viên high-fit xuất hiện → HR nhận thông báo tự động" chạy thật từ event ATS.
- **Backend (`RecruitPro.Application/Automation/`):**
  - Entities/domain (namespace `Automation`, tránh `Workflows`): `WorkflowDefinition`, `WorkflowDefinitionVersion`,
    `WorkflowTrigger`, `WorkflowCondition`, `WorkflowAction`, `WorkflowExecution`, `WorkflowExecutionStep`.
  - Services: `IWorkflowDefinitionService`, `IWorkflowExecutionService`, `IWorkflowTriggerDispatcher`,
    `IWorkflowActionRegistry`, `IWorkflowActionHandler` (một handler / action type).
  - **Dispatcher worker:** `WorkflowDispatcherBackgroundService : BackgroundService` — poll
    `published_domain_events` (status=Pending) theo batch, map event→trigger, eval condition (structured, KHÔNG
    free-text), tạo `WorkflowExecution` + step, gọi action handler, đánh dấu event Processed. **Idempotency key**
    = `(workflow_definition_version_id, event_dedup_key)` (unique) ⇒ chống chạy trùng.
  - Condition eval: so sánh trên giá trị structured (vd `semantic_score >= threshold`, `recommendation IN {...}`,
    `department_id == X`). Lấy dữ liệu qua application service (đọc), không truy vấn repo trực tiếp trong handler.
  - Action đầu tiên: `notify_hr` → gọi `INotificationEventService`/`INotificationRealtimeSender` (tái dùng SSE).
    Action AI (vd `run_fit_analysis`) **chạy async, fallback-safe**, và **không** phần nào của action được phép
    làm hỏng transaction ATS (action chạy ngoài transaction nghiệp vụ, trên bản ghi đã commit).
  - **Guard đổi state:** action làm đổi ATS state (nếu có sau này) phải đi qua application service với
    permission/approval — mục "What Not To Build" cấm auto reject/offer.
- **Database:** `workflow_definitions`, `workflow_definition_versions`, `workflow_executions`,
  `workflow_execution_steps` (mục 6). Index `workflow_executions(workflow_definition_id, created_at desc)`.
- **API:** `GET /api/workflows/{id}/executions`, `GET /api/workflows/executions/{executionId}` (đọc, HRAdmin).
- **Frontend:** chưa (demo qua template hard-code + trang execution ở v4.3).
- **Tests:** unit trigger-match, condition-eval, action-handler; integration event→execution (1 event ⇒ 1
  execution, chạy lại ⇒ không nhân đôi); action AI fail ⇒ execution FAILED nhưng ATS + notification khác không sao.
- **Dependencies:** v4.1.
- **Risks:** double-execution (→ idempotency unique key); spam (→ v4.4 rate-limit + condition threshold); nhầm
  namespace với `Domain/Workflows` (→ đặt `Automation`).
- **Done:** template "CandidateApplied + fit≥threshold → notify_hr" chạy thật từ event, log đủ step; chạy lại
  không nhân đôi.
- **Complexity:** **L**

### Phase v4.3 — Workflow templates + HR Admin UI

- **Goal:** 4 template chuẩn + UI cho HRAdmin: list / detail / create-edit (trigger-condition-action builder có
  giới hạn) / publish.
- **User value:** HRAdmin tự bật/tinh chỉnh automation không cần dev.
- **Backend:** `IWorkflowDefinitionService` CRUD + `publish` (tạo `WorkflowDefinitionVersion` immutable, set
  active version). Seed 4 template:
  1. **Candidate Applied → (nếu eligible) score/rank → notify HR** khi high-fit.
  2. **Pass CV → Head Review → notify Department Head + tạo review queue item.**
  3. **Head Review overdue (N ngày kể từ `DepartmentHeadReviewRequestedAt`) → reminder** (cần trigger theo thời
     gian — dùng scheduler ở v4.4; ở v4.3 chỉ định nghĩa template).
  4. **Interview Completed → gợi ý bước tiếp theo (AI, async) + notify HR/Manager.**
- **Database:** không thêm bảng (dùng của v4.2); seed template.
- **API:** `GET /api/workflows`, `POST /api/workflows`, `PATCH /api/workflows/{id}`,
  `POST /api/workflows/{id}/publish` (tất cả `workflows.manage`/`workflows.publish`, role HRAdmin/SysAdmin).
- **Frontend (mới `/hr-admin/workflows`):** `WorkflowListScreen`, `WorkflowDetailScreen`,
  `WorkflowEditorScreen` (builder: chọn trigger từ danh sách event; condition = form structured; action = chọn từ
  registry), nút Publish, badge version. Route guard `[HasPermission workflows.manage]`; thêm nav cho HRAdmin.
  Service `workflowService.ts` (axios + `request`).
- **Tests:** API CRUD + publish (RBAC: HR thường 403, HRAdmin 200); unit builder validation; **E2E Playwright**:
  HRAdmin tạo→publish→thấy execution.
- **Dependencies:** v4.2, Phase 0.
- **Risks:** builder quá tự do (→ chỉ cho chọn từ registry cố định, KHÔNG scripting); publish sai version (→
  version immutable + confirm).
- **Done:** HRAdmin cấu hình ≥4 workflow demo qua UI; publish sinh version; execution hiển thị.
- **Complexity:** **L**

### Phase v4.4 — Execution logs, retry, dead-letter, delayed action

- **Goal:** Vận hành tin cậy: retry có giới hạn, dead-letter, action trễ (deadline reminder), failure inspector.
- **User value:** Automation không "mất âm thầm"; HRAdmin xem/inspect/retry được.
- **Backend:**
  - `WorkflowActionDeadLetter` entity + `DeadLetterRetryBackgroundService` (retry cap N, backoff, lưu
    `error_reason`, `attempt_count`, `next_retry_at`).
  - **Delayed scheduler:** worker quét điều kiện thời gian (vd Head Review overdue) — poll bảng execution/queue
    có `run_at`; nguồn thời gian tránh `DateTime.Now` rải rác (dùng 1 clock service).
  - Rate-limit / dedup chống spam recipient (vd không gửi reminder trùng trong X giờ; gom theo user).
- **Database:** `workflow_action_dead_letters` (mục 6); thêm cột `status,next_retry_at,attempt_count,run_at,
  error_reason` cho `workflow_executions`/step; index `(status,next_retry_at)`.
- **API:** `POST /api/workflows/executions/{id}/retry` (`workflows.executions.retry`),
  mở rộng `GET .../executions/{id}` trả step + error + dead-letter.
- **Frontend:** execution history + **failure inspector** (timeline step, error, nút Retry) trong `/hr-admin`.
- **Tests:** idempotency/retry regression; dead-letter sau khi hết attempt; delayed action bắn đúng thời điểm
  (inject clock); anti-spam.
- **Dependencies:** v4.2.
- **Risks:** retry bão (→ cap + backoff + dead-letter); delayed job chạy trùng (→ idempotency key theo run window).
- **Done:** action fail → retry theo policy → dead-letter; reminder overdue chạy đúng; retry từ UI được.
- **Complexity:** **M**

### Phase v4.5 — MCP prototype (read-only, RBAC, audit)

- **Goal:** MCP server nội bộ (in-proc) expose **tool đọc** qua application service, có RBAC + audit input/output.
- **User value:** Chuẩn hóa cách AI/agent (tương lai v3) truy cập dữ liệu an toàn.
- **Backend:** `IMcpToolService` + `IMcpToolRegistry`; mỗi tool = wrapper gọi **application service** (không repo).
  Tool đọc ban đầu: `jobs.search`, `jobs.get`, `candidates.get_profile`, `applications.get`,
  `applications.get_fit_analysis`, `interviews.get_schedule`, `analytics.get_funnel_summary`, `notifications.create`
  (write duy nhất, đã idempotent + RBAC). **Mỗi call:** resolve identity → check permission (`mcp.tools.use.internal`
  + map scope→permission nghiệp vụ) → thực thi → **audit** input/output vào bảng `mcp_tool_audits`.
- **Database:** `mcp_tool_audits` (id, tool_name, caller_user_id, input_json, output_summary_json, allowed,
  denied_reason, latency_ms, created_at).
- **API:** `GET /api/mcp/tools` (catalog, `mcp.tools.manage`); MCP endpoint nội bộ (chưa expose external).
- **Frontend:** MCP tool catalog + access policy (đọc) trong `/hr-admin` hoặc `/system-admin` (SysAdmin quản lý
  scope). Optional ở phase này.
- **Tests:** RBAC per-tool (thiếu quyền → denied + audit); audit ghi đủ; tool gọi service không chạm repo.
- **Dependencies:** v4.2, Phase 0.
- **Risks:** MCP access quá rộng (→ read-first, allowlist tool, scope→permission, audit bắt buộc); rò rỉ dữ liệu
  nghiệp vụ cho SystemAdmin (→ tool nghiệp vụ vẫn qua ownership scope hiện tại).
- **Done:** ≥6 tool đọc chạy với RBAC + audit; tool không bao giờ chạm repository trực tiếp.
- **Complexity:** **L**

---

## 5. v5 Detailed Plan (5 phases) — telemetry TRƯỚC dashboard

### Phase v5.1 — AI telemetry capture foundation (KHÔNG dashboard)

- **Goal:** Bắt mọi AI-run vào 1 bảng telemetry trung tâm qua **decorator ở choke point**, write-behind, không
  ảnh hưởng latency người dùng.
- **User value:** (nội bộ) — dữ liệu nền để sau này đo cost/latency/fallback; chưa lộ UI.
- **Backend:**
  - Decorator: `TelemetryAiCopilotProvider : IAiCopilotProvider` bọc `AiCopilotProvider`; tương tự cho
    `IEmbeddingProvider`, `IResumeParsingAiProvider`. Ghi: `feature` (ranking/chat/fit/questions/embedding/resume-parse),
    `provider_name`, `model_name`, `prompt_version_id` (null tới v5.3), `prompt_tokens`/`completion_tokens` (từ
    `usage` nếu Gemini trả — **D4**), `estimated_cost_usd` + `is_cost_estimated`, `latency_ms` (Stopwatch),
    `success`, `fallback_used`, `schema_valid`, `error_code`, `correlation_id`, `workflow_execution_id?`.
  - `IAiTelemetryService.Record(...)` **enqueue** vào `Channel` + `AiTelemetryWriteBehindBackgroundService`
    (theo mẫu `SemanticScoringBackgroundService`) ghi batch → không chặn request. Mất event telemetry KHÔNG được
    ảnh hưởng nghiệp vụ (best-effort).
  - **Backfill:** set `prompt_tokens`/`completion_tokens` đang null ở `copilot_ranking_sessions` từ nay về sau.
- **Database:** `ai_run_telemetry` (mục 6). Index `(feature, created_at)`, `(provider_name, model_name, created_at)`,
  `(success)`, `(created_at)`.
- **API:** không (chưa dashboard).
- **Frontend:** không (trừ optional banner confidence/risk trên artifact — hoãn tới v5.4).
- **Tests:** unit decorator ghi đúng field cho success/fallback/schema-fail; write-behind không mất event khi tải
  cao; AI fail vẫn fallback (không regress v2).
- **Dependencies:** Phase 0 (nhẹ). **Chạy sớm/song song v4.**
- **Risks:** ghi telemetry gây chậm (→ write-behind async); cost sai nếu provider không trả usage (→ ước tính +
  gắn cờ `is_cost_estimated`).
- **Done:** mọi AI call (v2 + action AI của v4) sinh 1 row telemetry với latency/success/fallback; người dùng
  không thấy chậm.
- **Complexity:** **M**

### Phase v5.2 — SysAdmin AI metrics dashboard

- **Goal:** Rollup + API + dashboard SysAdmin: volume theo feature, provider/model split, token/cost trend,
  latency p50/p95, fallback rate, schema-fail rate.
- **User value:** SysAdmin trả lời được: AI chạy bao nhiêu lần, tốn token/cost bao nhiêu, provider/model nào lỗi
  nhiều, fallback bao nhiêu, latency p50/p95.
- **Backend:** `AiUsageDailyRollupBackgroundService` (cron-like: poll + `run_at` ngày) tổng hợp `ai_run_telemetry`
  → `ai_usage_daily_rollups`. `IAiTelemetryService.GetMetrics(query)` đọc rollup (fallback query thô cho khoảng
  ngắn). p50/p95 tính từ telemetry (percentile).
- **Database:** `ai_usage_daily_rollups` (date, feature, provider, model, request_count, prompt_tokens,
  completion_tokens, est_cost_usd, p50_latency_ms, p95_latency_ms, success_count, fallback_count,
  schema_fail_count).
- **API:** `GET /api/sysadmin/ai/metrics` (`ai.metrics.view`, role SystemAdmin). Query: range, feature, provider.
- **Frontend (`/system-admin/ai/operations` — thay stub):** AI Operations Dashboard (volume, provider/model,
  token/cost trend, latency, fallback, schema-fail). Dùng chart tự vẽ (Tailwind/SVG) — không thêm lib nặng.
- **Tests:** unit rollup aggregation (percentile, cost sum, đếm fallback); API RBAC (chỉ SystemAdmin); contract
  test shape response; E2E dashboard render với API mock.
- **Dependencies:** v5.1 + có dữ liệu tích lũy vài ngày (shadow trước khi mở rộng).
- **Risks:** dashboard sai lệch khi ít dữ liệu (→ hiển thị "n=…" + ẩn khi dưới ngưỡng); cost ước tính (→ nhãn rõ).
- **Done:** SysAdmin xem traffic/cost/latency/fallback theo feature & provider; số khớp telemetry.
- **Complexity:** **L**

### Phase v5.3 — Prompt registry + versioning + rollback

- **Goal:** Prompt có version, mỗi feature tham chiếu 1 `prompt_version_id`, rollback không cần deploy.
- **User value:** SysAdmin biết prompt version nào tốt/xấu và revert an toàn.
- **Backend:** `IPromptRegistryService`; entity `PromptTemplateVersion` (feature_key, version, body, variables,
  is_active, created_by, notes, staged_rollout_pct?). `AiCopilotProvider` đọc prompt active theo feature từ
  registry (thay vì hard-code); telemetry ghi `prompt_version_id` (nối vào v5.1). Rollback = set version cũ active.
  Không xóa version (immutable).
- **Database:** `prompt_template_versions`. (Lưu ý: khác `copilot_prompt_templates` hiện tại là prompt do HR lưu
  cho artifact — registry này là prompt *hệ thống* cho từng feature AI.)
- **API:** `GET /api/sysadmin/ai/prompts`, `POST /api/sysadmin/ai/prompts` (tạo version),
  `POST /api/sysadmin/ai/prompts/{id}/activate` (rollback/rollout) — `ai.prompts.manage`.
- **Frontend (`/system-admin/ai/prompts`):** registry + **so sánh version** (diff body) + nút activate/rollback.
- **Tests:** integration resolve prompt version đúng; rollback đổi hành vi không deploy; telemetry gắn đúng version;
  RBAC.
- **Dependencies:** v5.1.
- **Risks:** version sai làm hỏng feature (→ validate template variables trước activate; giữ fallback deterministic
  của v2); nhầm với `copilot_prompt_templates` (→ tách bảng + tài liệu).
- **Done:** đổi prompt version cho ≥1 feature qua UI, rollback được, telemetry phản ánh version.
- **Complexity:** **L**

### Phase v5.4 — Provider routing + risk flags + evaluation

- **Goal:** Routing provider/model theo feature (admin-only, feature-flag), risk flags heuristic, và evaluation
  batch để chấm chất lượng prompt/version.
- **User value:** SysAdmin đổi provider/model an toàn, thấy output rủi ro, so sánh chất lượng version.
- **Backend:**
  - `IAiProviderRoutingService` + `ProviderRoutingPolicy` (feature→provider/model, cost ceiling, latency target,
    fallback chain). **Feature-flagged, admin-only**; giữ fallback chain của v2. `AiCopilotProvider` hỏi routing
    service trước khi gọi.
  - Risk flags heuristic khi ghi telemetry/artifact: unsupported-claim count, missing-evidence, prohibited-criteria
    (vd tiêu chí phân biệt đối xử), confidence cao + evidence thấp → cờ `risk_flags_json`.
  - `IAiEvaluationService` + `AiEvaluationBatchRunnerBackgroundService`: chạy `AiEvaluationCase` (input + expected)
    qua prompt version → `AiEvaluationResult` (pass/score). Offline, không ảnh hưởng production.
- **Database:** `provider_routing_policies`, `ai_evaluation_cases`, `ai_evaluation_results`; thêm `risk_flags_json`
  vào `ai_run_telemetry` (hoặc bảng `ai_risk_flags`).
- **API:** `GET/PATCH /api/sysadmin/ai/provider-routing` (`ai.provider_routing.manage`),
  `POST /api/sysadmin/ai/provider-routing/test` (simulate), `GET /api/sysadmin/ai/evaluations`
  (`ai.evaluations.view`), `GET /api/sysadmin/ai/risk-flags` (`ai.risk_flags.view`).
- **Frontend:** provider routing policy screen (+ test simulate), evaluation report, risk-flag dashboard; banner
  confidence/risk trên artifact AI (HR side, read-only).
- **Tests:** routing decision unit (cost/latency/fallback); provider fallback regression; evaluation batch; risk
  heuristic; RBAC (chỉ SystemAdmin đổi routing).
- **Dependencies:** v5.1, v5.3.
- **Risks:** routing sai gây tăng cost/lỗi (→ test simulate trước, feature-flag, fallback giữ nguyên); risk flag
  nhiễu (→ heuristic bảo thủ, chỉ cảnh báo).
- **Done:** đổi routing per-feature (admin), simulate, evaluation chạy được, risk flags hiển thị.
- **Complexity:** **XL**

### Phase v5.5 — Talent Intelligence dashboard (Manager)

- **Goal:** Dashboard Manager: skill demanded, supply vs demand gap, funnel conversion, time-to-fill risk,
  department bottleneck, source quality (nếu đủ dữ liệu).
- **User value:** Manager thấy nhu cầu skill, gap cung/cầu, nghẽn ở phòng ban nào.
- **Backend:** `ITalentIntelligenceService` + `TalentIntelligenceSnapshotBuilderBackgroundService` build
  `TalentIntelligenceSnapshot` từ ATS (jobs open + `job_skills`, applications theo status/department, interviews,
  `candidate_skills`). Tổng hợp theo department/role. **Chỉ tính chỉ số có dữ liệu tin cậy** (D5): thiếu → ẩn/nhãn.
- **Database:** `talent_intelligence_snapshots` (snapshot_date, scope, metrics_json). Tái dùng bảng ATS + copilot
  ranking cho conversion.
- **API:** `GET /api/manager/talent-intelligence` (`talent_intelligence.view`, role Manager + SysAdmin đọc).
- **Frontend:** mở rộng `/manager/reports` (`ManagerRecruitmentAnalyticsScreen`) hoặc trang mới
  `/manager/talent-intelligence`: skill demand, supply/demand gap, funnel, time-to-fill risk, bottleneck.
- **Tests:** unit snapshot builder (aggregation đúng, xử lý dữ liệu thiếu); API RBAC (Manager pass, HR thường tùy
  chính sách); contract test; E2E.
- **Dependencies:** dữ liệu ATS đủ; không phụ thuộc v5.2–5.4 (có thể làm sau khi telemetry ổn).
- **Risks:** intelligence gây hiểu lầm nếu source yếu (→ ẩn/nhãn "dữ liệu chưa đủ", không narrative thị trường);
  quyền lộ dữ liệu tổng hợp (→ RBAC + không lộ PII cá nhân).
- **Done:** Manager xem skill-gap + funnel; chỉ số thiếu dữ liệu được ẩn/gắn nhãn rõ.
- **Complexity:** **L**

---

## 6. Database Migration Plan

> Mọi bảng mới cần **cả** EF migration **và** cập nhật `init.sql` + patch trong `db/patches/` (migration không
> auto-apply). Đặt tên bảng snake_case theo quy ước hiện tại. FK Guid, `created_at timestamptz`.

**Cần NGAY (Phase 0 / v4.1 / v4.2):**
- `HRAdmin` role + permission rows + role_permissions (seed) — *bắt buộc Phase 0*.
- `published_domain_events` (id, event_type, aggregate_type, aggregate_id, payload_json, dedup_key **unique**,
  status[Pending/Processed/Failed], occurred_at, processed_at, next_attempt_at, attempt_count) — *v4.1*.
- `workflow_definitions` (id, name, description, owner_user_id, is_enabled, active_version_id, created_at, updated_at).
- `workflow_definition_versions` (id, workflow_definition_id, version_no, trigger_json, conditions_json,
  actions_json, published_at, published_by, is_active) — immutable.
- `workflow_executions` (id, workflow_definition_version_id, event_dedup_key, **unique(version_id,event_dedup_key)**,
  status, started_at, finished_at, error_reason, attempt_count, next_retry_at, run_at).
- `workflow_execution_steps` (id, execution_id, step_no, action_type, input_json, output_json, status, error_reason,
  started_at, finished_at).

**Cần cho v4.4 / v4.5:**
- `workflow_action_dead_letters` (id, execution_id, action_type, payload_json, error_reason, attempt_count,
  next_retry_at, created_at).
- `mcp_tool_audits` (id, tool_name, caller_user_id, input_json, output_summary_json, allowed, denied_reason,
  latency_ms, created_at).

**Cần cho v5 (theo phase):**
- `ai_run_telemetry` (id, feature, provider_name, model_name, prompt_version_id?, prompt_tokens?, completion_tokens?,
  estimated_cost_usd, is_cost_estimated, latency_ms, success, fallback_used, schema_valid, error_code?,
  correlation_id, workflow_execution_id?, risk_flags_json?, created_at) — *v5.1* (index: feature+created_at,
  provider+model, success).
- `ai_usage_daily_rollups` — *v5.2*.
- `prompt_template_versions` — *v5.3* (khác `copilot_prompt_templates` sẵn có).
- `provider_routing_policies`, `ai_evaluation_cases`, `ai_evaluation_results` (+ `ai_risk_flags` nếu tách) — *v5.4*.
- `talent_intelligence_snapshots` — *v5.5*.

**Cột thêm vào bảng có sẵn:** backfill/ghi `copilot_ranking_sessions.prompt_tokens/completion_tokens` (đang null).

**LATER (không làm ngay):** phân vùng `ai_run_telemetry` theo tháng; vector DB (giữ jsonb embedding).

---

## 7. API Plan

| Method | Route | Mục đích | Role / Permission |
|---|---|---|---|
| GET | `/api/workflows` | list workflow | HRAdmin(+SysAdmin) / `workflows.view` |
| POST | `/api/workflows` | tạo workflow | HRAdmin / `workflows.manage` |
| PATCH | `/api/workflows/{id}` | sửa workflow | HRAdmin / `workflows.manage` |
| POST | `/api/workflows/{id}/publish` | publish version | HRAdmin / `workflows.publish` |
| GET | `/api/workflows/{id}/executions` | lịch sử execution | HRAdmin / `workflows.executions.view` |
| GET | `/api/workflows/executions/{executionId}` | chi tiết execution + step + lỗi | HRAdmin / `workflows.executions.view` |
| POST | `/api/workflows/executions/{id}/retry` | retry execution | HRAdmin / `workflows.executions.retry` |
| GET | `/api/mcp/tools` | catalog MCP tool | SysAdmin/HRAdmin / `mcp.tools.manage` |
| GET | `/api/sysadmin/ai/metrics` | dashboard AI ops | SystemAdmin / `ai.metrics.view` |
| GET | `/api/sysadmin/ai/prompts` | list prompt version | SystemAdmin / `ai.prompts.manage` |
| POST | `/api/sysadmin/ai/prompts` | tạo prompt version | SystemAdmin / `ai.prompts.manage` |
| POST | `/api/sysadmin/ai/prompts/{id}/activate` | activate/rollback | SystemAdmin / `ai.prompts.manage` |
| GET | `/api/sysadmin/ai/evaluations` | kết quả evaluation | SystemAdmin / `ai.evaluations.view` |
| GET/PATCH | `/api/sysadmin/ai/provider-routing` | xem/sửa routing | SystemAdmin / `ai.provider_routing.manage` |
| POST | `/api/sysadmin/ai/provider-routing/test` | simulate routing | SystemAdmin / `ai.provider_routing.manage` |
| GET | `/api/sysadmin/ai/risk-flags` | risk flag dashboard | SystemAdmin / `ai.risk_flags.view` |
| GET | `/api/manager/talent-intelligence` | Talent Intelligence | Manager(+SysAdmin) / `talent_intelligence.view` |

> Dùng tiền tố `/api/sysadmin/...` (thống nhất với FE `/system-admin`) thay vì `/api/admin` của bản roadmap cũ, để
> không trùng nghĩa "Admin nghiệp vụ".

---

## 8. Frontend Plan (theo role)

**HR Admin (mới `/hr-admin/*`, guard `[HasPermission workflows.manage]`):**
- `WorkflowListScreen`, `WorkflowDetailScreen`, `WorkflowEditorScreen` (trigger/condition/action builder giới hạn),
  Publish, Execution History + **Failure Inspector** (retry). Nav mới cho role HRAdmin.
- MCP tool catalog (đọc) — optional.

**SysAdmin (thay stub `/system-admin/*`):**
- `AiOperationsDashboard` (`/system-admin/ai/operations`): volume, provider/model, token/cost, latency p50/p95,
  fallback, schema-fail.
- `PromptRegistryScreen` (`/system-admin/ai/prompts`): list + version diff + rollback.
- `ProviderRoutingScreen`: policy + test simulate.
- `AiEvaluationScreen`: evaluation results.
- `RiskFlagDashboard`.

**Manager:**
- Talent Intelligence: mở rộng `/manager/reports` hoặc `/manager/talent-intelligence` (skill demand, supply/demand
  gap, funnel conversion, time-to-fill risk, department bottleneck, source quality nếu đủ dữ liệu).

**HR (thường):**
- Chỉ *consume* kết quả workflow (thông báo qua bell/SSE hiện có). v5.4: banner confidence/risk trên artifact AI.

Tất cả: mở rộng `src/permissions/permissions.ts` + `rolePermissions.ts`; service mới `workflowService.ts`,
`sysadminAiService.ts`, `talentIntelligenceService.ts` (theo mẫu `copilotService.ts`, dùng `request`). Chart tự vẽ
(SVG/Tailwind) tránh thêm dependency nặng (chú ý cảnh báo chunk-size Vite hiện tại).

---

## 9. Background Jobs / Events Plan

**Events (outbox `published_domain_events`, ghi trong transaction nghiệp vụ):**
`CandidateApplied`, `PassedToHeadReview`(Screening→ManagerReview), `HeadReviewOverdue`(time-based),
`InterviewCompleted`, `CandidateProfileUpdated`, `ResumeUploaded`, `JobApproved`. DedupKey =
`{eventType}:{aggregateId}:{transitionId/date}`.

**Workers (tất cả theo mẫu `BackgroundService` + `Channel`/poll, idempotent, lưu error+attempt):**
| Worker | Nhiệm vụ | Retry | Idempotency key |
|---|---|---|---|
| `WorkflowDispatcherBackgroundService` (v4.2) | poll outbox → tạo execution → chạy action | n/a (đánh dấu Processed) | `(version_id, event_dedup_key)` |
| `DelayedActionScheduler` (v4.4) | trigger time-based (overdue reminder) | theo `run_at` | `(workflow, aggregate, run_window)` |
| `DeadLetterRetryBackgroundService` (v4.4) | retry action fail, cap N + backoff | cap N | `dead_letter_id + attempt` |
| `AiTelemetryWriteBehindBackgroundService` (v5.1) | ghi batch telemetry | best-effort | `correlation_id` |
| `AiUsageDailyRollupBackgroundService` (v5.2) | rollup ngày | idempotent theo ngày | `(date, feature, provider, model)` |
| `AiEvaluationBatchRunnerBackgroundService` (v5.4) | chạy eval case | theo run | `(case_id, prompt_version_id)` |
| `TalentIntelligenceSnapshotBuilderBackgroundService` (v5.5) | build snapshot | theo ngày | `(snapshot_date, scope)` |

**Nguyên tắc (đúng `10-background-jobs-events.md`):** ATS transaction trước, enrichment sau; worker idempotent;
lưu `error_reason` + `attempt_count`; job đổi state phải qua application service + permission/approval; giữ
in-process cho tải thấp nhưng **workflow/telemetry dùng bảng DB làm nguồn bền vững** (không chỉ Channel in-memory).

---

## 10. Testing Plan

- **Unit:** permission authorization service; outbox write + dedup; trigger-match / condition-eval / action-handler;
  telemetry decorator (success/fallback/schema-fail); rollup aggregation (percentile, cost, fallback count); routing
  decision; risk heuristic; talent snapshot aggregation (+ dữ liệu thiếu).
- **Integration (Testcontainers PostgreSQL + WebApplicationFactory, fake AI providers có sẵn):** event→execution
  end-to-end; prompt version resolution + rollback đổi hành vi; provider fallback regression; MCP tool RBAC + audit.
- **RBAC (kiểu `Phase22bSecurityTests`):** HRAdmin vs HR thường trên workflow API; SystemAdmin-only trên
  `/api/sysadmin/ai/*`; Manager trên talent-intelligence; SystemAdmin **không** rò rỉ dữ liệu nghiệp vụ; per-tool MCP.
- **Workflow execution tests:** 1 event ⇒ 1 execution; chạy lại ⇒ không nhân đôi; action AI fail ⇒ execution FAILED
  nhưng ATS + notification khác không sao.
- **Idempotency tests:** outbox dedup; execution unique key; delayed job run-window; dead-letter cap.
- **Telemetry aggregation tests:** rollup khớp telemetry; p50/p95; cost ước tính gắn cờ.
- **Frontend E2E (Playwright, mock API):** HRAdmin tạo→publish→thấy execution + retry; SysAdmin AI dashboard render;
  prompt rollback; Manager talent-intelligence; banner risk trên artifact.

---

## 11. Risks and Mitigations

| Rủi ro | Mitigation |
|---|---|
| **Double workflow execution** | Outbox dedup_key + `unique(version_id, event_dedup_key)`; worker đánh dấu Processed atomically. |
| **Notification/automation spam** | Condition threshold; rate-limit/dedup recipient theo cửa sổ thời gian; gom thông báo; reminder không lặp trong X giờ. |
| **AI cost tăng** | Telemetry cost (v5.1) + rollup + alert cost spike (v5.4); routing cost ceiling; action AI async + fallback. |
| **Provider failure** | Fallback chain của v2 giữ nguyên; routing test simulate; risk/fallback rate theo dõi; deterministic luôn có. |
| **Dashboard sai lệch** | Telemetry trước dashboard; ẩn/nhãn khi n thấp; cost gắn `is_estimated`; không narrative khi dữ liệu yếu. |
| **MCP access quá rộng** | Read-first, allowlist tool, tool→application service (không repo), scope→permission, audit bắt buộc. |
| **Đổi state không approval** | Action đổi ATS state phải qua application service + permission/approval; cấm auto reject/offer (mục 12). |
| **Sai role/permission** | Phase 0 kích hoạt `[HasPermission]`; test RBAC dày; chỉ *thêm* gate, không gỡ gate cũ; giữ SystemAdmin lockout nghiệp vụ. |
| **AI phá transaction ATS** | Event ghi trong transaction (chỉ INSERT nhẹ); action chạy **sau commit**, ngoài transaction, bọc try/catch; fail → execution FAILED, không rollback nghiệp vụ. |
| **Nhầm namespace Workflow** | Engine v4 đặt `Automation`; giữ `Domain/Workflows` cho state-machine ATS. |
| **Migration lệch init.sql** | Mọi bảng mới: EF migration + init.sql + patch; CI kiểm tra khớp. |

---

## 12. What Not To Build Yet

- **n8n clone / builder scripting tùy ý** — chỉ trigger/condition/action từ registry cố định, structured.
- **Auto rejection / auto offer / auto approve** — mọi quyết định đổi state nhạy cảm phải có người + approval.
- **Multi-agent graph phức tạp** (để dành v3 agent runtime).
- **Hệ sinh thái connector ngoài rộng / MCP external HTTP** — MCP bắt đầu in-proc, read-only, có audit.
- **Vector DB riêng** — giữ jsonb embedding hiện tại (chưa bão hòa).
- **Market intelligence narrative / dự báo thị trường** khi dữ liệu chưa đủ (D5).
- **Durable broker (RabbitMQ/Kafka)** — dùng outbox DB + worker; chỉ chuyển khi throughput/độ tin cậy đòi hỏi.
- **Đổi tên enum/role/status** — giữ nguyên `ManagerReview`, role hiện có; chỉ *thêm* `HRAdmin`.

---

## 13. Final Recommendation

**Minimum viable v4:** Phase 0 → v4.1 (outbox) → v4.2 (engine + action `notify_hr`) → v4.3 (4 template + HR Admin
UI + publish + execution view). Đủ để "HRAdmin cấu hình ≥4 workflow, chạy thật từ event ATS, log mọi step,
AI-step fail không phá ATS". v4.4 (retry/dead-letter/delayed) và v4.5 (MCP) là hardening/mở rộng ngay sau.

**Minimum viable v5:** v5.1 (telemetry capture) **trước** → v5.2 (SysAdmin metrics dashboard). Đủ để SysAdmin trả
lời cost/latency/fallback/error theo feature & provider. v5.3 (prompt versioning/rollback) tiếp theo vì giá trị
governance cao và reversible; v5.4 (routing/risk/eval) và v5.5 (Talent Intelligence) sau.

**Thứ tự sprint đề xuất:**
1. **Sprint 1:** Phase 0 + **v5.1** (song song — telemetry bắt đầu tích lũy ngay).
2. **Sprint 2:** v4.1 + v4.2 (engine core, demo 1 workflow thật).
3. **Sprint 3:** v4.3 (templates + HR Admin UI) — mốc demo v4.
4. **Sprint 4:** v4.4 (retry/dead-letter/delayed) + **v5.2** (AI dashboard — đã đủ dữ liệu).
5. **Sprint 5:** v5.3 (prompt registry) + v4.5 (MCP read-only).
6. **Sprint 6:** v5.4 (routing/risk/eval) + v5.5 (Talent Intelligence).

**Code trước tiên (thứ tự file/khối):**
1. `IPermissionAuthorizationService` + `[HasPermission]` + seed role/permission (init.sql + patch + migration).
2. `TelemetryAiCopilotProvider` decorator + `ai_run_telemetry` + `AiTelemetryWriteBehindBackgroundService` (v5.1).
3. `published_domain_events` + `IRecruitProEventBus` + chèn publish tại call site trong `ApplicationService`/
   `InterviewService`/`OfferService` (v4.1).
4. Engine `Automation` + `WorkflowDispatcherBackgroundService` + action `notify_hr` + 1 template (v4.2).
5. Workflow CRUD/publish API + `/hr-admin/workflows` UI + 4 template (v4.3).

> Kim chỉ nam xuyên suốt: **deterministic-first, AI-optional; ATS transaction trước, automation/telemetry sau;
> mọi thứ idempotent; đổi state phải có permission/approval; SysAdmin sở hữu governance AI, HRAdmin sở hữu
> workflow, Manager chỉ đọc Talent Intelligence.**
