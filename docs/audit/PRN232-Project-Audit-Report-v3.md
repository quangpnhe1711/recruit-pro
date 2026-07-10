# PRN232 Project Audit Report — v3 (đối chiếu ĐỀ BÀI)

> Khác v1/v2: v3 đọc **đề bài chính thức** (PRN232 - ChiLP, file `.docx`) và map **từng requirement trong đề** vào code thật. Bằng chứng `file:line` lấy từ một lượt verify song song (5 agent) trên source hiện tại — **sau khi Phase 1 đã bổ sung OData / XML / gRPC**.
>
> - Đề tài: RecruitPro là **đề tự đề xuất** (đề cho phép) — hợp lệ vì có ≥3 role, ≥5 entity, ≥1 workflow trạng thái.
> - Build: `dotnet build` = **0 error**. gRPC round-trip, OData, XML negotiation đã **test chạy thật** (Phase 1).
> - **Kết luận:** sau Phase 1, dự án **đạt gần như toàn bộ requirement bắt buộc**. Ba điểm chưa đạt còn lại đều là **deliverable/kỹ thuật cụ thể, không phải kiến trúc**: (1) **SQL Server** (đề bắt buộc, đang dùng PostgreSQL), (2) **ERD**, (3) **Postman collection**. Riêng "tài khoản mẫu" — đã tìm ra: **mật khẩu chung `Password@123`** (xem §8).

---

## 1. Executive Summary

**Mức độ đáp ứng đề bài (sau Phase 1):**

| Nhóm requirement | Trạng thái |
|---|---|
| Nghiệp vụ chung (role, entity, quan hệ, workflow, business rules, CRUD, báo cáo) | ✅ Đạt (vượt yêu cầu) |
| Kiến trúc nhiều tầng + controller mỏng | ✅ Đạt |
| EF Core | ✅ Đạt · **SQL Server ❌ (đang PostgreSQL)** |
| RESTful API + status code + DTO | ✅ Đạt |
| **OData ≥2** | ✅ Đạt (Phase 1) |
| **Content negotiation JSON/XML** | ✅ Đạt (Phase 1) |
| Security (JWT, role, ownership, hash) | ✅ Đạt |
| JavaScript client | ✅ Đạt (React) |
| **gRPC service communication** | ✅ Đạt (Phase 1) |
| Tài liệu nộp kèm | ⚠️ Gần đủ — **thiếu ERD**; use-case chưa có section riêng |
| Sản phẩm nộp | ⚠️ **Thiếu Postman**; tài khoản mẫu cần gom vào 1 doc |
| Yêu cầu nâng cao (bonus) | ✅ Rất mạnh: 10 có / 2 một phần / 1 thiếu |

**Ba việc phải làm trước khi nộp (mandatory gaps):**
1. **SQL Server** — đề ghi rõ *"Project phải sử dụng Entity Framework Core với SQL Server"*; dự án dùng PostgreSQL → hoặc **xin xác nhận giảng viên cho dùng Postgres**, hoặc **migrate sang SQL Server**.
2. **ERD** — đề bắt buộc nộp ERD; hiện chưa có (chỉ có mô tả prose + flow diagram).
3. **Postman collection** — đề liệt kê trong "Sản phẩm cần nộp"; hiện chưa có.

**Ngoài ra nên gom:** 1 bảng **tài khoản mẫu + mật khẩu** (đã biết) và **1 README hợp nhất** hướng dẫn chạy.

---

## 2. Thay đổi so với v2

| Điểm | v2 | v3 (sau Phase 1 + đọc đề) |
|---|---|---|
| OData | ❌ Chưa có | ✅ 2 endpoint (`/odata/Jobs`, `/odata/Applications`), verified |
| XML content negotiation | ❌ Chưa có | ✅ `/api/xml-demo/jobs` JSON↔XML, verified |
| Service communication | ❌ Chưa có | ✅ gRPC `RecruitPro.ScoringService` + API gọi, verified round-trip |
| Tài khoản mẫu | "chưa rõ password" | ✅ **Đã biết: `Password@123`** cho mọi account (init.sql dùng `crypt('Password@123',...)`) |
| SQL Server | nêu (cần xác nhận) | ❌ **Xác nhận mismatch** theo đúng câu chữ đề bài |
| ERD / Postman | nêu thiếu | ❌ Xác nhận thiếu (đã search kỹ) |
| Business rules | "rải rác, cần gom" | ✅ **Liệt kê 10 rule cụ thể** (xem §7) — dùng cho doc nộp |

