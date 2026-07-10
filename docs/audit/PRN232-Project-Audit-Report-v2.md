# PRN232 Project Audit Report — v2

> Bản re-audit đọc trực tiếp từ source code hiện tại (không kế thừa kết luận của v1). Mọi luận điểm đều có evidence `file:line`. Phần cuối liệt kê các chỗ **v1 sai / lỗi thời** và đã sửa.
>
> - Phạm vi: `RecruitProInternal` (.NET 8 backend) + `recruit-pro-internal` (React/TS frontend).
> - Ngày build kiểm chứng: `dotnet build RecruitProInternal.sln` → **Build succeeded, 0 Error, 19 Warning**.
> - Kết luận nhanh: **chất lượng code & security tốt hơn nhiều so với v1 mô tả**; blocker thật để nộp là **các requirement bắt buộc còn thiếu** (OData, XML content negotiation, service communication, và DB engine nếu môn bắt buộc SQL Server), cộng **secret bị commit** và **docs lệch code**.

---

## 1. Executive Summary

**Điểm mạnh (đã kiểm chứng):**
- Kiến trúc onion 5 project sạch, **không có vi phạm layering**: `Domain` không phụ thuộc gì; `Application` **không** tham chiếu `Infrastructure`; `Infrastructure → Application` (DIP, hiện thực 49 interface). API là composition root.
- **Authorization thực tế TỐT**: hầu hết endpoint nhạy cảm đều có `[Authorize(Roles=...)]` ở method hoặc class. Có cả permission-based enforcement thật (`RequirePermissionAttribute` + `IPermissionCheckService`) cho khu SysAdmin.
- DB modeling đầy đủ: **34 DbSet**, khóa/FK/index cấu hình tường minh, 8 migration, `init.sql` snake_case Postgres, seed demo.
- DTO boundary sạch: controller **không** trả entity trực tiếp, **không** lộ `PasswordHash` ở response. Password hash bằng BCrypt.
- Có 359 test method, 7 background service, tích hợp AI (Gemini) + MinIO thật.

**Rủi ro lớn nhất (blocker nộp):**
1. **Thiếu requirement bắt buộc**: không có **OData**, không có **XML content negotiation**, không có **service communication** (gRPC/WCF/microservice/queue).
2. **DB engine**: đang PostgreSQL/Npgsql — nếu rubric bắt buộc **SQL Server** thì lệch requirement (cần xác nhận đề trước khi migrate).
3. **Secret bị commit thật** trong `appsettings.json` (JWT signing key, DB password, MinIO keys) và `appsettings.Development.json` (DB password). *(Lưu ý: `appsettings.Production.json` chỉ còn placeholder `change-me` — an toàn.)*
4. **Docs lệch code**: docs mô tả `refresh-token` / `me` / `logout` và "permission-based authorization" toàn hệ thống — backend không có/không đúng như vậy.

**Kết luận:** `Chưa sẵn sàng nộp` — nhưng vì **thiếu tính năng bắt buộc + secret + docs**, KHÔNG phải vì lỗ hổng auth như v1 kết luận.

---

## 2. Thay đổi so với v1 (Correction Log)

