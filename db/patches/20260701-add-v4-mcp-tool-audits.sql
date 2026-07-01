-- v4 MCP prototype: audit log for every internal MCP-style tool call (allowed and denied).
-- Idempotent; kept in sync with init.sql and AppDbContext.Automation.cs. NO EF migrations.

BEGIN;

CREATE TABLE IF NOT EXISTS public.mcp_tool_audits (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    tool_name character varying(150) NOT NULL,
    caller_user_id uuid,
    input_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    output_summary_json jsonb,
    allowed boolean NOT NULL,
    denied_reason text,
    latency_ms integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
ALTER TABLE ONLY public.mcp_tool_audits DROP CONSTRAINT IF EXISTS mcp_tool_audits_pkey;
ALTER TABLE ONLY public.mcp_tool_audits ADD CONSTRAINT mcp_tool_audits_pkey PRIMARY KEY (id);
CREATE INDEX IF NOT EXISTS ix_mcp_tool_audits_tool_created ON public.mcp_tool_audits USING btree (tool_name, created_at);

COMMIT;

SELECT to_regclass('public.mcp_tool_audits') AS mcp_tool_audits;
