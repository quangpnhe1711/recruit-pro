# UI Data Display & Status Presentation Inventory

> **Update — Frontend Conformance phase executed.** The HIGH findings below were remediated. See
> [UI-STATUS-PRESENTATION.md](UI-STATUS-PRESENTATION.md) for the centralized status contract.
>
> | Finding | Status after FE phase |
> |---|---|
> | UI-001 (HIGH) CandidateListScreen VI-label filter logic | **FIXED** — stable keys (`"all"`/review-state keys) |
> | UI-002 (HIGH) CandidateListScreen parallel taxonomy | **FIXED** — named derived display group, key-based |
> | UI-003 (HIGH) Job-status screens VI-label logic | **FIXED** — `jobStatus` constants/normalize/presentation |
> | UI-005 (MED) Apply button not gated by job status/deadline | **FIXED** — `jobApplyState` (status+deadline) |
> | UI-013 (LOW/MED) errorCode unused | **FIXED (candidate)** — `getApplicationErrorMessage` in apply/withdraw/accept/decline |
> | Bonus: CandidateApplicationScreen (HR list) VI-label filter | **FIXED** — `statusKey` + `applicationStatusFilterOptions` |
> | UI-008 (MED) offer/interview raw compares | **PARTIAL** — offer/interview routed through centralized normalizers |
> | UI-004 (MED) reviewer transitions FE-computed | **DEFERRED** — backend guards (INV-009); FE matrix still local |
> | UI-006 (MED) stale UI after HR mutations | **DEFERRED** |
> | UI-007 (MED) manager tone hardcoded | **DEFERRED** |
>
> Grep evidence after the phase: **0** VI-status-labels-in-logic, **0** `.status === "RawValue"` screen
> comparisons. Remaining raw-status logic = `getAvailableDecisions` (UI-004) + manager tone (UI-007) +
> candidate/job interview-list local normalizers; all backend-guarded or presentation-only.

**Status (original): INVENTORY / AUDIT ONLY.** This file documents what the frontend displayed at audit
time and how it derived status/labels/actions.

- **Frontend repo:** `recruit-pro-internal` (React 19 + TypeScript + Vite; no i18n framework, no
  React-Query/SWR — data is fetched imperatively and held in `useState`).
- **Backend baseline:** [00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md),
  [BUSINESS-RULES.md](BUSINESS-RULES.md), [STATE-MACHINE.md](STATE-MACHINE.md),
  [ERROR-CONTRACT.md](ERROR-CONTRACT.md).
- All paths below are relative to the **frontend** repo root unless noted.

> Method: three read-only sweeps (candidate / HR-manager / public-shared). Line numbers are accurate
> at audit time and may drift; treat them as pointers.

---

## 0. Cross-cutting architecture facts

| Fact | Detail | Implication |
|---|---|---|
| No i18n framework | All Vietnamese copy is hardcoded inline or in per-file maps (no i18next/locale files) | Status wording is scattered; no single translation source |
| No query cache | No React-Query/SWR/Apollo; mutations call `service.x()` then manually `loadData()` or `setState` | Cross-screen staleness is manual and easy to miss |
| Two status taxonomies | `applicationPresentation.ts` (canonical, 9 ApplicationStatus keys) **and** a separate ad-hoc candidate taxonomy in `CandidateListScreen.tsx` (`Mới/Đang xem xét/Đã phỏng vấn/Từ chối`) | The second does not map to `ApplicationStatus`; HIGH risk |
| Error-code util exists but unwired | `src/common/utils/apiError.ts` defines `ERROR_CODES` + `getApiErrorCode`/`resolveErrorMessage` (errorCode → status → message) but **screens do not call it yet** | `primaryErrorCode`/`errorCode` are available but unused in UI logic |
| Job status enum casing | FE uses `DRAFT/PENDING_APPROVAL/APPROVED/CLOSED/REJECTED`; backend enum is `Draft/PendingApproval/Approved/Closed/Rejected`. `jobsSchema` accepts a wide union | Mapping relies on normalization; fragile |