---

## 3. Bảng đối chiếu đề bài (Requirement Compliance)

### 3.1 Yêu cầu nghiệp vụ chung

| Requirement (đề) | Trạng thái | Evidence |
|---|---|---|
| ≥3 vai trò | ✅ | 5 role: `RoleNames.cs:10-14` (Candidate/HR/Manager/HeadDepartment/SystemAdmin), seed `init.sql:934-938` |
| ≥5 entity | ✅ | **34 DbSet** `AppDbContext.cs:22-88` |
| Quan hệ 1-n | ✅ | Job→Applications `AppDbContext.cs:140-142` |
| n-n hoặc junction có thuộc tính | ✅ | `JobSkill` PK(JobId,SkillId) + `IsRequired`, `MinYearsExperience` (`AppDbContext.cs:866-875`); `CandidateSkill`+`YearsOfExperience` |
| Workflow có trạng thái | ✅ | `ApplicationStatusWorkflow.cs:7-25` (9 trạng thái) + `InterviewWorkflow.cs` |
| ≥5 business rule | ✅ | **10 rule** (xem §7), evidence `ApplicationService.cs`, `OfferService.cs`, workflow |
| CRUD / tìm kiếm / lọc / phân trang / thống kê | ✅ | `JobQueryRequest.cs` + `JobRepository` (keyword/filter/sort/paging), `PaginatedResponseDto<T>` |
| Chức năng user (gửi request, xem status, cập nhật, lịch sử) | ✅ | apply/withdraw/accept-offer, profile, dashboard, applications history |
| ≥2 báo cáo/thống kê | ✅ | 4: `api/manager/reports/recruitment-analytics`, `api/{hr,manager,candidate}/dashboard` |

### 3.2 Kiến trúc

| Requirement | Trạng thái | Evidence |
|---|---|---|
| Nhiều tầng, trách nhiệm rõ | ✅ | 5 project onion: Domain(∅ ref) → Application → Infrastructure → API; deps hướng vào trong |
| Controller mỏng, không chứa business logic | ✅ | 22 controller đều mỏng; lớn nhất `CandidateController` 183 dòng; mẫu `var result = await _service.X(); return StatusCode(result.StatusCode, result)` |
| Tài liệu service design | ✅ | `docs/services/**`, `docs/overview/03-modules.md` |

> ⚠️ Nuance (nhỏ): 2 OData controller inject thẳng `AppDbContext` (bỏ qua service/repo) — cố ý để demo, có comment `ponytail:` (`OData/JobsController.cs:24`). Chỉ ảnh hưởng 2 endpoint read `/odata/*`; toàn bộ REST chính vẫn đi API→Service→Repository.
>
> ⚠️ Chất lượng (không phải lỗi đề): vài "god-service" lớn (CandidateService 3694, CopilotService 2513, ApplicationService 1940, JobService 1320 dòng) — nên tách nếu còn thời gian; đề cảnh báo "đừng dồn hết vào một service".

### 3.3 Database & EF Core

| Requirement | Trạng thái | Evidence |
|---|---|---|
| Dùng EF Core | ✅ | EF Core 8, `AppDbContext` |
| **… với SQL Server** | ❌ **FAIL** | `Program.cs:86` `UseNpgsql(...)`; `Infrastructure.csproj:40` `Npgsql.EntityFrameworkCore.PostgreSQL`; `appsettings.json:9` Postgres conn; `docker-compose.yml` `postgres:18`. **Không có `UseSqlServer` ở đâu.** |
| ≥5 entity, PK/FK/quan hệ | ✅ | 34 entity, cấu hình Fluent trong `OnModelCreating` |
| Dữ liệu mẫu | ✅ | `init.sql` + seeders |
| Migration hoặc script | ✅ | 8 EF migration + `init.sql` |
| Connection string trong appsettings | ✅ | `appsettings.json:9` (`Mycnn`) |
| Data Annotation hoặc Fluent API | ✅ | Fluent cho model (entity là POCO thuần); Data Annotation `[Required]/[EmailAddress]` trên request DTO + FluentValidation |
| **Nộp ERD** | ❌ **FAIL** | Không có `.drawio/.puml/.dbml/erDiagram`; `docs/overview/04-database.md` chỉ mô tả prose; `docs/diagrams/*` là flow chứ không phải ERD |

