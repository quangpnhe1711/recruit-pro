-- ============================================================================
-- Patch: 20260630 — Copilot v2 artifacts
--
-- Creates the three Copilot v2 tables that back the AI Copilot screen:
--   * copilot_prompt_templates   — saved HR prompt templates
--   * candidate_fit_analyses     — per-candidate AI fit-analysis snapshots
--   * copilot_generated_artifacts — persisted output of the v2 AI tools
--     (candidate search, fit analysis, interview questions, shortlist, email)
--
-- WHY THIS PATCH EXISTS
-- These tables live ONLY in EF migration 20260630000000_AddCopilotV2Artifacts,
-- which is never applied automatically (the API does not Migrate() on startup and
-- the migration ships without a .Designer.cs). Production databases are
-- provisioned from init.sql, which historically omitted them. As a result:
--   * GET /api/copilot/prompt-templates            -> 500 (relation does not exist)
--   * GET /api/copilot/artifacts?jobId=...&take=10  -> 500 (relation does not exist)
--   * the v2 AI tools 500 when they try to persist a fit analysis / artifact
--
-- The same DDL is now embedded in init.sql for fresh databases. Apply THIS patch
-- against an existing database that predates the tables. Idempotent and safe to
-- re-run (every object uses IF NOT EXISTS).
--
-- Keep in sync with:
--   RecruitPro.Infrastructure/Migrations/20260630000000_AddCopilotV2Artifacts.cs
--   init.sql (Copilot v2 artifacts block)
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.copilot_prompt_templates (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    owner_user_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    template_type character varying(60) NOT NULL,
    prompt text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.candidate_fit_analyses (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    audit_id uuid NOT NULL,
    job_id uuid NOT NULL,
    candidate_user_id uuid NOT NULL,
    application_id uuid NOT NULL,
    fit_label character varying(40) NOT NULL,
    confidence_score numeric(5,2) NOT NULL,
    total_score numeric(5,2) NOT NULL,
    strengths_json jsonb DEFAULT '[]'::jsonb NOT NULL,
    gaps_json jsonb DEFAULT '[]'::jsonb NOT NULL,
    evidence_json jsonb DEFAULT '[]'::jsonb NOT NULL,
    summary text NOT NULL,
    provider_name character varying(100) NOT NULL,
    model_name character varying(100) NOT NULL,
    fallback_used boolean DEFAULT false NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.copilot_generated_artifacts (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    owner_user_id uuid NOT NULL,
    job_id uuid,
    application_id uuid,
    artifact_type character varying(60) NOT NULL,
    prompt text DEFAULT ''::text NOT NULL,
    payload_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    provider_name character varying(100) NOT NULL,
    model_name character varying(100) NOT NULL,
    fallback_used boolean DEFAULT false NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Primary keys and foreign keys — guarded so the patch is safe to re-run against
-- a DB whose schema was already built from init.sql.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'copilot_prompt_templates_pkey') THEN
        ALTER TABLE ONLY public.copilot_prompt_templates ADD CONSTRAINT copilot_prompt_templates_pkey PRIMARY KEY (id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'candidate_fit_analyses_pkey') THEN
        ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_pkey PRIMARY KEY (id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'copilot_generated_artifacts_pkey') THEN
        ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'copilot_prompt_templates_owner_user_id_fkey') THEN
        ALTER TABLE ONLY public.copilot_prompt_templates ADD CONSTRAINT copilot_prompt_templates_owner_user_id_fkey FOREIGN KEY (owner_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'candidate_fit_analyses_job_id_fkey') THEN
        ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'candidate_fit_analyses_candidate_user_id_fkey') THEN
        ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_candidate_user_id_fkey FOREIGN KEY (candidate_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'candidate_fit_analyses_application_id_fkey') THEN
        ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'copilot_generated_artifacts_owner_user_id_fkey') THEN
        ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_owner_user_id_fkey FOREIGN KEY (owner_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'copilot_generated_artifacts_job_id_fkey') THEN
        ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'copilot_generated_artifacts_application_id_fkey') THEN
        ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_copilot_prompt_templates_owner_type_active ON public.copilot_prompt_templates USING btree (owner_user_id, template_type, is_active);
CREATE INDEX IF NOT EXISTS ix_candidate_fit_analyses_job_candidate_created ON public.candidate_fit_analyses USING btree (job_id, candidate_user_id, created_at);
CREATE INDEX IF NOT EXISTS ix_candidate_fit_analyses_audit_id ON public.candidate_fit_analyses USING btree (audit_id);
CREATE INDEX IF NOT EXISTS ix_copilot_generated_artifacts_owner_type_created ON public.copilot_generated_artifacts USING btree (owner_user_id, artifact_type, created_at);
CREATE INDEX IF NOT EXISTS ix_copilot_generated_artifacts_job_created ON public.copilot_generated_artifacts USING btree (job_id, created_at);

COMMIT;

-- Verification — confirm all three relations now exist (expect three non-null rows).
SELECT to_regclass('public.copilot_prompt_templates')   AS copilot_prompt_templates,
       to_regclass('public.candidate_fit_analyses')      AS candidate_fit_analyses,
       to_regclass('public.copilot_generated_artifacts') AS copilot_generated_artifacts;