### Canonical status presentation (the good part)
`src/common/utils/applicationPresentation.ts` is the single source for **ApplicationStatus** display:
- `APPLICATION_STATUS_META` (9 keys) → label + badge classes (default/candidate/detail variants).
- `normalizeApplicationStatusKey(status)` maps raw backend strings → stable key.
- `formatApplicationStatus(status)`, `getApplicationStatusBadgeClass(status, variant)`.
- **`Withdrawn` is explicitly rendered neutral** (`slate-100/slate-600`), never the red `Từ chối` — satisfies INV-004 on every screen that uses this helper.

| Key | Label (VI) | Badge |
|---|---|---|
| applied | Đã ứng tuyển | slate |
| screening | Sàng lọc | amber |
| managerreview | QL xét duyệt | emerald |
| interview | Phỏng vấn | sky |
| offer | Offer | violet |
| hired | Đã nhận việc | green |
| rejected | Từ chối | rose/red |
| offerdeclined | Từ chối offer | stone |
| withdrawn | Đã rút đơn | slate (neutral) |

---

## 1. Candidate screens

### 1.1 ApplyJobScreen
- **File / route:** `src/pages/candidate/ApplyJobScreen.tsx` — `/jobs/:jobId/apply`. Role: Candidate.
- **Endpoints:** `GET /jobs/{jobId}/apply-context` (`jobsService.getApplyContext`), `POST /jobs/{jobId}/apply` (`jobsService.applyToJob`).
- **Fields displayed:** job summary, candidate profile, current resume; eligibility (`canApply`, `alreadyApplied`, `blockers[]`, `existingApplicationStatus`, `guidanceMessage`, `primaryErrorCode`).
- **Status conversion:** none for application status; relies on eligibility booleans.
- **CTA:** Apply button `disabled={!eligibility.canApply || submitting}` (≈L373); submit guarded by `if (!canApply) return` (≈L67).
- **Blockers:** rendered as raw backend strings in a red panel (≈L196-202). `primaryErrorCode` is **not** consumed.
- **States:** loading skeleton; error → EmptyState "Không thể ứng tuyển"; null guard.
- **Cache after apply:** local `setScreenData` flips `canApply:false, alreadyApplied:true` and overwrites `blockers` with hardcoded `"Bạn đã ứng tuyển vị trí này rồi."` (≈L79-94). Does **not** refetch My Applications.
- **SoT mismatch:** Hired-same-job (INV-015) and duplicate-active (INV-003) blockers are shown only as backend text; no `errorCode`-driven UX. Apply screen itself trusts backend `canApply` → conformant for INV-003/015. **Risk: Low.**

### 1.2 MyApplicationScreen
- **File / route:** `src/pages/candidate/MyApplicationScreen.tsx` — `/candidate/my-applications`. Role: Candidate.
- **Endpoints:** `GET /candidate/applications`, `GET /candidate/interviews`; mutations `POST …/withdraw`, `…/accept-offer`, `…/decline-offer`.
- **Status conversion:** `getApplicationStatusMeta(item.status, "candidate")` → label + `statusKey` + badge class (canonical helper).
- **CTA visibility (backend-driven — good):** `availableActions.includes("withdraw"|"acceptOffer"|"declineOffer")` AND a permission flag (≈L139-143, 174-214).
- **Withdrawn:** neutral badge (INV-004 PASS).
- **Cache after mutation:** `await loadData()` refetches applications + interviews after withdraw/accept/decline (≈L369/392/412). **Good.**
- **SoT mismatch:** none material. Interview-count is derived by **matching `jobTitle` strings** (≈L54-56) rather than an application/interview id link — fragile display join. **Risk: Low/Medium (display only).**

### 1.3 DashboardCandidateScreen
- **File / route:** `src/pages/candidate/DashboardCandidateScreen.tsx` — `/candidate/dashboard`.
- **Endpoint:** `GET /candidate/dashboard`. Shows counts (appliedJobs, interviews, unread), upcoming interview, recommended jobs. No application status badges.
- **Cache:** one-time load on mount (`dashboardLoadedRef`); not refreshed after apply/withdraw on other screens. **Risk: Medium (stale counts).**

