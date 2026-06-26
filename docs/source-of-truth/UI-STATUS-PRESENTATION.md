# UI Status Presentation (Frontend)

> ✅ **UPDATE (2026-06-26, post-refactor re-audit).** The frontend conformance refactor has since
> **landed** (commits `e1b2dc5` "centralize frontend status presentation and workflow UI" and `bd54439`
> "add stable errorCode contract"). The centralized modules described below now **exist and are
> consumed**: `src/common/status/{applicationStatus,jobStatus,interviewStatus,offerStatus,
> statusPresentation}.ts`, `src/common/utils/apiError.ts` (`ERROR_CODES`, `getApplicationErrorMessage`),
> and the state-group helpers (`ACTIVE_/WITHDRAWABLE_/OFFER_ACTIONABLE_…APPLICATION_STATUSES`,
> `isOfferActionableStatus`, `isOpenForApplicationJobStatus`, `InterviewDraftState`). The tables below
> are now an accurate description of the code, **not** merely the design intent. The 2026-06-26
> CORRECTION banner immediately below was accurate for the *pre-refactor* tree and is retained for
> history only. Verified current state:
> [FE-BE-WORKFLOW-CONTRACT-AUDIT.md](FE-BE-WORKFLOW-CONTRACT-AUDIT.md).

> ⚠️ **CORRECTION (2026-06-26 FE↔BE contract audit) — superseded by the UPDATE above.** This document
> described a *target* frontend architecture that was **not implemented** in the `recruit-pro-internal`
> codebase *at the time of the pre-refactor audit*. The modules listed
> below under "Centralized modules" — `src/common/status/applicationStatus.ts`, `jobStatus.ts`,
> `interviewStatus.ts`, `offerStatus.ts`, `statusPresentation.ts` — **did not exist** (there was no
> `src/common/status/` directory). `src/common/utils/apiError.ts`, `ERROR_CODES`, and
> `getApplicationErrorMessage` **did not exist**. The state-group helpers
> (`ACTIVE_APPLICATION_STATUSES`, `WITHDRAWABLE_APPLICATION_STATUSES`, `isOfferActionableStatus`,
> `isOpenForApplicationJobStatus`, `InterviewDraftState`, …) **did not exist**. The only centralized
> status logic present then was `src/common/utils/applicationPresentation.ts`. (All of these now exist —
> see the UPDATE above.)

How the frontend (`recruit-pro-internal`) maps backend status enums to labels, tone/badges, and
actions. Produced by the Frontend Conformance phase. Backend was **not** modified in that phase.

Centralized modules (the single source for status logic + presentation):

```
src/common/status/applicationStatus.ts   constants, groups, helpers, normalizeApplicationStatus
src/common/status/jobStatus.ts            constants, normalizeJobStatus, isOpenForApplicationJobStatus, presentation, filter options
src/common/status/interviewStatus.ts      constants (Scheduled/Completed/Canceled), InterviewDraftState (local-only), presentation
src/common/status/offerStatus.ts          constants, normalizeOfferStatus, isOfferActionableStatus, presentation
src/common/status/statusPresentation.ts   StatusTone + tone→badge classes + getApplicationStatusPresentation, re-exports
src/common/utils/applicationPresentation.ts  existing canonical application badge/label map (delegated-to)
src/common/utils/apiError.ts              ERROR_CODES + getApplicationErrorMessage (errorCode → status → message)
```

**Rule:** raw status string literals (`"Withdrawn"`, `"Approved"`, …) and localized labels may appear
**only** in these modules (constants/normalizers/presentation) and tests. Screen components branch on
the exported constants/helpers (INV-012).

## ApplicationStatus

| Raw enum | FE constant | Vietnamese label | Tone / badge | Group | Candidate actions |
|---|---|---|---|---|---|
| Applied | `ApplicationStatus.Applied` | Đã ứng tuyển | neutral (slate) | Active | withdraw |
| Screening | `…Screening` | Sàng lọc | warning (amber) | Active | withdraw |
| ManagerReview | `…ManagerReview` | QL xét duyệt | success (emerald) | Active | withdraw |
| Interview | `…Interview` | Phỏng vấn | info (sky) | Active | withdraw |
| Offer | `…Offer` | Offer | primary (violet) | Active | acceptOffer / declineOffer |
| Hired | `…Hired` | Đã nhận việc | success (green) | ClosedForWorkflow (terminal for jobId) | — |
| Rejected | `…Rejected` | **Từ chối** | **danger (rose)** | Closed / re-apply-eligible | — |
| OfferDeclined | `…OfferDeclined` | Từ chối offer | neutral (stone) | Closed / re-apply-eligible | — |
| Withdrawn | `…Withdrawn` | **Đã rút đơn** | **neutral (slate)** | Closed / re-apply-eligible | — |

