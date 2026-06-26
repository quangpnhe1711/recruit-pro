-- ============================================================================
-- Patch: 20260626 — Active-application unique index (INV-014 / INV-003)
--
-- Enforces "at most one ACTIVE application per (candidate, job)" at the database
-- level as defense-in-depth behind the service check. Closed rows (Hired,
-- Rejected, OfferDeclined, Withdrawn) are history and are EXCLUDED from the
-- filter so re-apply remains possible.
--
-- The same index is embedded in init.sql for fresh databases and created by EF
-- migration 20260626023451_AddActiveApplicationUniqueIndex. The filter below is
-- byte-for-byte identical to AppDbContext.HasFilter(...) so the EF model and the
-- DB agree.
--
-- Apply against an existing database that predates the index. Idempotent and
-- safe to re-run.
--
-- See docs/source-of-truth/BUSINESS-RULES.md (INV-003/INV-014),
--     IMPLEMENTATION-PLAN-OWNERSHIP.md.
-- ============================================================================

-- 1. Verification — dirty duplicate ACTIVE rows. Run this FIRST and read the
--    result. Any rows returned are candidate/job pairs with more than one active
--    application; the unique index CANNOT be created until they are resolved.
SELECT user_id, job_id, COUNT(*)
FROM public.applications
WHERE status IN ('Applied', 'Screening', 'ManagerReview', 'Interview', 'Offer')
GROUP BY user_id, job_id
HAVING COUNT(*) > 1;

-- 2. Create the index, but refuse loudly if dirty duplicates exist (rather than
--    letting CREATE UNIQUE INDEX fail with an opaque error, or silently skipping).
DO $$
DECLARE
    dup_count integer;
BEGIN
    SELECT COUNT(*) INTO dup_count
    FROM (
        SELECT user_id, job_id
        FROM public.applications
        WHERE status IN ('Applied', 'Screening', 'ManagerReview', 'Interview', 'Offer')
        GROUP BY user_id, job_id
        HAVING COUNT(*) > 1
    ) dups;

    IF dup_count > 0 THEN
        RAISE EXCEPTION
            'Refusing to create ux_applications_active_user_job: % (candidate, job) pair(s) have duplicate ACTIVE applications. Resolve them first (see the SELECT in step 1).',
            dup_count;
    END IF;

    -- IF NOT EXISTS keeps this safe to re-run against a DB whose schema was built
    -- from init.sql / EnsureCreated() (which already define the index).
    CREATE UNIQUE INDEX IF NOT EXISTS ux_applications_active_user_job
    ON public.applications (user_id, job_id)
    WHERE status IN ('Applied', 'Screening', 'ManagerReview', 'Interview', 'Offer');
END $$;

-- 3. Verification — confirm the index now exists (expect one row).
SELECT indexname
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename = 'applications'
  AND indexname = 'ux_applications_active_user_job';
