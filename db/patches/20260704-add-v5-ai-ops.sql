-- v5 AI Ops + Talent Intelligence: AI run telemetry, prompt registry, provider routing, evaluation.
-- Idempotent: safe to run multiple times. Kept in sync with init.sql and the EF mappings in
-- AppDbContext.Ai.cs. NO EF migrations are used in this project.
--
-- WHY THIS PATCH EXISTS: production/dev databases are provisioned from init.sql (fresh volume) and never
-- run `dotnet ef database update`. Existing databases are brought forward by these forward-only patches.
-- Column types mirror AppDbContext.Ai.cs exactly (snake_case, jsonb, `timestamp without time zone` to
-- match DbDateTime.Now). Integration tests build the schema via EnsureCreated from the EF mappings, so
-- the DDL here and the EF mappings MUST agree (constraint names, filters, defaults).
--
-- NOTE: no PL/pgSQL DO blocks on purpose. Postgres has no ADD CONSTRAINT IF NOT EXISTS, so each
-- constraint is made idempotent with DROP CONSTRAINT IF EXISTS + ADD CONSTRAINT.

BEGIN;

-- 5.1 ai_run_telemetry --------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ai_run_telemetry (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    feature character varying(100) NOT NULL,
    provider_name character varying(100),
    model_name character varying(150),
    prompt_version_id uuid,
    prompt_tokens integer,
    completion_tokens integer,
    total_tokens integer,
    estimated_cost_usd numeric(18, 8),
    is_cost_estimated boolean DEFAULT true NOT NULL,
    latency_ms integer NOT NULL,
    success boolean NOT NULL,
    fallback_used boolean DEFAULT false NOT NULL,
    schema_valid boolean,
    error_code character varying(100),
    error_message text,
    correlation_id character varying(100),
    user_id uuid,
    workflow_execution_id uuid,
    risk_flags_json jsonb,
    metadata_json jsonb,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.ai_run_telemetry DROP CONSTRAINT IF EXISTS ai_run_telemetry_pkey;
ALTER TABLE ONLY public.ai_run_telemetry ADD CONSTRAINT ai_run_telemetry_pkey PRIMARY KEY (id);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_created_at ON public.ai_run_telemetry USING btree (created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_feature_created_at ON public.ai_run_telemetry USING btree (feature, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_provider_model_created_at ON public.ai_run_telemetry USING btree (provider_name, model_name, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_success_created_at ON public.ai_run_telemetry USING btree (success, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_correlation_id ON public.ai_run_telemetry USING btree (correlation_id);

-- 5.3 prompt_template_versions ------------------------------------------------
CREATE TABLE IF NOT EXISTS public.prompt_template_versions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    feature_key character varying(100) NOT NULL,
    version_no integer NOT NULL,
    name character varying(200) NOT NULL,
    description text,
    template_body text NOT NULL,
    variables_json jsonb DEFAULT '[]'::jsonb NOT NULL,
    is_active boolean DEFAULT false NOT NULL,
    created_by uuid,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    activated_by uuid,
    activated_at timestamp without time zone,
    notes text
);
ALTER TABLE ONLY public.prompt_template_versions DROP CONSTRAINT IF EXISTS prompt_template_versions_pkey;
ALTER TABLE ONLY public.prompt_template_versions ADD CONSTRAINT prompt_template_versions_pkey PRIMARY KEY (id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_prompt_template_versions_feature_version ON public.prompt_template_versions USING btree (feature_key, version_no);
-- At most one active version per feature. Partial unique index enforces the invariant at the DB level.
CREATE UNIQUE INDEX IF NOT EXISTS ux_prompt_template_versions_active_feature ON public.prompt_template_versions USING btree (feature_key) WHERE is_active;

-- 5.4 provider_routing_policies -----------------------------------------------
CREATE TABLE IF NOT EXISTS public.provider_routing_policies (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    feature_key character varying(100) NOT NULL,
    primary_provider character varying(100) NOT NULL,
    primary_model character varying(150) NOT NULL,
    fallback_provider character varying(100),
    fallback_model character varying(150),
    is_enabled boolean DEFAULT true NOT NULL,
    max_latency_ms integer,
    max_estimated_cost_usd numeric(18, 8),
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_by uuid
);
ALTER TABLE ONLY public.provider_routing_policies DROP CONSTRAINT IF EXISTS provider_routing_policies_pkey;
ALTER TABLE ONLY public.provider_routing_policies ADD CONSTRAINT provider_routing_policies_pkey PRIMARY KEY (id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_provider_routing_policies_feature ON public.provider_routing_policies USING btree (feature_key);

-- 5.4 ai_evaluation_cases -----------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ai_evaluation_cases (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    feature_key character varying(100) NOT NULL,
    name character varying(200) NOT NULL,
    input_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    expected_json jsonb,
    scoring_rubric_json jsonb,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.ai_evaluation_cases DROP CONSTRAINT IF EXISTS ai_evaluation_cases_pkey;
ALTER TABLE ONLY public.ai_evaluation_cases ADD CONSTRAINT ai_evaluation_cases_pkey PRIMARY KEY (id);
CREATE INDEX IF NOT EXISTS idx_ai_evaluation_cases_feature_active ON public.ai_evaluation_cases USING btree (feature_key, is_active);

-- 5.4 ai_evaluation_results ---------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ai_evaluation_results (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    run_id uuid NOT NULL,
    case_id uuid NOT NULL,
    prompt_version_id uuid,
    provider_name character varying(100),
    model_name character varying(150),
    score numeric(5, 2),
    passed boolean,
    output_json jsonb,
    error_message text,
    latency_ms integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.ai_evaluation_results DROP CONSTRAINT IF EXISTS ai_evaluation_results_pkey;
ALTER TABLE ONLY public.ai_evaluation_results ADD CONSTRAINT ai_evaluation_results_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.ai_evaluation_results DROP CONSTRAINT IF EXISTS ai_evaluation_results_case_id_fkey;
ALTER TABLE ONLY public.ai_evaluation_results ADD CONSTRAINT ai_evaluation_results_case_id_fkey FOREIGN KEY (case_id) REFERENCES public.ai_evaluation_cases(id) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_ai_evaluation_results_run_created ON public.ai_evaluation_results USING btree (run_id, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_evaluation_results_case ON public.ai_evaluation_results USING btree (case_id);

COMMIT;

-- Verification (run after COMMIT).
SELECT to_regclass('public.ai_run_telemetry') AS ai_run_telemetry;
SELECT to_regclass('public.prompt_template_versions') AS prompt_template_versions;
SELECT to_regclass('public.provider_routing_policies') AS provider_routing_policies;
SELECT to_regclass('public.ai_evaluation_cases') AS ai_evaluation_cases;
SELECT to_regclass('public.ai_evaluation_results') AS ai_evaluation_results;