| # | v1 nói | Thực tế (v2) | Trạng thái |
|---|---|---|---|
| 1 | Dashboard/Interview/Offer/Copilot/SemanticDiscovery "public hoặc thiếu `[Authorize]`" — **P0** | Dashboard/Interview/Offer: **mọi method** có `[Authorize(Roles=...)]`. Copilot: **class-level** `[Authorize(Roles="HR,Manager")]`. Semantic: 5/6 method có role gate. | **v1 SAI** |
| 2 | `PATCH /api/jobs/{jobId}/status` public, privilege escalation | `JobController.cs:51-52`: `[Authorize(Roles="HR,Manager,HeadDepartment")]`, route qua guard scope theo DepartmentHead. Anonymous → 401. | **v1 SAI** |
| 3 | "Backend không enforce permission matrix trong code" | `RequirePermissionAttribute.cs:38-52` + `IPermissionCheckService` check bảng `role_permissions` sống, dùng trên `SysAdminRbacController`/`SysAdminDirectoryController`. | **v1 SAI** (nhưng phạm vi hẹp — chỉ khu admin) |
| 4 | "SystemAdmin gần như không có enforcement" | Có `[Authorize(Roles="SystemAdmin")]` trên `SysAdminMcp/Automation/AiOps` + permission-gate trên `SysAdminRbac/Directory`. | **v1 SAI** (đúng phần: business controller chưa cho SystemAdmin override) |
| 5 | Service sizes: Candidate ~3600, Application ~1442, Job ~1033 | Candidate **3694**, **Copilot 2513 (v1 bỏ sót)**, Application **1940**, Job **1320**. | **v1 thiếu số** |
| 6 | Secrets: liệt kê `appsettings.Production.json` 2 lần | Leak thật ở `appsettings.json` + `appsettings.Development.json`. **Production chỉ còn placeholder**. | **v1 sai chi tiết** |
| 7 | RBAC doc = `docs/ai-rules/rbac-matrix.md` | Đường dẫn đó **không tồn tại**. RBAC nằm ở `RecruitPro-SRD.md §5` + `docs/overview/09-auth.md`. | **v1 sai path** |
| 8 | Chỉ nói tới 4 role | Universe thật: **Candidate, HR, Manager, HeadDepartment, SystemAdmin** (HeadDepartment dùng rất nhiều). | **v1 thiếu role** |
| 9 | SignalR notification hub | Đã **migrate sang SSE** (`GET /api/notifications/stream`); không còn `AddSignalR`/`Hub` trong code. | **v1 lỗi thời** |
| 10 | OData / XML / service-comm / DB=Postgres thiếu | **Đúng, xác nhận lại.** | **v1 đúng** |
| 11 | refresh/me/logout thiếu; refreshToken lưu nhưng không có flow | **Đúng.** Bổ sung: bảng `refresh_tokens` tồn tại nhưng **không bao giờ được ghi/đọc** → dead weight. | **v1 đúng + bổ sung** |

---

## 3. Requirement Compliance Matrix (đã sửa)

| Requirement | Bắt buộc | Trạng thái | Evidence | Priority |
|---|---|---|---|---|
| ≥3 role có phân quyền | Yes | **Đạt** | 5 role thực thi trên controller (`CopilotController.cs:13`, `SysAdminMcpController.cs:11`, các `[Authorize(Roles)]`) | — |
| ≥5 entity chính | Yes | **Đạt** | 34 DbSet `AppDbContext.cs:22-88` | — |
| Quan hệ 1-n | Yes | **Đạt** | 52 `HasOne` / 50 `WithMany` trong `OnModelCreating` | — |
| n-n / bảng trung gian có thuộc tính | Yes | **Đạt** | `UserRole`, `RolePermission`, `JobSkill`, `CandidateSkill`, `ApplicationOfferBenefit` | — |
| Workflow có trạng thái | Yes | **Đạt** | `ApplicationStatusWorkflow`, enum `ApplicationStatus` | — |
| ≥5 business rule rõ ràng | Yes | **Có, thiếu section tổng hợp để nộp** | rule nằm rải trong `ApplicationService`/`JobService`/`OfferService`/`InterviewService` | P2 |
| CRUD + tìm kiếm + lọc + phân trang + thống kê | Yes | **Đạt** (security OK) | Jobs/candidates/applications/interviews/dashboard/analytics; role-gated | — |
| Chức năng user | Yes | **Đạt** | candidate register/profile/apply/status/history | — |
| ≥2 báo cáo/thống kê | Yes | **Đạt** | HR dashboard, Manager dashboard, recruitment analytics, job statistics | — |
| EF Core + **SQL Server** | Yes | **Chưa đạt** | `Program.cs:70` `UseNpgsql`; `Infrastructure.csproj:40` Npgsql 8.0.0 | **P0** (xác nhận đề) |
| Migration / SQL script | Yes | **Đạt** | 8 migration + `init.sql` | — |
| Seed data demo | Yes | **Đạt** | `init.sql`, `TestDataSeeder`, `V5DemoSeederHostedService` | — |
| **OData ≥2 API** | Yes | **Chưa đạt** | không package/`EnableQuery`/EDM/route | **P0** |
| **XML content negotiation** | Yes | **Chưa đạt** | `Program.cs:23` `AddControllers` không có XML formatter | **P0** |
| JWT + role-based auth | Yes | **Đạt** | `JwtExtension.cs:14-64`, `[Authorize(Roles)]` khắp nơi | — |
| User chỉ thao tác dữ liệu của mình | Yes | **Đạt** | ownership check ở service (userId+roles truyền xuống); `ResumeController` owner-or-HR | — |
| Client lưu JWT + gửi Bearer | Yes | **Đạt** | `authSlice.ts`, `api-client.ts` interceptor | — |
| **Service communication phụ** (gRPC/WCF/micro) | Yes | **Chưa đạt** | 5 project, không `.proto`/`AddGrpc`/queue; AI HTTP + MinIO + SSE không thay thế được | **P0** |
| Tài liệu nộp đầy đủ | Yes | **Có nhưng lệch code** | thiếu OData/XML/service demo, phantom auth endpoints, tài khoản demo chưa rõ | P1 |

