# Test Matrix — Application Domain

All tests below are in `RecruitPro.Tests` and run under `dotnet test` (xUnit + Moq + FluentAssertions;
integration via Testcontainers PostgreSQL). Full suite at time of writing: **138 passed, 0 failed**.

| Test ID | Test | Layer | Rule / Bug | Expected |
|---|---|---|---|---|
| T-WD-001 | `WithdrawApplicationAsync_WhenAllowed_SetsStatusToWithdrawnNotRejected` | unit | BR-003 / BUG-001 | status → `Withdrawn` (not `Rejected`), 200 |
| T-WD-002 | `WithdrawApplicationAsync_WhenAlreadyClosed_Returns422` | unit | BR-003 | 422, status unchanged |
| T-RE-001 | `ApplyAsync_AfterWithdrawal_AllowsReapplyAndReturnsCreated` | unit | BR-002 / BUG-001 | 201, `AddAsync` called once |
| T-RE-002 | `ApplyAsync_AfterRejection_AllowsReapplyAndReturnsCreated` | unit | BR-002 | 201 |
| T-DUP-001 | `ApplyAsync_WhenActiveApplicationExists_Returns409Conflict` (5 states) | unit | BR-001 | 409, no insert |
| T-DUP-002 | `ApplyAsync_WhenCandidateAlreadyApplied_ReturnsConflict` | unit | BR-001 | 409 + exact message |
| T-MSG-001 | `ApplyAsync_WhenBlockedByNonDuplicateReason_DoesNotClaimAlreadyApplied` | unit | BR-004 / BUG-001 | 422, message ≠ "already applied" |
| T-500-001 | `ApplyAsync_WhenNotificationPublishFails_StillReturnsCreated` | unit | BR-005 / BUG-002 | 201 despite notification throw |
| T-500-002 | `ApplyAsync_WhenSemanticEnqueueFails_StillReturnsCreated` | unit | BR-005 / BUG-002 | 201 despite enqueue throw |
| T-NOTI-001 | `PublishNewApplicationReceivedAsync_FansOutToHrAndDeduplicatesRecipients` | unit | BR-005 | notify `job.CreatedBy` + role `HR`, no duplicates |
| T-CTX-001 | `GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied` | unit | BR-001/002 | canApply=true, alreadyApplied=false |
| T-WF-001 | `ApplicationStatusWorkflowTests.IsClosed_DetectsTerminalStates` (incl. Withdrawn) | unit | DL-002 / BR-007 | closed/active is derived from status |
| T-WF-002 | `ApplicationStatusWorkflowTests.CanCandidateWithdraw_MatchesPolicy` (incl. Withdrawn/Rejected) | unit | BR-003 / BR-007 | candidate actions depend on status |
| T-WF-003 | `ApplicationStatusWorkflowTests.CanTransition_RespectsConfiguredWorkflow` | unit | BR-006 / BR-007 | reviewer transitions depend on previous status |
| T-CV-001 | `ApplicationStatusValueConverter_MapsLegacyStrings` (incl. `withdrawn`/`Withdrawn`) | unit | DL-002 | round-trips Withdrawn |
| T-E2E-001 | `ApplicationService_WithdrawThenReapply_SucceedsWithoutConflictOr500` | integration (PG) | BUG-001/002 | withdraw 200 → reapply 201 → duplicate 409 |
| T-API-001 | `ApplicationController_ApplyContext_Should_Require_Candidate…` | integration (PG) | authz | candidate 200, HR-as-candidate 403 |
| T-API-002 | `Public_Apis_Should_Return_Controlled_404_Not_500_For_Invalid_Ids` | integration (PG) | 500 eradication | 404 not 500 |

## Implemented in the conformance pass (DL-007/008/009)

Full suite after this pass: **156 passed, 0 failed** (was 138). New/updated tests:

