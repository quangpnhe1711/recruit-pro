# Application Ownership Flow

**Status:** Phase 0 — **Target design**. Snapshot fields are _(planned)_; current behavior is verified
and labelled. `ManagerReview` (code status) **=** the **DepartmentHeadReview** business stage.

---

## 1. Target flow

```
Candidate applies to an Approved job
        ↓ at apply time, snapshot the owners onto the application  [planned]
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

Planned fields:

```
Application.AssignedRecruiterId        (planned)
Application.AssignedDepartmentHeadId   (planned)
```

If implementation keeps the legacy name `Application.AssignedManagerId`, the docs and code comments must
state that, in this workflow, it means the **assigned Department Head**.

Snapshot resolution order (target):

```
AssignedRecruiterId =
    Job.RecruiterId
    ?? Job.CreatedBy                         // legacy fallback (audit field)

AssignedDepartmentHeadId =
    Job.HiringManagerId                      // optional per-job override (planned)
    ?? Job.Department.HeadUserId             // department default (planned)
    ?? Job.ApprovedBy                        // legacy fallback (audit field)
```

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
| Owner snapshot fields | **None.** `Application` has `UserId`, `JobId`, `ReviewedBy?`, `Status`, `AppliedAt`, scores (`Application.cs`). | Add `AssignedRecruiterId` + `AssignedDepartmentHeadId` (or compat `AssignedManagerId`). |
| Reviewer of record | `Application.ReviewedBy` is set when a reviewer acts; it is **not** a pre-assigned owner taken at apply time. | Snapshot at apply time, independent of who later reviews. |
| Stage ownership | HR drives `Applied/Screening`; the generic `Manager` role drives `ManagerReview → Interview` (`[Authorize(Roles="HR,Manager")]` on decision endpoints; [APPLY-STATUS-FLOW.md](APPLY-STATUS-FLOW.md) §5). | Tie `ManagerReview` ownership to the application's **assigned DepartmentHead**, not "any Manager". |
| HR-first on apply | `new_application_received` → `Job.CreatedBy` + `HR` role (`NotificationEventService.PublishNewApplicationReceivedAsync`). DepartmentHead is **not** notified on apply. | Already HR-first by role; target switches the source to `AssignedRecruiterId`. |

This document is the target; the snapshot mechanism and DepartmentHead-scoped review are scheduled in
[IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md) (Phases 2–3).

---

## 5. Demo mapping

```
Phùng Nhật Quang applies         -> Application.Applied, AssignedRecruiterId = Nguyễn Thục Uyên (HR)
Nguyễn Thục Uyên screens         -> Screening (HR owns)
Nguyễn Thục Uyên passes to head  -> ManagerReview, owner = Trần Trọng Tiến Đạt (DepartmentHead)
Trần Trọng Tiến Đạt reviews      -> Interview / Offer follow
```