### 3.4 RESTful API

| Requirement | Trạng thái | Evidence |
|---|---|---|
| CRUD entity chính | ✅ | Jobs/Candidates/Applications/Interviews/Offers |
| API workflow | ✅ | decision, apply, withdraw, offer send, interview status |
| Tìm kiếm/lọc/sắp xếp/phân trang | ✅ | `GET /api/jobs` + `JobQueryRequest`; `GET /api/hr/candidates` |
| API thống kê/báo cáo | ✅ | analytics + 3 dashboard |
| Status code (200/201/400/401/403/404) | ✅ | `ApiResponse.cs` factory đầy đủ; **201 Created thật** ở `JobService.cs:653`, `ApplicationService.cs:247`, `InterviewService.cs:244`… + 409/422/204 |
| DTO request/response | ✅ | mọi controller trả `ApiResponse<...Dto>` |
| Không lộ dữ liệu nhạy cảm | ✅ | grep Response DTO: 0 `PasswordHash`; entity không trả trực tiếp |

### 3.5 OData (đề: ≥2 API)

| Requirement | Trạng thái | Evidence |
|---|---|---|
| ≥2 API OData ($filter/$orderby/$top/$skip/$select) | ✅ | `Microsoft.AspNetCore.OData 8.2.5`; `AddOData` `Program.cs:39-41`; **`GET /odata/Jobs`** (public, `OData/JobsController.cs`) + **`GET /odata/Applications`** (`[Authorize(Roles="HR,Manager")]`) `[EnableQuery]` |

### 3.6 Media Formatter & Content Negotiation

| Requirement | Trạng thái | Evidence |
|---|---|---|
| Trả JSON | ✅ | mặc định System.Text.Json |
| Trả XML khi `Accept: application/xml` | ✅ | `AddXmlDataContractSerializerFormatters` + `RespectBrowserAcceptHeader` (`Program.cs:35,45`); **`GET /api/xml-demo/jobs`** verified JSON↔XML |
| `[Consumes]` hoặc `[Produces]` ≥1 API | ✅ | `[Produces("application/json","application/xml")]` `XmlDemoController.cs:14`; `[Consumes("multipart/form-data")]` `CandidateController.cs:163` |

### 3.7 Security

| Requirement | Trạng thái | Evidence |
|---|---|---|
| Đăng ký/tạo tài khoản | ✅ | `POST api/candidates/register` (`CandidateController.cs:22`) |
| Đăng nhập | ✅ | `POST api/auth/login` (+candidate/internal) `AuthController.cs:20-39` |
| JWT | ✅ | `JwtExtension.cs:14-64`, `JwtService.cs:31-79` |
| Phân quyền theo role, ≥3 role | ✅ | 5 role enforced qua `[Authorize(Roles=...)]` |
| API chỉ Admin | ✅ | `SysAdminAiOpsController.cs:14` `[Authorize(Roles="SystemAdmin")]` (+Automation/Mcp) |
| API chỉ Staff | ✅ | `JobController.cs:63` `[Authorize(Roles="HR,Manager")]`; `GET api/hr/candidates` |
| User chỉ thao tác dữ liệu của mình | ✅ | ownership `ApplicationService.cs:1205-1220` (404 nếu khác owner) + `OwnershipScope` |
| Password hash (không plaintext) | ✅ | BCrypt `CandidateService.cs:158`, verify `AuthService.cs:118`; không hash trong response |
| Nộp security matrix | ✅ | `docs/overview/09-auth.md` + RBAC grants `init.sql:944-1007` (27 permission); bảng chi tiết trong audit v2 §5 |

### 3.8 Client (JavaScript)