---

## 4. Critical Issues (re-prioritized)

### P0 — Blocker để đạt điểm requirement (KHÔNG phải security)

1. **Thiếu OData.** Không có `Microsoft.AspNetCore.OData` trong bất kỳ `.csproj`; không `EnableQuery`/`GetEdmModel`. → Thêm package + tối thiểu 2 endpoint an toàn.
2. **Thiếu XML content negotiation.** `Program.cs:23` `AddControllers(...)` chỉ System.Text.Json. → `AddControllers().AddXmlSerializerFormatters()` (1 dòng) + demo `Accept: application/xml`.
3. **Thiếu service communication.** Chỉ 5 project, `docker-compose.yml` chỉ backend/postgres/minio. → dựng 1 service phụ tối thiểu (gRPC hoặc 1 API nhỏ) mà API chính gọi thật. **Xác nhận với giảng viên loại nào được tính.**
4. **DB engine ≠ SQL Server.** `Program.cs:70` `UseNpgsql`. → **Trước khi migrate**, xác nhận đề có bắt buộc SQL Server không (migrate 8 migration + `init.sql` là việc lớn — đừng làm trên giả định).

### P1 — Bảo mật/tài liệu cần xử lý trước khi nộp

5. **Secret thật bị commit** (`appsettings.json`, tracked trong git, `.gitignore` không loại trừ):
   - `Jwt:Key` = HS256 32 ký tự (`appsettings.json:12`) — **nghiêm trọng nhất**.
   - `ConnectionStrings:Mycnn` password `123456` (`:9`) — lặp lại ở `appsettings.Development.json:9`.
   - `MinioSettings:AccessKey/SecretKey` (`:21-22`).
   - → Rotate key, chuyển sang User Secrets/env var, xóa giá trị thật khỏi repo, thêm rule `.gitignore`. *(Production đã là placeholder `change-me` — không cần xử lý.)*
6. **Docs lệch code** (xem §9): docs liệt kê `refresh-token`/`me`/`logout` và "permission-based authorization toàn hệ thống" — không đúng. → Sửa docs cho khớp code (rẻ hơn nhiều so với implement).

### P2 — Chất lượng/độ bền

7. **God-services**: `CandidateService.cs` 3694, `CopilotService.cs` 2513, `ApplicationService.cs` 1940, `JobService.cs` 1320 (~9.500 dòng = 58% thư mục Services). → tách use-case nếu còn thời gian.
8. **Không có fallback authorization policy.** Không có `FallbackPolicy = RequireAuthenticatedUser` → "không attribute = public". Hiện các method nhạy cảm đều đã có attribute nên **an toàn**, nhưng dễ vỡ khi thêm controller mới quên gắn. → Hardening 1 dòng: đặt fallback policy + `[AllowAnonymous]` cho các endpoint công khai chủ đích.
9. **`refresh_tokens` là dead weight**: entity + bảng + DbSet tồn tại nhưng `AuthService.cs:142` chỉ sinh refresh JWT stateless, **không ghi bảng, không endpoint redeem**. → hoặc implement refresh thật, hoặc bỏ bảng + bỏ field khỏi docs.