### 1.4 Candidate profile + ResumeSection
- **Files:** `src/pages/candidate/candidate-profile-screen/CandidateProfileAndCVManagementScreen.tsx`, `sections/ResumeSection.tsx` — `/candidate/profile`.
- Shows resume (fileName, uploadedAt, version, `isCurrent` "CV đang dùng"), parse status, completionScore. Form-driven mutations. No workflow status. **Risk: Low.**

### 1.5 candidateService
- `src/services/candidate/candidateService.ts`: register, getDashboard, getApplications, getInterviews (post-processes `{items}`→array), withdrawApplication, acceptOffer, declineOffer, profile CRUD, resume upload/parse. Responses unwrapped to `.data`.

---

## 2. HR / Manager screens

### 2.1 HR Applications List
- **File / route:** `src/pages/hr/CandidateApplicationScreen.tsx` — `/hr/applications` (also Manager via role check; `?jobId=` filter = per-job list). Roles: HR/Manager.
- **Endpoints:** `GET /hr/applications`, `GET /hr/applications/{id}/cv`, `POST /hr/applications/{id}/send-email`.
- **Status conversion:** `formatApplicationStatus(item.status)` + `getApplicationStatusBadgeClass` (canonical helper). Good.
- **Actions:** "Rate/Review" (→ detail), "Send Email" (modal). Gated by **permissions only**, not state.
- **Email templates:** hardcoded VI templates in FE (≈L126-155): Interview Invitation / Job Offer / Rejection Mail / Custom.
- **States:** `loading` hardcoded `false` (≈L652) — no real loading indicator; empty "Không có dữ liệu"; errors swallowed → `setApplications([])`.
- **Cache after send-email:** **none** — list not refetched. **Risk: Medium (stale).**

### 2.2 HR/Manager Application Detail (Review)
- **File / route:** `src/pages/hr/CandidateReviewDetailScreen.tsx` — `/hr/applications/{id}` & `/manager/applications/{id}`.
- **Endpoints:** `GET /hr/applications/{id}`, `…/cv`, `PATCH /hr/applications/{id}/decision`, `GET …/offer`.
- **Status conversion:** local `formatStatusDescriptionVi(status)` hardcoded switch (≈L58-96); `formatOfferStatusVi(status)` (≈L98-111).
- **Decision buttons:** computed **client-side** by `getAvailableDecisions(status, role)` — hardcoded switch on `status.toLowerCase()` + role (≈L197-213). **Does NOT consult backend `availableActions`/allowed transitions.**
- **Offer/interview gating via raw string compare:** `detail.status.toLowerCase() === "offer"` (≈L495), `["managerreview","interview"].includes(detail.status.toLowerCase())` (≈L509), offer label keys off `detail.offerStatus?.toLowerCase() === "sent"`.
- **Cache after decision:** `setDetail(response.data)` only — parent list **not** invalidated (≈L337-354). **Risk: Medium (stale list).**
- **SoT mismatch:** FE-derived transitions duplicate the backend state machine (BR-APPLICATION-006) instead of consuming it; today they happen to agree (no Offer→Hired button), but drift is possible. Backend now blocks reviewer Offer→Hired (INV-009), so UI cannot force it. **Risk: Medium.**

### 2.3 Decision modal/form
- Integrated in 2.2. Submits `PATCH …/decision` with `{ targetStatus }` ∈ `Screening|ManagerReview|Interview|Offer|Rejected`. Target set from FE `getAvailableDecisions`. Buttons disabled while submitting.

### 2.4 Manager Review Queue
- **File / route:** `src/pages/hr/ManagerCandidateReviewListScreen.tsx` — `/manager/applications`. Endpoint `GET /manager/applications/review-queue`.
- Status via canonical helper. **Recommendation tone** via hardcoded **English** switch `recommendation.toLowerCase()` = `"strong hire"/"hire"/"hold"` (≈L15-26) → breaks if backend localizes. Read-only (no mutations). **Risk: Medium.**

### 2.5 Candidate CV view
- Integrated in 2.2 (iframe preview + download; `GET …/cv`). 404 fallback "Open in new tab". **Risk: Low.**