| Requirement | Trạng thái | Evidence (frontend `recruit-pro-internal/src`) |
|---|---|---|
| Màn hình đăng nhập | ✅ | `pages/public/CandidateLoginScreen.tsx`, `pages/internal/InternalLoginScreen.tsx` |
| Lưu JWT sau đăng nhập | ✅ | `store/slices/authSlice.ts:69-89` (localStorage) |
| Gửi `Authorization: Bearer` | ✅ | interceptor `services/http/api-client.ts:20-28` |
| Màn hình danh sách | ✅ | `pages/candidate/MyApplicationScreen.tsx`, `pages/hr/JobManagementScreen.tsx` |
| Thêm/cập nhật | ✅ | `jobsService.ts:665-680` create/update job; update profile |
| Gửi request nghiệp vụ | ✅ | apply `jobsService.ts:605-613`, withdraw `candidateService.ts:435-437` |
| Xử lý lỗi 401/403/404/400 | ✅ | `common/utils/apiError.ts:329-367` + force-logout 401 `api-client.ts:45-49` |

> React + TypeScript (không phải JS thuần) nhưng vẫn là client trình duyệt gọi API — thoả yêu cầu.

### 3.9 gRPC / WCF / Microservice (đề: chọn 1)

| Requirement | Trạng thái | Evidence |
|---|---|---|
| Thành phần giao tiếp giữa service (khuyến nghị gRPC) | ✅ | **gRPC**: project riêng `RecruitPro.ScoringService` (`Protos/scoring.proto`, `ScorerService`), API gọi qua `AddGrpcClient` (`Program.cs:134-137`) tại `ExternalScoreController.cs` (`GET /api/hr/applications/{id}/external-score`); `docker-compose.yml` service `scoring`. Call chain verified end-to-end. |

---

## 4. Điểm CHƯA ĐẠT bắt buộc (chi tiết + cách xử lý)

### ❌ 4.1 SQL Server (đề bắt buộc)
Đề: *"Project phải sử dụng Entity Framework Core với SQL Server."* → dự án dùng **PostgreSQL**.
- **Lựa chọn A — xin xác nhận giảng viên** cho dùng Postgres (nhanh nhất, rủi ro phụ thuộc quyết định của giảng viên). Nên có **văn bản/email xác nhận** để trình lúc chấm.
- **Lựa chọn B — migrate SQL Server**: đổi `UseNpgsql`→`UseSqlServer` + package `Microsoft.EntityFrameworkCore.SqlServer`; đổi connection string. **Việc nặng nhất** là `init.sql` (Postgres-specific: `gen_random_uuid()`, `pgcrypto`, native enum, snake_case) → phải viết lại DDL T-SQL **hoặc** bỏ init.sql và dùng `dotnet ef database update` (8 migration) cho SQL Server + seeder. Cần retest toàn bộ. Ước lượng: 0.5–1.5 ngày.

### ❌ 4.2 ERD (đề bắt buộc nộp)
Chưa có ERD dạng sơ đồ. Có sẵn schema đầy đủ (`init.sql` + migrations) → dựng ERD nhanh:
- Dùng **dbdiagram.io** / **mermaid `erDiagram`** / DBeaver reverse-engineer từ DB đang chạy. Chỉ cần thể hiện bảng + PK/FK + bội số quan hệ. Ước lượng: 1–3 giờ.

### ❌ 4.3 Postman collection (đề: "API testing file")
Chưa có `*.postman_collection.json`. Hiện chỉ có xUnit integration test.
- Tạo collection cho các flow chính (login → apply → screening → offer; OData; XML; external-score). Có thể export từ Swagger. Ước lượng: 1–3 giờ.

### ⚠️ 4.4 Cần gom (không phải thiếu, chỉ chưa gọn)
- **Bảng tài khoản mẫu + mật khẩu**: đã biết (§8) — đưa vào 1 doc nộp.
- **Hướng dẫn chạy hợp nhất**: hiện rải ở `DEPLOYMENT.md` + `docs/overview/10-phase1-required-features.md` + `docker-compose.yml`; nên gom 1 README gốc.
- **Use-case section**: chỉ có functional req trong BRD/SRD; nên thêm 1 mục use-case theo vai trò.

---

## 5. Điểm MẠNH (vượt yêu cầu đề)

Đề có mục "yêu cầu nâng cao khuyến khích" — dự án đạt phần lớn:

