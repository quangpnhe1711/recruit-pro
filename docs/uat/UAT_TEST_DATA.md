# UAT Test Data Specification — RecruitPro

> **Document:** `docs/uat/UAT_TEST_DATA.md`
> **Version:** 1.0 · **Date:** 2026-07-12 · **Owner:** QA / UAT
> **Source of truth:** `init.sql` (seed), `docs/SAMPLE-ACCOUNTS.md`, `docs/source-of-truth/*`, backend/frontend source.
> **Target environment:** Production — `https://www.recruitpro.site/` (API base `https://www.recruitpro.site/api`).

This document is the single reference for **all data** used by the UAT test cases in
[`UAT_TEST_CASES.md`](UAT_TEST_CASES.md). Every case's *Test Data* / *Preconditions* column refers to
identifiers defined here. It also lists the data that is **not** in the seed and must be prepared before a
case can run.

---

## 0. Production safety notes (READ FIRST)

- **This UAT phase is documentation only.** Executing these cases on production **mutates live data**
  (creates jobs, applications, interviews, offers, notifications, users). Follow §9 (Cleanup) and §10
  (Isolation) to keep production clean.
- **Never delete or mutate seed personas** (`admin`, `thucuyen`, `tiendat`, `nhatquang`, …). They are the
  fixed demo dataset shared with every other tester and with automated E2E.
- **All test-created entities MUST carry a `[UAT]` marker** in a human-visible text field (job title,
  cover letter, email subject, workflow name, department description) so they can be found and removed.
- **Do not drive seed applications to terminal states** (Hired / Rejected / OfferDeclined) — those seed
  rows are relied on for read-only verification by other cases. Create your own application to test
  terminal transitions.
- **Passwords / secrets:** the shared demo password below is **public test data committed in the repo**
  (`init.sql` comment lines 880 & 1071; `docs/SAMPLE-ACCOUNTS.md`). It is not a production secret. Do not
  copy any real secret (JWT signing key, AI provider API key, MinIO credentials, SMTP creds) into evidence.

---

## 1. Login facts (apply to every authenticated case)

| Fact | Value | Source |
|---|---|---|
| Shared demo password (ALL seed accounts) | `Password@123` | `init.sql` (bcrypt-hashed at load via `crypt('Password@123', gen_salt('bf',6))`); `docs/SAMPLE-ACCOUNTS.md` |
| Login identifier | **username** (not email) for candidate & internal portals | `AuthService.LoginCoreAsync` |
| Candidate login | `POST /api/auth/candidate/login`, screen `/login` | AuthController #2 |
| Internal login (HR/Manager/HeadDepartment/SystemAdmin) | `POST /api/auth/internal/login`, screen `/internal/login` | AuthController #3 |
| Universal login (email or username) | `POST /api/auth/login` | AuthController #1 |
| Access token lifetime | **15 minutes** (JWT, carries `token_version` claim) | `appsettings.json Jwt.ExpiryMinutes` |
| Refresh token lifetime | **7 days** (10080 min), opaque random string, single-use rotation | `appsettings.json Jwt.RefreshTokenExpiryMinutes` |
| Email domain (all seed users) | `@recruitpro.vn` | `init.sql` users |
| Portal separation | candidate portal rejects internal users and vice-versa → **401 `PORTAL_ACCESS_DENIED`** | `AuthService` |

> Placeholders used in test cases (so no secret is ever written into a case): `<CANDIDATE_SEED_ACCOUNT>`,
> `<RECRUITER_SEED_ACCOUNT>`, `<MANAGER_SEED_ACCOUNT>`, `<HEAD_SEED_ACCOUNT>`, `<SYSTEM_ADMIN_SEED_ACCOUNT>`,
> `<DISABLED_SEED_ACCOUNT>`. Resolve them from §2.

---

## 2. Seed account matrix

24 users seeded (`init.sql` users `:1080–1104`, role grants `:1118–1144`). Username = the local-part of the
email unless noted. **Password = `Password@123` for every row.**

