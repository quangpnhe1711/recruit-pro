-- ============================================================================
-- Patch: 20260701 — Copilot v2 ranking idempotency fingerprint
--
-- Adds copilot_ranking_sessions.input_hash: a SHA-256 fingerprint of the
-- effective ranking input (job, user, normalized prompt/criteria/rules and the
-- screening candidate evidence). CopilotService reuses the latest session with a
-- matching fingerprint instead of re-running the AI provider for unchanged input
-- (duplicate-ranking-spam prevention — v2 §8).
--
-- WHY THIS PATCH EXISTS
-- Production databases are provisioned from init.sql (now updated) and never run
-- EF migrations automatically. Apply THIS patch against an existing database that
-- predates the column. Idempotent and safe to re-run.
--
-- Keep in sync with:
--   RecruitPro.Infrastructure/Data/AppDbContext.cs (CopilotRankingSession mapping)
--   init.sql (copilot_ranking_sessions block)
-- ============================================================================

BEGIN;

ALTER TABLE public.copilot_ranking_sessions
    ADD COLUMN IF NOT EXISTS input_hash character varying(64);

CREATE INDEX IF NOT EXISTS ix_copilot_ranking_sessions_job_user_input_hash
    ON public.copilot_ranking_sessions USING btree (job_id, user_id, input_hash);

COMMIT;

-- Verification — confirm the column now exists (expect one row).
SELECT column_name
FROM information_schema.columns
WHERE table_name = 'copilot_ranking_sessions' AND column_name = 'input_hash';