| Test ID | Test | Layer | Rule / Inv | Expected |
|---|---|---|---|---|
| T-RE-003 | `ApplicationConformanceTests.ApplyAsync_AfterHired_ForSameJob_IsBlocked` | unit | BR-002 / INV-015 | re-apply after `Hired` same job → 422 `APPLICATION_ALREADY_HIRED`, no insert |
| T-DUP-003 | `ApplicationConformanceTests.ApplyAsync_UsesExistsActive_NotArbitraryRow` | unit | BR-001 / INV-003 | active detected via EXISTS query even when fetched row is closed → 409 |
| T-DUP-004 | `RepositoryIntegrationTests.ActiveApplicationUniqueIndex_*` (reject active dup / allow closed dup) | integration (PG) | INV-014 | DB index rejects 2nd active row; allows closed duplicate |
| T-OFR-001 | `AcceptOffer_WhenNotInOfferStage_Returns422`, `AcceptOffer_WhenOfferNotSent_Returns422`, `UpdateApplicationDecision_FromOffer_IsRejected` | unit | BR-009 / INV-009 | forbidden offer combos & reviewer Offer→Hired → 422 |
| T-OFR-002 | `AcceptOffer_WhenOfferSent_SetsOfferAcceptedAndApplicationHired` | unit | INV-009 | Offer Accepted + Application Hired, 200 |
| T-OFR-003 | `DeclineOffer_WhenOfferSent_SetsOfferDeclinedAndApplicationOfferDeclined` | unit | INV-009 | Offer Declined + Application OfferDeclined, 200 |
| T-INT-001 | `CreateInterview_WhenApplicationNotInInterviewStage_Returns422` | unit | BR-008 / INV-008 | off-stage interview create → 422 `INTERVIEW_NOT_ACTIONABLE` |
| T-INT-002 | `Withdraw_FromInterviewStage_CancelsPendingInterview` | unit | BR-008 / INV-008 | pending `Scheduled` → `Canceled`; completed untouched |
| T-WF-004 | `ApplicationStatusWorkflowTests.IsReapplyEligibleClosedStatus_ExcludesHiredAndActiveStates` | unit | INV-015 | Rejected/Withdrawn/OfferDeclined eligible; Hired not |

## Still planned (not in this pass)

| Test ID | Intended test | Rule / Invariant | Notes |
|---|---|---|---|
| T-DASH-001 | `Analytics_DeriveFromCanonicalGroups_WithdrawnNotRejected` | BR-011 / INV-011 | INV-011 already PASS in code; explicit analytics assertion deferred |

## Coverage evidence (cobertura, this run)

| Class / method | Line | Branch |
|---|---|---|
| `ApplicationService.ApplyAsync` | 100% | 87.5% |
| `ApplicationService.WithdrawApplicationAsync` | 100% | 100% |
| `ApplicationService.GetApplyScreenAsync` | 100% | 100% |
| `ApplicationStatusWorkflow` | 100% | 83% |
| `ApplicationRepository` | 81% | 100% |
| `ApplicationStatusValueConverter` | 96% | 70% |
| `NotificationEventService.PublishNewApplicationReceivedAsync` | 95% | 100% |

> The defect-relevant paths are at/near full coverage. Whole-solution line-rate is ~25% because large
> unrelated services (Copilot, Candidate import, analytics) are out of scope for this iteration — this
> is reported honestly rather than claimed as 100%.

## Ownership tests (T-OWN-*)

> **Status (updated 2026-06-26, Phase 2/3 + hardening):** the snapshot/persistence tests (Phase 1) **and**
> the Phase 2/3 API + authorization tests (T-OWN-010…027) **and** the pre-commit hardening tests
> (T-OWN-028…032 + departments-route compatibility) are **implemented and passing** — full suite
> **187 passed, 0 failed** (was 162). The only remaining T-OWN items are the frontend checks (FV-OWN-*,
> Phase 4) and Phase-6 notification tests. See
> [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md) and
> [BUSINESS-RULES.md](BUSINESS-RULES.md) BR-OWN-*.

### Backend — implemented (Phase 1)

| Test ID | Test | Layer | Rule | Expected |
|---|---|---|---|---|
| T-OWN-001 | `RepositoryIntegrationTests.Department_PersistsAndReturnsHeadUserId` | integration (PG) | BR-OWN-001 | seeded Department round-trips `HeadUserId` |
| T-OWN-007 | `ApplicationOwnershipTests.ApplyAsync_SnapshotsRecruiterAndDepartmentHead_FromPrimarySources` | unit | BR-OWN-005 | application snapshots `AssignedRecruiterId` = `Job.RecruiterId`, `AssignedDepartmentHeadId` = `Department.HeadUserId` |
| T-OWN-007a | `ApplicationOwnershipTests.ApplyAsync_RecruiterFallsBackToCreatedBy_WhenRecruiterIdMissing` | unit | BR-OWN-005 | recruiter falls back to `Job.CreatedBy` |
| T-OWN-007b | `ApplicationOwnershipTests.ApplyAsync_DepartmentHeadFallsBackToApprovedBy_OnlyWhenHeadUserMissing` | unit | BR-OWN-005 | head falls back to `Job.ApprovedBy` only when no `HeadUserId` |
| T-OWN-007c | `ApplicationOwnershipTests.ApplyAsync_PrefersDepartmentHead_OverApprovedBy_WhenBothPresent` | unit | BR-OWN-005 | head not overridden by the audit fallback |
| T-OWN-007d | `RepositoryIntegrationTests.Application_PersistsOwnershipSnapshotFields` | integration (PG) | BR-OWN-005 | snapshot fields round-trip through EF + PostgreSQL |

