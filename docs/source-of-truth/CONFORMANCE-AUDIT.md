# Code Conformance Audit — Application Domain

Audit of the current Backend + DB + API + Frontend implementation against
[00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md) invariants. Verified by reading code
(not assumed). Status legend: PASS / FAIL / PARTIAL.

| Inv | Expected | Backend | DB | API | Frontend | Tests | Status | Sev | Required fix |
|---|---|---|---|---|---|---|---|---|---|
| INV-001 | Only Approved, non-expired job accepts apply | `BuildApplyEligibility` checks `Status!=Approved` + deadline | n/a | 422 blocker | `canApply`/`blockers` | apply tests | **PASS** | — | — |
| INV-002 | Profile + current resume required | `BuildApplyEligibility` checks contact + `GetCurrentResume` | n/a | 422 blocker | `canApply` | apply tests | **PASS** | — | — |
| INV-003 | AlreadyApplied = EXISTS active | `GetExistingApplicationAsync` = `FirstOrDefault(JobId==)` — **arbitrary row**, then `IsClosed` on it | no constraint | depends on above | uses `alreadyApplied` | none for dirty data | **FAIL** | High | EXISTS-active repo query; ordering-independent |
| INV-004 | Withdrawn ≠ Rejected | `WithdrawApplicationAsync` sets `Withdrawn` | string col | 200 | neutral badge | T-WD-001 | **PASS** | — | — |
| INV-007 | Re-apply = new row | `ApplyAsync` always `AddAsync` | n/a | 201 | n/a | T-RE-001/002 | **PASS** | — | — |
| INV-008 | Application.status gates Interview/Offer | `CreateInterview` requires ManagerReview/Interview; offer accept/decline require status; **no cascade-cancel on withdraw/reject** | n/a | 400 (should be 422) | actions from `availableActions` | partial | **PARTIAL** | Med | cascade-cancel; create-guard → 422 |
| INV-009 | Hired only from candidate accept | `AllowedTransitions[Offer]={Hired,OfferDeclined}` + `UpdateApplicationDecisionAsync` lets HR/Manager move `Offer→Hired`; `AcceptOffer` also has a `==Hired` bypass | n/a | reviewer can hire | n/a | none | **FAIL** | High | block reviewer transitions from `Offer`; remove Hired bypass |
| INV-010 | Notification failure ≠ failed action | `ApplyAsync` try/catch ✔; `UpdateApplicationDecisionAsync` publishes **after commit but not guarded** → can 500 | n/a | risk 500 | n/a | T-500-001/002 (apply only) | **PARTIAL** | Med | guard post-commit notifications |
| INV-011 | Analytics from canonical groups | repo active/closed = explicit `!=` set (matches groups) but hard-coded, not `IsClosed` | n/a | n/a | n/a | partial | **PASS** | Low | optional: use `IsClosed` helper |
| INV-012 | FE not infer from labels | n/a | n/a | n/a | `normalizeApplicationStatusKey` + `availableActions`; no label-based logic | n/a | **PASS** | — | — |
| INV-013 | Business errors 4xx not 500 | apply 409/422 ✔; offer accept/decline **400**; invalid transition **400**; interview create **400** | n/a | wrong codes | parses status+msg | partial | **PARTIAL** | Med | business states → 422 |
| INV-014 | Duplicate-active enforced at DB | service-only check | **no unique index** | n/a | n/a | none | **FAIL** | High | partial unique index + 409 mapping |
| INV-015 | Hired terminal for jobId | `IsClosed` includes Hired; re-apply only blocks on active → **Hired re-apply allowed** | n/a | no blocker | n/a | none | **FAIL** | High | `IsReapplyEligibleClosedStatus`; Hired blocker → 422 |

## Summary (as audited, before fixes)

- **PASS:** INV-001, INV-002, INV-004, INV-007, INV-011, INV-012 (6)
- **PARTIAL:** INV-008, INV-010, INV-013 (3)
- **FAIL:** INV-003, INV-009, INV-014, INV-015 (4)

## Post-fix status (this pass)

All FAIL/PARTIAL items above were remediated and verified by the 156-test suite (Testcontainers
PostgreSQL):

| Inv | Fix | Verifying test |
|---|---|---|
| INV-003 | `HasActiveApplicationAsync` EXISTS-active query; eligibility no longer keys off an arbitrary row | T-DUP-003 (`ApplyAsync_UsesExistsActive_NotArbitraryRow`) |
| INV-008 | `CancelPendingInterviews` on withdraw/reject; interview create-guard → 422 | T-INT-001, T-INT-002 |
| INV-009 | reviewer blocked from transitioning out of `Offer`; `AcceptOffer` Hired-bypass removed | T-OFR-001/002/003, `UpdateApplicationDecision_FromOffer_IsRejected` |
| INV-010 | post-commit status-changed notification wrapped in try/catch | covered by existing 500-eradication + new transition tests |
| INV-013 | offer/transition/interview business failures → 422 (was 400) | T-OFR-001, T-INT-001, decision tests |
| INV-014 | partial unique index `ux_applications_active_user_job` (model + migration) + 409 mapping | T-DUP-004 (`ActiveApplicationUniqueIndex_*`) |
| INV-015 | `IsReapplyEligibleClosedStatus`; Hired blocker → 422 | T-RE-003 (`ApplyAsync_AfterHired_ForSameJob_IsBlocked`) |

After this pass: **all 13 audited invariants PASS in code and tests.** See [DECISION-LOG.md](DECISION-LOG.md)
DL-007/008/009 and the final report for residual risks.
