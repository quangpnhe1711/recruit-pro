# PRN232 Project Audit Report

## 1. Executive Summary

- Diem manh chinh:
  - Solution duoc tach thanh `RecruitPro.API`, `RecruitPro.Application`, `RecruitPro.Infrastructure`, `RecruitPro.Domain`, `RecruitPro.Tests`.
  - Co domain nghiep vu tuyen dung ro rang: candidate, jobs, applications, interviews, offers, notifications.
  - Co migration, `init.sql`, du lieu seed demo, frontend React, test project, va build backend thanh cong.
- Rui ro lon nhat:
  - Chua dat mot so requirement bat buoc de nop an toan: khong co OData, khong co XML content negotiation, khong co service communication gRPC/WCF/microservice.
  - Co nhieu API noi bo/thao tac nhay cam dang public hoac thieu `[Authorize]`.
  - Backend dang dung PostgreSQL/Npgsql thay vi SQL Server theo yeu cau audit.
  - Tai lieu mo ta mot so feature auth/RBAC khong khop code thuc te.
- Ket luan: `Not ready to submit`.

## 2. Requirement Compliance Matrix

| Requirement | Required | Current status | Evidence | Gap | Priority |
|---|---|---|---|---|---|
| It nhat 3 roles | Yes | Da co | `Candidate/HR/Manager/SystemAdmin/HeadDepartment` trong `init.sql` | `SystemAdmin` chua the hien ro thanh feature backend rieng | P2 |
| It nhat 5 entity chinh | Yes | Da co | `AppDbContext` co nhieu `DbSet` | Dat | P3 |
| Co quan he 1-n | Yes | Da co | `User -> Applications`, `Job -> Applications`, `CandidateProfile -> Resumes/Projects/Sections` | Dat | P3 |
| Co quan he n-n hoac bang trung gian co thuoc tinh | Yes | Da co | `UserRole`, `RolePermission`, `JobSkill`, `CandidateSkill`, `ApplicationOfferBenefit` | Dat | P3 |
| Co workflow co trang thai | Yes | Da co | `ApplicationStatusWorkflow` | Dat | P3 |
| It nhat 5 business rules ro rang | Yes | Co nhung chua duoc gom/chot trong tai lieu nop | Rules trong `ApplicationService`, `InterviewService`, `OfferService`, `AuthService` | Can viet thanh section business rules ro rang de nop | P2 |
| Chuc nang quan tri: CRUD, tim kiem, loc, phan trang, thong ke | Yes | Co nhung security chua dat | Jobs, candidates, applications, interviews, dashboard, analytics | Nhieu route internal chua duoc khoa dung | P1 |
| Chuc nang user: gui request, xem status, cap nhat thong tin, xem lich su | Yes | Da co | Candidate register/profile/applications/interviews/dashboard | Dat | P3 |
| It nhat 2 bao cao/thong ke | Yes | Da co | HR dashboard, manager dashboard, recruitment analytics, job statistics | Dat | P3 |
| EF Core voi SQL Server | Yes | Chua co | `Program.cs` dung `UseNpgsql`, `RecruitPro.Infrastructure.csproj` dung `Npgsql.EntityFrameworkCore.PostgreSQL` | Sai DB engine theo requirement | P1 |
| Migration hoac SQL script tao DB | Yes | Da co | `RecruitPro.Infrastructure/Migrations`, `init.sql` | Dat | P3 |
| Seed/sample data de demo | Yes | Da co | `init.sql`, `TestDataSeeder` | Dat | P3 |
| OData it nhat 2 API | Yes | Chua co | Khong thay package/config/endpoint OData | Thieu requirement bat buoc | P1 |
| XML formatter/content negotiation | Yes | Chua co | `Program.cs` chi `AddControllers` | Thieu requirement bat buoc | P1 |
| JWT auth + role-based auth | Yes | Co nhung chua dat | `JwtExtension`, `JwtService`, mot so controller co `[Authorize]` | Nhieu route noi bo chua duoc bao ve | P0 |
| It nhat 3 role co phan quyen | Yes | Co | Candidate/HR/Manager/SystemAdmin | Enforcement backend chua dong deu | P1 |
| User chi thao tac du lieu cua minh | Yes | Co o mot so flow | Candidate profile/applications/resume stream co ownership check | Internal/public route van co lo hong auth | P1 |
| Client luu JWT va gui Bearer | Yes | Da co | `authSlice.ts`, `api-client.ts` | Refresh/logout chua khop backend | P2 |
| Co service communication phu | Yes | Chua co | Repo khong co project gRPC/WCF/service phu | SignalR/AI provider khong thay the requirement nay | P1 |
| Tai lieu nop kem day du | Yes | Co nhieu docs nhung chua dat | `docs/overview`, `docs/ai-rules`, `docs/ai-roadmap` | Thieu OData/XML/service demo, sample accounts ro rang, docs lech code | P1 |