### Backend — implemented (Phase 2/3)

| Test ID | Test | Layer | Rule | Expected |
|---|---|---|---|---|
| T-OWN-010 | `OwnershipServiceIntegrationTests.Department_ReturnsHeadUserInfo` | integration (PG) | BR-OWN-001 | department detail returns `headUser*` |
| T-OWN-011 | `Department_Update_SetsHeadUserId` (+ `_RejectsCandidateAsHead`) | integration (PG) | BR-OWN-001 | head set+persisted; non-HeadDepartment/SystemAdmin → 422 `INVALID_DEPARTMENT_HEAD` |
| T-OWN-012 | `AssignableRecruitmentOwners_ExcludesCandidates` | integration (PG) | BR-OWN-009 | candidate absent from both lists |
| T-OWN-013 | `AssignableRecruitmentOwners_ReturnsHrAndHeadDepartment` | integration (PG) | BR-OWN-002 | recruiters=HR, departmentHeads=HeadDepartment |
| T-OWN-014 | `CreateJob_PersistsRecruiterId` | integration (PG) | BR-OWN-002 | created job persists supplied `RecruiterId` |
| T-OWN-015 | `CreateJob_DefaultsRecruiterToCurrentHr_WhenMissing` | integration (PG) | BR-OWN-002 | omitted recruiter → creating user |
| T-OWN-016 | `JobDetail_ReturnsRecruiterAndDepartmentHead` | integration (PG) | BR-OWN-002/003 | job detail returns recruiter + department head |
| T-OWN-017 | `JobList_ReturnsEffectiveDepartmentHead` | integration (PG) | BR-OWN-003 | HR list returns `effectiveDepartmentHeadId` |
| T-OWN-018 | `OwnershipGuardUnitTests.DepartmentHead_CanApproveOwnDepartmentJob` | unit | BR-OWN-003 | head approves → 200 Approved |
| T-OWN-019 | `NonDepartmentHead_CannotApproveOtherDepartmentJob` | unit | BR-OWN-003 | non-head/non-admin → 403 `FORBIDDEN`, status unchanged |
| T-OWN-020 | `SystemAdmin_CanApproveAnyDepartmentJob` | unit | BR-OWN-003 | SystemAdmin → 200 Approved |
| T-OWN-021 | `JobApproval_WhenDepartmentHasNoHead_Returns422` | unit | BR-OWN-003 | no head → 422 `DEPARTMENT_HEAD_REQUIRED` |
| T-OWN-022 | `ApprovedJob_SetsApprovedByToCurrentDepartmentHead` | unit | BR-OWN-003 | `Job.ApprovedBy` = acting head |
| T-OWN-023 | `HrCanMoveAppliedToScreening` | unit | BR-OWN-006 | HR stage unaffected by head guard → 200 |
| T-OWN-024 | `HrCanMoveScreeningToManagerReview` | unit | BR-OWN-006 | HR stage → 200 |
| T-OWN-025 | `AssignedDepartmentHeadCanMoveManagerReviewToInterview` | unit | BR-OWN-007 | assigned head → 200 Interview |
| T-OWN-026 | `NonAssignedHeadCannotMoveManagerReviewToInterview` | unit | BR-OWN-007 | non-head/non-admin → 403 `FORBIDDEN` |
| T-OWN-027 | `ManagerReviewGuard_FallsBackOnlyWhenAssignedHeadMissing` | unit | BR-OWN-007 | Manager fallback only when head null; else 403 |

(T-OWN-002/003/004 from the Phase-0 plan are subsumed by T-OWN-014/018/019.)

### Backend — pre-commit hardening (Phase 2/3)