| Username | Email | Role(s) | Status | UAT persona role | Notes |
|---|---|---|---|---|---|
| `admin` | admin@recruitpro.vn | SystemAdmin | Active | `<SYSTEM_ADMIN_SEED_ACCOUNT>` | Primary admin; RBAC, users, automation, MCP |
| `minhkhoi` | minhkhoi.vo@recruitpro.vn | SystemAdmin | Active | (2nd admin) | Use to test last-admin-lockout guard (there are 2 admins) |
| `thucuyen` | thucuyen.nguyen@recruitpro.vn | HR | Active | `<RECRUITER_SEED_ACCOUNT>` | **Primary recruiter** — owns most seed jobs & applications |
| `giahan` | giahan.le@recruitpro.vn | HR | Active | (HR #2) | Non-owner HR — for ownership/IDOR negative tests |
| `khanhlinh.pham` | khanhlinh.pham@recruitpro.vn | HR | Active | (HR #3) | |
| `haiyen` | haiyen.do@recruitpro.vn | HR | Active | (HR #4) | |
| `tiendat` | tiendat.tran@recruitpro.vn | **Manager + HeadDepartment** | Active | `<HEAD_SEED_ACCOUNT>` / `<MANAGER_SEED_ACCOUNT>` | **Dual role.** Head of **all 8** seeded departments → approves jobs, advances ManagerReview |
| `quocbao` | quocbao.vu@recruitpro.vn | Manager | Active | (Manager, non-head) | Manager who heads **no** department → empty approval queue (BR-OWN-003) |
| `minhkhang` | minhkhang.dang@recruitpro.vn | Manager | Active | (Manager, non-head) | |
| `nhatquang` | nhatquang.phung@recruitpro.vn | Candidate | Active | `<CANDIDATE_SEED_ACCOUNT>` | **Primary candidate** — has profile, resume, applications |
| `haidang` | haidang.tran@recruitpro.vn | Candidate | Active | (Candidate #2) | For cross-user IDOR checks against `nhatquang` |
| `ducminh` | ducminh.nguyen@recruitpro.vn | Candidate | Active | | |
| `khanhnam` | khanhnam.bui@recruitpro.vn | Candidate | Active | | |
| `thuha` | thuha.le@recruitpro.vn | Candidate | Active | | |
| `minhquan` | minhquan.nguyen@recruitpro.vn | Candidate | Active | (Hired) | Application **Hired** on Java Backend |
| `khanhlinh.tran` | khanhlinh.tran@recruitpro.vn | Candidate | Active | | |
| `quocanh` | quocanh.pham@recruitpro.vn | Candidate | Active | (has Offer) | Has a **Sent** offer (Data Analyst) — for accept/decline tests |
| `minhthao` | minhthao.le@recruitpro.vn | Candidate | Active | | |
| `hoangnam` | hoangnam.ho@recruitpro.vn | Candidate | Active | | |
| `ngocan` | ngocan.vo@recruitpro.vn | Candidate | Active | (Hired) | Application **Hired** (Product Designer) |
| `hoangphuc` | hoangphuc.bui@recruitpro.vn | Candidate | Active | | |
| `quynhmai` | quynhmai.do@recruitpro.vn | Candidate | Active | | |
| `anhkhoa` | anhkhoa.dang@recruitpro.vn | Candidate | Active | | |
| `yennhi` | yennhi.pham@recruitpro.vn | Candidate | **Inactive** | `<DISABLED_SEED_ACCOUNT>` | **The only disabled account** → login/refresh returns 401 `ACCOUNT_DISABLED` |

**Totals:** SystemAdmin 2 · HR 4 · Manager 3 (one dual-role) · HeadDepartment 1 (= `tiendat`) · Candidate 15
(14 active + 1 inactive).

> **Anonymous / Guest** is not an account — it is "no `Authorization` header". Used for all public-portal and
> 401 cases.

---

## 3. Seed role & permission matrix

### 3.1 Roles (`init.sql :934–940`)

| Role (exact name) | UUID | Description (VI) |
|---|---|---|
| `Candidate` | ef574bf3-08e1-4f25-932c-2d83ed8afd88 | Ứng viên |
| `HR` | e28e9442-682d-4e1a-b11f-663af56eb730 | Recruiter |
| `HeadDepartment` | 7a5b2c6d-1e2f-4a3b-9c8d-112233445566 | Trưởng bộ phận |
| `Manager` | 5f5350dc-a77f-4a68-88f8-69e27116aa8f | Quản lý tuyển dụng |
| `SystemAdmin` | 0137e9bc-7ee4-463c-b760-1580ac17cdb6 | Quản trị hệ thống |

### 3.2 Backend DB permissions (27 codes, `init.sql :901–929`)

> **Casing is significant and inconsistent** — `Job_*`, `Application_*`, `Interview_*`, `System_LOG_VIEW`
> use PascalCase-prefix; `USER_*`, `ROLE_*`, `PERMISSION_*`, `CANDIDATE_PROFILE_*`, `DEPARTMENT_*`,
> `SKILL_*`, `NOTIFICATION_*` use SCREAMING_CASE. Only these 7 are actually enforced on endpoints (via
> `[RequirePermission]`): `ROLE_VIEW`, `PERMISSION_VIEW`, `PERMISSION_MANAGE`, `USER_VIEW`, `USER_UPDATE`,
> `ROLE_MANAGE`, `System_LOG_VIEW`. The rest exist for the RBAC matrix editor.

`USER_VIEW`, `USER_CREATE`, `USER_UPDATE`, `USER_DELETE`, `ROLE_VIEW`, `ROLE_MANAGE`, `PERMISSION_VIEW`,
`PERMISSION_MANAGE`, `Job_VIEW`, `Job_CREATE`, `Job_UPDATE`, `Job_DELETE`, `Job_APPROVE`, `Application_VIEW`,
`Application_APPLY`, `Application_REVIEW`, `Interview_VIEW`, `Interview_CREATE`, `Interview_UPDATE`,
`CANDIDATE_PROFILE_VIEW`, `CANDIDATE_PROFILE_UPDATE`, `DEPARTMENT_VIEW`, `DEPARTMENT_MANAGE`, `SKILL_VIEW`,
`SKILL_MANAGE`, `NOTIFICATION_VIEW`, `System_LOG_VIEW`.

### 3.3 Role → permission grants (`init.sql :945–1010`)

| Role | Count | Permissions |
|---|---|---|
| Candidate | 8 | Job_VIEW, Application_VIEW, Application_APPLY, Interview_VIEW, CANDIDATE_PROFILE_VIEW, CANDIDATE_PROFILE_UPDATE, SKILL_VIEW, NOTIFICATION_VIEW |
| HR | 12 | Job_VIEW, Job_CREATE, Job_UPDATE, Application_VIEW, Application_REVIEW, Interview_VIEW, Interview_CREATE, Interview_UPDATE, CANDIDATE_PROFILE_VIEW, DEPARTMENT_VIEW, SKILL_VIEW, NOTIFICATION_VIEW *(no Job_APPROVE, no Job_DELETE, no system perms)* |
| HeadDepartment | 4 | Interview_VIEW, DEPARTMENT_VIEW, SKILL_VIEW, NOTIFICATION_VIEW *(job/application authority is enforced by ownership columns, not a permission code)* |
| Manager | 8 | Job_VIEW, Job_APPROVE, Application_VIEW, Application_REVIEW, Interview_VIEW, DEPARTMENT_VIEW, NOTIFICATION_VIEW, System_LOG_VIEW |
| SystemAdmin | 27 | **ALL** (incl. `PERMISSION_MANAGE` — the RBAC master key) |

### 3.4 Frontend permission keys (client-side, distinct from §3.2)

The SPA gates routes/menus with its **own** permission vocabulary (`src/permissions/permissions.ts`), derived
from the user's roles + backend-issued `user.permissions`. These are **not** the DB codes. Examples:
`system:admin`, `job:approve`, `application:view-all`, `interview:view-schedule-data`,
`candidate:view-own-profile`. See [`UAT_TRACEABILITY_MATRIX.md`](UAT_TRACEABILITY_MATRIX.md) §3 for the full
Role→FE-permission→Route map. **A mismatch between backend DB grants and this FE map is a known UAT risk
area** ([`UAT_OPEN_QUESTIONS.md`](UAT_OPEN_QUESTIONS.md) Q-RBAC-01).

---

## 4. Existing master data (seed)

| Data | Rows | Values |
|---|---|---|
| Departments (`:887`) | 8 | Engineering, Human Resources, Finance, Marketing, **Product** *(no job)*, Data & Analytics, Operations, Design. **All have `head_user_id = tiendat`.** |
| Skills (`:1015`) | 30 | Java, Spring Boot, ReactJS, NodeJS, PostgreSQL, Docker, Kubernetes, Redis, TypeScript, NextJS, C#, .NET, AWS, Python, SQL, Power BI, Selenium, Figma, SEO, Excel, ASP.NET Core, CI/CD, Microservices, REST API, EF Core, UI/UX, Data Analysis, Playwright, Communication, English |
| Offer templates (`:1051`) | 3 (all active) | Standard Tech Role, Management Offer, Contractor Agreement |
| Offer benefits (`:1057`) | 6 | Health Insurance, Paid Time Off, Remote Work, Gym Allowance, Relocation Bonus, Learning Budget |
| Offer currencies (`:1066`) | **1** | **VND only** (`₫`). No USD/EUR — multi-currency needs data prep (§6). |
| Notification events (`:2310`) | **4 (legacy codes)** | `new_application_received`, `application_status_changed`, `interview_scheduled`, `candidate_score_ready` — all `default_in_app_enabled=true`, `default_email_enabled=false`, `is_required=true`. **The Phase-6 event codes emitted by code (`application_applied`, `job_approved`, …) are NOT in this lookup table** → see Q-NOTI-01. |
| Candidate profiles (`:1149`) | 15 | All 15 candidates; **11** have `resume_parse_status='Completed'` (AI/screening-ready); 4 have NULL parse status |
| Candidate resumes (`:1812`) | ~11 | Derived for every profile with a non-null `resume_url` (nhatquang + 10). The 4 profiles with NULL `resume_url` have **no resume row** → useful for "apply without CV" tests |
| Employment type (seeded on jobs) | **FullTime only** | Enum also allows PartTime/Internship/Contract but none seeded |
| Work mode (seeded on jobs) | Onsite, Hybrid, Remote | all three used |

---

## 5. Existing business data by state (seed, base date 2026-07-04)

### 5.1 Jobs — 14 (`:1249`)

| Status | Count | Examples / notes |
|---|---|---|
| Approved | 11 | Java Backend Developer (posted "today" 07-04), Senior Frontend Developer, Full Stack Engineer, DevOps Engineer, HR Operations Specialist, Marketing Specialist, Java Backend Engineer, Data Analyst, Product Designer, Talent Acquisition Executive, **QA Automation Engineer (deadline 2026-07-12 = near/at expiry, still `Approved`)** |
| PendingApproval | 1 | Finance Analyst (`approved_by` NULL) — for approval-queue tests |
| Draft | 1 | Technical Support Specialist (`approved_by` NULL) — **seed ID `30000000-0000-4000-8000-000000000005`**, exposed by BUG-UAT-002 |
| Closed | 1 | Senior Data Engineer (deadline 2026-06-20, past) |

> **Enumerable seed job IDs:** `30000000-0000-4000-8000-00000000000X`. Pending = `...0004`, Draft = `...0005`
> (per BUG-UAT-002). Use these for public-visibility/IDOR cases.

### 5.2 Applications — 23 (`:1334`)

| Status | Count | Use for |
|---|---|---|
| Applied | 4 | Screening transition (4 freshest have NULL scores) |
| Screening | 4 | → ManagerReview; Copilot ranking pool (ranking uses **Screening only**) |
| ManagerReview | 1 | Head-review decision (head guard), interview scheduling |
| Interview | 7 | Interview → Offer/Reject gates |
| Offer | 1 | `quocanh` (Data Analyst) — accept/decline offer |
| Hired | 4 | read-only terminal verification (do NOT re-drive) |
| Rejected | 1 | read-only terminal verification |
| Withdrawn | 1 | re-apply-eligibility verification |

### 5.3 Interviews — 14 (`:1390`)

Scheduled 8 · Completed 5 · Canceled 1 (+ a rescheduled pair on one app: 1 Canceled + 1 new Scheduled).
Types (MeetingType): Online, Offline.

### 5.4 Offers — 5 (`:1372`)

Sent 1 (→ `quocanh`, app in `Offer`) · Accepted 4 (the 4 Hired apps). Currency all VND.

### 5.5 Other seed

- Notifications — 14 (`:1416`): read 6, unread 8; types Application/Interview/System; mixed `is_seen`/`is_read`.
- Refresh tokens — 8 (`:1445`), expiry 2026-08-02 (future).
- System logs — 12 (`:1459`), incl. the `yennhi` disable event (`:1470`).
- **Workflow automation** (`:2730+`): 5 workflow definitions —
  - WF1 *Pass CV → Notify Head Review* — active, **Live**
  - WF2 *Head Review Overdue Reminder* — active, **Shadow**
  - WF3 *High-fit Candidate Alert* — active, **Live**
  - WF4 *Interview Completed Follow-up* — **disabled** (`is_enabled=false`)
  - WF5 *Candidate Score Ready Digest* — draft/unpublished (`active_version_id=NULL`)
  - Outbox `published_domain_events`: 7 rows (Processed 6, Failed 1, **no Pending**).
  - `workflow_executions`: 8 (Success 4, Failed 2, Skipped 2); steps 11; dead-letters 1 (unresolved);
    worker heartbeats 2 (Running); `mcp_tool_audits` 5 (allowed 4, denied 1).

---

## 6. Data preparation — states NOT in seed (must create for full coverage)

Each item lists **who** creates it and **how**. Cross-referenced by test cases as *"prepare via DATA-PREP-n"*.

| ID | Missing data | How to create (production-safe, `[UAT]`-marked) | Needed by |
|---|---|---|---|
| DATA-PREP-01 | Application in **`OfferDeclined`** | Candidate `quocanh` (or a fresh `[UAT]` apply) declines a `Sent` offer → `POST /api/candidate/applications/{id}/decline-offer` | UAT-APP OfferDecline, UAT-E2E-05 |
| DATA-PREP-02 | Offer in **`Draft`** state | HR saves an offer draft without sending → `PUT /api/hr/applications/{id}/offer` on an Interview-stage app | UAT-OFFER draft cases |
| DATA-PREP-03 | Job that is **expired** (deadline past but still Approved) | HR creates an `[UAT]` job, gets it approved, then (DB not available on prod) rely on the QA Automation Engineer job whose deadline is **2026-07-12** — verify on/after that date; else create a job with a near deadline | UAT-CJOB expiry, UAT-APP-deadline |
| DATA-PREP-04 | Job in **`Rejected`** (rejected at approval) | Head `tiendat` rejects the PendingApproval `[UAT]` job → `PATCH /api/hr/jobs/{id}/status {status:"Rejected"}` | UAT-JOB reject |
| DATA-PREP-05 | **Disabled non-Candidate** user (locked-out privileged login) | Admin deactivates a **spare** `[UAT]`-created internal user (never a seed persona) → `PATCH /api/sysadmin/users/{id}/status {status:"Inactive"}` | UAT-AUTH disabled-internal |
| DATA-PREP-06 | **Second currency** (multi-currency offer) | No API to add a currency (master-data seed only) → **BLOCKED** on prod; record as limitation (Q-MDATA-01) | UAT-OFFER currency |
| DATA-PREP-07 | **Per-user notification override** | `user_notification_settings` is empty and **has no write API** → **BLOCKED**; record as limitation (Q-NOTI-02) | UAT-NOTI settings |
| DATA-PREP-08 | **Copilot conversation + ranking session** | HR opens `/hr/ai-copilot`, picks a job with Screening-stage candidates, creates a conversation, runs a ranking → populates `copilot_*` tables | UAT-AI ranking |
| DATA-PREP-09 | **Saved screening rule / prompt template / candidate tag** | Created through the Copilot UI/endpoints (#77, #84) | UAT-AI rules/templates |
| DATA-PREP-10 | **Fresh candidate account** (registration happy path) | `POST /api/candidates/register` with `[UAT]`-marked full name + unique username/email | UAT-REG, UAT-E2E-01 |
| DATA-PREP-11 | **A `Pending` outbox event** (observe live worker) | Trigger a real domain event (e.g. Pass CV Screening→ManagerReview) and inspect `/api/sysadmin/automation/events` before the worker drains it | UAT-WF dispatcher |
| DATA-PREP-12 | **`[UAT]` job through full lifecycle** | thucuyen creates Draft-then-PendingApproval → tiendat approves → becomes public/applyable | UAT-E2E-02, UAT-JOB lifecycle |
| DATA-PREP-13 | **Interview scheduled + completed** on an owned Interview-stage app | HR schedules (`POST /api/hr/interviews`) then completes (`PATCH …/status {Completed}`) | UAT-INT, UAT-E2E-03 |
| DATA-PREP-14 | **Corrupt / oversized / spoofed CV files** | Prepare local files: empty `.pdf`, 7 MB `.pdf` (> 6 MB cap), `.exe` renamed `.pdf`, Unicode-named `hồ-sơ-ứng-viên.pdf`, 200-char filename | UAT-REG, UAT-FILE, UAT-PROF |

---

## 7. Reusable valid test data (copy-paste ready)

Use these to fill "all other fields valid" steps. All are `[UAT]`-safe.

**Candidate registration (valid):**
```
username: uat_cand_01           (must be unique per run; bump the number)
fullName: [UAT] Nguyễn Văn Kiểm Thử
email:    uat.cand.01@example.com   (must be unique)
password: Password@123
phone:    0912345678
resume:   a small valid .pdf (< 5 MB)
```

**Job create (valid, multi-step):**
```
Title:            [UAT] Backend Engineer (delete me)
Department:       Engineering        DepartmentId: <from GET /api/departments>
Location:         Hà Nội
EmploymentType:   FullTime
WorkMode:         Hybrid
ShortPitch:       [UAT] test posting
Description:      [UAT] Build and maintain services.
Requirements:     ["3+ years backend", "PostgreSQL"]
VacancyCount:     2
MinExperienceYears: 3
SalaryMin: 20000000   SalaryMax: 40000000   (VND)
Skills: [{skillName:"Java", skillType:"Required", minimumYearsOfExperience:3}]
Deadline: <today + 30 days>
```

**Interview schedule (valid):**
```
ApplicationId: <an owned Interview- or ManagerReview-stage application>
Date:          <today + 3 days>   (local YYYY-MM-DD; BR-NOTI-009 no TZ off-by-one)
StartMinutes:  540   (09:00)
DurationMinutes: 60
Mode:          video          (FE) → MeetingType Online (BE)
LocationOrLink: https://meet.example.com/uat-123
InterviewerId: <tiendat or an HR user id>
```

**Offer (valid):**
```
BaseSalary:     30000000
CurrencyCode:   VND
EmploymentType: FullTime
ProbationPeriod: 2 months
BenefitIds:     [<Health Insurance id>, <Paid Time Off id>]
PersonalMessage: [UAT] Congratulations …
ProposedStartDate: <today + 30 days>
```

---

## 8. Negative & boundary test-data sets

These drive the *Validation* / *Boundary* / *Negative* cases. FE rules from
`src/common/validation/formValidation.ts`; BE rules from `RecruitPro.Application/Validators/*`.

### 8.1 Registration
| Field | Invalid values | Boundary values |
|---|---|---|
| username | empty, `ab` (< 4), `a@b!` (regex fail), 51 chars (> 50), duplicate `nhatquang` | 4 chars (min ok), 50 chars (max ok) |
| fullName | empty, 101 chars (> 100) | 100 chars |
| email | empty, `notanemail`, `a@`, duplicate seed email | valid |
| password | empty, `Pw@1` (< 6), 101 chars (> 100) | 6 chars, 100 chars |
| phone | `123` (no leading 0 / wrong length), `09123` (< 10), `abcdefghij`, `01234567890` (11) | `0912345678` (valid 10-digit) |

### 8.2 Job salary (equivalence partitions — per task §9.2)
Not entered · `SalaryMin=0` (min) · in-range · `SalaryMax` = `SalaryMin` (equal, allowed) ·
`SalaryMax < SalaryMin` (**reject** — FE + BE `salary_check`) · negative `-1` · decimal `10.5` ·
non-numeric `"abc"` · very large `999999999999`.

### 8.3 Interview
`StartMinutes`: `-1`, `0` (min), `1439` (max), `1440` (> max) · `DurationMinutes`: `14` (< 15), `15` (min),
`240` (max), `241` (> max) · `Mode`: not `video`/`inPerson` · `LocationOrLink`: empty, 501 chars (> 500) ·
`Date`: valid future, **past date** (no BE guard — see Q-INT-01), day-28 (BR-NOTI-009 no TZ shift).

### 8.4 Offer
`BaseSalary`: `0`, `-1`, non-numeric, valid `>0` · `CurrencyCode`: empty, 11 chars (> 10) ·
`EmploymentType`: empty · `PersonalMessage`: 2001 chars (> 2000) · `BenefitIds`: contains a non-GUID string.

### 8.5 Injection / security payloads (reuse across all text inputs)
```
XSS:            <script>alert('uat')</script>          and  "><img src=x onerror=alert(1)>
SQL-ish:        ' OR '1'='1   ;   '; DROP TABLE users;--
Unicode/VI:     Nguyễn Đình Chiểu — ăâêôơư đ — 你好 — 😀
Whitespace-only: "     "
CSV injection:  =1+1   ,   +CMD|' /C calc'!A0   ,   @SUM(1+1)   (for any CSV/Excel export field)
Path traversal: ../../etc/passwd   ../../../windows/win.ini   (for file/resume id + name inputs)
Oversized:      a 1 MB string in a free-text field
Malformed UUID: not-a-guid , 00000000-0000-0000-0000-000000000000 (empty/nil), 30000000-0000-4000-8000-0000000000zz
```

---

## 9. Cleanup strategy

| Entity type created during UAT | Cleanup action |
|---|---|
| `[UAT]` jobs | HR/Manager `DELETE /api/hr/jobs/{id}` (soft/hard per impl). Verify removed from public list. |
| `[UAT]` interviews | `DELETE /api/hr/interviews/{id}` (hard delete). |
| `[UAT]` applications | No delete API → **leave in place**, but keep them out of analytics narratives; mark the candidate/cover-letter with `[UAT]`. |
| `[UAT]` candidate accounts | No self-delete API → deactivate via SysAdmin (`status:"Inactive"`) and rename nothing; record the username in the execution log for later manual purge. |
| `[UAT]` workflows | Set `IsEnabled=false`; do not leave a `[UAT]` workflow in **Live** mode (it would send real notifications). |
| Re-activated / role-changed seed users | **Restore original role/status immediately** after the case (e.g. re-`Active` any account you disabled; restore the RBAC matrix you edited). |
| Notifications generated | No cleanup needed (append-only); note them in the execution log. |

**Golden rule:** after a test run, `admin`, `thucuyen`, `tiendat`, `nhatquang`, `yennhi` and all seed
role→permission grants must be **exactly** as they were at the start.

---

## 10. Data isolation

- Use **distinct usernames/emails per tester and per run** (`uat_<tester>_<n>`), so parallel testers do not
  collide on unique-constraint (`users.email`, `users.username`, `jobs`, one-offer-per-application).
- The **active-application partial unique index** means a candidate can hold only one active application per
  job — a second tester reusing the same candidate+job will get a legitimate 409. Use a **different seed
  candidate** per parallel apply test, or a fresh `[UAT]` candidate.
- RBAC matrix edits are **global** — only one tester at a time may run RBAC-mutation cases, and must restore
  the matrix before releasing.
- Automation **mode** (`WorkflowAutomation` setting) is global — Live/Shadow cutover cases must be
  coordinated (one owner) and reset to the documented default (`Shadow`) afterward.

---

## 11. Environment matrix

| Environment | URL / base | Use |
|---|---|---|
| Production (UAT target) | `https://www.recruitpro.site/` · API `…/api` · SSE `…/api/notifications/stream` | Primary UAT execution |
| Local (fallback / reproduce) | FE dev `http://localhost:5173` (Vite), API `http://localhost:5013/api` (or `:8080/api` via compose) | Reproduce blocked cases, file/MinIO/AI flows |
| API-direct (curl/Postman) | `docs/postman/RecruitPro.postman_collection.json` + `…environment.json` | Verify API expected-results when UI is blocked |

Browsers for UI cases: latest Chromium (primary), plus one of Firefox/Safari for compatibility spot-checks.
Viewports: desktop 1440×900 and mobile 390×844 (responsive/mobile-drawer cases).
