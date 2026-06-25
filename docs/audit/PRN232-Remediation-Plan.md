# PRN232 Remediation Plan

## Muc tieu

Tai lieu nay tach rieng cac viec can sua sau audit de team co the di tung phase, uu tien theo muc do anh huong den kha nang nop va diem cham.

## Phase 1: Submission Blockers

Muc tieu: xu ly cac van de P0/P1 khien project chua du dieu kien nop an toan.

| Priority | Task | File/Area | Why | Acceptance Criteria |
|---|---|---|---|---|
| P0 | Khoa tat ca internal APIs | `RecruitPro.API/Controllers` | Hien co nhieu route noi bo dang public | Tat ca route internal tra `401/403` dung |
| P0 | Khoa `PATCH /api/jobs/{jobId}/status` | `JobController.cs` | Dang la lo hong privilege escalation | Khong con anonymous/public status update |
| P1 | Chot DB engine theo requirement | API/Infra/DB | Requirement audit ghi SQL Server | Hoac migrate SQL Server, hoac co xac nhan thay the |
| P1 | Them OData cho it nhat 2 APIs | API config + controllers | Requirement bat buoc | Chay duoc `$filter`, `$orderby`, `$top`, `$skip` |
| P1 | Them XML formatter | `Program.cs` | Requirement bat buoc | XML response hoat dong voi `Accept: application/xml` |
| P1 | Tao service communication phu | New project/service | Requirement bat buoc | API chinh goi duoc service phu that |
| P1 | Xoa secrets that khoi repo | `appsettings*`, `.env*` | Risk bao mat va bi tru diem | Config dung env vars, secret da rotate |

## Phase 2: API + Security Correctness

Muc tieu: dua project ve dung ban chat nghiep vu va contract.

| Priority | Task | File/Area | Why | Acceptance Criteria |
|---|---|---|---|---|
| P1 | Dong bo auth contract | `AuthController`, `IAuthService`, frontend auth service | Docs/frontend dang ky vong `refresh/me/logout` | Contract auth thong nhat, test duoc |
| P1 | Chuan hoa role matrix | Controllers + docs | `SystemAdmin` chua ro, RBAC chua dong deu | Co bang quyen khop code |
| P1 | Review internal endpoints cho ownership/business rules | applications/offers/interviews/copilot | Tranh lo hong ngang quyen hoac action sai workflow | Main flows duoc role dung thao tac |
| P2 | Tach service qua to neu can | `CandidateService`, `ApplicationService`, `JobService` | Hien rat to, kho review | Service chia nho, de test hon |
| P2 | Chot message/status code nhat quan | middleware + controllers | Demo/cham can on dinh | `200/201/400/401/403/404` ro rang |

## Phase 3: Demo Readiness

Muc tieu: chuan bi bo tai lieu va script demo de nop/cham truot khoi rui ro “co code nhung khong chung minh duoc”.

| Priority | Task | File/Area | Why | Acceptance Criteria |
|---|---|---|---|---|
| P1 | Dong bo lai docs voi source | `docs/overview`, `docs/ai-rules` | Hien tai docs lech feature auth/RBAC/status | Docs khong con feature “ao” |
| P1 | Them OData/XML/service demo docs | `docs/overview`, `docs/diagrams` | Requirement bat buoc can show khi cham | Co request/response mau ro rang |
| P1 | Them bang tai khoan demo | docs + seed note | Can login nhanh tung role | Co username/password/role |
| P1 | Tao Postman collection | `docs` hoac root | Can demo nhanh main flows | Collection cover auth, workflow, OData, XML |
| P2 | Them ERD va workflow visuals | `docs/diagrams` | Giup bao ve de hon | Co ERD va workflow diagram ro rang |

## Suggested Execution Order

1. Fix auth cho controller truoc.
2. Chot DB engine requirement.
3. Them OData.
4. Them XML formatter.
5. Them service phu.
6. Dong bo auth contract frontend/backend.
7. Chot docs, accounts, Postman, demo checklist.

## Quick Acceptance Checklist

- [ ] Khong con internal route nao public sai vai tro.
- [ ] Co it nhat 2 endpoint OData hoat dong.
- [ ] Co it nhat 1 endpoint tra XML khi gui `Accept: application/xml`.
- [ ] Co 1 service phu duoc API chinh goi that.
- [ ] Co sample account tung role.
- [ ] Docs khop code hien tai.
- [ ] Khong con secrets that trong repo.
- [ ] Demo duoc backend, DB, client, workflow chinh, OData, XML, service phu.