## 3. Critical Issues

### P0

1. Internal va nhay cam APIs dang public hoac thieu `[Authorize]`.
   - Vi sao nguy hiem:
     - Co the bi chuyen trang thai job, xem dashboard noi bo, thao tac interview/offer/AI flows ma khong can token dung.
     - Rat de bi tru diem security va RBAC.
   - File lien quan:
     - `RecruitPro.API/Controllers/JobController.cs`
     - `RecruitPro.API/Controllers/DashboardController.cs`
     - `RecruitPro.API/Controllers/InterviewController.cs`
     - `RecruitPro.API/Controllers/OfferController.cs`
     - `RecruitPro.API/Controllers/CopilotController.cs`
     - `RecruitPro.API/Controllers/SemanticDiscoveryController.cs`
   - Cach sua cu the:
     - Gan `[Authorize]`/`[Authorize(Roles = "...")]` cho tung route.
     - Retest day du `401`, `403`, ownership, va matrix role.

### P1

1. Database engine khong khop requirement audit.
   - Hien tai dung PostgreSQL thay vi SQL Server.
   - Evidence:
     - `RecruitPro.API/Program.cs`
     - `RecruitPro.Infrastructure/RecruitPro.Infrastructure.csproj`
     - `init.sql`
   - Cach sua:
     - Neu requirement mon hoc bat buoc SQL Server, can migrate provider va script.
     - Neu giang vien cho phep DB khac, can xac nhan bang van ban/brief.

2. Khong co OData.
   - Khong thay `Microsoft.AspNetCore.OData`, `EnableQuery`, EDM model, hoac endpoint OData.
   - Cach sua:
     - Them OData va expose it nhat 2 endpoint an toan cho query.

3. Khong co XML formatter/content negotiation.
   - Khong thay `AddXmlSerializerFormatters` hay endpoint demo XML.
   - Cach sua:
     - Cau hinh XML output formatter.
     - Chuan bi request demo `Accept: application/xml`.

4. Khong co gRPC/WCF/microservice simulation dung nghia requirement.
   - `docker-compose.yml` chi co Postgres va MinIO.
   - Cach sua:
     - Tao service phu don gian, API chinh goi that, co config va huong dan run.

5. Docs lech code thuc te.
   - Docs noi co `refresh-token`, `me`, `logout`, permission-based authorization trung tam, nhung backend chua co day du.
   - File:
     - `docs/overview/RecruitPro-SRD.md`
     - `docs/overview/05-api.md`
     - `RecruitPro.API/Controllers/AuthController.cs`
   - Cach sua:
     - Hoac implement feature that, hoac sua docs cho trung code.

6. Secrets nhay cam bi commit trong source.
   - Evidence:
     - `RecruitPro.API/appsettings.json`
     - `RecruitPro.API/appsettings.Production.json`
     - `.env.railway.example`
   - Cach sua:
     - Rotate secrets, dua ve env vars/user secrets, xoa khoi repo.

## 4. Architecture Review