| Test ID | Test | Layer | Rule | Expected |
|---|---|---|---|---|
| T-OWN-028 | `JobStatusGuardIntegrationTests.UpdateJobStatus_WithoutAuth_Returns401` | integration (PG) | BR-OWN-003 | hardened `PATCH /api/jobs/{id}/status` requires auth → 401 |
| T-OWN-029 | `UpdateJobStatus_ApproveByNonHead_Returns403` | integration (PG) | BR-OWN-003 | HR (non-head) → 403 `FORBIDDEN` |
| T-OWN-030 | `UpdateJobStatus_ApproveByDepartmentHead_Succeeds` | integration (PG) | BR-OWN-003 | dept head → 200 Approved |
| T-OWN-031 | `UpdateJobStatus_ApproveBySystemAdmin_Succeeds` | integration (PG) | BR-OWN-003 | SystemAdmin → 200 Approved |
| T-OWN-032 | `UpdateJobStatus_WhenDepartmentHasNoHead_Returns422` | integration (PG) | BR-OWN-003 | no head → 422 `DEPARTMENT_HEAD_REQUIRED` |
| T-OWN-033 | `Departments_LookupRoute_StillReturnsDepartmentsWithHead` | integration (PG) | compat | `GET /api/departments` still works, now incl. head |
| T-OWN-034 | `ApprovalQueueAccessIntegrationTests.HeadDepartment_CanViewOwnDepartmentApprovalQueue` | integration (PG) | BR-OWN-003 | dept head sees the queue for the department they head |
| T-OWN-035 | `HeadDepartment_CannotViewOtherDepartmentApprovalQueue` | integration (PG) | BR-OWN-003 | head of nothing → empty queue (no other-dept jobs) |
| T-OWN-036 | `SystemAdmin_CanViewAllApprovalQueue` | integration (PG) | BR-OWN-003 | SystemAdmin sees all departments' pending jobs |
| T-OWN-037 | `HeadDepartment_CanViewOwnApprovalDetail` | integration (PG) | BR-OWN-003 | dept head opens detail for their department → 200 |
| T-OWN-038 | `HeadDepartment_CannotViewOtherDepartmentApprovalDetail` | integration (PG) | BR-OWN-003 | other department's detail → 403 `FORBIDDEN` |
| T-OWN-039 | `Manager_WhoIsDepartmentHead_CanViewQueueAndDetail` | integration (PG) | BR-OWN-003 | compat — a Manager who IS the head keeps queue + detail |
| T-OWN-040 | `LegacyManager_NotDepartmentHead_CannotApproveOrViewOtherDepartmentJob` | integration (PG) | BR-OWN-003 | non-head Manager → empty queue, 403 detail, 403 submit |
| T-STATUS-001 | `CandidateApplicationStatusContractTests.CandidateApplications_Screening_ReturnsCanonicalStatusAndStatusLabel` | integration (PG) | BR-APPLICATION-012 | `status` = `Screening`; `statusLabel` = `HR đang sàng lọc`; withdraw available |
| T-STATUS-002 | `CandidateApplications_Interview_ReturnsCanonicalStatusAndStatusLabel` | integration (PG) | BR-APPLICATION-012 | `status` = `Interview`; `statusLabel` = `Phỏng vấn` |
| T-STATUS-003 | `CandidateApplications_Rejected_ReturnsCanonicalStatusAndNoWithdraw` | integration (PG) | BR-APPLICATION-012 | `status` = `Rejected`; no `withdraw` action |

### Backend — planned (later phases)

| Test ID | Intended test | Rule | Phase |
|---|---|---|---|
| T-OWN-005 | `ApprovedJob_BecomesPublicAndApplyable` | BR-OWN-004 | already enforced (INV-001); explicit test deferred |
| T-OWN-006 | `Candidate_CannotApplyToNonApprovedJob` | BR-OWN-004 | already enforced; explicit test deferred |
| T-OWN-008 | `AppliedAndScreening_OwnedByHR` (notification) | BR-OWN-006 | Phase 6 (notification routing) |
| T-OWN-009 | `ManagerReview_OwnedByDepartmentHead` (notification) | BR-OWN-007 | Phase 6 |

### Frontend (planned verification)

| ID | Verification |
|---|---|
| FV-OWN-001 | Department screen can show/edit the DepartmentHead. |
| FV-OWN-002 | Job create screen shows Department and Recruiter selectors. |
| FV-OWN-003 | Job create screen displays the DepartmentHead based on the selected Department. |
| FV-OWN-004 | Job detail shows Recruiter and DepartmentHead. |
| FV-OWN-005 | Application detail shows Assigned Recruiter and Assigned DepartmentHead. |
| FV-STATUS-001 | My Applications: a `Screening` application shows the Screening badge, not `Rejected`/`Từ chối`. |
| FV-STATUS-002 | My Applications: an `Interview` application shows the Interview badge, not `Rejected`/`Từ chối`. |
| FV-STATUS-003 | `ManagerReview` displays as "Head Review". |
| FV-STATUS-004 | An unknown status displays neutral `Unknown`, never `Rejected`. |
| FV-STATUS-005 | No Vietnamese label drives status logic (filter/actions key off canonical status). |