### 2.6 Interview Scheduling
- **File / route:** `src/pages/hr/InterviewScheduleScreen.tsx` — `/hr/interviews/schedule?applicationId=`.
- **Endpoints:** `GET /hr/interviews/schedule-data`, `POST /hr/interviews`, `PATCH /hr/interviews/{id}/status`, `DELETE /hr/interviews/{id}`.
- Status at create = `"draft"|"confirmed"` (FE concept; **does not match backend `InterviewStatus` = Scheduled/Completed/Canceled**). Draft persisted to localStorage.
- **Cache after create:** clears draft, `navigate("/hr/interviews")` — does **not** refetch the application detail/interview list. **Risk: Medium.**
- **SoT mismatch:** FE interview status vocabulary (`draft/confirmed`) differs from backend enum; gating that an interview is only valid at `Application.status=Interview` (INV-008) is enforced **server-side only** (422), not reflected in this UI. **Risk: Medium.**

### 2.7 Offer Sending (SendOfferScreen)
- **File / route:** `src/pages/hr/SendOfferScreen.tsx` — `/hr/applications/{id}/send-offer`.
- **Endpoints:** `GET …/offer`, `PUT …/offer` (draft), `POST …/offer/send`.
- Offer status tone via `editor.offer.status.toLowerCase() === "sent"` (raw compare).
- **Cache after save/send:** updates local editor only; detail screen's offer badge + app list **not** refetched. **Risk: Medium (stale offer status).**

### 2.8 Email sending — see 2.1 (modal). Hardcoded VI templates; no refetch.

### 2.9 HR/Manager dashboards
- `src/pages/hr/HrDashboardScreen.tsx` (`GET /hr/dashboard`), `src/pages/manager/ManagerDashboardScreen.tsx` (`GET /manager/dashboard`).
- Recent-applications status via canonical helper (HR) / `statusChipTone()` hardcoded switch (Manager). Counts/funnel come from backend. No mutations. **Risk: Low/Medium (manager tone switch).**

### 2.10 Services
- `src/services/hr/hrService.ts` (20 fns: applications, decision, cv, offer editor/draft/send, manager queue, send-email, interviews CRUD).
- `src/services/manager/managerService.ts` (dashboard, recruitment-analytics). Managers reuse HR detail/decision components.

---

## 3. Public / Job screens

### 3.1 JobDetailScreen
- **File / route:** `src/pages/public/JobDetailScreen.tsx` — public job detail.
- **Job status display:** hardcoded string-compare chain on `detail.status` (≈L255-263): `CLOSED→"Đã đóng"`, `PENDING_APPROVAL→"Chờ duyệt"`, `DRAFT→"Nháp"`, `REJECTED→"Từ chối"`, default `"Đang tuyển"`.
- **Apply button:** gated by `JOB_APPLY` permission + auth only — **not** by job status/deadline (≈L498-506). A closed/expired job can still present Apply (backend 422s on submit). **Risk: Medium.**
- Recent applications use canonical status helper.

### 3.2 Job list / Landing
- `JobListingCandidateScreen.tsx`, `LandingPageScreen.tsx`: no status-driven logic (filter by type/skills/salary). **Risk: Low.**

### 3.3 Job management / approval (HR/Manager)
- `src/pages/hr/JobManagementScreen.tsx`, `src/pages/manager/ManagerJobApprovalListScreen.tsx`, `ManagerJobApprovalDetailScreen.tsx`: job-status chips via hardcoded VI switches and label ternaries; `JobManagementScreen` filter compares against VI label `"Tất cả trạng thái"`. **Risk: Medium (label logic).**

### 3.4 Notifications
- `src/services/notification/notificationService.ts` + `src/common/components/layout/NotificationProvider.tsx`.
- `markAsRead`/`markAllAsRead` try **PATCH then fall back to POST** on 405. Endpoints `/notifications/{id}/read`, `/notifications/read-all`.
- Local state updated immediately (toggle `isRead`, decrement `unreadCount`); SignalR `/hubs/notifications` prepends new items. **Cache:** optimistic local update + silent re-sync on reconnect. **Risk: Low.**