Groups: `ACTIVE_APPLICATION_STATUSES`, `CLOSED_FOR_WORKFLOW_APPLICATION_STATUSES`,
`REAPPLY_ELIGIBLE_CLOSED_APPLICATION_STATUSES` (excludes Hired — INV-015),
`WITHDRAWABLE_APPLICATION_STATUSES`, `OFFER_ACTIONABLE_APPLICATION_STATUSES`.
**`Withdrawn` is neutral and distinct from `Rejected` (danger)** — INV-004.

Screens consuming it: ApplyJobScreen, MyApplicationScreen, CandidateApplicationScreen (HR list),
CandidateReviewDetailScreen, JobDetailScreen (recent apps), dashboards.

## JobStatus

| Raw enum (backend / legacy) | FE constant | Label | Tone | Apply-able |
|---|---|---|---|---|
| Draft / DRAFT | `JobStatus.Draft` | Nháp | neutral | No |
| PendingApproval / PENDING_APPROVAL | `…PendingApproval` | Chờ duyệt | warning | No |
| Approved / APPROVED | `…Approved` | Đang tuyển | success | **Yes** (until deadline) |
| Closed / CLOSED | `…Closed` | Đã đóng | neutral | No |
| Rejected / REJECTED | `…Rejected` | Từ chối | danger | No |

`normalizeJobStatus` accepts both PascalCase and ALL_CAPS. `isOpenForApplicationJobStatus` = Approved
only. Filter screens use `jobStatusFilterOptions` (stable `"all" | JobStatus` keys).
Screens: JobManagementScreen, ManagerJobApprovalListScreen, ManagerJobApprovalDetailScreen,
JobDetailScreen (apply gating + status label).

## InterviewStatus

| Raw enum | FE constant | Label | Tone |
|---|---|---|---|
| Scheduled | `InterviewStatus.Scheduled` | Đã lên lịch | info |
| Completed | `…Completed` | Hoàn thành | success |
| Canceled | `…Canceled` | Đã hủy | neutral |

Local scheduling-form draft state is `InterviewDraftState` (`"draft" | "readyToSubmit"`) and is
**never** persisted as an InterviewStatus.

## OfferStatus

| Raw enum | FE constant | Label | Tone | Actionable |
|---|---|---|---|---|
| Draft | `OfferStatus.Draft` | Bản nháp | neutral | No |
| Sent | `…Sent` | Đã gửi | info | **Yes** (candidate accept/decline) |
| Accepted | `…Accepted` | Đã chấp nhận | success | No |
| Declined | `…Declined` | Đã từ chối | neutral | No |

`isOfferActionableStatus` = Sent only. Screens: SendOfferScreen, CandidateReviewDetailScreen,
MyApplicationScreen (candidate offer actions via backend `availableActions`).

## Notification

Read/unread is a boolean (`isRead`) from the backend; mark-read/mark-all-read mutate it (PATCH→POST
fallback) and update local state. No status enum.

## Error codes (apiError.ts)

`getApplicationErrorMessage(error, fallback)` resolves **errorCode → HTTP status → message → fallback**
using `APPLICATION_ERROR_MESSAGES`. Consumed by ApplyJobScreen submit and MyApplicationScreen
withdraw/accept/decline handlers. Codes mirror the backend ERROR-CONTRACT.

## Remaining UI risks (not addressed this phase)

- **UI-004 (MED):** `CandidateReviewDetailScreen.getAvailableDecisions` still computes reviewer
  transition buttons from a local status switch rather than backend allowed-transitions. It now uses
  `ApplicationStatus` semantics for offer gating, but the decision matrix remains FE-owned. Backend
  guards the real rule (INV-009/BR-006: reviewer cannot move `Offer → Hired/OfferDeclined`).
- **UI-006 (MED):** mutations on HR decision / offer / interview update only local state; parent
  lists/dashboards are not refetched (no React-Query). Candidate flows refetch via `loadData()`.
- **UI-007 (MED):** `ManagerDashboardScreen` recommendation/status tone still uses a local switch.
- **UI-008 (partial):** offer/interview raw comparisons in SendOfferScreen / CandidateReviewDetail /
  JobInterviewListScreen / CandidateInterviewScreen now route through the centralized normalizers,
  but JobInterviewListScreen/CandidateInterviewScreen still keep local interview normalizers.
- Pre-existing: 2 unrelated `tsc` errors (`CommonSelect.tsx`, `AiCopilotScreen.tsx`); ~30 pre-existing
  eslint errors in unrelated files; no FE test runner.