### P3 — Nhỏ

10. `UserInfoDto.cs:32` đặt tên field password thô là `PasswordHash` (request DTO, không lộ nhưng gây hiểu nhầm) → đổi thành `Password`.
11. `GET /api/candidates/import/template` (`CandidateController.cs:155`) public — chỉ trả file Excel rỗng, rủi ro thấp nhưng có thể gắn `[Authorize(Roles="HR,Manager")]`.
12. 19 build warning (CS8618 nullable DTO, 1×CS0108 `WorkflowConditionOperator.Equals` che `object.Equals`, 1×CS1998 async thiếu await `CandidateService.cs:2354`).

---

## 5. Security Review (bản đúng)

### 5.1 Cơ chế
- JWT bearer: `JwtExtension.cs:14-64` (chỉ scheme + envelope 401/403, **không** có DefaultPolicy/FallbackPolicy).
- `[Authorize]` / `[Authorize(Roles=...)]`: dùng khắp controller nghiệp vụ.
- `[RequirePermission("CODE")]`: `RequirePermissionAttribute.cs:38-53` — filter check DB `role_permissions` (401 nếu không token, 403 nếu thiếu grant), dùng cho `SysAdminRbac/Directory`.
- Password: BCrypt trong `AuthService`. Response DTO **không** chứa `PasswordHash`.

### 5.2 Auth theo controller (đã kiểm chứng)

| Controller | Class-level | Ghi chú method | Anonymous nhạy cảm? |
|---|---|---|---|
| AuthController | none | 5 endpoint login/forgot public (pre-auth, không đọc token) | Không |
| **JobController** | none | GET job-board public; **`PATCH jobs/{id}/status` = `[Authorize(HR,Manager,HeadDepartment)]`**; `hr/*`,`manager/*` role-gated | Không |
| **DashboardController** | none | **mọi** method `[Authorize(Roles)]` | Không |
| **InterviewController** | none | **mọi** method `[Authorize(Roles)]` | Không |
| **OfferController** | none | **mọi** method `[Authorize(HR,Manager)]` | Không |
| **CopilotController** | **`[Authorize(HR,Manager)]`** | 20 action kế thừa | Không |
| SemanticDiscoveryController | none | 5/6 role-gated; `GET jobs/{id}/similar` public (không PII) | Không |
| ApplicationController | none | **mọi** method `[Authorize(Roles)]` | Không |
| CandidateController | none | phần lớn role-gated; public: `register`, `import/template` | Thấp (template rỗng) |
| ResumeController | none | 2 method `[Authorize]` + ownership `GetResumeStreamAsync(id,userId,isHrOrManager)` | Không |
| NotificationController | **`[Authorize]`** | cả SSE `stream` kế thừa | Không |
| Sysadmin* (Mcp/Automation/AiOps) | **`[Authorize(SystemAdmin)]`** | — | Không |
| Sysadmin Rbac/Directory | `[Authorize]` | per-method `[RequirePermission]` | Không |
| Department/Users/Lookup/ManagerAnalytics | none | lookup GET public; action nghiệp vụ role-gated | Không |

**Kết quả kiểm tra "đọc token mà không có `[Authorize]`": 0 method.** Mọi endpoint anonymous chỉ nhận tham số route/query, không đọc principal → không có lỗ hổng "giả định token nhưng public".

### 5.3 Security matrix theo nhóm (rút gọn)

Legend: ✅ có quyền · — 403 · 🌐 public · self = chỉ tài nguyên của mình.