### 3.5 HTTP / error layer
- `src/services/http/api-client.ts`: unwraps `response.data`; on broken-JWT or 401 → force logout. No error-code branching here.
- `src/common/utils/apiError.ts`: `ERROR_CODES` + `getApiErrorCode`/`getApiStatusCode`/`resolveErrorMessage` (precedence errorCode → status → message). **Defined but not yet consumed by screens.**
- `src/services/auth/authFailure.ts`: JWT-failure detection via message substring matching.

### 3.6 Auth / permissions
- `src/guards/PermissionGuard.tsx`, `RouteGuard.tsx`, `src/permissions/permissions.ts`, `rolePermissions.ts`. Action/route gating is permission/role based, **never** status-based.

---

## 4. Status Display Audit (per status family)

| Enum | Raw value(s) | Localized label | Produced where | Logic source | Notes |
|---|---|---|---|---|---|
| ApplicationStatus | Applied…Withdrawn (+ legacy casings) | applicationPresentation labels | **FE** map | `normalizeApplicationStatusKey` (FE) | Canonical; Withdrawn neutral (INV-004 ✓). Normalization also matches VI `"đãrútđơn"` (label-in-logic, defensive) |
| ApplicationStatus (HR detail) | same | `formatStatusDescriptionVi` | **FE** hardcoded switch | raw `status.toLowerCase()` | Duplicate of canonical copy; drift risk |
| Candidate taxonomy (HR) | `Mới/Đang xem xét/Đã phỏng vấn/Từ chối` | n/a | **FE** `CandidateListScreen` | **VI labels in switch + filter** | Does NOT map to ApplicationStatus; INV-012 risk |
| JobStatus | DRAFT/PENDING_APPROVAL/APPROVED/CLOSED/REJECTED | `jobStatusLabels` + inline | **FE** map + per-screen switches | raw enum compare | Casing differs from backend enum |
| InterviewStatus | Scheduled/Completed/Canceled (backend) vs `draft/confirmed` (FE create) | `interviewPresentation` timing labels | **FE** | raw compare (`=== "Completed"/"Scheduled"`) | FE create vocabulary ≠ backend enum |
| OfferStatus | Draft/Sent/Accepted/Declined | `formatOfferStatusVi` | **FE** switch | `status.toLowerCase() === "sent"` | raw string compare |
| Notification | isRead boolean | n/a | backend flag | boolean | OK |

---

## 5. Critical Rule — localized-label-in-logic findings (INV-012)

**Rule:** FE must not infer business state from localized VI labels. Occurrences where a VI label
drives logic (not just display):

| File | ~Line | Code | Severity |
|---|---|---|---|
| `src/pages/hr/CandidateListScreen.tsx` | 297, 269 | `c.status === "Đang xem xét"`, `statusFilter === "Tất cả trạng thái"` | **HIGH** |
| `src/pages/hr/CandidateListScreen.tsx` | 71-84 | `switch(status)` on VI labels → CSS class | **HIGH** |
| `src/pages/hr/JobManagementScreen.tsx` | 45-64, 248 | status CSS switch + filter on VI `"Tất cả trạng thái"` | **HIGH** |
| `src/common/utils/applicationPresentation.ts` | 170-176 | `case "đãrútđơn"/"rútđơn": return "withdrawn"` | **Medium** (defensive fallback, but still label-keyed) |
| `src/pages/manager/ManagerCandidateReviewListScreen.tsx` | 15-26 | recommendation tone via English `"strong hire"/"hire"/"hold"` | **Medium** |

Raw-but-fragile string compares (not localized, but not stable-enum-typed): `CandidateReviewDetailScreen` (`status.toLowerCase()`), `SendOfferScreen`/`offerStatus`, `JobInterviewListScreen` (`=== "Completed"/"Scheduled"`), `JobDetailScreen` (`=== "CLOSED"`…). Severity Medium (break on casing/format change).

---

## 6. Application-flow UI checks

**Apply screen:** canApply ✓, alreadyApplied ✓, blockers ✓ (raw text), existingApplicationStatus ✓, button disabled by canApply ✓, guidance ✓, resume/profile/duplicate/hired blockers shown as backend text. Hired-same-job & duplicate handled by trusting backend (no FE label logic). Conformant.

