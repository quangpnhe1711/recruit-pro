-- ============================================================================
-- Patch: 20260626 — Department Head ownership foundation (Phase 1)
--
-- Adds the ownership data model and backfills existing data. Apply against an
-- existing database that predates the Phase-1 ownership columns. The same DDL +
-- backfill is also embedded in init.sql for fresh databases; this script is
-- idempotent and safe to re-run.
--
-- See docs/source-of-truth/RECRUITMENT-OWNERSHIP-MATRIX.md, JOB-APPROVAL-FLOW.md,
--     APPLICATION-OWNERSHIP-FLOW.md, BUSINESS-RULES.md (BR-OWN-*),
--     IMPLEMENTATION-PLAN-OWNERSHIP.md.
--
-- Demo persona mapping:
--   Candidate      = Phùng Nhật Quang    (4071353e-5816-4746-a8b6-c0bc3113c44d)
--   HR / Recruiter = Nguyễn Thục Uyên    (e781ccd9-e6f8-4ce1-b15d-e142f8977a4e)
--   DepartmentHead = Trần Trọng Tiến Đạt (721b1851-349a-48aa-acae-feed1c1843ed)
--
-- Ownership model:
--   departments.head_user_id                 = default approver + business reviewer.
--   jobs.recruiter_id                        = business owner; created_by/approved_by are audit fields.
--   applications.assigned_recruiter_id       = jobs.recruiter_id ?? jobs.created_by (snapshot).
--   applications.assigned_department_head_id = departments.head_user_id ?? jobs.approved_by (snapshot).
-- ============================================================================

BEGIN;

-- 1. Add Department.HeadUserId column.
ALTER TABLE public.departments ADD COLUMN IF NOT EXISTS head_user_id uuid;

-- 2. Add Job.RecruiterId column.
ALTER TABLE public.jobs ADD COLUMN IF NOT EXISTS recruiter_id uuid;

-- 3. Add Application.AssignedRecruiterId column.
ALTER TABLE public.applications ADD COLUMN IF NOT EXISTS assigned_recruiter_id uuid;

-- 4. Add Application.AssignedDepartmentHeadId column.
ALTER TABLE public.applications ADD COLUMN IF NOT EXISTS assigned_department_head_id uuid;

-- 5. Foreign keys to users — RESTRICT / NO ACTION (consistent with jobs_created_by_fkey,
--    jobs_approved_by_fkey, applications_reviewed_by_fkey). Never cascade-delete on user removal.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'departments_head_user_id_fkey') THEN
        ALTER TABLE ONLY public.departments
            ADD CONSTRAINT departments_head_user_id_fkey
            FOREIGN KEY (head_user_id) REFERENCES public.users(id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'jobs_recruiter_id_fkey') THEN
        ALTER TABLE ONLY public.jobs
            ADD CONSTRAINT jobs_recruiter_id_fkey
            FOREIGN KEY (recruiter_id) REFERENCES public.users(id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'applications_assigned_recruiter_id_fkey') THEN
        ALTER TABLE ONLY public.applications
            ADD CONSTRAINT applications_assigned_recruiter_id_fkey
            FOREIGN KEY (assigned_recruiter_id) REFERENCES public.users(id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'applications_assigned_department_head_id_fkey') THEN
        ALTER TABLE ONLY public.applications
            ADD CONSTRAINT applications_assigned_department_head_id_fkey
            FOREIGN KEY (assigned_department_head_id) REFERENCES public.users(id);
    END IF;
END $$;

-- 6. Indexes.
CREATE INDEX IF NOT EXISTS ix_departments_head_user_id
    ON public.departments USING btree (head_user_id);
CREATE INDEX IF NOT EXISTS ix_jobs_recruiter_id
    ON public.jobs USING btree (recruiter_id);
CREATE INDEX IF NOT EXISTS ix_applications_assigned_recruiter_id
    ON public.applications USING btree (assigned_recruiter_id);
CREATE INDEX IF NOT EXISTS ix_applications_assigned_department_head_id
    ON public.applications USING btree (assigned_department_head_id);

-- 7. Backfill.
-- 7a. Department head — demo: Trần Trọng Tiến Đạt heads all departments (sole DepartmentHead persona).
--     For a real migration, replace this with a per-department head mapping.
UPDATE public.departments
SET head_user_id = '721b1851-349a-48aa-acae-feed1c1843ed'::uuid
WHERE head_user_id IS NULL;

-- 7b. Job recruiter — from the audit created_by field.
UPDATE public.jobs
SET recruiter_id = created_by
WHERE recruiter_id IS NULL
  AND created_by IS NOT NULL;

