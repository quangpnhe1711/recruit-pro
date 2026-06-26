# FE ↔ BE Workflow / Status / Action / Error Contract Audit

**Date:** 2026-06-26 (re-audit after the frontend conformance refactor landed)
**Type:** End-to-end audit (audit-first; no backend business logic modified).
**Backend repo:** `RecruitProInternal` (branch `develop`).
**Frontend repo:** `recruit-pro-internal` (branch `develop`).
**Method:** read backend enums/DTOs/`ErrorCodes`/services + the source-of-truth docs, then read every FE
status/action/error site (centralized modules + screens + services), grep-classified the result, ran
`tsc`/`eslint`/`vite build`, and applied only small verified conformance fixes.

---

## 0a. Addendum — candidate status-contract fix (later pass)

A regression was later found and fixed in the **candidate applications** endpoint: it returned the
**localized** label as the `status` field (`"status": "HR đang sàng lọc"`), which the FE normalized to an
unknown and the presentation layer collapsed to `Rejected` ("Từ chối") — an active Screening/Interview row
rendered with a red rejected badge. Fix (BR-APPLICATION-012, T-STATUS-001..003):

- **Backend:** `CandidateApplicationListItemDto.Status = application.Status.ToString()` (canonical English);
  added `StatusLabel` for the localized display text; `nextStep` stays localized.
- **Frontend:** candidate badges render via `getApplicationStatusPresentation(status)` (canonical key →
  English label; `ManagerReview` → "Head Review"); the two unknown→`Rejected` fallbacks were removed
  (`statusPresentation` no longer `?? Rejected`; `applicationPresentation.normalizeApplicationStatusKey`
  default is now neutral `unknown`). Logic keys off the canonical status, never the localized label.

This supersedes the §3.1 "candidate path PASS" claim for the specific candidate-list `status` field, which
was carrying localized text at the time of the original audit.

---

## 0. Headline finding (read this first)

**The frontend conformance refactor has now landed.** This supersedes the previous version of this
document, which audited the *pre-refactor* tree and reported the centralized modules as missing. They
now exist and are consumed by the screens. Verified on the current `develop` working tree:

| Prior-audit claim (pre-refactor) | Current reality (this re-audit) | Proof |
|---|---|---|
| `src/common/status/` does not exist | **Exists** with `applicationStatus.ts`, `jobStatus.ts`, `interviewStatus.ts`, `offerStatus.ts`, `statusPresentation.ts` | files present; commit `e1b2dc5` "centralize frontend status presentation and workflow UI" |
| `src/common/utils/apiError.ts` does not exist | **Exists** — `ERROR_CODES`, `getApiErrorCode`, `getApiStatusCode`, `resolveErrorMessage`, `getApplicationErrorMessage`, `APPLICATION_ERROR_MESSAGES` | file present; commit `bd54439` "add stable errorCode contract to application/apply flow" |
| State-group helpers absent | **Present** — `ACTIVE_/CLOSED_FOR_WORKFLOW_/REAPPLY_ELIGIBLE_CLOSED_/WITHDRAWABLE_/OFFER_ACTIONABLE_APPLICATION_STATUSES` + predicates | `applicationStatus.ts:24-124` |
| UI-001 / UI-002 (CandidateListScreen) **not fixed** | **Fixed** — stable `CandidateReviewState` keys, normalizer, key-based filter/KPI | `CandidateListScreen.tsx:14,47-75,253,281` |
| UI-003 (JobManagementScreen / approval) **not fixed** | **Fixed** — `JobStatus` constants + `getJobStatusPresentation` + `jobStatusFilterOptions` | `JobManagementScreen.tsx:13-19,81-87,162,422`; both manager approval screens |
| UI-005 (apply gating) **not fixed** | **Fixed** — `jobApplyState` gates the CTA on `isOpenForApplicationJobStatus` + deadline | `JobDetailScreen.tsx:148-163,518-526` |
| UI-013 (errorCode unused) **not fixed** | **Fixed for candidate flows** — `getApplicationErrorMessage` in apply/withdraw/accept/decline | `ApplyJobScreen.tsx:4,100`; `MyApplicationScreen.tsx:4,375,395,415` |
| INV-004 `Withdrawn` neutral badge | **Still implemented** ✅ | `applicationPresentation.ts:104-114` |

