CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;

CREATE TABLE IF NOT EXISTS public.copilot_conversations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    job_id uuid NOT NULL,
    user_id uuid NOT NULL,
    title character varying(200),
    status character varying(30) DEFAULT 'Active'::character varying NOT NULL,
    latest_ranking_session_id uuid,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.copilot_messages (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    conversation_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    content text NOT NULL,
    metadata_json jsonb,
    sequence_no integer NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.copilot_ranking_sessions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    job_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    user_id uuid NOT NULL,
    user_prompt text NOT NULL,
    normalized_rules_json jsonb NOT NULL,
    total_candidates integer NOT NULL,
    model_name character varying(100),
    prompt_tokens integer,
    completion_tokens integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.copilot_ranking_results (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    ranking_session_id uuid NOT NULL,
    candidate_user_id uuid NOT NULL,
    application_id uuid NOT NULL,
    rank_position integer NOT NULL,
    total_score numeric(5,2) NOT NULL,
    skill_score numeric(5,2) NOT NULL,
    experience_score numeric(5,2) NOT NULL,
    education_score numeric(5,2) NOT NULL,
    project_score numeric(5,2) NOT NULL,
    recommendation character varying(30) NOT NULL,
    reject_reason text,
    is_auto_rejected boolean DEFAULT false NOT NULL,
    strengths_json jsonb NOT NULL,
    weaknesses_json jsonb NOT NULL,
    explanation_json jsonb,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS public.copilot_saved_rules (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    job_id uuid NOT NULL,
    user_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    rule_json jsonb NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    deleted_at timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.copilot_candidate_tags (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    candidate_user_id uuid NOT NULL,
    job_id uuid NOT NULL,
    ranking_session_id uuid,
    created_by_user_id uuid NOT NULL,
    tag_name character varying(100) NOT NULL,
    source character varying(20) DEFAULT 'AI'::character varying NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

ALTER TABLE public.copilot_saved_rules
    ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false NOT NULL;

ALTER TABLE public.copilot_saved_rules
    ADD COLUMN IF NOT EXISTS deleted_at timestamp without time zone;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_conversations_pkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_conversations ADD CONSTRAINT copilot_conversations_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_messages_pkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_messages ADD CONSTRAINT copilot_messages_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_sessions_pkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_sessions ADD CONSTRAINT copilot_ranking_sessions_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_results_pkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT copilot_ranking_results_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_saved_rules_pkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_saved_rules ADD CONSTRAINT copilot_saved_rules_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_candidate_tags_pkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT copilot_candidate_tags_pkey PRIMARY KEY (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_copilot_messages_role'
    ) THEN
        ALTER TABLE ONLY public.copilot_messages
            ADD CONSTRAINT ck_copilot_messages_role
            CHECK (((role)::text = ANY ((ARRAY['User'::character varying, 'Assistant'::character varying, 'System'::character varying])::text[])));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_copilot_ranking_results_recommendation'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_results
            ADD CONSTRAINT ck_copilot_ranking_results_recommendation
            CHECK (((recommendation)::text = ANY ((ARRAY['Interview'::character varying, 'Consider'::character varying, 'Hold'::character varying, 'Reject'::character varying])::text[])));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_copilot_candidate_tags_source'
    ) THEN
        ALTER TABLE ONLY public.copilot_candidate_tags
            ADD CONSTRAINT ck_copilot_candidate_tags_source
            CHECK (((source)::text = ANY ((ARRAY['AI'::character varying, 'HR'::character varying, 'System'::character varying])::text[])));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'uq_copilot_ranking_results_session_candidate'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_results
            ADD CONSTRAINT uq_copilot_ranking_results_session_candidate UNIQUE (ranking_session_id, candidate_user_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_conversations_job_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_conversations
            ADD CONSTRAINT copilot_conversations_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_conversations_user_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_conversations
            ADD CONSTRAINT copilot_conversations_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_messages_conversation_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_messages
            ADD CONSTRAINT copilot_messages_conversation_id_fkey FOREIGN KEY (conversation_id) REFERENCES public.copilot_conversations(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_sessions_job_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_sessions
            ADD CONSTRAINT copilot_ranking_sessions_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_sessions_conversation_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_sessions
            ADD CONSTRAINT copilot_ranking_sessions_conversation_id_fkey FOREIGN KEY (conversation_id) REFERENCES public.copilot_conversations(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_sessions_user_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_sessions
            ADD CONSTRAINT copilot_ranking_sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_conversations_latest_ranking_session_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_conversations
            ADD CONSTRAINT copilot_conversations_latest_ranking_session_id_fkey FOREIGN KEY (latest_ranking_session_id) REFERENCES public.copilot_ranking_sessions(id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_results_ranking_session_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_results
            ADD CONSTRAINT copilot_ranking_results_ranking_session_id_fkey FOREIGN KEY (ranking_session_id) REFERENCES public.copilot_ranking_sessions(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_results_candidate_user_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_results
            ADD CONSTRAINT copilot_ranking_results_candidate_user_id_fkey FOREIGN KEY (candidate_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_ranking_results_application_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_ranking_results
            ADD CONSTRAINT copilot_ranking_results_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_saved_rules_job_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_saved_rules
            ADD CONSTRAINT copilot_saved_rules_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_saved_rules_user_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_saved_rules
            ADD CONSTRAINT copilot_saved_rules_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_candidate_tags_candidate_user_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_candidate_tags
            ADD CONSTRAINT copilot_candidate_tags_candidate_user_id_fkey FOREIGN KEY (candidate_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_candidate_tags_job_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_candidate_tags
            ADD CONSTRAINT copilot_candidate_tags_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_candidate_tags_ranking_session_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_candidate_tags
            ADD CONSTRAINT copilot_candidate_tags_ranking_session_id_fkey FOREIGN KEY (ranking_session_id) REFERENCES public.copilot_ranking_sessions(id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'copilot_candidate_tags_created_by_user_id_fkey'
    ) THEN
        ALTER TABLE ONLY public.copilot_candidate_tags
            ADD CONSTRAINT copilot_candidate_tags_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_copilot_conversations_job_user_created_at ON public.copilot_conversations USING btree (job_id, user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_conversations_latest_ranking_session_id ON public.copilot_conversations USING btree (latest_ranking_session_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_copilot_conversations_active_job_user ON public.copilot_conversations USING btree (job_id, user_id) WHERE ((status)::text = 'Active'::text);
CREATE INDEX IF NOT EXISTS ix_copilot_messages_conversation_sequence ON public.copilot_messages USING btree (conversation_id, sequence_no);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_sessions_conversation_created_at ON public.copilot_ranking_sessions USING btree (conversation_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_sessions_job_created_at ON public.copilot_ranking_sessions USING btree (job_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_results_session_rank ON public.copilot_ranking_results USING btree (ranking_session_id, rank_position);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_results_session_reject_score ON public.copilot_ranking_results USING btree (ranking_session_id, is_auto_rejected, total_score DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_saved_rules_job_active ON public.copilot_saved_rules USING btree (job_id, is_active);
CREATE INDEX IF NOT EXISTS ix_copilot_saved_rules_job_user_updated_at ON public.copilot_saved_rules USING btree (job_id, user_id, updated_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_saved_rules_job_user_deleted ON public.copilot_saved_rules USING btree (job_id, user_id, is_deleted);
CREATE INDEX IF NOT EXISTS ix_copilot_candidate_tags_job_tag ON public.copilot_candidate_tags USING btree (job_id, tag_name);