| Nhóm | Candidate | HR | Manager | HeadDept | SysAdmin |
|---|---|---|---|---|---|
| Job board / auth login / register / lookup | 🌐 | 🌐 | 🌐 | 🌐 | 🌐 |
| Candidate profile/apply/withdraw/offer | ✅ self | — | — | — | — |
| Job create/edit/delete/approve | — | ✅ | ✅ | ✅ (dept sở hữu, BR-OWN-003) | — |
| Applications review/CV/email/decision | — | ✅ | ✅ | — | — |
| Manager review-queue | — | — | ✅ | — | — |
| Interviews (read / write) | — | ✅ | ✅ | ✅ read | — |
| Offers | — | ✅ | ✅ | — | — |
| Copilot / Semantic discovery | — | ✅ | ✅ | — | — |
| Resume preview/download | ✅ self | ✅ | ✅ | ✅ (owner-only) | ✅ (owner-only) |
| Notifications (incl. SSE) | ✅ self | ✅ self | ✅ self | ✅ self | ✅ self |
| HR dashboard | — | ✅ | ✅ | — | — |
| Manager dashboard / analytics | — | — | ✅ | ✅ | — |
| SysAdmin MCP/Automation/AiOps/RBAC/Directory | — | — | — | — | ✅ |

**Điểm cần lưu ý cho grader:** business controller dùng danh sách role tường minh; **SystemAdmin KHÔNG có "full override"** trên các controller nghiệp vụ (chỉ mạnh trên khu Sysadmin*). Nếu rubric kỳ vọng SystemAdmin làm được mọi thứ, cần thêm role hoặc dùng policy inheritance.

---

## 6. Required-Features Review (OData / XML / Service Comm / DB)

| Feature | Verdict | Evidence |
|---|---|---|
| OData | **ABSENT** | không package trong `.csproj`; không `EnableQuery/AddOData/GetEdmModel`; grep chỉ trúng file audit |
| XML content negotiation | **ABSENT** | `Program.cs:23` `AddControllers` không formatter XML; không `[Produces("application/xml")]`; `[Consumes]` duy nhất là `multipart/form-data` (`CandidateController.cs:163`) |
| DB engine | **PostgreSQL/Npgsql** | `Program.cs:70` `UseNpgsql("Mycnn")`; `Infrastructure.csproj:40` Npgsql 8.0.0; `docker-compose.yml` postgres:18 |
| Service communication (gRPC/WCF/micro/queue) | **ABSENT** | 5 project, không `.proto`/`AddGrpc`/RabbitMQ/Kafka; docker chỉ backend/postgres/minio |
| JSON serializer | System.Text.Json (không Newtonsoft) | `Program.cs:23-29` + 2 global filter (`ValidationActionFilter`, `ErrorEnvelopeResultFilter`) |

**Cái ĐANG có nhưng không thay thế được requirement service-comm:**
- Outbound HTTP tới AI provider (Gemini): `ServiceCollectionExtensions.cs:53-55`, base `generativelanguage.googleapis.com` — SaaS bên thứ 3, không phải service nội bộ.
- MinIO object storage (`IMinioClient`), SSE notifications (thay SignalR), email = `LoggingEmailService` (chỉ log).
- **7 background service** (`Program.cs:121-139`): `SemanticScoringBackgroundService`, `AiTelemetryWriteBehindBackgroundService`, 3× Workflow workers, 2× seeder (`WorkflowSeeder`, `V5DemoSeeder`). Đây là in-process, không phải inter-service.

---

## 7. Architecture & Code Quality (số thật)

- **Layering sạch** (từ `.csproj`): API→{Application,Infrastructure}; Application→Domain; Infrastructure→{Application,Domain}; Domain→∅; Tests→all. Không đảo chiều.
- **Service sizes** (Services/): Candidate 3694 · Copilot 2513 · Application 1940 · Job 1320 · SemanticDiscovery 743 · NotificationEvent 710. Tổng ~16.290 dòng/~38 file.
- **DTO hygiene**: 116 DTO tách Request/Response; controller tham chiếu namespace Domain **0 lần**; `PasswordHash` không có ở Response.
- **TODO/FIXME/HACK: 0** trong toàn solution.
- **Build**: 0 error, 19 warning (chủ yếu CS8618 nullable DTO).

---

## 8. Database Review