| Bonus (đề) | Trạng thái | Evidence |
|---|---|---|
| Global exception handling | ✅ | `ExceptionMiddleware.cs` (`Program.cs:178`) |
| Response wrapper / Result pattern | ✅ | `ApiResponse.cs` (envelope + ErrorCode) |
| Pagination chuẩn hoá | ✅ | `PaginationMetaBuilder.cs`, `PaginatedResponseDto<T>` |
| AutoMapper | ✅ | `Program.cs:165` + `Mappings/*Profile.cs` |
| FluentValidation | ✅ | `Program.cs:67` + 11 validator |
| Repository + Unit of Work | ✅ | `IUnitOfWork` + `UnitOfWork.cs` + ~12 repo |
| Email/notification giả lập | ✅ | `LoggingEmailService.cs` + `NotificationService` (in-app + SSE) |
| Dashboard thống kê | ✅ | `DashboardController` + per-role DTO |
| Docker compose (DB + service) | ✅ | `docker-compose.yml` (backend+scoring+postgres+minio) |
| Audit log | ✅ (phạm vi AI) | `mcp_tool_audits` + `McpToolAuditService` (chỉ cho AI/MCP tool, chưa phải audit entity tổng quát) |
| Refresh token | ⚠️ Một phần | Có entity + phát refresh JWT, **nhưng không có endpoint `/refresh`** → chưa đổi/rotate được (client 401 là logout) |
| Soft delete | ⚠️ Một phần | Chỉ `CopilotSavedRule` có `IsDeleted`; chưa có global query filter |
| Serilog | ❌ | Dùng logging mặc định của .NET |

**Điểm mạnh khác không nằm trong đề:** AI Copilot (CV ranking), semantic search, SSE realtime notification, workflow automation engine — dự án ở tầm production, vượt xa mức "project cuối kỳ".

---

## 6. (Từ v2) Lưu ý bảo mật/chất lượng — không phải requirement đề nhưng nên xử lý

- **Secret bị commit** trong `appsettings.json` (JWT key, DB password, MinIO key) + `appsettings.Development.json`. Đề chỉ yêu cầu "password phải hash" (đã đạt), nhưng nên rotate + đưa secret ra env/User Secrets trước khi nộp public. (`appsettings.Production.json` đã là placeholder — an toàn.)
- **Docs lệch code** (từ v2): docs cũ nhắc `refresh-token`/`me`/`logout` và "permission-based toàn hệ thống" — nên sửa cho khớp (permission thật chỉ ở khu SysAdmin; refresh chưa có endpoint).

---

## 7. Business Rules (≥5 — dùng cho doc nộp)

Đề yêu cầu ≥5 business rule rõ ràng. Dưới đây **10 rule thật** (đã verify `file:line`):

1. **Chống ứng tuyển trùng (active)** — `ApplicationService.cs:147-158`: có application active → 409; DB partial unique index `ux_applications_active_user_job` (`AppDbContext.cs:171-173`).
2. **Guard chuyển trạng thái** — `ApplicationService.cs:663-666`: chỉ cho phép transition trong `ApplicationStatusWorkflow` (`:7-25`), sai → 422.
3. **Job phải Approved + chưa hết hạn khi apply** — `ApplicationService.cs:996-1004`.
4. **Offer/Reject không đi qua decision endpoint** (email-gated) — `ApplicationService.cs:653-661`.
5. **Phải có interview Completed trước khi Offer** — `OfferService.cs:141-150` + `InterviewWorkflow.cs:29-43`.
6. **Gửi email từ chối TRƯỚC khi đổi status Rejected** — `ApplicationService.cs:810-831` (email fail → giữ nguyên).
7. **Quyết định ManagerReview thuộc DepartmentHead được gán** — `ApplicationService.cs:672-680` + `GuardManagerReviewDecisionAsync:1091-1114` (sai → 403).
8. **Candidate chỉ withdraw ở trạng thái active trước offer** — `ApplicationStatusWorkflow.cs:68-74`; withdraw set `Withdrawn`, không phải `Rejected`.
9. **Accept/Decline offer là của candidate + offer phải `Sent`** — `ApplicationService.cs:428-437,475-484`.
10. **`Hired` là terminal cho 1 job; Rejected/OfferDeclined/Withdrawn được apply lại** — `ApplicationService.cs:1024-1027` + `ApplicationStatusWorkflow.cs:61-66`.

*(Bonus: rời pipeline sẽ huỷ interview đang Scheduled — `ApplicationService.cs:1054-1063`.)*

---

## 8. Tài khoản mẫu (đề bắt buộc ghi rõ)