**My Applications:** status label ✓ (canonical), badge ✓, next-step copy ✓, actions from `availableActions` ✓, withdraw/accept/decline visibility ✓, withdrawn neutral ✓. Conformant.

**Job detail/list:** job status displayed ✓, **Apply button NOT gated by job status/deadline** (permission only) ✗ Medium, already-applied behavior delegated to apply-context on the apply screen (job detail itself doesn't reflect it).

---

## 7. HR/Manager UI checks

- Visible statuses: all ApplicationStatus via canonical/`formatStatusDescriptionVi`.
- Transition buttons: from **FE `getAvailableDecisions`**, not backend allowed-transitions/`availableActions`.
- Role gating: HR vs Manager handled in FE switch + permissions.
- Invalid transition via UI: not currently exposed (no Offer→Hired button), but FE owns the rule → drift risk; backend is the real guard (INV-009 ✓).
- Mutations rely on local switch, and **do not refetch parent lists** after decision/offer/email/interview.

---

## 8. Data-quality display notes

- **Interview ↔ application join by `jobTitle` string** (MyApplicationScreen) — should be id-based.
- **Candidate taxonomy** (`Mới/Đang xem xét/…`) is a parallel, non-canonical status model.
- **Stale counts/badges** after mutations on dashboards, HR list, detail offer badge.
- Dates/salary/scores formatted via `Intl` (`vi-VN`) in `format.ts` — OK.
- `loading` hardcoded `false` on HR list → no loading affordance; errors swallowed to empty arrays (looks like "no data" instead of "error").

---

## 9. Vietnamese localization audit (summary)

No i18n framework; all VI strings hardcoded. Status-bearing label sources:
- `src/modules/jobs/jobsSchema.ts` (`jobStatusLabels`, 5).
- `src/common/utils/applicationPresentation.ts` (9 ApplicationStatus labels + filter options).
- `src/common/utils/interviewPresentation.ts` (timing labels).
- Per-screen inline: `JobDetailScreen`, `JobManagementScreen`, `CandidateListScreen`,
  `ManagerJobApprovalList/DetailScreen`, `MyApplicationScreen`, `CandidateReviewDetailScreen`
  (`formatStatusDescriptionVi`, `formatOfferStatusVi`), email templates in `CandidateApplicationScreen`.

SoT wording check on the sensitive set:
- `Rejected` = "Từ chối" ✓ (company rejection).
- `Withdrawn` = "Đã rút đơn" ✓ (never rendered as rejected — INV-004 PASS).
- `OfferDeclined` = "Từ chối offer" ✓ (distinct from Rejected).
- `Hired` = "Đã nhận việc" (canonical) / "Đã tuyển dụng" (APPLY-STATUS-FLOW doc wording) — **wording
  inconsistency** between FE and doc; same meaning. Low.

---

## 10. Cache / stale-UI audit

| Mutation | File/fn | Currently refreshes | Missing refresh | Risk |
|---|---|---|---|---|
| apply | ApplyJobScreen | local eligibility flip | My Applications list, candidate dashboard counts | Low (navigates) |
| withdraw | MyApplicationScreen `handleWithdraw`→`loadData` | applications + interviews | apply-context for that job, dashboard counts | Low/Med |
| accept/decline offer | MyApplicationScreen `loadData` | applications + interviews | dashboard counts | Low |
| HR decision | CandidateReviewDetailScreen `handleDecision` | detail only | `/hr/applications` list, manager queue, dashboards | **Medium** |
| send offer / save draft | SendOfferScreen | local editor | review detail offer badge, app list | **Medium** |
| schedule interview | InterviewScheduleScreen | navigates away | application detail, interview list | **Medium** |
| send email | CandidateApplicationScreen | nothing | list | Low/Med |
| read / read-all notif | NotificationProvider | local state + reconnect re-sync | — | Low |

---

## UI Findings Summary

| ID | Severity | Screen | File (FE) | Issue | SoT rule | Suggested fix (later) |
|---|---|---|---|---|---|---|
| UI-001 | High | HR Candidate List | `src/pages/hr/CandidateListScreen.tsx` | Business logic & filtering keyed off localized VI labels (`=== "Đang xem xét"`, `"Tất cả trạng thái"`) | INV-012 | Filter/branch on stable enum keys; route through `normalizeApplicationStatusKey` |
| UI-002 | High | HR Candidate List | `CandidateListScreen.tsx` | Parallel non-canonical status taxonomy (`Mới/Đang xem xét/Đã phỏng vấn/Từ chối`) not mapped to ApplicationStatus | 00-DOMAIN §3, INV-012 | Replace with canonical ApplicationStatus presentation |
| UI-003 | High | Job Management / Approval | `JobManagementScreen.tsx`, `ManagerJobApproval*` | Job-status CSS/filter logic on VI labels | INV-012 | Branch on `JobStatus` enum; centralize job-status meta |
| UI-004 | Medium | HR/Manager Review Detail | `CandidateReviewDetailScreen.tsx` | Allowed transitions computed in FE (`getAvailableDecisions` switch), not from backend allowed-transitions/`availableActions` | BR-APPLICATION-006, INV-009 | Have API return allowed transitions / consume `availableActions`; render from it |
| UI-005 | Medium | Public Job Detail | `JobDetailScreen.tsx` | Apply button gated by permission only, not job status/deadline | BR-APPLICATION-004, INV-001 | Disable/hide Apply when job not Approved or past deadline |
| UI-006 | Medium | Review Detail / Send Offer / Interview Schedule / HR list | multiple | Mutations don't invalidate parent list/detail → stale status/offer/interview after action | derived-state from dependencies (BR-007) | Refetch affected queries after each mutation |
| UI-007 | Medium | Manager Review Queue / Manager Dashboard | `ManagerCandidateReviewListScreen.tsx`, `ManagerDashboardScreen.tsx` | Tone/label logic on hardcoded English/raw strings (`"strong hire"`, status switch) | INV-012 (spirit) | Map from stable fields |
| UI-008 | Medium | Interview Schedule | `InterviewScheduleScreen.tsx` | FE interview vocabulary `draft/confirmed` ≠ backend `Scheduled/Completed/Canceled`; INV-008 stage-gating not reflected in UI | INV-008, STATE-MACHINE Interview | Align FE to `InterviewStatus`; surface stage gating |
| UI-009 | Medium | Candidate Dashboard | `DashboardCandidateScreen.tsx` | Counts loaded once; stale after apply/withdraw | BR-007 | Refresh on focus/after mutation |
| UI-010 | Low/Med | My Applications | `MyApplicationScreen.tsx` | Interview↔application joined by `jobTitle` string | data quality | Join by application/interview id |
| UI-011 | Low/Med | applicationPresentation | `applicationPresentation.ts` | `normalizeApplicationStatusKey` matches VI label `"đãrútđơn"` (label-in-logic, defensive) | INV-012 | Keep enum-only; treat VI fallback as last resort, log if hit |
| UI-012 | Low | Review Detail | `CandidateReviewDetailScreen.tsx` | `formatStatusDescriptionVi` duplicates canonical status copy → drift | single-source | Centralize copy |
| UI-013 | Low | Error handling | screens + `apiError.ts` | `errorCode`/`primaryErrorCode` available but unused; UX still status/message-based | ERROR-CONTRACT | Wire `getApiErrorCode`/`primaryErrorCode` into apply/withdraw/offer UX |
| UI-014 | Low | HR Applications List | `CandidateApplicationScreen.tsx` | `loading` hardcoded false; errors swallowed to empty array (reads as "no data") | data quality | Real loading/error states |

No **Critical** (UI-allows-impossible-business-action) findings: the backend now enforces the state
machine (INV-008/009/015) and 4xx contract, so FE gaps are stale-data / label-logic / missing-guards,
not impossible mutations.

---

## UI ↔ Source of Truth Traceability

| Rule / Invariant | Expected UI behavior | Screen/component | Current status | Gap |
|---|---|---|---|---|
| INV-003 AlreadyApplied = EXISTS active | Apply enabled only when `canApply`; "already applied" only for active | ApplyJobScreen | **OK** | Uses `eligibility.canApply/alreadyApplied`; no FE row logic |
| INV-004 Withdrawn ≠ Rejected | Withdrawn shows neutral, never red "Từ chối" | applicationPresentation + all consumers | **OK** | Explicit neutral mapping |
| INV-012 FE must not infer state from localized labels | All logic on stable enum/status/action fields | CandidateListScreen, JobManagementScreen, Manager screens, applicationPresentation fallback | **GAP** | UI-001/002/003/007/011 use VI/English labels in logic |
| INV-015 Hired terminal for same jobId | Apply blocked after Hired with clear reason | ApplyJobScreen | **Partial** | Relies on backend blocker text; `APPLICATION_ALREADY_HIRED` not surfaced via errorCode |
| BR-APPLICATION-003 Withdraw visibility | Withdraw shown only for Applied/Screening/ManagerReview/Interview | MyApplicationScreen | **OK** | Driven by backend `availableActions` |
| BR-APPLICATION-009 Offer actions | Accept/Decline only when status Offer & offer Sent; reviewer can't hire | MyApplicationScreen (candidate), CandidateReviewDetailScreen (reviewer) | **OK (candidate) / Partial (reviewer)** | Candidate actions backend-driven; reviewer transitions FE-derived (UI-004) but backend guards INV-009 |
| BR-APPLICATION-004 / INV-001 Apply only on open job | Apply hidden/disabled when job not Approved or expired | JobDetailScreen, ApplyJobScreen | **Partial** | Apply-context enforces on apply screen; public job detail shows Apply regardless (UI-005) |
| BR-APPLICATION-006 Reviewer transitions follow state machine | Transition buttons from backend allowed set | CandidateReviewDetailScreen | **GAP** | FE-computed (UI-004) |

---

## Report (this pass)

- **Files inspected:** ~40+ across `src/pages/{candidate,hr,manager,public,internal}`, `src/services/*`, `src/common/utils/{applicationPresentation,interviewPresentation,apiError,format}.ts`, `src/modules/jobs/jobsSchema.ts`, `src/services/http/*`, `src/permissions/*`, `src/guards/*`, `src/common/components/layout/NotificationProvider.tsx`.
- **Screens documented:** 18 (candidate ×5, HR/Manager ×10, public/shared ×3 groups) + shared helpers/services.
- **Status conversions found:** 1 canonical (`applicationPresentation`), plus ≥6 ad-hoc/per-screen converters (HR detail desc, offer status, job status inline, candidate taxonomy, manager tone, interview timing).
- **Hardcoded Vietnamese status labels found:** ~40 occurrences across ~13 files (full list in §9 and the agent sweep).
- **Localized-label logic found:** 5 sites (UI-001/002/003/007/011) — 3 HIGH (CandidateListScreen, JobManagementScreen), 2 Medium.
- **Cache invalidation gaps found:** 6 mutations missing parent refresh (HR decision, send/save offer, schedule interview, send email, candidate dashboard counts, apply→my-applications).

### Top 10 UI / data-display risks
1. **UI-002** Parallel non-canonical candidate status taxonomy (CandidateListScreen). HIGH.
2. **UI-001** Filtering/branching on localized VI labels (CandidateListScreen). HIGH (INV-012).
3. **UI-003** Job status logic on VI labels (JobManagement/Approval). HIGH (INV-012).
4. **UI-004** Reviewer allowed transitions computed in FE, not from backend. Medium (drift vs BR-006/INV-009).
5. **UI-006** No list/detail refetch after HR decision/offer/interview → stale workflow state. Medium.
6. **UI-005** Apply button not gated by job status/deadline on public job detail. Medium (INV-001).
7. **UI-008** FE interview vocabulary (`draft/confirmed`) ≠ backend `InterviewStatus`; INV-008 not reflected. Medium.
8. **UI-007** Manager recommendation/status tone on hardcoded strings. Medium.
9. **UI-013** `errorCode`/`primaryErrorCode` available but unused; Hired/duplicate UX is text-only. Low/Med.
10. **UI-009/010/012/014** Stale dashboard counts, jobTitle-based interview join, duplicated status copy, fake loading/error states. Low/Med.

**No UI code was modified in this pass.** This inventory is the basis for a separate UI refactor phase.