-- 7c. Application ownership snapshot.
UPDATE public.applications a
SET assigned_recruiter_id = COALESCE(j.recruiter_id, j.created_by),
    assigned_department_head_id = COALESCE(d.head_user_id, j.approved_by)
FROM public.jobs j
LEFT JOIN public.departments d ON j.department_id = d.id
WHERE a.job_id = j.id
  AND (
    a.assigned_recruiter_id IS NULL
    OR a.assigned_department_head_id IS NULL
  );

COMMIT;

-- ============================================================================
-- 8. Verification queries — run after COMMIT. Each should return ZERO rows for a
--    clean ownership state. Any rows returned are dirty data to investigate
--    (do NOT silently ignore them).
-- ============================================================================

-- 8.1 Departments without a head.
SELECT 'departments_without_head' AS check_name, d.id, d.name
FROM public.departments d
WHERE d.head_user_id IS NULL;

-- 8.2 Approved jobs without a recruiter.
SELECT 'approved_jobs_without_recruiter' AS check_name, j.id, j.title
FROM public.jobs j
WHERE j.status = 'Approved' AND j.recruiter_id IS NULL;

-- 8.3 Applications without an assigned recruiter.
SELECT 'applications_without_assigned_recruiter' AS check_name, a.id, a.status
FROM public.applications a
WHERE a.assigned_recruiter_id IS NULL;

-- 8.4 Applications in ManagerReview/Interview/Offer without an assigned department head.
SELECT 'review_stage_applications_without_head' AS check_name, a.id, a.status
FROM public.applications a
WHERE a.status IN ('ManagerReview', 'Interview', 'Offer')
  AND a.assigned_department_head_id IS NULL;

-- 8.5 Duplicate ACTIVE applications for the same candidate/job (INV-003/014).
SELECT 'duplicate_active_applications' AS check_name, a.user_id, a.job_id, COUNT(*) AS active_count
FROM public.applications a
WHERE a.status IN ('Applied', 'Screening', 'ManagerReview', 'Interview', 'Offer')
GROUP BY a.user_id, a.job_id
HAVING COUNT(*) > 1;

-- 8.6 Jobs where the same user is candidate (has an application) AND creator/recruiter/approver.
SELECT 'candidate_is_owner_of_job' AS check_name, j.id, j.title, a.user_id
FROM public.jobs j
JOIN public.applications a ON a.job_id = j.id
WHERE a.user_id IN (j.created_by, j.recruiter_id, j.approved_by);

-- 8.7 Demo personas assigned to the wrong role in the main flow.
--     Candidate (Phùng Nhật Quang) must never create/approve/recruit a job.
SELECT 'candidate_persona_as_job_owner' AS check_name, j.id, j.title
FROM public.jobs j
WHERE '4071353e-5816-4746-a8b6-c0bc3113c44d'::uuid IN (j.created_by, j.recruiter_id, j.approved_by);
--     HR (Nguyễn Thục Uyên) must not be a department head in the main flow.
SELECT 'hr_persona_as_department_head' AS check_name, d.id, d.name
FROM public.departments d
WHERE d.head_user_id = 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e'::uuid;
--     DepartmentHead (Trần Trọng Tiến Đạt) must not be a recruiter in the main flow.
SELECT 'head_persona_as_recruiter' AS check_name, j.id, j.title
FROM public.jobs j
WHERE j.recruiter_id = '721b1851-349a-48aa-acae-feed1c1843ed'::uuid;

-- 8.8 Pre-existing (non-ownership) offer/hired data-quality checks — REPORTED, not fixed by this patch
--     (BR-APPLICATION-009). init.sql now seeds consistent offer rows (Offer -> Sent, Hired -> Accepted),
--     so a fresh DB returns zero rows here; on an existing DB any rows returned are dirty data to fix.
SELECT 'offer_status_without_offer_row' AS check_name, a.id
FROM public.applications a
LEFT JOIN public.application_offers o ON o.application_id = a.id
WHERE a.status = 'Offer' AND o.id IS NULL;

SELECT 'hired_without_accepted_offer' AS check_name, a.id
FROM public.applications a
LEFT JOIN public.application_offers o ON o.application_id = a.id AND o.status = 'Accepted'
WHERE a.status = 'Hired' AND o.id IS NULL;

-- 8.9 Snapshot mapping spot-check for the main demo application
--     (candidate Phùng Nhật Quang → recruiter Nguyễn Thục Uyên, head Trần Trọng Tiến Đạt).
SELECT 'main_demo_application_ownership' AS check_name,
       a.id, a.user_id, a.assigned_recruiter_id, a.assigned_department_head_id
FROM public.applications a
WHERE a.user_id = '4071353e-5816-4746-a8b6-c0bc3113c44d'::uuid;