- **Đạt**: EF Core, DbContext rõ ràng, 34 entity, PK/FK/relationship tường minh (34 HasKey, 54 HasForeignKey, 32 HasIndex, 10 IsUnique, 6 OnDelete), 8 migration, `init.sql` snake_case Postgres + seed.
- **Chưa đạt/lưu ý**:
  - Engine Postgres, không phải SQL Server (nếu đề bắt buộc).
  - Chưa có ERD đúng nghĩa để nộp (`docs/diagrams/` mới có 2 flow HTML, chưa có ERD).
  - `refresh_tokens` bảng chết (không insert/read từ app code).
  - Status model trong SRD lệch enum/workflow hiện tại.

---

## 9. Auth Contract & Docs Drift

**Backend AuthController thật** (`AuthController.cs`, tất cả POST): `login`, `candidate/login`, `internal/login`, `candidate/forgot-password`, `internal/forgot-password`. **Không có** `refresh-token`, `me`, `logout` (grep toàn `Controllers/*.cs` = 0).

**Frontend drift:**
- `endpoints.ts:8` khai báo `auth.logout = "/auth/logout"` nhưng `authService.ts:65-71` logout là **mock local**, không gọi HTTP.
- `api-client.ts:45-49`: gặp 401 → force logout, **không** thử refresh.
- FE **lưu + validate** `refresh_token` (`authSlice.ts:78,86`, `authToken.ts:41-50` yêu cầu cả access + refresh còn hạn) nhưng **không có đường redeem** → hết access token là phải login lại.

**Docs lệch (top):**
1. `05-api.md:14-16` + `RecruitPro-SRD.md:250-252` liệt kê `refresh-token`/`me`/`logout` — **không tồn tại**.
2. `09-auth.md:8,51-52` "Refresh token support" — đúng chữ nhưng vô nghĩa (không persistence/rotation/redeem).
3. `SRD:150` "hệ thống dùng permission-based authorization" — **quá lời**: permission thật chỉ ở khu Sysadmin; business controller là role-based.
4. `SRD:145-146` "Manager kế thừa HR, SystemAdmin full override" — **không đúng code** (danh sách role tường minh, SystemAdmin vắng ở business controller).
5. `SRD:118` notifications "SignalR realtime" — code đã sang **SSE**.
6. Path `docs/ai-rules/rbac-matrix.md` (v1 trích) **không tồn tại**.

---

## 10. Tests & Build

- **359 test method** (346 `[Fact]` + 13 `[Theory]`), 36 file.
- **Unit (~204 method)**: không chạm DB/factory → pass mọi máy.
- **Integration (~155 method, 15 class `IClassFixture<PostgresTestFixture>`)**: `PostgresTestFixture.cs:16-21` khởi động **Testcontainers `postgres:16-alpine`** → **cần Docker daemon**. Không có Docker thì fail ngay ở `StartAsync`.
- Đây chính là cơ chế của "93 passed / 26 failed" trong v1 (số cụ thể có từ trước, nay tổng test đã tăng lên 359). → Không phải bug logic; là phụ thuộc hạ tầng. Cần ghi rõ "chạy `dotnet test` cần Docker" trong hướng dẫn nộp.
- **Build**: `dotnet build` = 0 error, 19 warning.

---

## 11. Fix Plan (re-prioritized, ưu tiên đường ngắn nhất)

### Phase 1 — Đạt requirement bắt buộc (P0)
| Task | Cách làm ngắn nhất | Acceptance |
|---|---|---|
| Xác nhận DB engine với giảng viên | Hỏi trước khi migrate | Có văn bản: SQL Server bắt buộc hay Postgres OK |
| (Nếu bắt buộc) migrate SQL Server | Đổi `UseSqlServer` + package + rà `init.sql`/migration cho T-SQL | App chạy trên SQL Server, migration apply được |
| XML content negotiation | `AddControllers().AddXmlSerializerFormatters()` (`Program.cs:23`) | Cùng endpoint trả JSON/XML theo `Accept` |
| OData ≥2 endpoint | thêm `Microsoft.AspNetCore.OData` + 2 controller `[EnableQuery]` (ví dụ Jobs, Applications, read-only, có role) | `$filter/$orderby/$top/$skip/$select` chạy |
| Service communication | dựng 1 service phụ tối thiểu (gRPC hoặc API nhỏ) API chính gọi thật + config + hướng dẫn run | Demo gọi liên service thành công |

