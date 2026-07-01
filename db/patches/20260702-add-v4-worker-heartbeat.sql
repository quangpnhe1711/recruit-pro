-- v4 Workflow Automation: background worker heartbeat.
-- Idempotent: safe to run multiple times. Kept in sync with init.sql and the EF mapping in
-- AppDbContext.Automation.cs. NO EF migrations are used in this project.
--
-- Purpose: the dispatcher upserts a row here every loop. The SystemAdmin diagnostics screen reads it to
-- prove the worker is alive; a stale/absent beat is the diagnosable root cause of "no execution appeared".

BEGIN;

CREATE TABLE IF NOT EXISTS public.workflow_worker_heartbeats (
    worker_name character varying(100) NOT NULL,
    last_beat_at timestamp without time zone NOT NULL,
    status character varying(50) DEFAULT 'Running' NOT NULL,
    detail text,
    updated_at timestamp without time zone
);
ALTER TABLE ONLY public.workflow_worker_heartbeats DROP CONSTRAINT IF EXISTS workflow_worker_heartbeats_pkey;
ALTER TABLE ONLY public.workflow_worker_heartbeats ADD CONSTRAINT workflow_worker_heartbeats_pkey PRIMARY KEY (worker_name);

COMMIT;

-- Verification (run after COMMIT).
SELECT to_regclass('public.workflow_worker_heartbeats') AS workflow_worker_heartbeats;