**Mật khẩu chung cho MỌI tài khoản: `Password@123`** (đăng nhập bằng **username**). Nguồn: `init.sql:1079-1143` dùng `crypt('Password@123', gen_salt('bf',6))`; xác nhận ở `db/patches/20260704-refresh-demo-seed.sql:25`.

| Vai trò | Username demo | Mật khẩu |
|---|---|---|
| Candidate | `nhatquang` | `Password@123` |
| HR | `thucuyen` | `Password@123` |
| Manager | `tiendat` (hoặc `quocbao`) | `Password@123` |
| HeadDepartment | `tiendat` | `Password@123` |
| SystemAdmin | `admin` (hoặc `minhkhoi`) | `Password@123` |

*(HR khác: giahan, khanhlinh.pham, haiyen · Manager: minhkhang · nhiều Candidate khác trong seed.)*

---

## 9. Fix Plan (ưu tiên trước khi nộp)

| # | Việc | Loại | Ước lượng | Ghi chú |
|---|---|---|---|---|
| 1 | **Xác nhận SQL Server** với giảng viên; nếu bắt buộc → migrate | ❌ bắt buộc | 5' hỏi / 0.5–1.5 ngày migrate | Blocker rõ nhất theo câu chữ đề |
| 2 | **Tạo ERD** (dbdiagram/mermaid từ schema có sẵn) | ❌ bắt buộc | 1–3h | Nộp kèm |
| 3 | **Postman collection** các flow chính | ❌ bắt buộc | 1–3h | "API testing file" |
| 4 | **Doc tài khoản mẫu** (§8) + **README hợp nhất** hướng dẫn chạy | ⚠️ gom | 1–2h | Nội dung đã có |
| 5 | Rotate & tách secret khỏi repo | 🔒 nên | 1h | Không phải req đề nhưng nên |
| 6 | Sửa docs lệch code (refresh/me/logout, permission wording) | ⚠️ nên | 1–2h | Từ v2 |
| 7 | (tuỳ) Thêm endpoint `/refresh`, hoặc bỏ refresh khỏi docs | bonus | 2h | Cho nhất quán |
| 8 | (tuỳ) Tách god-service, thêm Serilog | bonus | — | Nâng chất lượng |

---

## 10. Final Submission Checklist (map "Sản phẩm cần nộp" của đề)

- [x] Source code đầy đủ
- [x] Database: migration + `init.sql` + dữ liệu mẫu
- [x] EF Core · [ ] **SQL Server** (đang Postgres — cần xác nhận/migrate)
- [x] Client app (React) gọi API
- [x] gRPC service phụ (`RecruitPro.ScoringService`)
- [x] OData demo · [x] XML content negotiation demo · [x] JWT · [x] JS client demo
- [x] Security matrix (`09-auth.md`)
- [x] Business rules (§7, ≥5) · [x] Workflow · [x] Architecture · [x] Service design · [x] API list
- [ ] **ERD**
- [ ] **Postman collection**
- [x] Tài khoản mẫu (đã biết §8) — [ ] gom vào 1 doc nộp
- [x] Hướng dẫn chạy (rải rác) — [ ] gom 1 README
- [ ] (nên) Xoá secret khỏi repo · [ ] (nên) đồng bộ docs

**Tổng:** đạt gần hết. Còn **SQL Server (xác nhận/migrate), ERD, Postman** là bắt buộc; phần còn lại là gom tài liệu.

---

## Appendix — Phương pháp & phạm vi

- **Nguồn requirement:** đề bài `02_06_2026___...docx` (PRN232 - ChiLP) — trích text từ `word/document.xml`.
- **Verify:** 1 workflow 5 agent song song đối chiếu từng cluster requirement với source (domain/rules · architecture/REST · OData/XML/gRPC/DB · security/client · deliverables/seed/bonus). 355k token, 137 tool calls, 0 lỗi.
- **File chính đã đọc:** `RecruitProInternal/{Program.cs, RecruitPro.API/Controllers/*, RecruitPro.Application/Services/*, Domain/{Entities,Enums,Workflows,Constants}/*, Infrastructure/Data/AppDbContext.cs, *.csproj, init.sql, db/patches/*}`; frontend `recruit-pro-internal/src/{pages,services,store,common}/*`; `docs/**`.
- Line number theo thời điểm audit; symbol name là mốc ổn định.