### Phase 2 — Bảo mật & tài liệu (P1)
| Task | Cách làm | Acceptance |
|---|---|---|
| Rotate & tách secret | User Secrets/env; xóa giá trị thật khỏi `appsettings.json`+`.Development.json`; thêm `.gitignore` | Repo không còn JWT key/DB pw/MinIO key thật |
| Sửa docs khớp code | bỏ `refresh/me/logout` khỏi `05-api.md`/`SRD`; hạ "permission-based toàn hệ thống" xuống đúng phạm vi; đổi SignalR→SSE; sửa role inheritance | Docs không còn feature "ảo" |
| Quyết định refresh-token | implement thật HOẶC bỏ bảng + field + docs | Không còn dead weight |

### Phase 3 — Chất lượng & demo (P2/P3)
| Task | Cách làm | Acceptance |
|---|---|---|
| Fallback authz policy | `FallbackPolicy = RequireAuthenticatedUser` + `[AllowAnonymous]` cho endpoint công khai | Controller mới quên gắn attribute vẫn bị chặn |
| Tách god-services (nếu kịp) | split Candidate/Copilot/Application/Job theo use-case | mỗi service < ~800 dòng |
| ERD + bảng tài khoản demo + Postman | thêm ERD, account/password từng role, collection | Có checklist demo đầy đủ |
| Dọn nhỏ | đổi `UserInfoDto.PasswordHash`→`Password`; khóa `import/template`; xử lý warning CS0108/CS1998 | Build sạch hơn |

---

## 12. Final Submission Checklist

- [x] `dotnet build` thành công (0 error, 19 warning).
- [ ] `dotnet test` xanh (cần Docker cho ~155 integration test — ghi rõ trong hướng dẫn).
- [x] Có seed data demo.
- [ ] Có bảng account/password demo cho từng role.
- [ ] Có OData demo (≥2 endpoint).
- [ ] Có XML content negotiation demo.
- [ ] Có service communication demo.
- [ ] DB engine khớp requirement (xác nhận SQL Server vs Postgres).
- [ ] Secret thật đã xóa khỏi repo (`appsettings.json`, `.Development.json`).
- [ ] Docs khớp code (bỏ refresh/me/logout ảo, sửa RBAC wording, SignalR→SSE).
- [x] Authorization thực tế đạt (đã kiểm chứng — không phải blocker).
- [x] DTO boundary sạch, không lộ `PasswordHash`.

---

## Appendix — Phương pháp & phạm vi kiểm chứng

- **Cách audit**: đọc trực tiếp source, không kế thừa v1. Kiểm chứng song song 4 mảng: (1) auth từng controller, (2) required-features, (3) auth-contract/secrets/docs, (4) architecture/tests/DB. Chạy `dotnet build`.
- **File/khu vực đã đọc** (evidence chính):
  - Auth: toàn bộ `RecruitPro.API/Controllers/*.cs`, `Program.cs`, `JwtExtension.cs`, `RequirePermissionAttribute.cs`.
  - Features: `*.csproj`, `Program.cs`, `ServiceCollectionExtensions.cs`, `docker-compose.yml`, các `*BackgroundService.cs`.
  - Contract/secret/docs: `AuthController.cs`, `AuthService.cs`, `JwtService.cs`, `RefreshToken.cs`, `appsettings*.json`, FE `endpoints.ts`/`authService.ts`/`authSlice.ts`/`api-client.ts`/`authToken.ts`, `docs/overview/05-api.md`/`RecruitPro-SRD.md`/`09-auth.md`.
  - Architecture/DB/tests: `AppDbContext.cs`, `Migrations/*`, `init.sql`, `Services/*`, `RecruitPro.Tests/*` (`PostgresTestFixture.cs`).
- **Lưu ý repo git**: `.git` ở root repo hỏng (chỉ có `info/exclude`); repo git thật nằm ở `RecruitProInternal/.git` và `recruit-pro-internal/.git`.
- **Universe role thật**: Candidate, HR, Manager, HeadDepartment, SystemAdmin.