| Layer | Trang thai | Van de | File lien quan | Muc do nghiem trong | Cach sua |
|---|---|---|---|---|---|
| API Layer | Co nhung chua dat | Controllers kha mong nhung auth gan khong dong deu | `RecruitPro.API/Controllers/*` | P0 | Chuan hoa `[Authorize]`, role matrix, status code tests |
| Service Layer | Co nhung qua to | `CandidateService` ~3600 dong, `ApplicationService` ~1442 dong, `JobService` ~1033 dong | `RecruitPro.Application/Services/*` | P2 | Tach use case/service nho hon |
| Data Access Layer | Dat | Repo + UoW ro rang | `RecruitPro.Infrastructure/Repositories/*` | P3 | Giu nguyen, bo sung tests sau khi fix env |
| Domain/Model Layer | Dat | Co entity, enum, workflow | `RecruitPro.Domain/*` | P3 | Giu nguyen |
| DTO Layer | Dat | Co DTO request/response, khong thay tra entity truc tiep | `RecruitPro.Application/DTOs/*` | P3 | Giu nguyen |
| Infrastructure | Co nhung chua dat | Co DB/JWT/AI/MinIO, nhung khong co service communication bat buoc | `RecruitPro.Infrastructure/*` | P1 | Them service phu dung requirement |

## 5. API Review

### Danh sach endpoint chinh xac nhan duoc tu source

- Auth:
  - `POST /api/auth/login`
  - `POST /api/auth/candidate/login`
  - `POST /api/auth/internal/login`
  - `POST /api/auth/candidate/forgot-password`
  - `POST /api/auth/internal/forgot-password`
- Candidate:
  - `POST /api/candidates/register`
  - `GET /api/candidate/profile`
  - `PUT /api/candidate/profile`
  - `POST /api/candidate/profile/save`
  - `PUT /api/candidate/profile/skills`
  - `POST|PUT|DELETE /api/candidate/profile/experience...`
  - `POST /api/candidate/profile/resume/parse`
  - `POST /api/candidate/profile/resume`
- Applications:
  - `GET /api/jobs/{jobId}/apply-context`
  - `POST /api/jobs/{jobId}/apply`
  - `GET /api/candidate/applications`
  - `POST /api/candidate/applications/{applicationId}/withdraw`
  - `POST /api/candidate/applications/{applicationId}/accept-offer`
  - `POST /api/candidate/applications/{applicationId}/decline-offer`
  - `GET /api/hr/applications`
  - `GET /api/hr/applications/{applicationId}`
  - `PATCH /api/hr/applications/{applicationId}/decision`
  - `GET /api/hr/applications/{applicationId}/cv`
  - `POST /api/hr/applications/{applicationId}/send-email`
- Jobs:
  - `GET /api/jobs`
  - `GET /api/jobs/filters`
  - `GET /api/jobs/{jobId}`
  - `GET /api/jobs/{jobId}/statistics`
  - `PATCH /api/jobs/{jobId}/status`
  - `GET|POST|PATCH|DELETE /api/hr/jobs...`
  - `GET /api/manager/jobs/approval-queue`
  - `GET /api/manager/jobs/{jobId}/approval-detail`
- Dashboards/Analytics:
  - `GET /api/candidate/dashboard`
  - `GET /api/hr/dashboard`
  - `GET /api/manager/dashboard`
  - `GET /api/manager/reports/recruitment-analytics`
- Interviews:
  - `GET /api/hr/interviews`
  - `GET /api/candidate/interviews`
  - `GET /api/hr/interviews/schedule-data`
  - `POST /api/hr/interviews`
  - `PATCH /api/hr/interviews/{interviewId}/status`
  - `DELETE /api/hr/interviews/{interviewId}`
- Notifications:
  - `GET /api/notifications`
  - `GET /api/notifications/unread-count`
  - `PATCH /api/notifications/{notificationId}/read`
  - `PATCH /api/notifications/read-all`

### Van de REST/API chinh

- `PATCH /api/jobs/{jobId}/status` dang public, sai ban chat security.
- Mot so route noi bo khong auth nhung goi `User.GetCurrentUserId()`, de phat sinh loi khong can thiet.
- Docs va frontend contract dang ky vong `logout/refresh/me`, nhung backend auth chua co.
- DTOs duoc dung kha tot, khong thay tra thang entity hay `PasswordHash`.

## 6. Security Review

### JWT, role, password hash

- Co JWT authentication: `JwtExtension`, `JwtService`.
- Role claim duoc map tu `user.UserRoles`.
- Password duoc hash bang BCrypt trong `AuthService`.
- Khong thay tra `PasswordHash` trong response DTOs.

### Van de security chinh

- Route protection khong dong deu, day la lo hong lon nhat.
- Docs noi permission-based auth, nhung backend hien tai chu yeu la role-based va khong enforce permission matrix trong code.
- `SystemAdmin` xuat hien trong du lieu/docs nhung gan nhu khong co enforcement rieng tren controller.
- Secrets dang commit trong repo.

