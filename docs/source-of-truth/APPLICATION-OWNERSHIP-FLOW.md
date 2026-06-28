# Application Ownership Flow

**Status:** Phase 2/3 — the apply-time **owner snapshot** (Phase 1), the **DTO/API exposure** of the
assigned owners on HR/internal endpoints (Phase 2), and **DepartmentHead-scoped review authorization**
(Phase 3) are all **implemented**. The `ManagerReview → Interview/Rejected` decision is now restricted to
the application's assigned DepartmentHead or a SystemAdmin (BR-OWN-007), resolved via
`IApplicationOwnershipResolver`. **Frontend (Phase 4) is implemented** (ownership display, approval UI,
canonical English status, Playwright E2E); **notification use (Phase 6)** remains **not implemented**.
`ManagerReview` (code status) **=** the **DepartmentHeadReview** business stage.

---

## 1. Target flow

```
Candidate applies to an Approved job
        ↓ at apply time, snapshot the owners onto the application  [implemented — Phase 1]
Application = Applied
        ↓ owned by HR / Recruiter (AssignedRecruiterId)
HR screens → Screening                 (owned by HR / Recruiter)
        ↓ HR passes the candidate to the head
ManagerReview / DepartmentHeadReview   (owned by DepartmentHead, AssignedDepartmentHeadId)
        ↓ head passes
Interview                              (HR coordinates; DepartmentHead/Interviewer evaluates)
        ↓
Offer                                  (HR sends; Candidate responds)
        ↓ accept → Hired   (HR close-out; DepartmentHead informed)
          decline → OfferDeclined
```

Ownership by stage:

```
Applied        -> HR / Recruiter
Screening      -> HR / Recruiter
ManagerReview  -> DepartmentHead         (DepartmentHeadReview business stage)
Interview      -> HR / Recruiter (coordinates) + DepartmentHead / Interviewer (evaluates)
Offer          -> Candidate (response), HR / Recruiter (coordination)
Hired          -> HR / Recruiter (close-out), DepartmentHead (informed)
Rejected       -> closed (company decision)
Withdrawn      -> closed (candidate decision)
OfferDeclined  -> closed (candidate decision; HR follow-up)
```

---

## 2. Owner snapshot at apply time (BR-OWN-005)

When the Candidate applies, the application should **snapshot** the current recruiter and DepartmentHead
so that later changes to the job's recruiter or the department's head do not silently re-route in-flight
applications.

Fields (implemented, Phase 1):

```
Application.AssignedRecruiterId        (implemented — Application.cs:21)
Application.AssignedDepartmentHeadId   (implemented — Application.cs:23)
```

The implemented names are `AssignedRecruiterId` / `AssignedDepartmentHeadId` (the legacy
`AssignedManagerId` name was **not** used).

Snapshot resolution order (implemented, Phase 1 — `ApplicationService.ApplyAsync:166-174`):

```
AssignedRecruiterId =
    Job.RecruiterId
    ?? Job.CreatedBy                         // legacy fallback (audit field)

AssignedDepartmentHeadId =
    Job.Department.HeadUserId                // department default
    ?? Job.ApprovedBy                        // legacy fallback (audit field)
```

`Job.HiringManagerId` (an optional per-job head override) is **deferred** — the implemented model is
`EffectiveDepartmentHead = Department.HeadUserId ?? Job.ApprovedBy`.

---

## 3. Ownership rules (cross-reference)

- **HR-first ownership (BR-OWN-006).** On apply, the primary owner is `AssignedRecruiterId`. The
  DepartmentHead is **not** notified of every new application — HR screens first.
- **DepartmentHead after screening (BR-OWN-007).** When HR moves the application to
  `ManagerReview`/DepartmentHeadReview, the primary owner becomes `AssignedDepartmentHeadId`.
- **Interview & offer (BR-OWN-008).** HR coordinates interviews and sends offers; the DepartmentHead
  evaluates/participates; the Candidate owns the offer response.
- Re-apply creates a **new** application row (INV-007) and therefore takes a **fresh** owner snapshot.

---

## 4. Current reality (verified from code)

| Aspect | Current behavior | Gap vs target |
|---|---|---|
| Owner snapshot fields | **Implemented (Phase 1).** `Application.AssignedRecruiterId` + `AssignedDepartmentHeadId` exist and are set on apply (`Application.cs:21-23`, `ApplicationService.cs:166-174`). | — done; DTO/API exposure is Phase 2. |
| Reviewer of record | `Application.ReviewedBy` is set when a reviewer acts; it is **not** the apply-time owner. | Snapshot now taken at apply time (`AssignedRecruiterId`/`AssignedDepartmentHeadId`), independent of `ReviewedBy`. |
| Stage ownership | HR drives `Applied/Screening`; **`ManagerReview → Interview/Rejected` is now authorized against the application's `AssignedDepartmentHeadId` or a SystemAdmin** (else 403), with a Manager-role fallback only when no head was snapshotted (`UpdateApplicationDecisionAsync` guard). | **Closed** — tied to the assigned DepartmentHead, not "any Manager". |
| HR-first on apply | `new_application_received` → `Job.CreatedBy` + `HR` role (`NotificationEventService.PublishNewApplicationReceivedAsync`). DepartmentHead is **not** notified on apply. | Already HR-first by role; target switches the source to `AssignedRecruiterId`. |

The **snapshot mechanism (Phase 1)** and **DepartmentHead-scoped review authorization (Phase 3)** are
**implemented**; only notification routing (Phase 6) remains scheduled in
[IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md).

---

## 5. Demo mapping

```
Phùng Nhật Quang applies         -> Application.Applied, AssignedRecruiterId = Nguyễn Thục Uyên (HR)
Nguyễn Thục Uyên screens         -> Screening (HR owns)
Nguyễn Thục Uyên passes to head  -> ManagerReview, owner = Trần Trọng Tiến Đạt (DepartmentHead)
                                    + stamps DepartmentHeadReviewRequestedAt (the "received for review" date)
Trần Trọng Tiến Đạt reviews      -> Interview
Nguyễn Thục Uyên schedules + interview happens + marks Completed
Nguyễn Thục Uyên sends offer/rejection email -> Offer / Rejected (email-gated, BR-WF-001/002)
```

---

## 6. Head Review hand-off date + post-Head-Review gates (BR-WF-001…005)

`Screening → ManagerReview` records `Application.DepartmentHeadReviewRequestedAt` (set once, never
overwritten). The Manager/DepartmentHead review queue and detail show this as the **"received for
review"** work date — not `AppliedAt` — so the DepartmentHead sees how long the application has waited on
**them**. Legacy rows with a null value fall back to `AppliedAt` for display. Exposed as
`departmentHeadReviewRequestedAt` on the manager review queue item and the application review detail DTOs.

Once in `Interview`, HR must schedule an interview (INV-008) and the interview must be **completed**
before the application can be offered or rejected. Both `Offer` and `Rejected` are **email-gated**
(offer-send / rejection-email flows) and transition only after the email send succeeds;
`ManagerReview → Rejected` keeps the BR-OWN-007 head/SystemAdmin guard. See
[STATE-MACHINE.md](STATE-MACHINE.md) and [BUSINESS-RULES.md](BUSINESS-RULES.md) (BR-WF-001…005).
**Notification dispatch remains Phase 6 (not implemented).**