The backend status/action/error contract was already sound and complete (CONFORMANCE-AUDIT "all 13
invariants PASS"). The frontend now consumes it correctly on the candidate path and on the
status-presentation/job-gating paths. Residual gaps are **Medium/Low** (reviewer decision matrix still
FE-owned but backend-guarded; some HR mutations don't refetch parent lists; the interview list/candidate
screens keep local read-side normalizers). **No HIGH finding remains.**

This pass applied **7 small verified fixes** — errorCode wiring into 3 reviewer/offer/interview catches;
2 badge-tone corrections; 1 derived-label render; and (follow-up) the interview create request now sends
the canonical `InterviewStatus.Scheduled` instead of the non-canonical `"confirmed"` token — and **kept**
the prior pass's one-line `jobsService` `CLOSED` normalization. **No backend logic was modified.**

---

## 1. Backend source of truth (verified from code)

| Enum | Backend file | Values (PascalCase, persisted as string) |
|---|---|---|
| `ApplicationStatus` | `RecruitPro.Domain/Enums/ApplicationStatus.cs` | Applied, Screening, ManagerReview, Interview, Offer, Hired, Rejected, OfferDeclined, Withdrawn |
| `JobStatus` | `RecruitPro.Domain/Enums/JobStatus.cs` | Draft, PendingApproval, Approved, Closed, Rejected |
| `InterviewStatus` | `RecruitPro.Domain/Enums/InterviewStatus.cs` | Scheduled, Completed, Canceled (single `l`) |
| `OfferStatus` | `RecruitPro.Domain/Enums/OfferStatus.cs` | Draft, Sent, Accepted, Declined |

API envelope / fields (verified):
- `ApiResponse<T>.ErrorCode` exists; `Conflict/UnprocessableEntity/NotFound/Forbidden/Unauthorized/BadRequest` factory overloads accept `errorCode`.
- `ApplyJobEligibilityDto` (`DTOs/Response/ApplyJobScreenDto.cs:46-60`): `CanApply, AlreadyApplied, ExistingApplicationId, ExistingApplicationStatus, Blockers[], GuidanceMessage,` **`PrimaryErrorCode`**. `ApplicationService.cs:777` sets `PrimaryErrorCode = blockers[0].Code`.
- `CandidateApplicationListItemDto`: `Status, NextStep,` **`AvailableActions[]`**. `BuildCandidateAvailableActions` (`ApplicationService.cs:1583-1597`) emits `"viewDetail"` always, `"withdraw"` iff `CanCandidateWithdraw`, `"acceptOffer"`+`"declineOffer"` iff `CanCandidateRespondToOffer`.
- `ErrorCodes.cs` defines all 13 contract codes (exact match to FE `ERROR_CODES`).
- errorCode is actually **emitted**: `ApplicationService` 409/422/transition (lines 154,158,193,359,391,398,426,433,573,580 + blocker codes 737-765), `InterviewService` (`InterviewNotActionable`), `ExceptionMiddleware` (401→`Unauthenticated`, 403→`Forbidden`, validation→`ValidationError`).

---

## 2. Frontend centralized surface (what now exists and is consumed)

| Concern | File | Consumed by |
|---|---|---|
| ApplicationStatus constants + state groups + normalizer + predicates | `src/common/status/applicationStatus.ts` | CandidateReviewDetailScreen (offer/interview gating) |
| ApplicationStatus labels/badges | `src/common/utils/applicationPresentation.ts` (`normalizeApplicationStatusKey`, `getApplicationStatusMeta`, `formatApplicationStatus`, `getApplicationStatusBadgeClass`, `applicationStatusFilterOptions`) | MyApplication, HR list, ReviewDetail, JobDetail, ManagerReviewList, dashboards |
| JobStatus constants + normalize + `isOpenForApplicationJobStatus` + presentation + filter options | `src/common/status/jobStatus.ts` | JobManagementScreen, JobDetailScreen, ManagerJobApprovalList/Detail |
| InterviewStatus constants + `InterviewDraftState` + normalize + presentation | `src/common/status/interviewStatus.ts` | *(module present; the 3 interview screens still keep local normalizers — see §3.3)* |
| OfferStatus constants + `isOfferActionableStatus` + presentation | `src/common/status/offerStatus.ts` | SendOfferScreen, CandidateReviewDetailScreen |
| Tone→badge classes + `getApplicationStatusPresentation` | `src/common/status/statusPresentation.ts` | JobManagementScreen (`toneBadgeClassName`) |
| Error codes + resolver (errorCode → status → message) | `src/common/utils/apiError.ts` | ApplyJob, MyApplication, + (this pass) InterviewSchedule, CandidateReviewDetail, SendOffer |

FE `JobStatus` casing: the canonical `common/status/jobStatus.ts` is PascalCase and `normalizeJobStatus`
accepts both PascalCase and ALL_CAPS. The legacy `modules/jobs/jobsSchema.ts` `JobStatus` (ALL_CAPS) and
`jobsService.normalizeJobStatus` (ALL_CAPS) still exist for the list/detail DTO pipeline; the manager
approval *action dispatch* uses the ALL_CAPS type while *labels* now route through
`getJobStatusPresentation`. Two casings coexist but are bridged by the normalizers (not a contract break).

---

## 3. Status mapping matrix

### 3.1 ApplicationStatus

Backend file `ApplicationStatus.cs`; API field `status` (+ `availableActions`, `nextStep`).
FE normalizer `normalizeApplicationStatusKey` / `normalizeApplicationStatus`; label/badge
`getApplicationStatusMeta`; tone `APPLICATION_STATUS_TONE` (`statusPresentation.ts`).

| BE value | FE key | FE label (VI) | Badge tone | Group | Allowed candidate actions (backend) | Screens | Verdict |
|---|---|---|---|---|---|---|---|
| Applied | applied | Đã ứng tuyển | neutral (slate) | Active | withdraw | MyApplication, HR list/detail, JobDetail, dashboards | **PASS** |
| Screening | screening | Sàng lọc | warning (amber) | Active | withdraw | same | **PASS** |
| ManagerReview | managerreview | QL xét duyệt | success (emerald) | Active | withdraw | same | **PASS** |
| Interview | interview | Phỏng vấn | info (sky) | Active | withdraw | same | **PASS** |
| Offer | offer | Offer | primary (violet) | Active / OfferActionable | acceptOffer, declineOffer | MyApplication, ReviewDetail | **PASS** |
| Hired | hired | Đã nhận việc | success (green) | ClosedForWorkflow (terminal for jobId) | — | same | **PASS** |
| Rejected | rejected | **Từ chối** | **danger (rose)** | Closed / re-apply-eligible | — | same | **PASS** (company rejection) |
| OfferDeclined | offerdeclined | Từ chối offer | neutral (stone) | Closed / re-apply-eligible | — | same | **PASS** (distinct from Rejected) |
| Withdrawn | withdrawn | **Đã rút đơn** | **neutral (slate)** | Closed / re-apply-eligible | — | same | **PASS** — INV-004 satisfied |

Required business groups — **materialized** in `applicationStatus.ts`: `ACTIVE_APPLICATION_STATUSES`,
`CLOSED_FOR_WORKFLOW_APPLICATION_STATUSES`, `REAPPLY_ELIGIBLE_CLOSED_APPLICATION_STATUSES` (excludes
Hired — INV-015), `WITHDRAWABLE_APPLICATION_STATUSES`, `OFFER_ACTIONABLE_APPLICATION_STATUSES`, with
predicates `isActive/isClosedForWorkflow/isReapplyEligibleClosed/isWithdrawable/canShowOfferActions`.

Hard-requirement checks: ✅ `Withdrawn` neutral and distinct from `Rejected` (danger). ✅ `Rejected` =
company rejection, `OfferDeclined` = candidate declined offer (distinct labels/tones). ✅ `Hired`
terminal — backend INV-015 enforces; FE shows no reapply/withdraw for Hired. ✅ Offer accept/decline
shown only when backend `availableActions` includes them (candidate `Offer`+`Sent`).
✅ **UI-002 resolved** — `CandidateListScreen` is now a documented *derived display group*
(`new/reviewing/interviewed/rejected`) keyed off stable keys via `normalizeCandidateReviewState`, not a
VI-label taxonomy.

### 3.2 JobStatus

Backend `JobStatus.cs`; API field `status` (list/detail) / `statusLabel`/`approvalStatus` (manager).
FE: `common/status/jobStatus.ts` (`normalizeJobStatus`, `isOpenForApplicationJobStatus`,
`getJobStatusPresentation`, `jobStatusFilterOptions`).

| BE value | FE label | Tone | Apply-able | Verdict |
|---|---|---|---|---|
| Draft | Nháp | neutral | No | **PASS** |
| PendingApproval | Chờ duyệt | warning | No | **PASS** |
| Approved | Đang tuyển | success | **Yes** until deadline | **PASS** (single label now; "Đã duyệt" inconsistency gone) |
| Closed | Đã đóng | neutral | No | **PASS** (legacy `normalizeJobStatus` `CLOSED` case fix kept) |
| Rejected | Từ chối | danger | No | **PASS** |

Checks: ✅ **UI-005 resolved** — `JobDetailScreen.jobApplyState` disables the Apply CTA and shows a
reason when the job is not `Approved` or the deadline passed; backend remains the guard (422 on submit).
✅ Apply not gated by VI labels anywhere. ✅ ALL_CAPS legacy values normalized once
(`normalizeJobStatus`), not per screen. ✅ **UI-003 resolved** — `JobManagementScreen` and both manager
approval screens render labels via `getJobStatusPresentation` and filter via `jobStatusFilterOptions`
(stable `"all" | JobStatus` keys); no VI-label branching remains.
⚠️ `ManagerJobApprovalListScreen.statusBadge` (`:25-36`) keeps a local tone switch keyed on **raw enum
tokens** (`pendingapproval/approved/rejected`) — not VI labels, label itself is centralized. Cosmetic
duplication only. Low.

### 3.3 InterviewStatus

Backend `InterviewStatus.cs` (Scheduled/Completed/Canceled); API field `status`.
FE module `common/status/interviewStatus.ts` exists (constants, `InterviewDraftState`,
`normalizeInterviewStatus`, presentation) — but the three interview screens **keep local normalizers**:

| Concern | Finding | Verdict |
|---|---|---|
| Persisted-status values | All screens use the correct enum values (Scheduled/Completed/Canceled). `JobInterviewListScreen.tsx:16,60-70` and `CandidateInterviewScreen.tsx:13-25` redeclare local normalizers; `markCompleted` sends `"Completed"` (real enum). | PARTIAL (correct values, local duplication — UI-008) |
| FE draft vs persisted | **Fixed (follow-up pass).** `InterviewScheduleScreen.saveSchedule` now sends `status: InterviewStatus.Scheduled` (canonical), and `hrService.CreateInterviewRequest.status` is typed `InterviewStatus` (optional). The non-canonical `"draft"\|"confirmed"` token is no longer sent to the backend. `InterviewDraftState = "draft" \| "readyToSubmit"` remains available in `interviewStatus.ts` as a local-only type for any form that needs it. | **PASS** |
| Off-stage 422 | Backend returns 422 `INTERVIEW_NOT_ACTIONABLE` off-`Interview`-stage. **Now surfaced** — `InterviewScheduleScreen.saveSchedule` catch routes through `getApplicationErrorMessage` (this pass). | **PASS** (fixed this pass) |
| Action gating | `JobInterviewListScreen` row actions gated by permissions only; "Đổi lịch"/"Đánh dấu hoàn tất" guarded by `normalizeInterviewStatus(...) === "Completed"`. | PARTIAL (no per-row backend actions exposed) |

### 3.4 OfferStatus

Backend `OfferStatus.cs` (Draft/Sent/Accepted/Declined); API field `offerStatus` / `offer.status`.
FE module `common/status/offerStatus.ts` (`normalizeOfferStatus`, `isOfferActionableStatus`, presentation).

| Concern | Finding | Verdict |
|---|---|---|
| Candidate actionable only when `Sent` | Backend-driven via `availableActions`; only `Offer`+`Sent` shows accept/decline (`MyApplicationScreen`). | **PASS** |
| `isOfferActionableStatus` helper | **Exists and used** — `SendOfferScreen.tsx:257` and `CandidateReviewDetailScreen.tsx:507` use it instead of raw `=== "sent"`. | **PASS** |
| Reviewer must not expose Offer→Hired/OfferDeclined | Reviewer buttons from FE `getAvailableDecisions` omit Offer transitions; backend blocks Offer→Hired (INV-009). | PARTIAL (UI-004; backend-guarded) |
| Raw offer status rendered | `SendOfferScreen.tsx:279` renders `Offer {editor.offer.status}` (raw enum, unlocalized) — display only; `getOfferStatusPresentation` available. | Low (display) |

---

## 4. Action mapping audit

| Action | BE endpoint | FE service | Visibility source | Refetch on success | Error handling | Verdict |
|---|---|---|---|---|---|---|
| apply | POST `/jobs/{id}/apply` | `jobsService.applyToJob` | `ApplyJobScreen` button `disabled={!canApply\|\|submitting}` | navigates away; local flip | `getApplicationErrorMessage` (`:100`) | **PASS** |
| withdraw | POST `/candidate/applications/{id}/withdraw` | `candidateService.withdrawApplication` | `availableActions.includes("withdraw")` ✅ | `await loadData()` ✅ | `getApplicationErrorMessage` (`:375`) | **PASS** |
| re-apply | POST `/jobs/{id}/apply` (new row) | `jobsService.applyToJob` | apply-context `canApply` after closed | n/a | as apply | **PASS** (trusts backend eligibility) |
| accept offer | POST `…/accept-offer` | `candidateService.acceptOffer` | `availableActions.includes("acceptOffer")` ✅ | `loadData()` ✅ | `getApplicationErrorMessage` (`:395`) | **PASS** |
| decline offer | POST `…/decline-offer` | `candidateService.declineOffer` | `availableActions.includes("declineOffer")` ✅ | `loadData()` ✅ | `getApplicationErrorMessage` (`:415`) | **PASS** |
| HR/Manager decision | PATCH `/hr/applications/{id}/decision` | `hrService.updateApplicationDecision` | **FE-local** `getAvailableDecisions(status,role)` (backend-guarded) | local `setDetail` only — parent list not refetched | `getApplicationErrorMessage` (this pass) | PARTIAL (UI-004 + UI-006) |
| schedule interview | POST `/hr/interviews` | `hrService.createInterview` | permissions + local slot state | navigates away; detail not refetched | `getApplicationErrorMessage` (this pass) — surfaces `INTERVIEW_NOT_ACTIONABLE` | PARTIAL (UI-006 only; create now sends canonical `InterviewStatus.Scheduled`) |
| send offer / save draft | POST `…/offer/send`, PUT `…/offer` | `hrService.sendOffer/saveOfferDraft` | always rendered, no state gate | local editor only; detail/list not refetched | `getApplicationErrorMessage` (this pass) — surfaces `OFFER_NOT_ACTIONABLE` | PARTIAL (UI-006) |
| send email | POST `…/send-email` | `hrService.sendApplicationEmail` | permission `canSendEmail` | none | bare `catch` (generic toast) | PARTIAL (UI-006; non-workflow action) |
| mark notification read | PATCH `…/read` (POST 405 fallback) | `notificationService.markAsRead` | bell UI | optimistic + reconnect resync | reads `status===405` for fallback ✅ | **PASS** |
| mark all read | PATCH `…/read-all` (POST fallback) | `notificationService.markAllAsRead` | bell UI | optimistic | 405 fallback ✅ | **PASS** |

Hard-requirement checks: ✅ Candidate action visibility prefers backend `availableActions`.
⚠️ Reviewer decision matrix is still an FE-local `getAvailableDecisions` switch (UI-004) — backend
enforces the real state machine (BR-006/INV-009), so it is not exploitable, but the FE still owns the
matrix. ⚠️ HR decision/offer/interview/email mutations do not refetch parent lists (UI-006).

---

## 5. ErrorCode / blocker mapping audit

FE precedence **errorCode → HTTP status → message** is implemented in `apiError.ts`
(`resolveErrorMessage`) and wired via `getApplicationErrorMessage`. The FE eligibility schema now
carries `primaryErrorCode` (`jobsSchema.ts:420`).

| Code | BE emits (file) | In API shape | FE reads it | Screen uses / falls back | Verdict |
|---|---|---|---|---|---|
| APPLICATION_ALREADY_ACTIVE | `ApplicationService` 409 | `ApiResponse.ErrorCode` | ✅ `getApiErrorCode` | apply toast (`getApplicationErrorMessage`) | **PASS** |
| APPLICATION_ALREADY_HIRED | `BuildApplyEligibility` 422 / `PrimaryErrorCode` | `eligibility.primaryErrorCode` ✅ + envelope on submit | ✅ | apply submit toast; blockers shown as backend text | **PASS** |
| JOB_NOT_ACCEPTING_APPLICATIONS | `BuildApplyEligibility` 422 | ✅ | ✅ | apply submit toast / blocker text | **PASS** |
| JOB_DEADLINE_PASSED | 422 | ✅ | ✅ | apply submit toast / blocker text | **PASS** |
| CANDIDATE_PROFILE_INCOMPLETE | 422 | ✅ | ✅ | apply submit toast / blocker text | **PASS** |
| RESUME_REQUIRED | 422 | ✅ | ✅ | apply submit toast / blocker text | **PASS** |
| APPLICATION_NOT_WITHDRAWABLE | `WithdrawApplicationAsync` 422 | ✅ | ✅ | withdraw toast | **PASS** |
| INVALID_APPLICATION_TRANSITION | `UpdateApplicationDecisionAsync` | ✅ | ✅ (this pass) | decision toast | **PASS** |
| INTERVIEW_NOT_ACTIONABLE | `InterviewService` 422 | ✅ | ✅ (this pass) | schedule toast | **PASS** |
| OFFER_NOT_ACTIONABLE | `ApplicationService`/offer 422 | ✅ | ✅ (this pass) | send-offer toast | **PASS** |
| UNAUTHENTICATED | `ExceptionMiddleware` 401 | ✅ | force-logout on 401 (`api-client.ts`) | partial (status-based, not code) | PARTIAL |
| FORBIDDEN | `ExceptionMiddleware` 403 | ✅ | not code-branched | generic toast / route guard | PARTIAL (non-workflow) |
| VALIDATION_ERROR | `ExceptionMiddleware` (+`errors{}`) | ✅ | not code-branched | generic toast | PARTIAL (non-workflow) |

All **application/apply/withdraw/offer/interview/decision** domain codes are now read code-first. The
3 cross-cutting codes (UNAUTHENTICATED/FORBIDDEN/VALIDATION_ERROR) are handled by status/route-guard
rather than code branching — acceptable (not workflow-state codes).

---

## 6. Grep evidence (classified)

Searches over `recruit-pro-internal/src` (node_modules excluded).

**Localized VI status labels used in LOGIC — NOT ALLOWED (INV-012): 0 sites.** The prior HIGH sites
(`CandidateListScreen` VI taxonomy/filter/tone, `JobManagementScreen` VI taxonomy/filter/tone) are gone;
filters/KPIs/tones now key off stable enum keys.

**VI labels in ALLOWED locations** (constants/normalizers/presentation/display copy):
`applicationPresentation.ts` (labels, filter options, normalizer), `common/status/{jobStatus,
interviewStatus,offerStatus}.ts` (presentation maps), `jobsSchema.ts` (`jobStatusLabels`),
`jobsService.ts` (funnel display labels), `CandidateListScreen.CANDIDATE_REVIEW_STATE_META`,
stat-card/summary copy (`JobManagementScreen` "Chờ duyệt", `MyApplicationScreen` "Đang xử lý"/"Đã đóng"),
non-status filter options (`"Tất cả nguồn"`, `"Tất cả phòng ban"`), `CandidateReviewDetailScreen`
`formatStatusDescriptionVi`/`formatOfferStatusVi` (description copy keyed off normalized tokens).

**Raw status comparisons in screen logic — classified:**
- Through a normalizer (allowed): `JobInterviewListScreen` `normalizeInterviewStatus(...) === "Completed"/"Scheduled"`; `CandidateReviewDetailScreen` `normalizeApplicationStatus(...) === ApplicationStatus.Offer/...`.
- Stable-value compares (acceptable, local): `CandidateInterviewScreen.tsx:129,132` `item.status.toLowerCase() === "scheduled"/"completed"` (stat counts); `JobDetailScreen.tsx:296` funnel label `=== "applied"` (analytics).
- Tone-from-token switches (cosmetic, raw enum tokens — not VI labels): `ManagerJobApprovalListScreen.statusBadge`, `ManagerDashboardScreen.statusChipTone`, `JobInterviewListScreen.statusChip`, `CandidateInterviewScreen.statusChip`.
- Non-status tone switch on English tokens: `ManagerCandidateReviewListScreen.recommendationTone` (`"strong hire"/"hire"/"hold"`) — UI-007; `recommendation` is an advisory score label, **not** a workflow status enum.

**Target metric:** "0 not-allowed localized-label logic" → **MET**. "0 not-allowed raw status comparisons
in screens when a centralized helper exists" → **substantially met**; remaining raw compares are either
routed through a normalizer, stable-value, or cosmetic tone on raw enum tokens (not VI labels).

---

## 7. Regression check vs previous UI findings

| ID | Before (pre-refactor baseline) | After (this re-audit) | Evidence | Status |
|---|---|---|---|---|
| UI-001 | CandidateList VI-label filter/branch logic | Filter/KPI/tone now key off stable `CandidateReviewState` keys | `CandidateListScreen.tsx:47-75,253,281` | **FIXED** |
| UI-002 | CandidateList parallel VI taxonomy | Documented derived display group, normalized from raw status | `CandidateListScreen.tsx:11-14,56-75` | **FIXED** |
| UI-003 | Job-status VI-label logic (management + approval) | `getJobStatusPresentation` + `jobStatusFilterOptions`, key-based filter | `JobManagementScreen.tsx:81-87,422`; `ManagerJobApproval*` | **FIXED** |
| UI-004 | Reviewer transitions FE-owned | Still FE-local `getAvailableDecisions`; offer/interview gating now via `ApplicationStatus`/`isOfferActionableStatus`; backend guards INV-009 | `CandidateReviewDetailScreen.tsx:202-218,501,507,514` | **PARTIAL** (MED, backend-mitigated) |
| UI-005 | JobDetail apply not gated by status/deadline | `jobApplyState` gates CTA + reason text | `JobDetailScreen.tsx:148-163,518-526` | **FIXED** |
| UI-006 | Stale UI after HR mutations | Candidate flows refetch via `loadData()`; HR decision/offer/interview/email still don't refetch parent lists | `ReviewDetail:352`, `SendOffer:207,225`, `InterviewSchedule:324` | **NOT FIXED** (MED; no exploit) |
| UI-007 | Manager recommendation/status tone strings | `ManagerCandidateReviewList.recommendationTone` (English) + `ManagerDashboard.statusChipTone` (raw tokens) remain | `ManagerCandidateReviewListScreen.tsx:15-26`; `ManagerDashboardScreen.tsx:8-19` | **NOT FIXED** (MED; non-status field) |
| UI-008 | Interview `draft/confirmed` vocabulary; off-stage 422 not surfaced | Off-stage 422 **now surfaced**; create now sends canonical `InterviewStatus.Scheduled` (no `"confirmed"`); request typed `InterviewStatus`; `InterviewDraftState` kept local-only. Interview list/candidate screens still keep local read-side normalizers (correct values). | `InterviewScheduleScreen.tsx`; `hrService.ts`; `interviewStatus.ts` | **FIXED** (residual local read-side normalizers = Low) |
| UI-011 | `normalizeApplicationStatusKey` VI-label fallback | VI fallback retained (defensive); the label→tone misuse it enabled is **fixed this pass** (badges now use the key) | `applicationPresentation.ts:170-176`; `CandidateApplicationScreen:247`, `JobDetailScreen:976` | **IMPROVED** |
| UI-013 | errorCode/primaryErrorCode unused | Wired into candidate apply/withdraw/accept/decline; this pass added reviewer decision / interview / offer | `apiError.ts`; ApplyJob/MyApplication; ReviewDetail/InterviewSchedule/SendOffer | **FIXED** (candidate) / improved (reviewer) |
| INV-004 | Withdrawn rendered as Rejected | Neutral badge | `applicationPresentation.ts:104-114` | **FIXED** |

Remaining risk: no HIGH item. UI-004/UI-006/UI-007 are Medium and backend-guarded or stale-only; UI-008
is a functionally-safe smell. None permit an impossible business action.

---

## 8. Build / type / lint results (verified, before AND after fixes)

| Command | Result | Notes |
|---|---|---|
| `npm run build` (`vite build`) | **PASS** (exit 0, "✓ built") | Only benign warnings (signalr `/*#__PURE__*/`, chunk-size). vite/rolldown transpiles without type-checking. |
| `npx tsc --noEmit` | **2 errors (pre-existing, unchanged)** | `CommonSelect.tsx:188` (button/select prop variance), `AiCopilotScreen.tsx:85` (DTO missing `didRank`/`assistantMessage`). Both unrelated to status/contract. The previously-documented 3rd error (`MyApplicationScreen:475`) is **no longer present**. Count identical before and after this pass's fixes. |
| `npm run lint` (`eslint .`) | **32 problems (30 errors, 2 warnings) — pre-existing, unchanged** | Top: `LandingPageScreen` (`react-hooks/refs`), `JobDetailScreen`/`CandidateListScreen`/`InterviewScheduleScreen`/`MyApplicationScreen` (`react-hooks/*`), `rolePermissions.ts` (1 unused var). Count identical before and after this pass. |
| `npm test` | **N/A** | `package.json` has no `test` script; no FE test runner. Not faked. |

Proof the fixes are clean: `tsc` error count (2) and `eslint` problem count (32) are **identical**
before and after the 6 edits; `vite build` exits 0 in both states.

---

## 9. Changes made this pass

All in `recruit-pro-internal` (frontend). **No backend file modified.**

| File | Change | Type |
|---|---|---|
| `src/pages/hr/InterviewScheduleScreen.tsx` | import `getApplicationErrorMessage`; `saveSchedule` catch surfaces errorCode (`INTERVIEW_NOT_ACTIONABLE`); **(follow-up)** create now sends `status: InterviewStatus.Scheduled` (was `"confirmed"`) | small fix — UI-008 (off-stage 422 + canonical persisted status) |
| `src/services/hr/hrService.ts` | **(follow-up)** `CreateInterviewRequest.status` retyped `InterviewStatus` (optional) — no longer `"draft" \| "confirmed"` | small fix — UI-008 (FE form vocabulary no longer leaks into the write contract) |
| `src/pages/hr/CandidateReviewDetailScreen.tsx` | import `getApplicationErrorMessage`; `handleDecision` catch surfaces errorCode (`INVALID_APPLICATION_TRANSITION`) | small fix — errorCode-first hard requirement |
| `src/pages/hr/SendOfferScreen.tsx` | import `getApplicationErrorMessage`; `handleSaveDraft`/`handleSendOffer` catches surface errorCode (`OFFER_NOT_ACTIONABLE`) | small fix — errorCode-first hard requirement |
| `src/pages/hr/CandidateApplicationScreen.tsx` | status badge tone now from `app.statusKey` (was `app.status` label) | small fix — INV-012 (tone must not derive from localized label) |
| `src/pages/public/JobDetailScreen.tsx` | recent-apps badge: add `statusClass` from raw status; render it | small fix — INV-012 (same label→tone bug) |
| `src/pages/hr/CandidateListScreen.tsx` | status chip renders `chip.label` (was the raw review-state key) | small fix — derived-display-group label correctness |
| `src/services/jobs/jobsService.ts` | (kept from prior pass) `normalizeJobStatus` `CLOSED` case | small fix — JobStatus conformance (uncommitted in working tree) |
| `docs/source-of-truth/FE-BE-WORKFLOW-CONTRACT-AUDIT.md` | this re-audit (rewritten) | doc |
| `docs/source-of-truth/UI-STATUS-PRESENTATION.md` | correction-banner update: refactor has landed | doc-integrity |
| `docs/source-of-truth/UI-DATA-DISPLAY-INVENTORY.md` | correction-banner update: HIGH findings resolved | doc-integrity |

**The label→tone fixes are INV-012 conformance, not cosmetics:** before, any status whose VI label was
not in the normalizer's defensive VI branches (i.e. everything except Withdrawn) fell through to the
`rejected` (rose) tone — so Hired/Offer/etc. rendered with a red rejected badge on the HR list and the
job-detail recent-apps table.

---

## 10. Remaining gaps (none HIGH)

MEDIUM:
1. **UI-004** — `CandidateReviewDetailScreen.getAvailableDecisions` still computes reviewer transition buttons from a local switch; backend should return allowed-transitions / FE should consume them. Backend enforces the rule (INV-009/BR-006), so not exploitable.
2. **UI-006** — HR decision / offer save-send / interview / email mutations do not refetch parent lists/dashboards (no query cache). Candidate flows already refetch.
3. **UI-007** — `ManagerCandidateReviewListScreen.recommendationTone` (English tokens) and `ManagerDashboardScreen.statusChipTone` (raw tokens) map tone from non-centralized strings. `recommendation` is a non-workflow advisory field.
4. **UI-008 (residual, Low)** — the create write now sends canonical `InterviewStatus.Scheduled`; the remaining item is that `JobInterviewListScreen`/`CandidateInterviewScreen` keep **local read-side** interview normalizers (correct values, but duplicating `common/status/interviewStatus.ts`). Display-only; recommend adopting the centralized normalizer/presentation.

LOW / hygiene:
5. `hrService.getApplications` drops backend `availableActions` for HR rows (root of UI-004 on the list side).
6. Two FE `JobStatus` casings coexist (PascalCase `common/status` vs ALL_CAPS `jobsSchema`/`jobsService`); bridged by normalizers.
7. Pre-existing build health: 2 `tsc` errors + 32 `eslint` problems in unrelated files; `vite build` does not type-check, so they ship. Consider a `tsc -b`/lint CI gate.
8. `SendOfferScreen.tsx:279` renders raw `editor.offer.status` unlocalized (use `getOfferStatusPresentation`).

These are genuine follow-ups, not contract breaks, and were left for a dedicated cleanup pass per the
audit-first / small-fix-only scope.

---

## 11. Verdict

**The frontend can now be considered conformant with the backend status/action/error contract.** All
prior HIGH findings (UI-001/002/003 localized-label logic + parallel taxonomy; UI-005 apply gating;
absent error-code layer) are resolved. The candidate workflow path (status labels, `availableActions`-
driven actions, refetch, errorCode-first error handling) and the status-presentation/job-gating paths
consume the backend contract correctly; `Withdrawn` is neutral and distinct from `Rejected` (INV-004);
business state is never inferred from localized Vietnamese labels (INV-012); no screen owns a workflow
truth the backend doesn't enforce (the one FE-local reviewer matrix is backend-guarded).

Residual items are **Medium/Low** (FE-owned-but-backend-guarded reviewer matrix, missing HR-side
refetch, local read-side interview normalizers, manager tone strings). The interview create write now
sends the canonical `InterviewStatus.Scheduled` — the FE form vocabulary no longer leaks into the write
contract. **No HIGH finding remains.**

**Backend business logic was not modified in this pass.** Backend conformance (CONFORMANCE-AUDIT "all 13
invariants PASS in code and tests") stands unchanged.