### Security Matrix tom tat

| Feature/API | Admin | Staff/HR | User | Code hien tai | Dat/chua dat | Ghi chu |
|---|---|---|---|---|---|---|
| Candidate profile own | Khong can | Khong | Co | Dung `Candidate` + `userId` token | Dat | Ownership on |
| Candidate application own actions | Khong can | Khong | Co | Service check owner application | Dat | Tot |
| HR candidates/applications | Nen co | Co | Khong | Co mot phan `[Authorize(Roles=\"HR,Manager\")]` | Co nhung chua dat | Chua cover `SystemAdmin` |
| Dashboards noi bo | Nen co | Co | Khong | Dang public | Chua dat | P0 |
| Interview management | Nen co | Co | Candidate chi xem cua minh | HR routes dang public | Chua dat | P0 |
| Offer management | Nen co | Co | Candidate chi phan hoi offer cua minh | HR offer routes dang public | Chua dat | P0 |
| Copilot/Semantic discovery | Nen co | Co | Khong | Dang public | Chua dat | P0 |

## 7. Database Review

### Dat

- Dung EF Core.
- Co `DbContext` ro rang.
- Co hon 5 entity.
- Co PK, FK, relationships.
- Co migrations.
- Co `init.sql` seed data.
- Co connection string trong `appsettings`.

### Chua dat/Can luu y

- Database engine la PostgreSQL, khong phai SQL Server theo requirement.
- Chua thay ERD dung nghia de nop; `docs/diagrams/README.md` moi la placeholder.
- Docs status model trong SRD khong khop enum/workflow hien tai.
- Script va config chua an toan vi chua tach secrets.

## 8. OData & Content Negotiation Review

### OData

- Trang thai: `Chua dat`.
- Evidence:
  - Khong thay package OData trong `.csproj`.
  - Khong thay config OData trong `Program.cs`.
  - Khong thay endpoint co `EnableQuery`.
- Rủi ro:
  - Thieu requirement bat buoc.
- Cach test neu bo sung:
  - `GET /api/odata/jobs?$filter=contains(Title,'Java')&$orderby=CreatedAt desc&$top=5&$skip=0`
  - `GET /api/odata/applications?$filter=Status eq 'Interview'&$select=Id,Status`

### Content Negotiation

- JSON response: `Co`.
- XML response: `Chua dat`.
- XML formatter: `Chua co`.
- `[Consumes]`:
  - Co it nhat 1 cho `multipart/form-data` trong `CandidateController`.
- `[Produces]`:
  - Chua thay demo dung yeu cau XML/JSON.

## 9. Client & Service Communication Review

### Client JWT/API handling

- Co login screen candidate va internal.
- Co luu `access_token`, `refresh_token`, `auth_user` trong `localStorage`.
- Axios interceptor gui `Authorization: Bearer <token>`.
- Co route guards.
- Co xu ly 401 bang force logout.

### Van de client

- `refreshToken` duoc luu nhung khong co refresh flow that.
- `logout` frontend la local-only mock, backend khong co endpoint that.
- Co mot so contract frontend phu thuoc vao API/docs lech hien trang.

### Service communication

- Co SignalR notification hub.
- Co external AI provider abstraction va MinIO.
- Nhung khong co project service phu/gRPC/WCF/microservice simulation dung requirement.

## 10. Documentation Review

