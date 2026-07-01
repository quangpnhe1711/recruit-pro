-- v4 Workflow Automation: durable event outbox + versioned workflow definitions + execution logs.
-- Idempotent: safe to run multiple times. Kept in sync with init.sql and the EF mappings in
-- AppDbContext.Automation.cs. NO EF migrations are used in this project.
--
-- NOTE: no PL/pgSQL DO blocks on purpose. Some SQL runners split scripts on ";" and choke on
-- dollar-quoted bodies. Postgres has no ADD CONSTRAINT IF NOT EXISTS, so each constraint is made
-- idempotent with DROP CONSTRAINT IF EXISTS + ADD CONSTRAINT.

BEGIN;

-- 4.1 published_domain_events -------------------------------------------------
CREATE TABLE IF NOT EXISTS public.published_domain_events (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    event_type character varying(100) NOT NULL,
    aggregate_type character varying(100) NOT NULL,
    aggregate_id uuid NOT NULL,
    dedup_key character varying(300) NOT NULL,
    payload_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    status character varying(50) DEFAULT 'Pending' NOT NULL,
    occurred_at timestamp without time zone NOT NULL,
    processed_at timestamp without time zone,
    next_attempt_at timestamp without time zone,
    attempt_count integer DEFAULT 0 NOT NULL,
    error_reason text,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.published_domain_events DROP CONSTRAINT IF EXISTS published_domain_events_pkey;
ALTER TABLE ONLY public.published_domain_events ADD CONSTRAINT published_domain_events_pkey PRIMARY KEY (id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_published_domain_events_dedup_key ON public.published_domain_events USING btree (dedup_key);
CREATE INDEX IF NOT EXISTS ix_published_domain_events_status_next_attempt ON public.published_domain_events USING btree (status, next_attempt_at);
CREATE INDEX IF NOT EXISTS ix_published_domain_events_event_type_occurred_at ON public.published_domain_events USING btree (event_type, occurred_at);

-- 4.2 workflow_definitions ----------------------------------------------------
CREATE TABLE IF NOT EXISTS public.workflow_definitions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(200) NOT NULL,
    description text,
    is_enabled boolean DEFAULT true NOT NULL,
    active_version_id uuid,
    created_by uuid,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone
);
ALTER TABLE ONLY public.workflow_definitions DROP CONSTRAINT IF EXISTS workflow_definitions_pkey;
ALTER TABLE ONLY public.workflow_definitions ADD CONSTRAINT workflow_definitions_pkey PRIMARY KEY (id);

-- 4.3 workflow_definition_versions --------------------------------------------
CREATE TABLE IF NOT EXISTS public.workflow_definition_versions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    workflow_definition_id uuid NOT NULL,
    version_no integer NOT NULL,
    trigger_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    conditions_json jsonb DEFAULT '[]'::jsonb NOT NULL,
    actions_json jsonb DEFAULT '[]'::jsonb NOT NULL,
    mode character varying(50) DEFAULT 'Shadow' NOT NULL,
    is_active boolean DEFAULT false NOT NULL,
    published_by uuid,
    published_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.workflow_definition_versions DROP CONSTRAINT IF EXISTS workflow_definition_versions_pkey;
ALTER TABLE ONLY public.workflow_definition_versions ADD CONSTRAINT workflow_definition_versions_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.workflow_definition_versions DROP CONSTRAINT IF EXISTS workflow_definition_versions_workflow_definition_id_fkey;
ALTER TABLE ONLY public.workflow_definition_versions ADD CONSTRAINT workflow_definition_versions_workflow_definition_id_fkey FOREIGN KEY (workflow_definition_id) REFERENCES public.workflow_definitions(id) ON DELETE CASCADE;
CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_definition_versions_workflow_version ON public.workflow_definition_versions USING btree (workflow_definition_id, version_no);

-- active_version_id FK added after versions table exists.
ALTER TABLE ONLY public.workflow_definitions DROP CONSTRAINT IF EXISTS workflow_definitions_active_version_id_fkey;
ALTER TABLE ONLY public.workflow_definitions ADD CONSTRAINT workflow_definitions_active_version_id_fkey FOREIGN KEY (active_version_id) REFERENCES public.workflow_definition_versions(id);

-- 4.4 workflow_executions -----------------------------------------------------
CREATE TABLE IF NOT EXISTS public.workflow_executions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    workflow_definition_id uuid NOT NULL,
    workflow_definition_version_id uuid NOT NULL,
    event_id uuid,
    event_dedup_key character varying(300),
    status character varying(50) NOT NULL,
    mode character varying(50) NOT NULL,
    trigger_event_type character varying(100) NOT NULL,
    input_payload_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    output_json jsonb,
    error_reason text,
    attempt_count integer DEFAULT 0 NOT NULL,
    next_retry_at timestamp without time zone,
    started_at timestamp without time zone,
    finished_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.workflow_executions DROP CONSTRAINT IF EXISTS workflow_executions_pkey;
ALTER TABLE ONLY public.workflow_executions ADD CONSTRAINT workflow_executions_pkey PRIMARY KEY (id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_executions_version_event ON public.workflow_executions USING btree (workflow_definition_version_id, event_dedup_key) WHERE event_dedup_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_workflow_executions_status_next_retry ON public.workflow_executions USING btree (status, next_retry_at);
CREATE INDEX IF NOT EXISTS ix_workflow_executions_definition_created ON public.workflow_executions USING btree (workflow_definition_id, created_at);

-- 4.5 workflow_execution_steps ------------------------------------------------
CREATE TABLE IF NOT EXISTS public.workflow_execution_steps (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    execution_id uuid NOT NULL,
    step_no integer NOT NULL,
    step_type character varying(100) NOT NULL,
    action_type character varying(100),
    status character varying(50) NOT NULL,
    input_json jsonb,
    output_json jsonb,
    error_reason text,
    started_at timestamp without time zone,
    finished_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.workflow_execution_steps DROP CONSTRAINT IF EXISTS workflow_execution_steps_pkey;
ALTER TABLE ONLY public.workflow_execution_steps ADD CONSTRAINT workflow_execution_steps_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.workflow_execution_steps DROP CONSTRAINT IF EXISTS workflow_execution_steps_execution_id_fkey;
ALTER TABLE ONLY public.workflow_execution_steps ADD CONSTRAINT workflow_execution_steps_execution_id_fkey FOREIGN KEY (execution_id) REFERENCES public.workflow_executions(id) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS ix_workflow_execution_steps_execution_step ON public.workflow_execution_steps USING btree (execution_id, step_no);

-- 4.6 workflow_action_dead_letters --------------------------------------------
CREATE TABLE IF NOT EXISTS public.workflow_action_dead_letters (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    execution_id uuid NOT NULL,
    step_id uuid,
    action_type character varying(100) NOT NULL,
    payload_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    error_reason text NOT NULL,
    attempt_count integer DEFAULT 0 NOT NULL,
    next_retry_at timestamp without time zone,
    resolved_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.workflow_action_dead_letters DROP CONSTRAINT IF EXISTS workflow_action_dead_letters_pkey;
ALTER TABLE ONLY public.workflow_action_dead_letters ADD CONSTRAINT workflow_action_dead_letters_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.workflow_action_dead_letters DROP CONSTRAINT IF EXISTS workflow_action_dead_letters_execution_id_fkey;
ALTER TABLE ONLY public.workflow_action_dead_letters ADD CONSTRAINT workflow_action_dead_letters_execution_id_fkey FOREIGN KEY (execution_id) REFERENCES public.workflow_executions(id) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS ix_workflow_action_dead_letters_execution_id ON public.workflow_action_dead_letters USING btree (execution_id);

COMMIT;

-- Verification (run after COMMIT).
SELECT to_regclass('public.published_domain_events') AS published_domain_events;
SELECT to_regclass('public.workflow_definitions') AS workflow_definitions;
SELECT to_regclass('public.workflow_definition_versions') AS workflow_definition_versions;
SELECT to_regclass('public.workflow_executions') AS workflow_executions;
SELECT to_regclass('public.workflow_execution_steps') AS workflow_execution_steps;
SELECT to_regclass('public.workflow_action_dead_letters') AS workflow_action_dead_letters;