| Tai lieu | Co/chua | File | Thieu gi | Can bo sung |
|---|---|---|---|---|
| Gioi thieu project, muc tieu, pham vi | Co | `docs/overview/RecruitPro-BRD.md`, `docs/overview/01-system-overview.md` | Can dong bo lai wording voi code | Update |
| Role va quyen | Co | `docs/ai-rules/rbac-matrix.md` | Enforcement backend chua match | Sua theo code that |
| Use case/workflow | Co | `docs/overview/06-workflows.md`, `RecruitPro-BRD.md` | Chua map acceptance criteria demo | Bo sung |
| DB schema/ERD | Co nhung yeu | `docs/overview/04-database.md`, `docs/diagrams/README.md` | Chua co ERD ro rang | Them ERD |
| API endpoint list | Co | `docs/overview/05-api.md`, `RecruitPro-SRD.md` | Lech code auth va mot so endpoint | Sua |
| Security matrix | Co | `docs/ai-rules/rbac-matrix.md` | Chua khop backend | Sua |
| OData demo | Chua |  | Thieu hoan toan | Bat buoc bo sung |
| Content negotiation demo | Chua |  | Thieu hoan toan | Bat buoc bo sung |
| gRPC/WCF/Microservice demo | Chua |  | Thieu hoan toan | Bat buoc bo sung |
| Huong dan chay backend/database/client | Co mot phan | root files + docs | Chua thanh checklist nop | Bo sung |
| Tai khoan mau tung role | Co user seed nhung khong ro password de demo | `init.sql` | Chua ro account/password final | Bo sung bang tai khoan demo |
| Postman collection | Chua xac nhan duoc |  | Chua thay noi bat trong repo | Them collection |

## 11. Fix Plan

### Phase 1: Fix de project chay va pass requirement bat buoc

| Task | File/Area | Why | Acceptance Criteria |
|---|---|---|---|
| Khoa tat ca internal APIs bang `[Authorize]` dung role | `RecruitPro.API/Controllers/*` | Tranh rot security/RBAC | Anonymous bi chan dung `401/403` |
| Bo hoac khoa route public `PATCH /api/jobs/{id}/status` | `JobController.cs` | Tranh privilege escalation | User thuong khong doi duoc status job |
| Quyết dinh lai DB engine theo requirement | API/Infra/DB | Requirement bat buoc | SQL Server hoac co xac nhan thay the |
| Bo sung OData it nhat 2 endpoint | API config + new controllers | Requirement bat buoc | Query duoc `$filter/$orderby/$top/$skip` |
| Bo sung XML formatter | `Program.cs` | Requirement bat buoc | Cung endpoint tra JSON/XML theo `Accept` |
| Them service phu simulation | New service project | Requirement bat buoc | API chinh goi duoc service phu that |

### Phase 2: Fix security/workflow/API correctness

| Task | File/Area | Why | Acceptance Criteria |
|---|---|---|---|
| Dong bo auth contract `refresh/me/logout` | Auth backend + frontend | Docs/client dang lech code | Contract auth thong nhat |
| Rotate va tach secrets khoi repo | `appsettings*`, `.env*` | Bao mat | Repo khong con secret that |
| Chuan hoa security matrix va role cover `SystemAdmin` | Controllers/services/docs | Role dang mo ta manh hon code | `SystemAdmin` co behavior ro |
| Tach service qua to neu kip | Application services | De review/bao tri | Service giam do monolithic |

### Phase 3: Polish demo, docs, Postman, advanced features

| Task | File/Area | Why | Acceptance Criteria |
|---|---|---|---|
| Viet docs nop cuoi cung khop source | `docs/overview`, `docs/audit` | Tranh chenh docs-code | Docs khong con feature “ao” |
| Bo sung ERD, OData/XML demo, service demo | `docs/diagrams`, `docs/overview` | Can cho luc bao ve | Demo checklist day du |
| Bo sung sample accounts va passwords demo | docs + seed note | Can de cham nhanh | Co bang tai khoan tung role |
| Them Postman collection | docs/postman | Can demo nhanh | Chay duoc main flows |

## 12. Final Submission Checklist

- [x] `dotnet build` thanh cong.
- [ ] `dotnet test` xanh hoan toan.
- [x] Co seed data demo.
- [ ] Co account/password demo ro rang cho tung role.
- [ ] Co OData demo.
- [ ] Co XML content negotiation demo.
- [ ] Co service phu demo duoc.
- [ ] Security matrix khop backend thuc te.
- [ ] Docs khop hien trang code.
- [ ] Khong con secrets nhay cam trong repo.

## Appendix: Verification Notes

- Build:
  - `dotnet build RecruitProInternal.sln --no-restore` thanh cong, co warning package va nullable warnings.
- Tests:
  - `dotnet test RecruitProInternal.sln` cho ket qua `93 passed, 26 failed`.
  - Nhieu fail hien tai den tu Docker/Testcontainers khong san sang trong moi truong audit, bat dau tu `RecruitPro.Tests/Infrastructure/PostgresTestFixture.cs`.
