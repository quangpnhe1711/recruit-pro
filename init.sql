

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;


--
-- TOC entry 5233 (class 0 OID 0)
-- Dependencies: 2
-- Name: EXTENSION pgcrypto; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION pgcrypto IS 'cryptographic functions';


--
-- TOC entry 914 (class 1247 OID 16424)
-- Name: application_status; Type: TYPE; Schema: public; Owner: postgres
--


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 231 (class 1259 OID 16692)
-- Name: applications; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.applications (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    job_id uuid NOT NULL,
    reviewed_by uuid,
    status character varying(50) DEFAULT 'Applied',
    applied_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    cover_letter text
);


ALTER TABLE public.applications OWNER TO postgres;

ALTER TABLE public.applications
    ADD COLUMN rule_score numeric(5,2),
    ADD COLUMN semantic_score numeric(5,2),
    ADD COLUMN final_score numeric(5,2),
    ADD COLUMN score_status character varying(50),
    ADD COLUMN score_error text,
    ADD COLUMN scored_at timestamp without time zone;

--
-- TOC entry 240 (class 1259 OID 16810)
-- Name: offer_templates; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.offer_templates (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(150) NOT NULL,
    description text,
    template_body text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    display_order integer DEFAULT 0 NOT NULL
);


ALTER TABLE public.offer_templates OWNER TO postgres;

--
-- TOC entry 241 (class 1259 OID 16824)
-- Name: offer_benefits; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.offer_benefits (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(120) NOT NULL,
    description text,
    is_active boolean DEFAULT true NOT NULL,
    display_order integer DEFAULT 0 NOT NULL
);


ALTER TABLE public.offer_benefits OWNER TO postgres;

--
-- TOC entry 242 (class 1259 OID 16838)
-- Name: offer_currencies; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.offer_currencies (
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    symbol character varying(10) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    display_order integer DEFAULT 0 NOT NULL
);


ALTER TABLE public.offer_currencies OWNER TO postgres;

--
-- TOC entry 243 (class 1259 OID 16849)
-- Name: application_offers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.application_offers (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    application_id uuid NOT NULL,
    offer_template_id uuid,
    base_salary numeric(15,2) NOT NULL,
    currency_code character varying(10) NOT NULL,
    bonus_description text,
    equity_notes text,
    employment_type character varying(100) NOT NULL,
    proposed_start_date timestamp without time zone,
    probation_period character varying(100),
    reporting_manager_id uuid,
    personal_message text,
    status character varying(30) DEFAULT 'Draft' NOT NULL,
    sent_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.application_offers OWNER TO postgres;

--
-- TOC entry 244 (class 1259 OID 16867)
-- Name: application_offer_benefits; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.application_offer_benefits (
    offer_id uuid NOT NULL,
    benefit_id uuid NOT NULL
);


ALTER TABLE public.application_offer_benefits OWNER TO postgres;

--
-- TOC entry 226 (class 1259 OID 16601)
-- Name: candidate_profiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.candidate_profiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    current_position character varying(255),
    experience_years integer DEFAULT 0,
    education text,
    address text,
    bio text,
    resume_url text,
    github_url text,
    linkedin_url text
);


ALTER TABLE public.candidate_profiles OWNER TO postgres;

ALTER TABLE public.candidate_profiles
    ADD COLUMN parsed_resume_json jsonb,
    ADD COLUMN resume_extracted_text text,
    ADD COLUMN resume_parse_status character varying(50),
    ADD COLUMN resume_parse_error text,
    ADD COLUMN resume_parse_model character varying(100),
    ADD COLUMN resume_parser_warnings_json jsonb,
    ADD COLUMN resume_parsed_at timestamp without time zone,
    ADD COLUMN candidate_embedding_vector jsonb,
    ADD COLUMN candidate_embedding_text_hash character varying(128),
    ADD COLUMN candidate_embedding_status character varying(50),
    ADD COLUMN candidate_embedding_error text,
    ADD COLUMN candidate_embedding_updated_at timestamp without time zone;

--
-- TOC entry 228 (class 1259 OID 16629)
-- Name: candidate_skills; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.candidate_skills (
    candidate_id uuid NOT NULL,
    skill_id uuid NOT NULL
);


ALTER TABLE public.candidate_skills OWNER TO postgres;

--
-- TOC entry 225 (class 1259 OID 16589)
-- Name: departments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.departments (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(100) NOT NULL,
    description text
);


ALTER TABLE public.departments OWNER TO postgres;

--
-- TOC entry 232 (class 1259 OID 16718)
-- Name: interviews; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.interviews (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    application_id uuid NOT NULL,
    interview_date timestamp without time zone NOT NULL,
    interviewer_id uuid,
    duration_minutes integer NOT NULL DEFAULT 60,
    meeting_type character varying(50),
    meeting_link text,
    location text,
    notes text,
    status character varying(50) DEFAULT 'Scheduled',
    candidate_confirmed_at timestamp without time zone
);


ALTER TABLE public.interviews OWNER TO postgres;

--
-- TOC entry 230 (class 1259 OID 16675)
-- Name: job_skills; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.job_skills (
    job_id uuid NOT NULL,
    skill_id uuid NOT NULL,

    min_years_experience integer,

    is_required boolean NOT NULL DEFAULT true
);

ALTER TABLE public.job_skills OWNER TO postgres;

--
-- TOC entry 229 (class 1259 OID 16646)
-- Name: jobs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.jobs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,

    department_id uuid,

    created_by uuid NOT NULL,

    approved_by uuid,

    title character varying(255) NOT NULL,

    short_pitch character varying(500),

    description text NOT NULL,

    requirements text,

    benefits text,

    location character varying(255) NOT NULL,

    work_mode character varying(50) NOT NULL DEFAULT 'Onsite',

    employment_type character varying(50) NOT NULL,

    min_experience_years integer DEFAULT 0,

    vacancy_count integer DEFAULT 1,

    salary_min numeric(15,2),

    salary_max numeric(15,2),

    deadline timestamp without time zone,

    status character varying(50) DEFAULT 'Draft',

    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT salary_check
    CHECK (
        salary_max IS NULL
        OR salary_min IS NULL
        OR salary_max >= salary_min
    )
);


ALTER TABLE public.jobs OWNER TO postgres;

ALTER TABLE public.jobs
    ADD COLUMN job_embedding_vector jsonb,
    ADD COLUMN job_embedding_text_hash character varying(128),
    ADD COLUMN job_embedding_status character varying(50),
    ADD COLUMN job_embedding_error text,
    ADD COLUMN job_embedding_updated_at timestamp without time zone;

--
-- TOC entry 233 (class 1259 OID 16735)
-- Name: notifications; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.notifications (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    title character varying(255),
    body text,
    content text,
    event_code character varying(100),
    data_json jsonb,
    entity_type character varying(100),
    entity_id uuid,
    type character varying(50),
    is_read boolean DEFAULT false,
    read_at timestamp without time zone,
    is_seen boolean DEFAULT false,
    seen_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.notifications OWNER TO postgres;

--
-- TOC entry 222 (class 1259 OID 16542)
-- Name: permissions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.permissions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(100) NOT NULL,
    code character varying(100) NOT NULL,
    description text
);


ALTER TABLE public.permissions OWNER TO postgres;

--
-- TOC entry 234 (class 1259 OID 16752)
-- Name: refresh_tokens; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.refresh_tokens (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    token text NOT NULL,
    expiry_date timestamp without time zone NOT NULL
);


ALTER TABLE public.refresh_tokens OWNER TO postgres;

--
-- TOC entry 224 (class 1259 OID 16572)
-- Name: role_permissions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.role_permissions (
    role_id uuid NOT NULL,
    permission_id uuid NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.role_permissions OWNER TO postgres;

--
-- TOC entry 221 (class 1259 OID 16530)
-- Name: roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.roles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(100) NOT NULL,
    description text
);


ALTER TABLE public.roles OWNER TO postgres;

--
-- TOC entry 227 (class 1259 OID 16619)
-- Name: skills; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.skills (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(100) NOT NULL
);


ALTER TABLE public.skills OWNER TO postgres;

--
-- TOC entry 235 (class 1259 OID 16769)
-- Name: system_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.system_logs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid,
    action character varying(255),
    description text,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.system_logs OWNER TO postgres;

--
-- TOC entry 223 (class 1259 OID 16555)
-- Name: user_roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_roles (
    user_id uuid NOT NULL,
    role_id uuid NOT NULL,
    assigned_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_roles OWNER TO postgres;

--
-- TOC entry 220 (class 1259 OID 16513)
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    username character varying(50) NOT NULL,
    email character varying(255) NOT NULL,
    password_hash text NOT NULL,
    full_name character varying(255) NOT NULL,
    phone character varying(20),
    avatar_url text,
    status character varying(50) DEFAULT 'Active',
    token_version integer DEFAULT 0 NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.users OWNER TO postgres;

--
-- TOC entry 5036 (class 2606 OID 16702)
-- Name: applications applications_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.applications
    ADD CONSTRAINT applications_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.offer_templates
    ADD CONSTRAINT offer_templates_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.offer_benefits
    ADD CONSTRAINT offer_benefits_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.offer_currencies
    ADD CONSTRAINT offer_currencies_pkey PRIMARY KEY (code);

ALTER TABLE ONLY public.application_offers
    ADD CONSTRAINT application_offers_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.application_offers
    ADD CONSTRAINT application_offers_application_id_key UNIQUE (application_id);

ALTER TABLE ONLY public.application_offer_benefits
    ADD CONSTRAINT application_offer_benefits_pkey PRIMARY KEY (offer_id, benefit_id);


--
-- TOC entry 5022 (class 2606 OID 16611)
-- Name: candidate_profiles candidate_profiles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.candidate_profiles
    ADD CONSTRAINT candidate_profiles_pkey PRIMARY KEY (id);


--
-- TOC entry 5024 (class 2606 OID 16613)
-- Name: candidate_profiles candidate_profiles_user_id_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.candidate_profiles
    ADD CONSTRAINT candidate_profiles_user_id_key UNIQUE (user_id);


--
-- TOC entry 5030 (class 2606 OID 16635)
-- Name: candidate_skills candidate_skills_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.candidate_skills
    ADD CONSTRAINT candidate_skills_pkey PRIMARY KEY (candidate_id, skill_id);


--
-- TOC entry 5018 (class 2606 OID 16600)
-- Name: departments departments_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.departments
    ADD CONSTRAINT departments_name_key UNIQUE (name);


--
-- TOC entry 5020 (class 2606 OID 16598)
-- Name: departments departments_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.departments
    ADD CONSTRAINT departments_pkey PRIMARY KEY (id);


--
-- TOC entry 5038 (class 2606 OID 16729)
-- Name: interviews interviews_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.interviews
    ADD CONSTRAINT interviews_pkey PRIMARY KEY (id);


--
-- TOC entry 5034 (class 2606 OID 16681)
-- Name: job_skills job_skills_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.job_skills
    ADD CONSTRAINT job_skills_pkey PRIMARY KEY (job_id, skill_id);


--
-- TOC entry 5032 (class 2606 OID 16659)
-- Name: jobs jobs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.jobs
    ADD CONSTRAINT jobs_pkey PRIMARY KEY (id);


--
-- TOC entry 5040 (class 2606 OID 16746)
-- Name: notifications notifications_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT notifications_pkey PRIMARY KEY (id);


--
-- TOC entry 5010 (class 2606 OID 16554)
-- Name: permissions permissions_code_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.permissions
    ADD CONSTRAINT permissions_code_key UNIQUE (code);


--
-- TOC entry 5012 (class 2606 OID 16552)
-- Name: permissions permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.permissions
    ADD CONSTRAINT permissions_pkey PRIMARY KEY (id);


--
-- TOC entry 5042 (class 2606 OID 16763)
-- Name: refresh_tokens refresh_tokens_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.refresh_tokens
    ADD CONSTRAINT refresh_tokens_pkey PRIMARY KEY (id);


--
-- TOC entry 5016 (class 2606 OID 16578)
-- Name: role_permissions role_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.role_permissions
    ADD CONSTRAINT role_permissions_pkey PRIMARY KEY (role_id, permission_id);


--
-- TOC entry 5006 (class 2606 OID 16541)
-- Name: roles roles_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_name_key UNIQUE (name);


--
-- TOC entry 5008 (class 2606 OID 16539)
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);


--
-- TOC entry 5026 (class 2606 OID 16628)
-- Name: skills skills_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.skills
    ADD CONSTRAINT skills_name_key UNIQUE (name);


--
-- TOC entry 5028 (class 2606 OID 16626)
-- Name: skills skills_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.skills
    ADD CONSTRAINT skills_pkey PRIMARY KEY (id);


--
-- TOC entry 5044 (class 2606 OID 16778)
-- Name: system_logs system_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.system_logs
    ADD CONSTRAINT system_logs_pkey PRIMARY KEY (id);


--
-- TOC entry 5014 (class 2606 OID 16561)
-- Name: user_roles user_roles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT user_roles_pkey PRIMARY KEY (user_id, role_id);


--
-- TOC entry 5002 (class 2606 OID 16529)
-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- TOC entry 1082 (class 2606 OID 16528)
-- Name: users users_username_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_username_key UNIQUE (username);


--
-- TOC entry 5004 (class 2606 OID 16527)
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- TOC entry 5057 (class 2606 OID 16703)
-- Name: applications applications_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.applications
    ADD CONSTRAINT applications_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;


--
-- TOC entry 5058 (class 2606 OID 16708)
-- Name: applications applications_job_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.applications
    ADD CONSTRAINT applications_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;


--
-- TOC entry 5059 (class 2606 OID 16713)
-- Name: applications applications_reviewed_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.applications
    ADD CONSTRAINT applications_reviewed_by_fkey FOREIGN KEY (reviewed_by) REFERENCES public.users(id);

ALTER TABLE ONLY public.application_offers
    ADD CONSTRAINT application_offers_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.application_offers
    ADD CONSTRAINT application_offers_currency_code_fkey FOREIGN KEY (currency_code) REFERENCES public.offer_currencies(code);

ALTER TABLE ONLY public.application_offers
    ADD CONSTRAINT application_offers_offer_template_id_fkey FOREIGN KEY (offer_template_id) REFERENCES public.offer_templates(id);

ALTER TABLE ONLY public.application_offers
    ADD CONSTRAINT application_offers_reporting_manager_id_fkey FOREIGN KEY (reporting_manager_id) REFERENCES public.users(id);

ALTER TABLE ONLY public.application_offer_benefits
    ADD CONSTRAINT application_offer_benefits_offer_id_fkey FOREIGN KEY (offer_id) REFERENCES public.application_offers(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.application_offer_benefits
    ADD CONSTRAINT application_offer_benefits_benefit_id_fkey FOREIGN KEY (benefit_id) REFERENCES public.offer_benefits(id) ON DELETE CASCADE;


--
-- TOC entry 5049 (class 2606 OID 16614)
-- Name: candidate_profiles candidate_profiles_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.candidate_profiles
    ADD CONSTRAINT candidate_profiles_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;


--
-- TOC entry 5050 (class 2606 OID 16636)
-- Name: candidate_skills candidate_skills_candidate_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.candidate_skills
    ADD CONSTRAINT candidate_skills_candidate_id_fkey FOREIGN KEY (candidate_id) REFERENCES public.candidate_profiles(id) ON DELETE CASCADE;


--
-- TOC entry 5051 (class 2606 OID 16641)
-- Name: candidate_skills candidate_skills_skill_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.candidate_skills
    ADD CONSTRAINT candidate_skills_skill_id_fkey FOREIGN KEY (skill_id) REFERENCES public.skills(id) ON DELETE CASCADE;


--
-- TOC entry 5060 (class 2606 OID 16730)
-- Name: interviews interviews_application_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.interviews
    ADD CONSTRAINT interviews_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.interviews
    ADD CONSTRAINT interviews_interviewer_id_fkey FOREIGN KEY (interviewer_id) REFERENCES public.users(id);

CREATE INDEX IF NOT EXISTS ix_interviews_interviewer_id ON public.interviews (interviewer_id);


--
-- TOC entry 5055 (class 2606 OID 16682)
-- Name: job_skills job_skills_job_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.job_skills
    ADD CONSTRAINT job_skills_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;


--
-- TOC entry 5056 (class 2606 OID 16687)
-- Name: job_skills job_skills_skill_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.job_skills
    ADD CONSTRAINT job_skills_skill_id_fkey FOREIGN KEY (skill_id) REFERENCES public.skills(id) ON DELETE CASCADE;


--
-- TOC entry 5052 (class 2606 OID 16670)
-- Name: jobs jobs_approved_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.jobs
    ADD CONSTRAINT jobs_approved_by_fkey FOREIGN KEY (approved_by) REFERENCES public.users(id);


--
-- TOC entry 5053 (class 2606 OID 16665)
-- Name: jobs jobs_created_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.jobs
    ADD CONSTRAINT jobs_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(id);


--
-- TOC entry 5054 (class 2606 OID 16660)
-- Name: jobs jobs_department_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.jobs
    ADD CONSTRAINT jobs_department_id_fkey FOREIGN KEY (department_id) REFERENCES public.departments(id);


--
-- TOC entry 5061 (class 2606 OID 16747)
-- Name: notifications notifications_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT notifications_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);


--
-- TOC entry 5062 (class 2606 OID 16764)
-- Name: refresh_tokens refresh_tokens_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.refresh_tokens
    ADD CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;


--
-- TOC entry 5047 (class 2606 OID 16584)
-- Name: role_permissions role_permissions_permission_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.role_permissions
    ADD CONSTRAINT role_permissions_permission_id_fkey FOREIGN KEY (permission_id) REFERENCES public.permissions(id) ON DELETE CASCADE;


--
-- TOC entry 5048 (class 2606 OID 16579)
-- Name: role_permissions role_permissions_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.role_permissions
    ADD CONSTRAINT role_permissions_role_id_fkey FOREIGN KEY (role_id) REFERENCES public.roles(id) ON DELETE CASCADE;


--
-- TOC entry 5063 (class 2606 OID 16779)
-- Name: system_logs system_logs_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.system_logs
    ADD CONSTRAINT system_logs_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);


--
-- TOC entry 5045 (class 2606 OID 16567)
-- Name: user_roles user_roles_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT user_roles_role_id_fkey FOREIGN KEY (role_id) REFERENCES public.roles(id) ON DELETE CASCADE;


--
-- TOC entry 5046 (class 2606 OID 16562)
-- Name: user_roles user_roles_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT user_roles_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;


-- Completed on 2026-05-27 14:01:22

--
-- PostgreSQL database dump complete
--

--
-- ============================================================================
-- DEMO SEED — RecruitPro (base date: 2026-07-04)
-- ============================================================================
-- Toàn bộ dữ liệu demo được neo quanh mốc 2026-07-04:
--   * users/candidates       : created 2026-06-15 → 2026-07-03
--   * jobs                   : posted 2026-06-18 → 2026-07-04, deadline +7 → +90 ngày
--   * applications           : applied 2026-06-20 → 2026-07-04
--   * interviews             : hoàn thành cuối tháng 6, lịch mới tháng 7–9/2026
--   * notifications/logs     : 1–7 ngày gần nhất trước 2026-07-04
-- Mỗi statement là UPSERT (ON CONFLICT) với ID cố định nên chạy lại nhiều lần
-- không tạo bản ghi trùng; chạy trên DB cũ sẽ refresh ngày tháng về mốc mới.
-- Mật khẩu demo cho MỌI tài khoản: Password@123 (hash bcrypt sinh bởi pgcrypto).
-- Giữ đồng bộ với db/patches/20260704-refresh-demo-seed.sql.
-- ============================================================================

-- ---------------------------------------------------------------------------
-- Departments
-- ---------------------------------------------------------------------------
INSERT INTO public.departments (id, name, description) VALUES
    ('fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'Engineering', 'Engineering department'),
    ('95db214f-5ce2-4e89-b350-93da747c7bdb', 'Human Resources', 'Human resources department'),
    ('cc05372d-9ee5-4962-a071-74a8d7630279', 'Finance', 'Finance department'),
    ('3067eaeb-3893-468f-b854-08f1319448c9', 'Marketing', 'Marketing department'),
    ('d1000000-0000-4000-8000-000000000001', 'Product', 'Product management and design department'),
    ('d1000000-0000-4000-8000-000000000002', 'Data & Analytics', 'Data analysis, reporting, and business intelligence department'),
    ('d1000000-0000-4000-8000-000000000003', 'Operations', 'Operations, support, and internal service excellence department'),
    ('d1000000-0000-4000-8000-000000000004', 'Design', 'Product design and user experience department')
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description;

-- ---------------------------------------------------------------------------
-- Permissions (RBAC catalog — mirrored by RecruitPro.Application.Common.RbacCatalog)
-- ---------------------------------------------------------------------------
INSERT INTO public.permissions (id, name, code, description) VALUES
    ('0c84117e-ce89-430b-9d17-073e83e6a8b9', 'View Users', 'USER_VIEW', 'View users'),
    ('b2ac9a5a-014d-4952-8965-f947f3b5fec2', 'Create User', 'USER_CREATE', 'Create users'),
    ('9beaf30e-5c57-4c14-a160-89beca9a27eb', 'Update User', 'USER_UPDATE', 'Update users'),
    ('346841cb-b9c6-4d41-9f2f-ddad38755b52', 'Delete User', 'USER_DELETE', 'Delete users'),
    ('679b403b-c773-498e-92a2-eb65e9b73472', 'View Roles', 'ROLE_VIEW', 'View roles'),
    ('ca9ca784-c1e0-4e8f-99ce-a80ee5db03c5', 'Manage Roles', 'ROLE_MANAGE', 'Manage roles'),
    ('7b2702b7-7a1a-4249-81bb-ca7ca614dc4a', 'View Permissions', 'PERMISSION_VIEW', 'View permissions'),
    ('a771bd2e-bbed-4b27-ac94-d9bb951a415d', 'Manage Permissions', 'PERMISSION_MANAGE', 'Manage permissions'),
    ('f0515aef-c29c-42d6-add4-9ef95b92da6d', 'View Jobs', 'Job_VIEW', 'View jobs'),
    ('b98f8544-bce4-442d-ac70-ae5f942c4db7', 'Create Job', 'Job_CREATE', 'Create jobs'),
    ('51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', 'Update Job', 'Job_UPDATE', 'Update jobs'),
    ('80c0ba49-f562-42b9-b09d-533db4b394a5', 'Delete Job', 'Job_DELETE', 'Delete jobs'),
    ('eabc0d36-032d-4eb2-81f7-6d9a50c82b42', 'Approve Job', 'Job_APPROVE', 'Approve jobs'),
    ('b33f762b-1a44-4292-9b88-1ce34f5387e8', 'View Applications', 'Application_VIEW', 'View applications'),
    ('3130c14f-bfe8-4d5f-84ac-7e6886104070', 'Apply Job', 'Application_APPLY', 'Apply jobs'),
    ('bdf8cb0d-b58d-400c-ad47-064f88054275', 'Review Application', 'Application_REVIEW', 'Review applications'),
    ('7d33a32f-737a-4d94-9271-3ce4bba57659', 'View Interviews', 'Interview_VIEW', 'View interviews'),
    ('b410e0de-f182-4288-b200-9b0eed42424d', 'Create Interview', 'Interview_CREATE', 'Create interviews'),
    ('753339d4-9adf-4fc5-b80f-268f77ca0849', 'Update Interview', 'Interview_UPDATE', 'Update interviews'),
    ('ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', 'View Candidate Profile', 'CANDIDATE_PROFILE_VIEW', 'View candidate profile'),
    ('52b771f7-e9b4-4502-a7a0-afab7f1bd715', 'Update Candidate Profile', 'CANDIDATE_PROFILE_UPDATE', 'Update candidate profile'),
    ('4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', 'View Departments', 'DEPARTMENT_VIEW', 'View departments'),
    ('6ca18f1f-43ff-4602-a617-23d8543077a6', 'Manage Departments', 'DEPARTMENT_MANAGE', 'Manage departments'),
    ('b0a5f45f-53f3-4e09-9b7c-9d713135fe91', 'View Skills', 'SKILL_VIEW', 'View skills'),
    ('fc5bbc69-f2ac-48e3-b54a-5aac96c1d062', 'Manage Skills', 'SKILL_MANAGE', 'Manage skills'),
    ('99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', 'View Notifications', 'NOTIFICATION_VIEW', 'View notifications'),
    ('cdda07cb-20f5-4b82-9842-dfd65057c425', 'View Logs', 'System_LOG_VIEW', 'View system logs')
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code, description = EXCLUDED.description;

-- ---------------------------------------------------------------------------
-- Roles
-- ---------------------------------------------------------------------------
INSERT INTO public.roles (id, name, description) VALUES
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'Candidate', 'Ứng viên — nộp hồ sơ, theo dõi tiến trình tuyển dụng của chính mình'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'HR', 'Chuyên viên tuyển dụng — đăng tin, sàng lọc hồ sơ, đặt lịch phỏng vấn'),
    ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566', 'HeadDepartment', 'Trưởng bộ phận — duyệt job và hồ sơ thuộc bộ phận mình phụ trách'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'Manager', 'Quản lý tuyển dụng — duyệt job, xem báo cáo, ra quyết định tuyển'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'SystemAdmin', 'Quản trị hệ thống — quản lý người dùng, phân quyền RBAC, workflow automation')
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description;

-- ---------------------------------------------------------------------------
-- Role → Permission grants
-- ---------------------------------------------------------------------------
INSERT INTO public.role_permissions (role_id, permission_id, assigned_at) VALUES
    -- Candidate: xem job, nộp/xem hồ sơ của mình, xem lịch phỏng vấn, hồ sơ cá nhân
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '3130c14f-bfe8-4d5f-84ac-7e6886104070', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '52b771f7-e9b4-4502-a7a0-afab7f1bd715', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-06-15 09:00:00+07'),
    ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-06-15 09:00:00+07'),
    -- HR: vận hành tuyển dụng end-to-end (không có quyền hệ thống)
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b98f8544-bce4-442d-ac70-ae5f942c4db7', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', '51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'bdf8cb0d-b58d-400c-ad47-064f88054275', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b410e0de-f182-4288-b200-9b0eed42424d', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', '753339d4-9adf-4fc5-b80f-268f77ca0849', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-06-15 09:00:00+07'),
    ('e28e9442-682d-4e1a-b11f-663af56eb730', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-06-15 09:00:00+07'),
    -- HeadDepartment: duyệt trong phạm vi bộ phận
    ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-06-15 09:00:00+07'),
    ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-06-15 09:00:00+07'),
    ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-06-15 09:00:00+07'),
    ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-06-15 09:00:00+07'),
    -- Manager: duyệt job, review hồ sơ, xem log hệ thống
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'eabc0d36-032d-4eb2-81f7-6d9a50c82b42', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'bdf8cb0d-b58d-400c-ad47-064f88054275', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-06-15 09:00:00+07'),
    ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'cdda07cb-20f5-4b82-9842-dfd65057c425', '2026-06-15 09:00:00+07'),
    -- SystemAdmin: toàn bộ 27 quyền (bao gồm PERMISSION_MANAGE — quyền "chìa khóa" của RBAC)
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '0c84117e-ce89-430b-9d17-073e83e6a8b9', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b2ac9a5a-014d-4952-8965-f947f3b5fec2', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '9beaf30e-5c57-4c14-a160-89beca9a27eb', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '346841cb-b9c6-4d41-9f2f-ddad38755b52', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '679b403b-c773-498e-92a2-eb65e9b73472', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'ca9ca784-c1e0-4e8f-99ce-a80ee5db03c5', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '7b2702b7-7a1a-4249-81bb-ca7ca614dc4a', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'a771bd2e-bbed-4b27-ac94-d9bb951a415d', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b98f8544-bce4-442d-ac70-ae5f942c4db7', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '80c0ba49-f562-42b9-b09d-533db4b394a5', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'eabc0d36-032d-4eb2-81f7-6d9a50c82b42', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '3130c14f-bfe8-4d5f-84ac-7e6886104070', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'bdf8cb0d-b58d-400c-ad47-064f88054275', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b410e0de-f182-4288-b200-9b0eed42424d', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '753339d4-9adf-4fc5-b80f-268f77ca0849', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '52b771f7-e9b4-4502-a7a0-afab7f1bd715', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '6ca18f1f-43ff-4602-a617-23d8543077a6', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'fc5bbc69-f2ac-48e3-b54a-5aac96c1d062', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-06-15 09:00:00+07'),
    ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'cdda07cb-20f5-4b82-9842-dfd65057c425', '2026-06-15 09:00:00+07')
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Skills
-- ---------------------------------------------------------------------------
INSERT INTO public.skills (id, name) VALUES
    ('b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 'Java'),
    ('e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 'Spring Boot'),
    ('d29e5aff-c3e3-40c4-8440-8ddc7040bf0d', 'ReactJS'),
    ('e1330bc9-5fe4-4e6d-a5f3-768670d8104a', 'NodeJS'),
    ('b1779c35-b7c4-47bb-a69b-43131c084a0f', 'PostgreSQL'),
    ('f69bf36f-a210-4b76-8008-a5a1fefc6b4b', 'Docker'),
    ('ee3fd9dd-15b1-45e0-875d-ebd18a48db41', 'Kubernetes'),
    ('984f6a6b-e625-449c-8eb4-526348f279ef', 'Redis'),
    ('23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 'Communication'),
    ('2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 'English'),
    ('90000000-0000-4000-8000-000000000001', 'TypeScript'),
    ('90000000-0000-4000-8000-000000000002', 'NextJS'),
    ('90000000-0000-4000-8000-000000000003', 'C#'),
    ('90000000-0000-4000-8000-000000000004', '.NET'),
    ('90000000-0000-4000-8000-000000000005', 'AWS'),
    ('90000000-0000-4000-8000-000000000006', 'Python'),
    ('90000000-0000-4000-8000-000000000007', 'SQL'),
    ('90000000-0000-4000-8000-000000000008', 'Power BI'),
    ('90000000-0000-4000-8000-000000000009', 'Selenium'),
    ('90000000-0000-4000-8000-000000000010', 'Figma'),
    ('90000000-0000-4000-8000-000000000011', 'SEO'),
    ('90000000-0000-4000-8000-000000000012', 'Excel'),
    ('90000000-0000-4000-8000-000000000013', 'ASP.NET Core'),
    ('90000000-0000-4000-8000-000000000014', 'CI/CD'),
    ('90000000-0000-4000-8000-000000000015', 'Microservices'),
    ('90000000-0000-4000-8000-000000000016', 'REST API'),
    ('90000000-0000-4000-8000-000000000017', 'Entity Framework Core'),
    ('90000000-0000-4000-8000-000000000018', 'UI/UX'),
    ('90000000-0000-4000-8000-000000000019', 'Data Analysis'),
    ('90000000-0000-4000-8000-000000000020', 'Playwright')
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;

-- ---------------------------------------------------------------------------
-- Offer catalog
-- ---------------------------------------------------------------------------
INSERT INTO public.offer_templates (id, name, description, template_body, is_active, display_order) VALUES
    ('91000000-0000-4000-8000-000000000001', 'Standard Tech Role', 'Default offer template for engineering and product positions.', 'Standard tech offer body', true, 1),
    ('91000000-0000-4000-8000-000000000002', 'Management Offer', 'Offer template with leadership-oriented language and approvals.', 'Management offer body', true, 2),
    ('91000000-0000-4000-8000-000000000003', 'Contractor Agreement', 'Template for fixed-term and contractor roles.', 'Contractor offer body', true, 3)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description;

INSERT INTO public.offer_benefits (id, name, description, is_active, display_order) VALUES
    ('92000000-0000-4000-8000-000000000001', 'Health Insurance', 'Company-sponsored health coverage package', true, 1),
    ('92000000-0000-4000-8000-000000000002', 'Paid Time Off', 'Annual leave and public holiday package', true, 2),
    ('92000000-0000-4000-8000-000000000003', 'Remote Work', 'Hybrid or remote work flexibility', true, 3),
    ('92000000-0000-4000-8000-000000000004', 'Gym Allowance', 'Monthly fitness or wellness reimbursement', true, 4),
    ('92000000-0000-4000-8000-000000000005', 'Relocation Bonus', 'One-time relocation support for new hires', true, 5),
    ('92000000-0000-4000-8000-000000000006', 'Learning Budget', 'Annual development and certification budget', true, 6)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;

INSERT INTO public.offer_currencies (code, name, symbol, is_active, display_order) VALUES
    ('VND', 'Vietnamese Dong', '₫', true, 1)
ON CONFLICT (code) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Users — mật khẩu demo chung: Password@123 (bcrypt qua pgcrypto)
-- Personas chính:
--   admin@recruitpro.vn            → SystemAdmin
--   thucuyen.nguyen@recruitpro.vn  → HR (recruiter chính của demo)
--   tiendat.tran@recruitpro.vn     → Manager + HeadDepartment (duyệt job/hồ sơ)
--   minhkhoi.vo@recruitpro.vn      → SystemAdmin (admin thứ hai)
--   giahan/khanhlinh/haiyen        → HR; quocbao/minhkhang → Manager
--   nhatquang + 14 user khác       → Candidate (nhatquang là candidate demo chính)
-- ---------------------------------------------------------------------------
INSERT INTO public.users (id, username, email, password_hash, full_name, phone, avatar_url, status, created_at, updated_at) VALUES
    ('4071353e-5816-4746-a8b6-c0bc3113c44d', 'nhatquang', 'nhatquang.phung@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Phùng Nhật Quang', '0900000001', NULL, 'Active', '2026-06-15 08:30:00', '2026-06-28 10:00:00'),
    ('e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'thucuyen', 'thucuyen.nguyen@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Nguyễn Thục Uyên', '0900000002', NULL, 'Active', '2026-06-15 08:35:00', '2026-07-01 09:00:00'),
    ('721b1851-349a-48aa-acae-feed1c1843ed', 'tiendat', 'tiendat.tran@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Trần Trọng Tiến Đạt', '0900000003', NULL, 'Active', '2026-06-15 08:40:00', '2026-07-02 14:00:00'),
    ('92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'minhkhoi', 'minhkhoi.vo@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Võ Minh Khôi', '0900000004', NULL, 'Active', '2026-06-15 08:45:00', '2026-06-15 08:45:00'),
    ('a0000000-0000-4000-8000-000000000001', 'admin', 'admin@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Quản trị hệ thống', '0900000009', NULL, 'Active', '2026-06-15 08:00:00', '2026-06-15 08:00:00'),
    ('d8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', 'haidang', 'haidang.tran@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Trần Hải Đăng', '0900000011', NULL, 'Active', '2026-06-16 09:00:00', '2026-06-16 09:00:00'),
    ('a1b2c3d4-e5f6-4701-9802-abcdefabcdef', 'ducminh', 'ducminh.nguyen@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Nguyễn Đức Minh', '0900000012', NULL, 'Active', '2026-06-16 09:05:00', '2026-06-16 09:05:00'),
    ('b2c3d4e5-f6a7-4802-9903-fedcbafedcba', 'khanhnam', 'khanhnam.bui@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Bùi Khánh Nam', '0900000013', NULL, 'Active', '2026-06-16 09:10:00', '2026-06-16 09:10:00'),
    ('c3d4e5f6-a7b8-4903-9a04-001122334455', 'thuha', 'thuha.le@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Lê Thu Hà', '0900000014', NULL, 'Active', '2026-06-16 09:15:00', '2026-06-16 09:15:00'),
    ('10000000-0000-4000-8000-000000000001', 'giahan', 'giahan.le@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Lê Gia Hân', '0900000101', NULL, 'Active', '2026-06-17 08:00:00', '2026-06-17 08:00:00'),
    ('10000000-0000-4000-8000-000000000002', 'khanhlinh.pham', 'khanhlinh.pham@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Phạm Khánh Linh', '0900000102', NULL, 'Active', '2026-06-17 08:05:00', '2026-06-17 08:05:00'),
    ('10000000-0000-4000-8000-000000000003', 'haiyen', 'haiyen.do@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Đỗ Hải Yến', '0900000103', NULL, 'Active', '2026-06-17 08:10:00', '2026-06-17 08:10:00'),
    ('10000000-0000-4000-8000-000000000004', 'quocbao', 'quocbao.vu@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Vũ Quốc Bảo', '0900000104', NULL, 'Active', '2026-06-17 08:15:00', '2026-06-17 08:15:00'),
    ('10000000-0000-4000-8000-000000000005', 'minhkhang', 'minhkhang.dang@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Đặng Minh Khang', '0900000105', NULL, 'Active', '2026-06-17 08:20:00', '2026-06-17 08:20:00'),
    ('10000000-0000-4000-8000-000000000101', 'minhquan', 'minhquan.nguyen@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Nguyễn Minh Quân', '0900000201', NULL, 'Active', '2026-06-18 09:20:00', '2026-06-18 09:20:00'),
    ('10000000-0000-4000-8000-000000000102', 'khanhlinh.tran', 'khanhlinh.tran@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Trần Khánh Linh', '0900000202', NULL, 'Active', '2026-06-18 09:22:00', '2026-06-18 09:22:00'),
    ('10000000-0000-4000-8000-000000000103', 'quocanh', 'quocanh.pham@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Phạm Quốc Anh', '0900000203', NULL, 'Active', '2026-06-18 09:24:00', '2026-06-18 09:24:00'),
    ('10000000-0000-4000-8000-000000000104', 'minhthao', 'minhthao.le@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Lê Minh Thảo', '0900000204', NULL, 'Active', '2026-06-18 09:26:00', '2026-06-18 09:26:00'),
    ('10000000-0000-4000-8000-000000000105', 'hoangnam', 'hoangnam.ho@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Hồ Hoàng Nam', '0900000205', NULL, 'Active', '2026-06-18 09:28:00', '2026-06-18 09:28:00'),
    ('10000000-0000-4000-8000-000000000106', 'ngocan', 'ngocan.vo@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Võ Ngọc An', '0900000206', NULL, 'Active', '2026-06-18 09:30:00', '2026-06-18 09:30:00'),
    ('10000000-0000-4000-8000-000000000107', 'hoangphuc', 'hoangphuc.bui@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Bùi Hoàng Phúc', '0900000207', NULL, 'Active', '2026-06-18 09:32:00', '2026-06-18 09:32:00'),
    ('10000000-0000-4000-8000-000000000108', 'quynhmai', 'quynhmai.do@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Đỗ Quỳnh Mai', '0900000208', NULL, 'Active', '2026-06-18 09:34:00', '2026-06-18 09:34:00'),
    ('10000000-0000-4000-8000-000000000109', 'anhkhoa', 'anhkhoa.dang@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Đặng Anh Khoa', '0900000209', NULL, 'Active', '2026-06-18 09:36:00', '2026-06-18 09:36:00'),
    ('10000000-0000-4000-8000-000000000110', 'yennhi', 'yennhi.pham@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Phạm Yến Nhi', '0900000210', NULL, 'Inactive', '2026-06-18 09:38:00', '2026-07-02 11:00:00')
ON CONFLICT (id) DO UPDATE SET
    username = EXCLUDED.username,
    email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash,
    full_name = EXCLUDED.full_name,
    phone = EXCLUDED.phone,
    status = EXCLUDED.status,
    created_at = EXCLUDED.created_at,
    updated_at = EXCLUDED.updated_at;

-- ---------------------------------------------------------------------------
-- User → Role
-- ---------------------------------------------------------------------------
INSERT INTO public.user_roles (user_id, role_id, assigned_at) VALUES
    ('4071353e-5816-4746-a8b6-c0bc3113c44d', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-15 08:30:00'),
    ('e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-06-15 08:35:00'),
    ('721b1851-349a-48aa-acae-feed1c1843ed', '5f5350dc-a77f-4a68-88f8-69e27116aa8f', '2026-06-15 08:40:00'),
    ('721b1851-349a-48aa-acae-feed1c1843ed', '7a5b2c6d-1e2f-4a3b-9c8d-112233445566', '2026-06-15 08:40:00'),
    ('92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', '0137e9bc-7ee4-463c-b760-1580ac17cdb6', '2026-06-15 08:45:00'),
    ('a0000000-0000-4000-8000-000000000001', '0137e9bc-7ee4-463c-b760-1580ac17cdb6', '2026-06-15 08:00:00'),
    ('d8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-16 09:00:00'),
    ('a1b2c3d4-e5f6-4701-9802-abcdefabcdef', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-16 09:05:00'),
    ('b2c3d4e5-f6a7-4802-9903-fedcbafedcba', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-16 09:10:00'),
    ('c3d4e5f6-a7b8-4903-9a04-001122334455', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-16 09:15:00'),
    ('10000000-0000-4000-8000-000000000001', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-06-17 08:00:00'),
    ('10000000-0000-4000-8000-000000000002', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-06-17 08:05:00'),
    ('10000000-0000-4000-8000-000000000003', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-06-17 08:10:00'),
    ('10000000-0000-4000-8000-000000000004', '5f5350dc-a77f-4a68-88f8-69e27116aa8f', '2026-06-17 08:15:00'),
    ('10000000-0000-4000-8000-000000000005', '5f5350dc-a77f-4a68-88f8-69e27116aa8f', '2026-06-17 08:20:00'),
    ('10000000-0000-4000-8000-000000000101', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:20:00'),
    ('10000000-0000-4000-8000-000000000102', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:22:00'),
    ('10000000-0000-4000-8000-000000000103', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:24:00'),
    ('10000000-0000-4000-8000-000000000104', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:26:00'),
    ('10000000-0000-4000-8000-000000000105', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:28:00'),
    ('10000000-0000-4000-8000-000000000106', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:30:00'),
    ('10000000-0000-4000-8000-000000000107', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:32:00'),
    ('10000000-0000-4000-8000-000000000108', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:34:00'),
    ('10000000-0000-4000-8000-000000000109', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:36:00'),
    ('10000000-0000-4000-8000-000000000110', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-06-18 09:38:00')
ON CONFLICT (user_id, role_id) DO UPDATE SET assigned_at = EXCLUDED.assigned_at;

-- ---------------------------------------------------------------------------
-- Candidate profiles (resume_parse_status Completed cho vài hồ sơ để copilot/screening có dữ liệu)
-- ---------------------------------------------------------------------------
INSERT INTO public.candidate_profiles (id, user_id, current_position, experience_years, education, address, bio, resume_url, github_url, linkedin_url, resume_extracted_text, resume_parse_status) VALUES
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'Full Stack Developer', 4, 'Đại học Công nghệ Thông tin - ĐHQG TP.HCM', 'Thành phố Hồ Chí Minh', 'Lập trình viên full stack tập trung vào Java backend, React frontend và trải nghiệm ứng tuyển mượt mà.', 'https://cv.recruitpro.local/phung-nhat-quang.pdf', 'https://github.com/nhatquangphung', 'https://linkedin.com/in/nhatquangphung', 'Full stack developer with 4 years in Java, Spring Boot, ReactJS, TypeScript and PostgreSQL. Built recruiting workflows end to end.', 'Completed'),
    ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', 'd8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', 'Frontend Developer', 3, 'Hanoi University', 'Ha Noi', 'Frontend engineer focused on React and modern UI systems', NULL, 'https://github.com/frontend-dev', 'https://linkedin.com/in/frontend-dev', 'Frontend engineer, 3 years ReactJS, strong communication and English.', 'Completed'),
    ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', 'Full Stack Developer', 4, 'VNU', 'Ho Chi Minh City', 'Full stack developer with product mindset', NULL, 'https://github.com/fullstack-dev', 'https://linkedin.com/in/fullstack-dev', 'Full stack developer: Java, ReactJS, NodeJS, PostgreSQL. Product mindset, 4 years experience.', 'Completed'),
    ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', 'DevOps Engineer', 5, 'FPT Polytechnic', 'Da Nang', 'Infrastructure and delivery automation specialist', NULL, 'https://github.com/devops-dev', 'https://linkedin.com/in/devops-dev', 'DevOps engineer, 5 years: Docker, Kubernetes, Redis, CI/CD pipelines and cloud reliability.', 'Completed'),
    ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', 'c3d4e5f6-a7b8-4903-9a04-001122334455', 'Business Analyst', 3, 'NEU', 'Ha Noi', 'Business analyst with strong communication and process design skills', NULL, 'https://github.com/ba-dev', 'https://linkedin.com/in/ba-dev', NULL, NULL),
    ('20000000-0000-4000-8000-000000000101', '10000000-0000-4000-8000-000000000101', 'Senior Java Developer', 4, 'Hanoi University of Science and Technology', 'Ha Noi', 'Backend engineer focused on Java microservices, clean APIs, and performance tuning.', 'https://cv.recruitpro.local/minh-nguyen.pdf', 'https://github.com/minh-nguyen-dev', 'https://linkedin.com/in/minh-nguyen-dev', 'Senior Java developer: Java, Spring Boot, PostgreSQL, SQL, microservices. 4 years experience.', 'Completed'),
    ('20000000-0000-4000-8000-000000000102', '10000000-0000-4000-8000-000000000102', 'Frontend Engineer', 3, 'University of Engineering and Technology', 'Ha Noi', 'Frontend engineer building polished recruiter workflows with React and TypeScript.', 'https://cv.recruitpro.local/linh-tran.pdf', 'https://github.com/linh-tran-ui', 'https://linkedin.com/in/linh-tran-ui', 'Frontend engineer: ReactJS, TypeScript, NextJS. 3 years, strong UI systems.', 'Completed'),
    ('20000000-0000-4000-8000-000000000103', '10000000-0000-4000-8000-000000000103', 'Data Analyst', 2, 'Foreign Trade University', 'Ho Chi Minh City', 'Data analyst with dashboard design, SQL modeling, and business reporting experience.', 'https://cv.recruitpro.local/quang-pham.pdf', 'https://github.com/quang-pham-data', 'https://linkedin.com/in/quang-pham-data', 'Data analyst: SQL, Power BI, Python, Excel, English. 2 years experience.', 'Completed'),
    ('20000000-0000-4000-8000-000000000104', '10000000-0000-4000-8000-000000000104', 'QA Automation Engineer', 3, 'Posts and Telecommunications Institute of Technology', 'Da Nang', 'QA engineer automating regression suites and stabilizing release quality.', 'https://cv.recruitpro.local/thao-le.pdf', 'https://github.com/thao-le-qa', 'https://linkedin.com/in/thao-le-qa', 'QA automation engineer: Selenium, Playwright, Docker. 3 years regression automation.', 'Completed'),
    ('20000000-0000-4000-8000-000000000105', '10000000-0000-4000-8000-000000000105', '.NET Backend Developer', 4, 'FPT University', 'Ho Chi Minh City', 'Backend developer delivering REST APIs with .NET, SQL, and cloud deployment practices.', 'https://cv.recruitpro.local/nam-ho.pdf', 'https://github.com/nam-ho-dotnet', 'https://linkedin.com/in/nam-ho-dotnet', '.NET backend developer: C#, ASP.NET Core, Entity Framework Core, SQL, AWS. 4 years.', 'Completed'),
    ('20000000-0000-4000-8000-000000000106', '10000000-0000-4000-8000-000000000106', 'Product Designer', 3, 'RMIT Vietnam', 'Ho Chi Minh City', 'Product designer translating user research into production-ready flows and design systems.', 'https://cv.recruitpro.local/an-vo.pdf', 'https://github.com/an-vo-design', 'https://linkedin.com/in/an-vo-design', 'Product designer: Figma, UI/UX, design systems, communication. 3 years.', 'Completed'),
    ('20000000-0000-4000-8000-000000000107', '10000000-0000-4000-8000-000000000107', 'Talent Acquisition Specialist', 2, 'National Economics University', 'Ha Noi', 'Recruitment operations specialist with sourcing, interview coordination, and candidate care experience.', 'https://cv.recruitpro.local/phuc-bui.pdf', 'https://github.com/phuc-bui-ops', 'https://linkedin.com/in/phuc-bui-ops', NULL, NULL),
    ('20000000-0000-4000-8000-000000000108', '10000000-0000-4000-8000-000000000108', 'Finance Analyst', 4, 'Academy of Finance', 'Ha Noi', 'Finance analyst experienced in planning, budgeting, and stakeholder reporting.', 'https://cv.recruitpro.local/mai-do.pdf', 'https://github.com/mai-do-finance', 'https://linkedin.com/in/mai-do-finance', NULL, NULL),
    ('20000000-0000-4000-8000-000000000109', '10000000-0000-4000-8000-000000000109', 'Cloud and DevOps Engineer', 6, 'Duy Tan University', 'Da Nang', 'DevOps engineer specializing in Kubernetes, AWS, CI/CD, and reliability engineering.', 'https://cv.recruitpro.local/khoa-dang.pdf', 'https://github.com/khoa-dang-cloud', 'https://linkedin.com/in/khoa-dang-cloud', 'DevOps engineer: Kubernetes, AWS, Docker, Redis, CI/CD. 6 years, reliability focus.', 'Completed'),
    ('20000000-0000-4000-8000-000000000110', '10000000-0000-4000-8000-000000000110', 'Content Marketing Executive', 2, 'University of Social Sciences and Humanities', 'Ho Chi Minh City', 'Content marketer focused on SEO, campaign execution, and audience growth.', 'https://cv.recruitpro.local/yen-pham.pdf', 'https://github.com/yen-pham-content', 'https://linkedin.com/in/yen-pham-content', NULL, NULL)
ON CONFLICT (id) DO UPDATE SET
    current_position = EXCLUDED.current_position,
    experience_years = EXCLUDED.experience_years,
    education = EXCLUDED.education,
    address = EXCLUDED.address,
    bio = EXCLUDED.bio,
    resume_url = EXCLUDED.resume_url,
    resume_extracted_text = EXCLUDED.resume_extracted_text,
    resume_parse_status = EXCLUDED.resume_parse_status;

-- CV/resume seed intentionally EMPTY: every candidate starts with NO resume (they upload their own).
-- Runs before the candidate_resumes derivation below, so no resume rows are generated.
UPDATE public.candidate_profiles SET
    resume_url = NULL,
    resume_extracted_text = NULL,
    resume_parse_status = NULL,
    parsed_resume_json = NULL,
    resume_parse_error = NULL,
    resume_parse_model = NULL,
    resume_parser_warnings_json = NULL,
    resume_parsed_at = NULL;

-- ---------------------------------------------------------------------------
-- Candidate skills
-- ---------------------------------------------------------------------------
INSERT INTO public.candidate_skills (candidate_id, skill_id) VALUES
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a'),
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67'),
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d'),
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', '90000000-0000-4000-8000-000000000001'),
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'b1779c35-b7c4-47bb-a69b-43131c084a0f'),
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d'),
    ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a'),
    ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d'),
    ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'e1330bc9-5fe4-4e6d-a5f3-768670d8104a'),
    ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'b1779c35-b7c4-47bb-a69b-43131c084a0f'),
    ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b'),
    ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', 'ee3fd9dd-15b1-45e0-875d-ebd18a48db41'),
    ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', '984f6a6b-e625-449c-8eb4-526348f279ef'),
    ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', '90000000-0000-4000-8000-000000000014'),
    ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', '984f6a6b-e625-449c-8eb4-526348f279ef'),
    ('20000000-0000-4000-8000-000000000101', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a'),
    ('20000000-0000-4000-8000-000000000101', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67'),
    ('20000000-0000-4000-8000-000000000101', 'b1779c35-b7c4-47bb-a69b-43131c084a0f'),
    ('20000000-0000-4000-8000-000000000101', '90000000-0000-4000-8000-000000000007'),
    ('20000000-0000-4000-8000-000000000101', '90000000-0000-4000-8000-000000000015'),
    ('20000000-0000-4000-8000-000000000102', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d'),
    ('20000000-0000-4000-8000-000000000102', '90000000-0000-4000-8000-000000000001'),
    ('20000000-0000-4000-8000-000000000102', '90000000-0000-4000-8000-000000000002'),
    ('20000000-0000-4000-8000-000000000102', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000006'),
    ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000007'),
    ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000008'),
    ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000019'),
    ('20000000-0000-4000-8000-000000000103', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('20000000-0000-4000-8000-000000000104', '90000000-0000-4000-8000-000000000009'),
    ('20000000-0000-4000-8000-000000000104', '90000000-0000-4000-8000-000000000020'),
    ('20000000-0000-4000-8000-000000000104', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b'),
    ('20000000-0000-4000-8000-000000000104', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('20000000-0000-4000-8000-000000000104', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000003'),
    ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000004'),
    ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000013'),
    ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000017'),
    ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000007'),
    ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000005'),
    ('20000000-0000-4000-8000-000000000106', '90000000-0000-4000-8000-000000000010'),
    ('20000000-0000-4000-8000-000000000106', '90000000-0000-4000-8000-000000000018'),
    ('20000000-0000-4000-8000-000000000106', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('20000000-0000-4000-8000-000000000106', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('20000000-0000-4000-8000-000000000107', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('20000000-0000-4000-8000-000000000107', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('20000000-0000-4000-8000-000000000107', '90000000-0000-4000-8000-000000000012'),
    ('20000000-0000-4000-8000-000000000108', '90000000-0000-4000-8000-000000000012'),
    ('20000000-0000-4000-8000-000000000108', '90000000-0000-4000-8000-000000000007'),
    ('20000000-0000-4000-8000-000000000108', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4'),
    ('20000000-0000-4000-8000-000000000109', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b'),
    ('20000000-0000-4000-8000-000000000109', 'ee3fd9dd-15b1-45e0-875d-ebd18a48db41'),
    ('20000000-0000-4000-8000-000000000109', '90000000-0000-4000-8000-000000000005'),
    ('20000000-0000-4000-8000-000000000109', '90000000-0000-4000-8000-000000000014'),
    ('20000000-0000-4000-8000-000000000109', '984f6a6b-e625-449c-8eb4-526348f279ef'),
    ('20000000-0000-4000-8000-000000000110', '90000000-0000-4000-8000-000000000011'),
    ('20000000-0000-4000-8000-000000000110', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2'),
    ('20000000-0000-4000-8000-000000000110', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4')
ON CONFLICT (candidate_id, skill_id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Jobs — 14 tin tuyển dụng quanh mốc 2026-07-04
-- Deadline trải: gần hết hạn (07-12), +30d (08-03), +60d (09-02), +90d (10-01);
-- 10 Approved, 1 PendingApproval, 1 Draft, 1 Closed (lịch sử), 1 Approved mới đăng hôm nay.
-- ---------------------------------------------------------------------------
INSERT INTO public.jobs (id, department_id, created_by, approved_by, title, short_pitch, description, requirements, benefits, location, work_mode, employment_type, min_experience_years, vacancy_count, salary_min, salary_max, deadline, status, created_at) VALUES
    ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Java Backend Developer', 'Build scalable backend services for high-traffic recruiting workflows.', 'Develop backend services using Spring Boot', 'Java, Spring Boot, PostgreSQL', 'Hybrid model, annual bonus, premium healthcare', 'Ha Noi', 'Onsite', 'FullTime', 2, 1, 25000000.00, 40000000.00, '2026-08-03 18:00:00', 'Approved', '2026-07-04 08:30:00'),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111111', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Senior Frontend Developer', 'Own the user experience for recruiter and candidate-facing product flows.', 'Build and maintain responsive frontend interfaces for the recruitment platform.', 'ReactJS, TypeScript, CSS, API integration', 'Hybrid work, premium laptop, learning budget', 'Ha Noi', 'Hybrid', 'FullTime', 3, 2, 30000000.00, 45000000.00, '2026-09-02 18:00:00', 'Approved', '2026-07-02 09:15:00'),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Full Stack Engineer', 'Work across frontend and backend on a fast-moving product team.', 'Build end-to-end features, from APIs to polished UI workflows.', 'NodeJS, ReactJS, PostgreSQL, REST, Git', 'Competitive package, flexible hours, onsite gym', 'Ho Chi Minh City', 'Hybrid', 'FullTime', 4, 3, 32000000.00, 50000000.00, '2026-08-15 18:00:00', 'Approved', '2026-06-25 10:00:00'),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000002', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'DevOps Engineer', 'Automate delivery, infrastructure, and observability across services.', 'Own CI/CD pipelines, deployment strategy, and cloud infrastructure reliability.', 'Docker, Kubernetes, CI/CD, Linux, Cloud', 'Remote-friendly, certification support, modern stack', 'Da Nang', 'Remote', 'FullTime', 5, 2, 35000000.00, 55000000.00, '2026-10-01 18:00:00', 'Approved', '2026-06-28 11:00:00'),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000003', '95db214f-5ce2-4e89-b350-93da747c7bdb', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'HR Operations Specialist', 'Support hiring operations and candidate experience end-to-end.', 'Coordinate interviews, manage records, and keep recruitment workflows moving.', 'Communication, Excel, process management, English', 'Stable team, process ownership, annual review', 'Ha Noi', 'Onsite', 'FullTime', 2, 1, 15000000.00, 22000000.00, '2026-07-31 18:00:00', 'Approved', '2026-06-20 08:30:00'),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000004', '3067eaeb-3893-468f-b854-08f1319448c9', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Marketing Specialist', 'Drive product awareness and support growth campaigns.', 'Plan and execute marketing campaigns, content, and analytics.', 'Content writing, analytics, communication, social media', 'Flexible scope, creative ownership, annual bonus', 'Ho Chi Minh City', 'Hybrid', 'FullTime', 2, 1, 18000000.00, 28000000.00, '2026-08-21 18:00:00', 'Approved', '2026-06-22 09:00:00'),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111112', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Java Backend Engineer', 'Join a backend team shipping APIs and core business services.', 'Design and implement reliable Java services for the recruiting domain.', 'Java, Spring Boot, PostgreSQL, REST, OOP', 'Hybrid model, laptop allowance, training budget', 'Da Nang', 'Hybrid', 'FullTime', 2, 2, 24000000.00, 38000000.00, '2026-09-15 18:00:00', 'Approved', '2026-06-26 09:00:00'),
    ('30000000-0000-4000-8000-000000000001', 'd1000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000005', 'Data Analyst', 'Turn hiring and funnel data into clear business decisions.', 'Model recruiting data, maintain dashboards, and present actionable insights to leaders.', 'SQL, Power BI, Excel, statistics, and strong stakeholder communication.', 'Performance bonus, hybrid office schedule, and mentoring from product leadership.', 'Ho Chi Minh City', 'Hybrid', 'FullTime', 2, 1, 22000000.00, 35000000.00, '2026-08-07 18:00:00', 'Approved', '2026-06-24 09:30:00'),
    ('30000000-0000-4000-8000-000000000002', 'd1000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000005', 'Product Designer', 'Shape end-to-end candidate and recruiter experiences.', 'Own product discovery artifacts, wireframes, handoff quality, and interface consistency.', 'Figma, design systems, communication, and usability thinking.', 'Modern design tooling, hybrid work, and product discovery exposure.', 'Ho Chi Minh City', 'Hybrid', 'FullTime', 3, 1, 28000000.00, 40000000.00, '2026-08-25 18:00:00', 'Approved', '2026-06-18 10:00:00'),
    ('30000000-0000-4000-8000-000000000003', '95db214f-5ce2-4e89-b350-93da747c7bdb', '10000000-0000-4000-8000-000000000003', '721b1851-349a-48aa-acae-feed1c1843ed', 'Talent Acquisition Executive', 'Scale candidate sourcing and scheduling for fast-growing teams.', 'Source candidates, keep pipelines moving, and partner closely with hiring managers on interview operations.', 'Communication, English, Excel, ATS discipline, and sourcing mindset.', 'Clear promotion path, KPI bonus, and supportive team rituals.', 'Ha Noi', 'Onsite', 'FullTime', 2, 2, 16000000.00, 24000000.00, '2026-08-30 18:00:00', 'Approved', '2026-06-21 10:30:00'),
    ('30000000-0000-4000-8000-000000000004', 'cc05372d-9ee5-4962-a071-74a8d7630279', '10000000-0000-4000-8000-000000000001', NULL, 'Finance Analyst', 'Strengthen planning and business finance visibility for the company.', 'Support budgeting, cost reporting, and operational finance analysis for leadership teams.', 'Excel, SQL, financial modeling, and attention to reporting accuracy.', 'Meal allowance, healthcare support, and clear annual review process.', 'Ha Noi', 'Onsite', 'FullTime', 3, 1, 20000000.00, 32000000.00, '2026-08-14 18:00:00', 'PendingApproval', '2026-07-01 08:45:00'),
    ('30000000-0000-4000-8000-000000000005', 'd1000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000003', NULL, 'Technical Support Specialist', 'Prepare internal support coverage for the next platform rollout.', 'Handle issue triage, knowledge base upkeep, and first-line support coordination.', 'Communication, English, process ownership, and customer mindset.', 'Shift allowance, onboarding roadmap, and structured coaching.', 'Da Nang', 'Onsite', 'FullTime', 1, 2, 12000000.00, 18000000.00, '2026-10-02 18:00:00', 'Draft', '2026-07-03 09:15:00'),
    ('30000000-0000-4000-8000-000000000006', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'QA Automation Engineer', 'Guard release quality with reliable automated regression coverage.', 'Design and maintain automated test suites for web and API layers, integrate them into CI/CD, and champion release quality gates.', 'Selenium or Playwright, Docker, API testing, and a strong quality mindset.', 'Quality-first culture, device lab access, and conference budget.', 'Da Nang', 'Hybrid', 'FullTime', 2, 1, 20000000.00, 32000000.00, '2026-07-12 18:00:00', 'Approved', '2026-06-20 09:00:00'),
    ('30000000-0000-4000-8000-000000000007', 'd1000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000004', 'Senior Data Engineer', 'Build the data platform powering recruiting analytics.', 'Design data pipelines and warehouse models for hiring analytics at scale.', 'Python, SQL, data pipelines, and cloud data warehousing.', 'Data-driven culture, modern stack, and platform ownership.', 'Ho Chi Minh City', 'Remote', 'FullTime', 4, 1, 38000000.00, 55000000.00, '2026-06-20 18:00:00', 'Closed', '2026-05-20 09:00:00')
ON CONFLICT (id) DO UPDATE SET
    department_id = EXCLUDED.department_id,
    approved_by = EXCLUDED.approved_by,
    title = EXCLUDED.title,
    short_pitch = EXCLUDED.short_pitch,
    description = EXCLUDED.description,
    requirements = EXCLUDED.requirements,
    benefits = EXCLUDED.benefits,
    location = EXCLUDED.location,
    work_mode = EXCLUDED.work_mode,
    employment_type = EXCLUDED.employment_type,
    min_experience_years = EXCLUDED.min_experience_years,
    vacancy_count = EXCLUDED.vacancy_count,
    salary_min = EXCLUDED.salary_min,
    salary_max = EXCLUDED.salary_max,
    deadline = EXCLUDED.deadline,
    status = EXCLUDED.status,
    created_at = EXCLUDED.created_at;

-- ---------------------------------------------------------------------------
-- Job skills
-- ---------------------------------------------------------------------------
INSERT INTO public.job_skills (job_id, skill_id, min_years_experience, is_required) VALUES
    ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 2, true),
    ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 1, true),
    ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'b1779c35-b7c4-47bb-a69b-43131c084a0f', 1, false),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111111', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d', 3, true),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111111', '90000000-0000-4000-8000-000000000001', 2, true),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111111', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, false),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'e1330bc9-5fe4-4e6d-a5f3-768670d8104a', 4, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d', 3, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'b1779c35-b7c4-47bb-a69b-43131c084a0f', 2, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000002', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b', 5, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000002', 'ee3fd9dd-15b1-45e0-875d-ebd18a48db41', 4, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '90000000-0000-4000-8000-000000000014', 3, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '984f6a6b-e625-449c-8eb4-526348f279ef', 3, false),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000003', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000003', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 2, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000004', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true),
    ('7c2d3e4f-5a6b-4c7d-8e9f-000000000004', '90000000-0000-4000-8000-000000000011', 1, false),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111112', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 2, true),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111112', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 2, true),
    ('8c1b65c9-1c9f-4a36-9e6a-111111111112', 'b1779c35-b7c4-47bb-a69b-43131c084a0f', 1, true),
    ('30000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000007', 2, true),
    ('30000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000008', 2, true),
    ('30000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000012', 2, true),
    ('30000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000019', 1, false),
    ('30000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000010', 3, true),
    ('30000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000018', 2, true),
    ('30000000-0000-4000-8000-000000000002', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true),
    ('30000000-0000-4000-8000-000000000003', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true),
    ('30000000-0000-4000-8000-000000000003', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 2, true),
    ('30000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000012', 2, false),
    ('30000000-0000-4000-8000-000000000004', '90000000-0000-4000-8000-000000000012', 3, true),
    ('30000000-0000-4000-8000-000000000004', '90000000-0000-4000-8000-000000000007', 2, true),
    ('30000000-0000-4000-8000-000000000005', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 1, true),
    ('30000000-0000-4000-8000-000000000005', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 1, true),
    ('30000000-0000-4000-8000-000000000006', '90000000-0000-4000-8000-000000000009', 2, true),
    ('30000000-0000-4000-8000-000000000006', '90000000-0000-4000-8000-000000000020', 1, false),
    ('30000000-0000-4000-8000-000000000006', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b', 1, false),
    ('30000000-0000-4000-8000-000000000007', '90000000-0000-4000-8000-000000000006', 3, true),
    ('30000000-0000-4000-8000-000000000007', '90000000-0000-4000-8000-000000000007', 3, true)
ON CONFLICT (job_id, skill_id) DO UPDATE SET
    min_years_experience = EXCLUDED.min_years_experience,
    is_required = EXCLUDED.is_required;

-- ---------------------------------------------------------------------------
-- Applications — applied 2026-06-20 → 2026-07-04, đủ trạng thái pipeline
-- (INV-014: mỗi cặp candidate–job chỉ có tối đa 1 hồ sơ ACTIVE; Hired/Rejected/Withdrawn là closed)
-- ---------------------------------------------------------------------------
INSERT INTO public.applications (id, user_id, job_id, reviewed_by, status, applied_at, cover_letter, rule_score, semantic_score, final_score, score_status, scored_at) VALUES
    ('75afffb4-ad67-4974-807c-0308a90f07bf', '4071353e-5816-4746-a8b6-c0bc3113c44d', '59a5221b-43bb-489f-83b7-d41c347e37f9', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Interview', '2026-06-24 09:20:00', 'Tôi muốn đóng góp cho nền tảng tuyển dụng bằng kinh nghiệm Java và React.', 86.00, 84.50, 85.40, 'Completed', '2026-06-24 09:25:00'),
    ('a1111111-1111-4111-8111-111111111111', 'd8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', '8c1b65c9-1c9f-4a36-9e6a-111111111111', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Applied', '2026-07-03 09:00:00', 'React là thế mạnh chính của tôi trong 3 năm qua.', NULL, NULL, NULL, NULL, NULL),
    ('a1111111-1111-4111-8111-111111111112', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', '7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-06-30 09:15:00', 'Kinh nghiệm full stack 4 năm với NodeJS và React.', 78.00, 80.00, 79.20, 'Completed', '2026-06-30 10:00:00'),
    ('a1111111-1111-4111-8111-111111111113', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '721b1851-349a-48aa-acae-feed1c1843ed', 'Interview', '2026-06-27 09:30:00', 'DevOps 5 năm: Docker, Kubernetes, CI/CD.', 90.00, 88.00, 88.80, 'Completed', '2026-06-27 10:00:00'),
    ('a1111111-1111-4111-8111-111111111114', 'c3d4e5f6-a7b8-4903-9a04-001122334455', '7c2d3e4f-5a6b-4c7d-8e9f-000000000003', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Applied', '2026-07-02 09:45:00', 'Kỹ năng giao tiếp và thiết kế quy trình phù hợp vị trí HR Operations.', NULL, NULL, NULL, NULL, NULL),
    ('40000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000101', '59a5221b-43bb-489f-83b7-d41c347e37f9', '10000000-0000-4000-8000-000000000002', 'Hired', '2026-06-20 08:00:00', 'Java microservices là trọng tâm sự nghiệp của tôi.', 92.00, 90.00, 90.80, 'Completed', '2026-06-20 09:00:00'),
    ('40000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000102', '8c1b65c9-1c9f-4a36-9e6a-111111111111', '10000000-0000-4000-8000-000000000002', 'Interview', '2026-06-25 08:15:00', 'Tôi xây dựng UI tuyển dụng bằng React + TypeScript.', 84.00, 86.00, 85.20, 'Completed', '2026-06-25 09:00:00'),
    ('40000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000103', '30000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000001', 'Offer', '2026-06-22 08:30:00', 'Dashboard và SQL modeling là thế mạnh của tôi.', 88.00, 85.00, 86.20, 'Completed', '2026-06-22 09:30:00'),
    ('40000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000104', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '10000000-0000-4000-8000-000000000003', 'Interview', '2026-06-26 08:45:00', 'Tôi muốn mở rộng từ QA automation sang delivery pipeline.', 74.00, 72.00, 72.80, 'Completed', '2026-06-26 09:30:00'),
    ('40000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000105', '30000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000002', 'Screening', '2026-07-01 09:00:00', 'Backend .NET với nền tảng SQL vững.', 70.00, 68.00, 68.80, 'Completed', '2026-07-01 10:00:00'),
    ('40000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000106', '30000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000001', 'Hired', '2026-06-21 09:10:00', 'Design system và discovery là sở trường của tôi.', 91.00, 89.00, 89.80, 'Completed', '2026-06-21 10:00:00'),
    ('40000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000107', '30000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000003', 'Interview', '2026-06-29 09:20:00', 'Sourcing và phối hợp phỏng vấn là công việc hằng ngày của tôi.', 76.00, 75.00, 75.40, 'Completed', '2026-06-29 10:00:00'),
    ('40000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000108', '30000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000001', 'Applied', '2026-07-04 08:25:00', 'Tôi muốn chuyển hướng sang phân tích dữ liệu tuyển dụng.', NULL, NULL, NULL, NULL, NULL),
    ('40000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000109', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '10000000-0000-4000-8000-000000000002', 'Interview', '2026-06-23 09:30:00', 'Kubernetes và AWS ở quy mô lớn là kinh nghiệm chính.', 93.00, 91.00, 91.80, 'Completed', '2026-06-23 10:15:00'),
    ('40000000-0000-4000-8000-000000000010', '10000000-0000-4000-8000-000000000110', '7c2d3e4f-5a6b-4c7d-8e9f-000000000004', '10000000-0000-4000-8000-000000000003', 'Interview', '2026-06-28 09:35:00', 'SEO và content growth cho sản phẩm B2B.', 71.00, 70.00, 70.40, 'Completed', '2026-06-28 10:15:00'),
    ('40000000-0000-4000-8000-000000000011', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', '59a5221b-43bb-489f-83b7-d41c347e37f9', '10000000-0000-4000-8000-000000000001', 'ManagerReview', '2026-06-30 09:40:00', 'Ứng tuyển thêm vị trí Java backend vì nền tảng Java của tôi.', 82.00, 79.00, 80.20, 'Completed', '2026-06-30 10:30:00'),
    ('40000000-0000-4000-8000-000000000012', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '10000000-0000-4000-8000-000000000001', 'Hired', '2026-06-20 09:45:00', 'Hồ sơ DevOps trước đó của tôi — đã hoàn tất offer.', 90.00, 89.00, 89.40, 'Completed', '2026-06-20 10:30:00'),
    ('40000000-0000-4000-8000-000000000013', 'c3d4e5f6-a7b8-4903-9a04-001122334455', '7c2d3e4f-5a6b-4c7d-8e9f-000000000003', '10000000-0000-4000-8000-000000000003', 'Hired', '2026-06-20 09:50:00', 'Quy trình và vận hành là thế mạnh của tôi.', 85.00, 83.00, 83.80, 'Completed', '2026-06-20 10:40:00'),
    ('40000000-0000-4000-8000-000000000014', '4071353e-5816-4746-a8b6-c0bc3113c44d', '8c1b65c9-1c9f-4a36-9e6a-111111111112', '10000000-0000-4000-8000-000000000002', 'Screening', '2026-07-02 09:55:00', 'Java backend tại Đà Nẵng phù hợp định hướng của tôi.', 81.00, 78.00, 79.20, 'Completed', '2026-07-02 10:30:00'),
    ('41000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000104', '30000000-0000-4000-8000-000000000006', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-01 10:00:00', 'Regression automation với Selenium/Playwright là công việc chính của tôi.', 87.00, 85.00, 85.80, 'Completed', '2026-07-01 10:45:00'),
    ('41000000-0000-4000-8000-000000000002', 'd8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', '7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Rejected', '2026-06-21 10:10:00', 'Ứng tuyển full stack dù thiên về frontend.', 55.00, 52.00, 53.20, 'Completed', '2026-06-21 11:00:00'),
    ('41000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000103', '30000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000002', 'Withdrawn', '2026-05-25 10:20:00', 'Ứng tuyển Data Engineer (đã rút để nhận offer Data Analyst).', 80.00, 77.00, 78.20, 'Completed', '2026-05-25 11:00:00'),
    ('41000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000105', '30000000-0000-4000-8000-000000000006', NULL, 'Applied', '2026-07-04 09:05:00', 'Tôi có kinh nghiệm test tự động API trong dự án .NET.', NULL, NULL, NULL, NULL, NULL)
ON CONFLICT (id) DO UPDATE SET
    reviewed_by = EXCLUDED.reviewed_by,
    status = EXCLUDED.status,
    applied_at = EXCLUDED.applied_at,
    cover_letter = EXCLUDED.cover_letter,
    rule_score = EXCLUDED.rule_score,
    semantic_score = EXCLUDED.semantic_score,
    final_score = EXCLUDED.final_score,
    score_status = EXCLUDED.score_status,
    scored_at = EXCLUDED.scored_at;

-- ---------------------------------------------------------------------------
-- Offers (BR-APPLICATION-009: Offer → offer 'Sent'; Hired → offer 'Accepted')
-- ---------------------------------------------------------------------------
INSERT INTO public.application_offers
    (id, application_id, offer_template_id, base_salary, currency_code, bonus_description, equity_notes, employment_type, proposed_start_date, probation_period, reporting_manager_id, personal_message, status, sent_at, created_at, updated_at) VALUES
    ('93000000-0000-4000-8000-000000000001', '40000000-0000-4000-8000-000000000003', '91000000-0000-4000-8000-000000000001', 32000000.00, 'VND', 'Quarterly performance bonus.', NULL, 'Full-time', '2026-08-03 00:00:00', '2 months', NULL, 'We are pleased to extend this offer for the Data Analyst role.', 'Sent', '2026-07-02 10:00:00', '2026-07-02 09:30:00', '2026-07-02 10:00:00'),
    ('93000000-0000-4000-8000-000000000002', '40000000-0000-4000-8000-000000000001', '91000000-0000-4000-8000-000000000001', 45000000.00, 'VND', 'Annual performance bonus.', NULL, 'Full-time', '2026-08-03 00:00:00', '2 months', NULL, 'Congratulations on completing the Java Backend interview process.', 'Accepted', '2026-06-29 09:00:00', '2026-06-28 16:00:00', '2026-07-01 11:00:00'),
    ('93000000-0000-4000-8000-000000000003', '40000000-0000-4000-8000-000000000006', '91000000-0000-4000-8000-000000000001', 38000000.00, 'VND', NULL, NULL, 'Full-time', '2026-08-10 00:00:00', '2 months', NULL, 'Welcome aboard as our new Product Designer.', 'Accepted', '2026-06-30 09:30:00', '2026-06-29 16:30:00', '2026-07-02 11:15:00'),
    ('93000000-0000-4000-8000-000000000004', '40000000-0000-4000-8000-000000000012', '91000000-0000-4000-8000-000000000001', 42000000.00, 'VND', 'Sign-on bonus.', NULL, 'Full-time', '2026-08-17 00:00:00', '2 months', NULL, 'We look forward to having you on the DevOps team.', 'Accepted', '2026-07-01 09:00:00', '2026-06-30 17:00:00', '2026-07-03 10:00:00'),
    ('93000000-0000-4000-8000-000000000005', '40000000-0000-4000-8000-000000000013', '91000000-0000-4000-8000-000000000001', 35000000.00, 'VND', NULL, NULL, 'Full-time', '2026-08-03 00:00:00', '2 months', NULL, 'Excited to confirm your operations role.', 'Accepted', '2026-06-30 14:00:00', '2026-06-29 18:00:00', '2026-07-02 09:00:00')
ON CONFLICT (id) DO UPDATE SET
    base_salary = EXCLUDED.base_salary,
    proposed_start_date = EXCLUDED.proposed_start_date,
    status = EXCLUDED.status,
    sent_at = EXCLUDED.sent_at,
    created_at = EXCLUDED.created_at,
    updated_at = EXCLUDED.updated_at;

-- ---------------------------------------------------------------------------
-- Interviews — hoàn thành cuối tháng 6, lịch sắp tới rải tháng 7–9/2026, 1 lịch Canceled
-- ---------------------------------------------------------------------------
INSERT INTO public.interviews (id, application_id, interview_date, meeting_type, meeting_link, location, notes, status) VALUES
    ('ce4c916b-dfe9-455c-9fbc-f8f3bfa6c994', '75afffb4-ad67-4974-807c-0308a90f07bf', '2026-07-08 10:00:00', 'Online', 'https://meet.google.com/rp-java-r1', NULL, 'Technical interview round 1', 'Scheduled'),
    ('d2222222-2222-4222-8222-222222222221', 'a1111111-1111-4111-8111-111111111112', '2026-07-06 14:00:00', 'Online', 'https://meet.google.com/rp-fullstack-screen', NULL, 'Frontend screening interview', 'Scheduled'),
    ('d2222222-2222-4222-8222-222222222222', 'a1111111-1111-4111-8111-111111111113', '2026-07-09 14:00:00', 'Offline', NULL, 'RecruitPro Da Nang Office', 'HR and culture fit interview', 'Scheduled'),
    ('50000000-0000-4000-8000-000000000001', '40000000-0000-4000-8000-000000000001', '2026-06-24 09:00:00', 'Online', 'https://meet.google.com/rp-java-final', NULL, 'Final technical round completed successfully.', 'Completed'),
    ('50000000-0000-4000-8000-000000000002', '40000000-0000-4000-8000-000000000002', '2026-07-15 14:00:00', 'Online', 'https://meet.google.com/rp-frontend-1', NULL, 'Portfolio walkthrough and frontend architecture discussion.', 'Scheduled'),
    ('50000000-0000-4000-8000-000000000003', '40000000-0000-4000-8000-000000000003', '2026-06-26 09:30:00', 'Offline', NULL, 'RecruitPro HCM Office', 'Manager review session with product and analytics stakeholders.', 'Completed'),
    ('50000000-0000-4000-8000-000000000004', '40000000-0000-4000-8000-000000000004', '2026-07-07 10:00:00', 'Online', 'https://meet.google.com/rp-devops-cloud', NULL, 'Cloud architecture and incident response discussion.', 'Scheduled'),
    ('50000000-0000-4000-8000-000000000005', '40000000-0000-4000-8000-000000000006', '2026-06-22 15:00:00', 'Offline', NULL, 'RecruitPro HCM Office', 'Design case study review completed.', 'Completed'),
    ('50000000-0000-4000-8000-000000000006', '40000000-0000-4000-8000-000000000007', '2026-08-06 09:00:00', 'Offline', NULL, 'RecruitPro Ha Noi Office', 'Culture and process interview with HR leadership.', 'Scheduled'),
    ('50000000-0000-4000-8000-000000000007', '40000000-0000-4000-8000-000000000010', '2026-09-10 10:00:00', 'Online', 'https://meet.google.com/rp-content-final', NULL, 'Content strategy and campaign planning round.', 'Scheduled'),
    ('50000000-0000-4000-8000-000000000008', '40000000-0000-4000-8000-000000000012', '2026-06-30 11:00:00', 'Online', 'https://meet.google.com/rp-devops-final', NULL, 'Offer readiness confirmed after technical closeout.', 'Completed'),
    ('50000000-0000-4000-8000-000000000009', '40000000-0000-4000-8000-000000000013', '2026-06-28 13:30:00', 'Offline', NULL, 'RecruitPro Ha Noi Office', 'Operations and process ownership interview completed.', 'Completed'),
    ('50000000-0000-4000-8000-000000000010', '40000000-0000-4000-8000-000000000009', '2026-07-01 10:00:00', 'Online', 'https://meet.google.com/rp-cloud-r1', NULL, 'Candidate requested reschedule — slot released.', 'Canceled'),
    ('50000000-0000-4000-8000-000000000011', '40000000-0000-4000-8000-000000000009', '2026-07-11 10:00:00', 'Online', 'https://meet.google.com/rp-cloud-r1b', NULL, 'Rescheduled cloud architecture round.', 'Scheduled')
ON CONFLICT (id) DO UPDATE SET
    interview_date = EXCLUDED.interview_date,
    meeting_type = EXCLUDED.meeting_type,
    meeting_link = EXCLUDED.meeting_link,
    location = EXCLUDED.location,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status;

-- ---------------------------------------------------------------------------
-- Notifications — 1–7 ngày gần nhất trước 2026-07-04, trộn đã đọc/chưa đọc
-- ---------------------------------------------------------------------------
INSERT INTO public.notifications (id, user_id, event_code, title, content, type, is_read, read_at, is_seen, seen_at, created_at) VALUES
    ('60000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000101', 'application_status_changed', 'Chúc mừng! Bạn đã trúng tuyển', 'Hồ sơ Java Backend Developer của bạn đã hoàn tất — chào mừng gia nhập đội ngũ từ 03/08/2026.', 'Application', true, '2026-07-01 11:30:00', true, '2026-07-01 11:29:00', '2026-07-01 11:00:00'),
    ('60000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000102', 'interview_scheduled', 'Lịch phỏng vấn mới', 'Phỏng vấn Senior Frontend Developer được đặt vào 14:00 ngày 15/07/2026.', 'Interview', false, NULL, true, '2026-07-03 08:05:00', '2026-07-03 08:00:00'),
    ('60000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000103', 'application_status_changed', 'Bạn nhận được offer', 'Offer vị trí Data Analyst đã được gửi — vui lòng phản hồi trước 12/07/2026.', 'Application', false, NULL, false, NULL, '2026-07-02 10:05:00'),
    ('60000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000104', 'interview_scheduled', 'Lịch phỏng vấn DevOps', 'Phỏng vấn DevOps Engineer diễn ra 10:00 ngày 07/07/2026 (online).', 'Interview', false, NULL, true, '2026-07-02 09:10:00', '2026-07-02 09:00:00'),
    ('60000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000105', 'application_status_changed', 'Hồ sơ đang được sàng lọc', 'Hồ sơ Data Analyst của bạn đã chuyển sang bước Screening.', 'Application', true, '2026-07-01 12:00:00', true, '2026-07-01 11:55:00', '2026-07-01 10:05:00'),
    ('60000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000106', 'application_status_changed', 'Chúc mừng Product Designer mới', 'Bạn đã hoàn tất quy trình Product Designer — offer đã được xác nhận.', 'Application', true, '2026-07-02 11:20:00', true, '2026-07-02 11:19:00', '2026-07-02 11:16:00'),
    ('60000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000107', 'interview_scheduled', 'Lịch phỏng vấn tháng 8', 'Phỏng vấn Talent Acquisition Executive được đặt vào 09:00 ngày 06/08/2026.', 'Interview', false, NULL, false, NULL, '2026-07-03 14:00:00'),
    ('60000000-0000-4000-8000-000000000008', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'new_application_received', 'Hồ sơ mới hôm nay', '2 ứng viên vừa nộp hồ sơ vào QA Automation Engineer và Data Analyst.', 'Application', false, NULL, false, NULL, '2026-07-04 09:10:00'),
    ('60000000-0000-4000-8000-000000000009', '721b1851-349a-48aa-acae-feed1c1843ed', 'application_status_changed', 'Hồ sơ chờ bạn duyệt', 'Nguyễn Đức Minh (Java Backend Developer) đang chờ Trưởng bộ phận review.', 'Application', false, NULL, true, '2026-07-03 09:00:00', '2026-07-02 17:00:00'),
    ('60000000-0000-4000-8000-000000000010', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', NULL, 'Tổng quan hệ thống tuần', 'Tuần qua có 6 hồ sơ mới, 4 lịch phỏng vấn và 2 offer được chấp nhận.', 'System', true, '2026-07-03 08:30:00', true, '2026-07-03 08:29:00', '2026-07-03 08:00:00'),
    ('60000000-0000-4000-8000-000000000011', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'interview_scheduled', 'Lịch phỏng vấn đã được xác nhận', 'Bạn có lịch phỏng vấn vị trí Java Backend Developer vào 10:00 ngày 08/07/2026.', 'Interview', false, NULL, true, '2026-07-03 18:05:00', '2026-07-03 18:00:00'),
    ('60000000-0000-4000-8000-000000000012', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'application_status_changed', 'Hồ sơ đang được đánh giá', 'Nguyễn Thục Uyên đã chuyển hồ sơ của bạn sang vòng phỏng vấn kỹ thuật.', 'Application', true, '2026-06-29 17:35:00', true, '2026-06-29 17:34:00', '2026-06-29 17:30:00'),
    ('60000000-0000-4000-8000-000000000013', 'a0000000-0000-4000-8000-000000000001', NULL, 'Workflow automation có lỗi mới', '1 execution của "Pass CV → Notify Head Review" thất bại lúc 09:12 ngày 03/07 — xem Execution History.', 'System', false, NULL, false, NULL, '2026-07-03 09:15:00'),
    ('60000000-0000-4000-8000-000000000014', 'a0000000-0000-4000-8000-000000000001', NULL, 'Tài khoản bị vô hiệu hóa', 'Tài khoản yennhi.pham@recruitpro.vn đã được chuyển sang trạng thái Inactive.', 'System', true, '2026-07-02 11:10:00', true, '2026-07-02 11:09:00', '2026-07-02 11:05:00')
ON CONFLICT (id) DO UPDATE SET
    event_code = EXCLUDED.event_code,
    title = EXCLUDED.title,
    content = EXCLUDED.content,
    type = EXCLUDED.type,
    is_read = EXCLUDED.is_read,
    read_at = EXCLUDED.read_at,
    is_seen = EXCLUDED.is_seen,
    seen_at = EXCLUDED.seen_at,
    created_at = EXCLUDED.created_at;

-- ---------------------------------------------------------------------------
-- Refresh tokens (demo phiên đăng nhập gần đây; hết hạn sau mốc base date)
-- ---------------------------------------------------------------------------
INSERT INTO public.refresh_tokens (id, user_id, token, expiry_date) VALUES
    ('70000000-0000-4000-8000-000000000001', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'rt_candidate_20260703_0001', '2026-08-02 09:00:00'),
    ('70000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'rt_hr_20260703_0001', '2026-08-02 09:05:00'),
    ('70000000-0000-4000-8000-000000000003', '721b1851-349a-48aa-acae-feed1c1843ed', 'rt_manager_20260703_0001', '2026-08-02 09:10:00'),
    ('70000000-0000-4000-8000-000000000004', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'rt_admin_20260703_0001', '2026-08-02 09:15:00'),
    ('70000000-0000-4000-8000-000000000005', 'a0000000-0000-4000-8000-000000000001', 'rt_sysadmin_20260703_0001', '2026-08-02 09:20:00'),
    ('70000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000101', 'rt_minh_20260703_0001', '2026-08-02 09:45:00'),
    ('70000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000102', 'rt_linh_20260703_0001', '2026-08-02 09:50:00'),
    ('70000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000109', 'rt_khoa_20260703_0001', '2026-08-02 10:05:00')
ON CONFLICT (id) DO UPDATE SET token = EXCLUDED.token, expiry_date = EXCLUDED.expiry_date;

-- ---------------------------------------------------------------------------
-- System logs — audit trail demo cho màn System Admin → Audit Logs
-- ---------------------------------------------------------------------------
INSERT INTO public.system_logs (id, user_id, action, description, created_at) VALUES
    ('80000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000001', 'JOB_CREATED', 'Created Data Analyst job posting.', '2026-06-24 09:31:00'),
    ('80000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000003', 'JOB_CREATED', 'Created Talent Acquisition Executive and Technical Support Specialist job postings.', '2026-06-21 10:31:00'),
    ('80000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000004', 'JOB_APPROVED', 'Approved engineering and operations job postings.', '2026-06-25 09:40:00'),
    ('80000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000101', 'APPLICATION_SUBMITTED', 'Submitted application for Java Backend Developer.', '2026-06-20 08:00:10'),
    ('80000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000104', 'APPLICATION_SUBMITTED', 'Submitted application for QA Automation Engineer.', '2026-07-01 10:00:10'),
    ('80000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000108', 'APPLICATION_SUBMITTED', 'Submitted application for Data Analyst.', '2026-07-04 08:25:10'),
    ('80000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000003', 'INTERVIEW_SCHEDULED', 'Scheduled Talent Acquisition Executive and DevOps Engineer interview rounds.', '2026-07-02 09:05:00'),
    ('80000000-0000-4000-8000-000000000008', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'OFFER_SENT', 'Sent Data Analyst offer to Phạm Quốc Anh.', '2026-07-02 10:01:00'),
    ('80000000-0000-4000-8000-000000000009', '721b1851-349a-48aa-acae-feed1c1843ed', 'APPROVAL_QUEUE_VIEWED', 'Reviewed pending approval queue and shortlisted candidate pipeline.', '2026-07-03 09:25:00'),
    ('80000000-0000-4000-8000-000000000010', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'SYSTEM_AUDIT_VIEWED', 'Viewed system usage summary and audit coverage.', '2026-07-03 09:30:00'),
    ('80000000-0000-4000-8000-000000000011', 'a0000000-0000-4000-8000-000000000001', 'USER_STATUS_CHANGED', 'Đổi trạng thái tài khoản yennhi.pham@recruitpro.vn: Active → Inactive.', '2026-07-02 11:05:00'),
    ('80000000-0000-4000-8000-000000000012', 'a0000000-0000-4000-8000-000000000001', 'RBAC_PERMISSIONS_UPDATED', 'Cập nhật quyền cho vai trò HeadDepartment: 4 quyền được cấp.', '2026-07-01 15:20:00')
ON CONFLICT (id) DO UPDATE SET
    action = EXCLUDED.action,
    description = EXCLUDED.description,
    created_at = EXCLUDED.created_at;


-- AI Recruitment Copilot module

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
    input_hash character varying(64),
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

ALTER TABLE ONLY public.copilot_conversations ADD CONSTRAINT copilot_conversations_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.copilot_messages ADD CONSTRAINT copilot_messages_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.copilot_ranking_sessions ADD CONSTRAINT copilot_ranking_sessions_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT copilot_ranking_results_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.copilot_saved_rules ADD CONSTRAINT copilot_saved_rules_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT copilot_candidate_tags_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.copilot_messages ADD CONSTRAINT ck_copilot_messages_role CHECK (((role)::text = ANY ((ARRAY['User'::character varying, 'Assistant'::character varying, 'System'::character varying])::text[])));
ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT ck_copilot_ranking_results_recommendation CHECK (((recommendation)::text = ANY ((ARRAY['Interview'::character varying, 'Consider'::character varying, 'Hold'::character varying, 'Reject'::character varying])::text[])));
ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT ck_copilot_candidate_tags_source CHECK (((source)::text = ANY ((ARRAY['AI'::character varying, 'HR'::character varying, 'System'::character varying])::text[])));
ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT uq_copilot_ranking_results_session_candidate UNIQUE (ranking_session_id, candidate_user_id);

ALTER TABLE ONLY public.copilot_conversations ADD CONSTRAINT copilot_conversations_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_conversations ADD CONSTRAINT copilot_conversations_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_messages ADD CONSTRAINT copilot_messages_conversation_id_fkey FOREIGN KEY (conversation_id) REFERENCES public.copilot_conversations(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_ranking_sessions ADD CONSTRAINT copilot_ranking_sessions_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_ranking_sessions ADD CONSTRAINT copilot_ranking_sessions_conversation_id_fkey FOREIGN KEY (conversation_id) REFERENCES public.copilot_conversations(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_ranking_sessions ADD CONSTRAINT copilot_ranking_sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_conversations ADD CONSTRAINT copilot_conversations_latest_ranking_session_id_fkey FOREIGN KEY (latest_ranking_session_id) REFERENCES public.copilot_ranking_sessions(id) ON DELETE SET NULL;
ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT copilot_ranking_results_ranking_session_id_fkey FOREIGN KEY (ranking_session_id) REFERENCES public.copilot_ranking_sessions(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT copilot_ranking_results_candidate_user_id_fkey FOREIGN KEY (candidate_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_ranking_results ADD CONSTRAINT copilot_ranking_results_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_saved_rules ADD CONSTRAINT copilot_saved_rules_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_saved_rules ADD CONSTRAINT copilot_saved_rules_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT copilot_candidate_tags_candidate_user_id_fkey FOREIGN KEY (candidate_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT copilot_candidate_tags_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT copilot_candidate_tags_ranking_session_id_fkey FOREIGN KEY (ranking_session_id) REFERENCES public.copilot_ranking_sessions(id) ON DELETE SET NULL;
ALTER TABLE ONLY public.copilot_candidate_tags ADD CONSTRAINT copilot_candidate_tags_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.users(id) ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS ix_copilot_conversations_job_user_created_at ON public.copilot_conversations USING btree (job_id, user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_conversations_latest_ranking_session_id ON public.copilot_conversations USING btree (latest_ranking_session_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_copilot_conversations_active_job_user ON public.copilot_conversations USING btree (job_id, user_id) WHERE ((status)::text = 'Active'::text);
CREATE INDEX IF NOT EXISTS ix_copilot_messages_conversation_sequence ON public.copilot_messages USING btree (conversation_id, sequence_no);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_sessions_conversation_created_at ON public.copilot_ranking_sessions USING btree (conversation_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_sessions_job_created_at ON public.copilot_ranking_sessions USING btree (job_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_sessions_job_user_input_hash ON public.copilot_ranking_sessions USING btree (job_id, user_id, input_hash);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_results_session_rank ON public.copilot_ranking_results USING btree (ranking_session_id, rank_position);
CREATE INDEX IF NOT EXISTS ix_copilot_ranking_results_session_reject_score ON public.copilot_ranking_results USING btree (ranking_session_id, is_auto_rejected, total_score DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_saved_rules_job_active ON public.copilot_saved_rules USING btree (job_id, is_active);
CREATE INDEX IF NOT EXISTS ix_copilot_saved_rules_job_user_updated_at ON public.copilot_saved_rules USING btree (job_id, user_id, updated_at DESC);
CREATE INDEX IF NOT EXISTS ix_copilot_saved_rules_job_user_deleted ON public.copilot_saved_rules USING btree (job_id, user_id, is_deleted);
CREATE INDEX IF NOT EXISTS ix_copilot_candidate_tags_job_tag ON public.copilot_candidate_tags USING btree (job_id, tag_name);

--
-- Copilot v2 artifacts (migration 20260630000000_AddCopilotV2Artifacts)
-- Prompt templates, per-candidate fit analyses, and persisted generated artifacts.
-- Keep in sync with RecruitPro.Infrastructure/Migrations/20260630000000_AddCopilotV2Artifacts.cs.
--

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

-- NOTE: no PL/pgSQL DO blocks here on purpose. Some SQL runners split scripts on ";" and choke on
-- dollar-quoted bodies ("unterminated dollar-quoted string"). Postgres has no ADD CONSTRAINT IF NOT
-- EXISTS, so each constraint is made idempotent with DROP CONSTRAINT IF EXISTS + ADD CONSTRAINT.
ALTER TABLE ONLY public.copilot_prompt_templates DROP CONSTRAINT IF EXISTS copilot_prompt_templates_pkey;
ALTER TABLE ONLY public.copilot_prompt_templates ADD CONSTRAINT copilot_prompt_templates_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.candidate_fit_analyses DROP CONSTRAINT IF EXISTS candidate_fit_analyses_pkey;
ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.copilot_generated_artifacts DROP CONSTRAINT IF EXISTS copilot_generated_artifacts_pkey;
ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.copilot_prompt_templates DROP CONSTRAINT IF EXISTS copilot_prompt_templates_owner_user_id_fkey;
ALTER TABLE ONLY public.copilot_prompt_templates ADD CONSTRAINT copilot_prompt_templates_owner_user_id_fkey FOREIGN KEY (owner_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.candidate_fit_analyses DROP CONSTRAINT IF EXISTS candidate_fit_analyses_job_id_fkey;
ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.candidate_fit_analyses DROP CONSTRAINT IF EXISTS candidate_fit_analyses_candidate_user_id_fkey;
ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_candidate_user_id_fkey FOREIGN KEY (candidate_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.candidate_fit_analyses DROP CONSTRAINT IF EXISTS candidate_fit_analyses_application_id_fkey;
ALTER TABLE ONLY public.candidate_fit_analyses ADD CONSTRAINT candidate_fit_analyses_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_generated_artifacts DROP CONSTRAINT IF EXISTS copilot_generated_artifacts_owner_user_id_fkey;
ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_owner_user_id_fkey FOREIGN KEY (owner_user_id) REFERENCES public.users(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.copilot_generated_artifacts DROP CONSTRAINT IF EXISTS copilot_generated_artifacts_job_id_fkey;
ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_job_id_fkey FOREIGN KEY (job_id) REFERENCES public.jobs(id) ON DELETE SET NULL;
ALTER TABLE ONLY public.copilot_generated_artifacts DROP CONSTRAINT IF EXISTS copilot_generated_artifacts_application_id_fkey;
ALTER TABLE ONLY public.copilot_generated_artifacts ADD CONSTRAINT copilot_generated_artifacts_application_id_fkey FOREIGN KEY (application_id) REFERENCES public.applications(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_copilot_prompt_templates_owner_type_active ON public.copilot_prompt_templates USING btree (owner_user_id, template_type, is_active);
CREATE INDEX IF NOT EXISTS ix_candidate_fit_analyses_job_candidate_created ON public.candidate_fit_analyses USING btree (job_id, candidate_user_id, created_at);
CREATE INDEX IF NOT EXISTS ix_candidate_fit_analyses_audit_id ON public.candidate_fit_analyses USING btree (audit_id);
CREATE INDEX IF NOT EXISTS ix_copilot_generated_artifacts_owner_type_created ON public.copilot_generated_artifacts USING btree (owner_user_id, artifact_type, created_at);
CREATE INDEX IF NOT EXISTS ix_copilot_generated_artifacts_job_created ON public.copilot_generated_artifacts USING btree (job_id, created_at);

--
-- Candidate profile / job skill redesign patch
-- Keeps init.sql aligned with the newer structured profile model.
--

ALTER TABLE public.candidate_profiles
    ADD COLUMN IF NOT EXISTS experience_entries_json text,
    ADD COLUMN IF NOT EXISTS education_records_json text,
    ADD COLUMN IF NOT EXISTS certification_records_json text,
    ADD COLUMN IF NOT EXISTS language_records_json text;

ALTER TABLE public.job_skills
    ALTER COLUMN min_years_experience TYPE numeric(5,1)
    USING min_years_experience::numeric(5,1);

CREATE TABLE IF NOT EXISTS public.candidate_resumes (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    candidate_profile_id uuid NOT NULL,
    storage_key text NOT NULL,
    file_name character varying(255) NOT NULL,
    version integer NOT NULL,
    upload_date timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    is_current boolean DEFAULT false NOT NULL
);

ALTER TABLE public.candidate_resumes OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.candidate_projects (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    candidate_profile_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    role character varying(255),
    description text,
    technologies_json text,
    start_month integer NOT NULL,
    start_year integer NOT NULL,
    end_month integer,
    end_year integer,
    is_current boolean DEFAULT false NOT NULL
);

ALTER TABLE public.candidate_projects OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.candidate_profile_sections (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    candidate_profile_id uuid NOT NULL,
    section_key character varying(100),
    title character varying(255) NOT NULL,
    section_type character varying(100) DEFAULT 'Custom' NOT NULL,
    source character varying(50) DEFAULT 'User' NOT NULL,
    display_order integer DEFAULT 0 NOT NULL,
    schema_json text,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

ALTER TABLE public.candidate_profile_sections OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.candidate_profile_section_items (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    section_id uuid NOT NULL,
    item_type character varying(100) DEFAULT 'Entry' NOT NULL,
    title character varying(255) NOT NULL,
    subtitle character varying(255),
    organization character varying(255),
    location character varying(255),
    description text,
    date_label character varying(120),
    start_month integer,
    start_year integer,
    end_month integer,
    end_year integer,
    is_current boolean DEFAULT false NOT NULL,
    display_order integer DEFAULT 0 NOT NULL,
    tags_json text,
    attributes_json text,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

ALTER TABLE public.candidate_profile_section_items OWNER TO postgres;

-- Scorecard đánh giá sau phỏng vấn: mỗi buổi phỏng vấn tối đa MỘT bản đánh giá.
-- HR/Manager ghi nhận sau khi interview Completed (PUT /api/hr/interviews/{id}/evaluation).
CREATE TABLE IF NOT EXISTS public.interview_evaluations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    interview_id uuid NOT NULL,
    evaluator_id uuid,
    technical_score integer NOT NULL,
    communication_score integer NOT NULL,
    problem_solving_score integer NOT NULL,
    culture_fit_score integer NOT NULL,
    overall_score integer NOT NULL,
    recommendation character varying(50) NOT NULL,
    strengths text,
    concerns text,
    notes text,
    created_at timestamp without time zone DEFAULT now(),
    updated_at timestamp without time zone,
    CONSTRAINT interview_evaluations_pkey PRIMARY KEY (id),
    CONSTRAINT interview_evaluations_interview_id_key UNIQUE (interview_id),
    CONSTRAINT interview_evaluations_interview_id_fkey FOREIGN KEY (interview_id)
        REFERENCES public.interviews(id) ON DELETE CASCADE,
    CONSTRAINT interview_evaluations_evaluator_id_fkey FOREIGN KEY (evaluator_id)
        REFERENCES public.users(id)
);

ALTER TABLE public.interview_evaluations OWNER TO postgres;

ALTER TABLE public.candidate_skills
    ADD COLUMN IF NOT EXISTS years_of_experience numeric(5,1);

ALTER TABLE ONLY public.candidate_resumes
    ADD CONSTRAINT candidate_resumes_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.candidate_projects
    ADD CONSTRAINT candidate_projects_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.candidate_profile_sections
    ADD CONSTRAINT candidate_profile_sections_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.candidate_profile_section_items
    ADD CONSTRAINT candidate_profile_section_items_pkey PRIMARY KEY (id);

CREATE INDEX IF NOT EXISTS ix_candidate_resumes_candidate_profile_id
    ON public.candidate_resumes USING btree (candidate_profile_id, upload_date DESC);

CREATE INDEX IF NOT EXISTS ix_candidate_projects_candidate_profile_id
    ON public.candidate_projects USING btree (candidate_profile_id, start_year DESC, start_month DESC);

CREATE INDEX IF NOT EXISTS ix_candidate_skills_skill_id
    ON public.candidate_skills USING btree (skill_id);

CREATE INDEX IF NOT EXISTS ix_candidate_profile_sections_profile_order
    ON public.candidate_profile_sections USING btree (candidate_profile_id, display_order);

CREATE INDEX IF NOT EXISTS ix_candidate_profile_section_items_section_order
    ON public.candidate_profile_section_items USING btree (section_id, display_order);

ALTER TABLE ONLY public.candidate_resumes
    ADD CONSTRAINT candidate_resumes_candidate_profile_id_fkey
    FOREIGN KEY (candidate_profile_id) REFERENCES public.candidate_profiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.candidate_projects
    ADD CONSTRAINT candidate_projects_candidate_profile_id_fkey
    FOREIGN KEY (candidate_profile_id) REFERENCES public.candidate_profiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.candidate_profile_sections
    ADD CONSTRAINT candidate_profile_sections_candidate_profile_id_fkey
    FOREIGN KEY (candidate_profile_id) REFERENCES public.candidate_profiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.candidate_profile_section_items
    ADD CONSTRAINT candidate_profile_section_items_section_id_fkey
    FOREIGN KEY (section_id) REFERENCES public.candidate_profile_sections(id) ON DELETE CASCADE;

-- CV seed always empty: clear any derived resume rows (re-seed safety); the SELECT below inserts
-- nothing because every candidate_profiles.resume_url was nulled above.
DELETE FROM public.candidate_resumes;

INSERT INTO public.candidate_resumes (
    id,
    candidate_profile_id,
    storage_key,
    file_name,
    version,
    upload_date,
    is_current
)
SELECT
    gen_random_uuid(),
    cp.id,
    cp.resume_url,
    regexp_replace(split_part(cp.resume_url, '/', array_length(string_to_array(cp.resume_url, '/'), 1)), '^[^_]*_', ''),
    1,
    CURRENT_TIMESTAMP,
    true
FROM public.candidate_profiles cp
WHERE cp.resume_url IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_resumes cr
      WHERE cr.candidate_profile_id = cp.id
        AND cr.is_current = true
  );

UPDATE public.candidate_skills cs
SET years_of_experience = CASE
        WHEN cp.experience_years IS NULL THEN NULL
        ELSE cp.experience_years::numeric(5,1)
    END
FROM public.candidate_profiles cp
WHERE cp.id = cs.candidate_id
  AND cs.years_of_experience IS NULL;

DROP TABLE IF EXISTS public.candidate_skill_details;

UPDATE public.candidate_profiles cp
SET education_records_json = json_build_array(
        json_build_object(
            'id', md5(cp.id::text || '-education'),
            'school', cp.education,
            'degree', cp.current_position,
            'fieldOfStudy', NULL,
            'startYear', NULL,
            'endYear', NULL,
            'description', cp.education
        )
    )::text
WHERE cp.education IS NOT NULL
  AND cp.education_records_json IS NULL;

UPDATE public.candidate_profiles cp
SET language_records_json = json_build_array(
        json_build_object(
            'id', md5(cp.id::text || '-english'),
            'name', 'English',
            'proficiency', 'Working'
        )
    )::text
WHERE cp.language_records_json IS NULL
  AND EXISTS (
      SELECT 1
      FROM public.candidate_skills cs
      INNER JOIN public.skills s
          ON s.id = cs.skill_id
      WHERE cs.candidate_id = cp.id
        AND lower(s.name) = 'english'
  );

INSERT INTO public.candidate_profile_sections (
    id,
    candidate_profile_id,
    section_key,
    title,
    section_type,
    source,
    display_order,
    schema_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cp.id,
    'experience',
    'Experience',
    'Timeline',
    'System',
    100,
    NULL,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profiles cp
WHERE cp.experience_entries_json IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_sections cps
      WHERE cps.candidate_profile_id = cp.id
        AND cps.section_key = 'experience'
  );

INSERT INTO public.candidate_profile_sections (
    id,
    candidate_profile_id,
    section_key,
    title,
    section_type,
    source,
    display_order,
    schema_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cp.id,
    'education',
    'Education',
    'Education',
    'System',
    300,
    NULL,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profiles cp
WHERE cp.education_records_json IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_sections cps
      WHERE cps.candidate_profile_id = cp.id
        AND cps.section_key = 'education'
  );

INSERT INTO public.candidate_profile_sections (
    id,
    candidate_profile_id,
    section_key,
    title,
    section_type,
    source,
    display_order,
    schema_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cp.id,
    'certifications',
    'Certifications',
    'Achievements',
    'System',
    400,
    NULL,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profiles cp
WHERE cp.certification_records_json IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_sections cps
      WHERE cps.candidate_profile_id = cp.id
        AND cps.section_key = 'certifications'
  );

INSERT INTO public.candidate_profile_sections (
    id,
    candidate_profile_id,
    section_key,
    title,
    section_type,
    source,
    display_order,
    schema_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cp.id,
    'languages',
    'Languages',
    'Attributes',
    'System',
    500,
    NULL,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profiles cp
WHERE cp.language_records_json IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_sections cps
      WHERE cps.candidate_profile_id = cp.id
        AND cps.section_key = 'languages'
  );

INSERT INTO public.candidate_profile_sections (
    id,
    candidate_profile_id,
    section_key,
    title,
    section_type,
    source,
    display_order,
    schema_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cp.id,
    'projects',
    'Projects',
    'Portfolio',
    'System',
    200,
    NULL,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profiles cp
WHERE EXISTS (
        SELECT 1
        FROM public.candidate_projects cpr
        WHERE cpr.candidate_profile_id = cp.id
    )
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_sections cps
      WHERE cps.candidate_profile_id = cp.id
        AND cps.section_key = 'projects'
  );

INSERT INTO public.candidate_profile_section_items (
    id,
    section_id,
    item_type,
    title,
    organization,
    description,
    start_month,
    start_year,
    end_month,
    end_year,
    is_current,
    display_order,
    tags_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cps.id,
    'Project',
    cpr.name,
    cpr.role,
    cpr.description,
    cpr.start_month,
    cpr.start_year,
    cpr.end_month,
    cpr.end_year,
    cpr.is_current,
    ROW_NUMBER() OVER (PARTITION BY cps.id ORDER BY cpr.start_year DESC, cpr.start_month DESC) - 1,
    cpr.technologies_json,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profile_sections cps
INNER JOIN public.candidate_projects cpr
    ON cpr.candidate_profile_id = cps.candidate_profile_id
WHERE cps.section_key = 'projects'
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_section_items cpsi
      WHERE cpsi.section_id = cps.id
  );

INSERT INTO public.candidate_profile_section_items (
    id,
    section_id,
    item_type,
    title,
    organization,
    description,
    start_month,
    start_year,
    end_month,
    end_year,
    is_current,
    display_order,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cps.id,
    'Experience',
    COALESCE(item.value->>'title', 'Experience'),
    item.value->>'company',
    NULLIF(array_to_string(ARRAY(
        SELECT jsonb_array_elements_text(COALESCE(item.value->'bullets', '[]'::jsonb))
    ), E'\n'), ''),
    NULLIF(item.value->'period'->>'startMonth', '')::integer,
    NULLIF(item.value->'period'->>'startYear', '')::integer,
    NULLIF(item.value->'period'->>'endMonth', '')::integer,
    NULLIF(item.value->'period'->>'endYear', '')::integer,
    COALESCE((item.value->'period'->>'isCurrent')::boolean, false),
    item.ordinality - 1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profile_sections cps
INNER JOIN public.candidate_profiles cp
    ON cp.id = cps.candidate_profile_id
CROSS JOIN LATERAL jsonb_array_elements(COALESCE(cp.experience_entries_json::jsonb, '[]'::jsonb)) WITH ORDINALITY AS item(value, ordinality)
WHERE cps.section_key = 'experience'
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_section_items cpsi
      WHERE cpsi.section_id = cps.id
  );

INSERT INTO public.candidate_profile_section_items (
    id,
    section_id,
    item_type,
    title,
    subtitle,
    description,
    start_year,
    end_year,
    display_order,
    attributes_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cps.id,
    'Education',
    COALESCE(item.value->>'school', 'Education'),
    item.value->>'degree',
    item.value->>'description',
    NULLIF(item.value->>'startYear', '')::integer,
    NULLIF(item.value->>'endYear', '')::integer,
    item.ordinality - 1,
    json_build_object(
        'fieldOfStudy', COALESCE(item.value->>'fieldOfStudy', '')
    )::text,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profile_sections cps
INNER JOIN public.candidate_profiles cp
    ON cp.id = cps.candidate_profile_id
CROSS JOIN LATERAL jsonb_array_elements(COALESCE(cp.education_records_json::jsonb, '[]'::jsonb)) WITH ORDINALITY AS item(value, ordinality)
WHERE cps.section_key = 'education'
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_section_items cpsi
      WHERE cpsi.section_id = cps.id
  );

INSERT INTO public.candidate_profile_section_items (
    id,
    section_id,
    item_type,
    title,
    organization,
    date_label,
    display_order,
    attributes_json,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cps.id,
    'Certification',
    COALESCE(item.value->>'name', 'Certification'),
    item.value->>'issuer',
    item.value->>'issuedOn',
    item.ordinality - 1,
    json_build_object(
        'expiresOn', COALESCE(item.value->>'expiresOn', ''),
        'credentialId', COALESCE(item.value->>'credentialId', ''),
        'credentialUrl', COALESCE(item.value->>'credentialUrl', '')
    )::text,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profile_sections cps
INNER JOIN public.candidate_profiles cp
    ON cp.id = cps.candidate_profile_id
CROSS JOIN LATERAL jsonb_array_elements(COALESCE(cp.certification_records_json::jsonb, '[]'::jsonb)) WITH ORDINALITY AS item(value, ordinality)
WHERE cps.section_key = 'certifications'
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_section_items cpsi
      WHERE cpsi.section_id = cps.id
  );

INSERT INTO public.candidate_profile_section_items (
    id,
    section_id,
    item_type,
    title,
    subtitle,
    display_order,
    created_at,
    updated_at
)
SELECT
    gen_random_uuid(),
    cps.id,
    'Language',
    COALESCE(item.value->>'name', 'Language'),
    item.value->>'proficiency',
    item.ordinality - 1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM public.candidate_profile_sections cps
INNER JOIN public.candidate_profiles cp
    ON cp.id = cps.candidate_profile_id
CROSS JOIN LATERAL jsonb_array_elements(COALESCE(cp.language_records_json::jsonb, '[]'::jsonb)) WITH ORDINALITY AS item(value, ordinality)
WHERE cps.section_key = 'languages'
  AND NOT EXISTS (
      SELECT 1
      FROM public.candidate_profile_section_items cpsi
      WHERE cpsi.section_id = cps.id
  );

--
-- Notification module phase 3 patch
-- Keeps init.sql aligned with richer notification metadata and settings.
--

ALTER TABLE public.notifications
    ADD COLUMN IF NOT EXISTS body text,
    ADD COLUMN IF NOT EXISTS event_code character varying(100),
    ADD COLUMN IF NOT EXISTS data_json jsonb,
    ADD COLUMN IF NOT EXISTS entity_type character varying(100),
    ADD COLUMN IF NOT EXISTS entity_id uuid;

UPDATE public.notifications
SET body = COALESCE(body, content)
WHERE body IS NULL;

CREATE TABLE IF NOT EXISTS public.notification_events (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code character varying(100) NOT NULL,
    module_code character varying(100) NOT NULL,
    name character varying(255) NOT NULL,
    description text,
    title_template character varying(255) NOT NULL,
    body_template text,
    default_in_app_enabled boolean DEFAULT true NOT NULL,
    default_email_enabled boolean DEFAULT false NOT NULL,
    is_required boolean DEFAULT false NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL
);

ALTER TABLE public.notification_events OWNER TO postgres;

CREATE TABLE IF NOT EXISTS public.user_notification_settings (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    event_code character varying(100) NOT NULL,
    in_app_enabled boolean DEFAULT true NOT NULL,
    email_enabled boolean DEFAULT false NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);

ALTER TABLE public.user_notification_settings OWNER TO postgres;

ALTER TABLE ONLY public.notification_events
    ADD CONSTRAINT notification_events_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.notification_events
    ADD CONSTRAINT notification_events_code_key UNIQUE (code);

ALTER TABLE ONLY public.user_notification_settings
    ADD CONSTRAINT user_notification_settings_pkey PRIMARY KEY (id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_notification_settings_user_event
    ON public.user_notification_settings USING btree (user_id, event_code);

CREATE INDEX IF NOT EXISTS ix_notifications_user_created_at
    ON public.notifications USING btree (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_notifications_event_code
    ON public.notifications USING btree (event_code);

ALTER TABLE ONLY public.user_notification_settings
    ADD CONSTRAINT user_notification_settings_user_id_fkey
    FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.user_notification_settings
    ADD CONSTRAINT user_notification_settings_event_code_fkey
    FOREIGN KEY (event_code) REFERENCES public.notification_events(code) ON DELETE CASCADE;

INSERT INTO public.notification_events (
    id,
    code,
    module_code,
    name,
    description,
    title_template,
    body_template,
    default_in_app_enabled,
    default_email_enabled,
    is_required,
    sort_order
)
SELECT *
FROM (
    VALUES
        ('11111111-1111-4111-8111-111111111111'::uuid, 'new_application_received', 'application', 'New application received', 'Candidate submitted an application', 'Ứng viên mới ứng tuyển vào {jobTitle}', '{candidateName} vừa ứng tuyển vào vị trí {jobTitle}.', true, false, true, 10),
        ('22222222-2222-4222-8222-222222222222'::uuid, 'application_status_changed', 'application', 'Application status changed', 'Application status updated by recruiter or manager', 'Trạng thái hồ sơ đã được cập nhật', 'Hồ sơ của bạn cho vị trí {jobTitle} đã chuyển sang trạng thái {newStatus}.', true, false, true, 20),
        ('33333333-3333-4333-8333-333333333333'::uuid, 'interview_scheduled', 'interview', 'Interview scheduled', 'Interview was scheduled for a candidate', 'Bạn có lịch phỏng vấn mới', 'Lịch phỏng vấn cho vị trí {jobTitle} được đặt vào {scheduledAt}.', true, false, true, 30),
        ('44444444-4444-4444-8444-444444444444'::uuid, 'candidate_score_ready', 'candidate', 'Candidate score ready', 'AI scoring finished for a candidate', 'Điểm đánh giá ứng viên đã sẵn sàng', 'Hệ thống đã hoàn tất đánh giá ứng viên {candidateName} cho vị trí {jobTitle}.', true, false, true, 40)
) AS seed_data (
    id,
    code,
    module_code,
    name,
    description,
    title_template,
    body_template,
    default_in_app_enabled,
    default_email_enabled,
    is_required,
    sort_order
)
WHERE NOT EXISTS (
    SELECT 1
    FROM public.notification_events existing
    WHERE existing.code = seed_data.code
);

INSERT INTO public.roles (id, name, description)
SELECT
    '7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid,
    'HeadDepartment',
    'Head department role'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.roles
    WHERE name = 'HeadDepartment'
);

INSERT INTO public.role_permissions (role_id, permission_id, assigned_at)
SELECT role_id, permission_id, assigned_at
FROM (
    VALUES
        ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid, '7d33a32f-737a-4d94-9271-3ce4bba57659'::uuid, '2026-06-23 09:00:00+07'::timestamp with time zone),
        ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid, '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87'::uuid, '2026-06-23 09:00:00+07'::timestamp with time zone),
        ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid, 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91'::uuid, '2026-06-23 09:00:00+07'::timestamp with time zone),
        ('7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid, '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa'::uuid, '2026-06-23 09:00:00+07'::timestamp with time zone)
) AS seed_data(role_id, permission_id, assigned_at)
WHERE NOT EXISTS (
    SELECT 1
    FROM public.role_permissions existing
    WHERE existing.role_id = seed_data.role_id
      AND existing.permission_id = seed_data.permission_id
);

INSERT INTO public.user_roles (user_id, role_id, assigned_at)
SELECT
    '721b1851-349a-48aa-acae-feed1c1843ed'::uuid,
    '7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid,
    '2026-06-23 09:00:00'::timestamp without time zone
WHERE EXISTS (
    SELECT 1
    FROM public.users
    WHERE id = '721b1851-349a-48aa-acae-feed1c1843ed'::uuid
)
AND NOT EXISTS (
    SELECT 1
    FROM public.user_roles
    WHERE user_id = '721b1851-349a-48aa-acae-feed1c1843ed'::uuid
      AND role_id = '7a5b2c6d-1e2f-4a3b-9c8d-112233445566'::uuid
);

-- ============================================================================
-- Phase 1 — Department Head ownership foundation
-- See docs/source-of-truth/RECRUITMENT-OWNERSHIP-MATRIX.md, JOB-APPROVAL-FLOW.md,
--     APPLICATION-OWNERSHIP-FLOW.md, IMPLEMENTATION-PLAN-OWNERSHIP.md.
--
-- Demo persona mapping:
--   Candidate      = Phùng Nhật Quang    (4071353e-5816-4746-a8b6-c0bc3113c44d)
--   HR / Recruiter = Nguyễn Thục Uyên    (e781ccd9-e6f8-4ce1-b15d-e142f8977a4e)
--   DepartmentHead = Trần Trọng Tiến Đạt (721b1851-349a-48aa-acae-feed1c1843ed)
--
-- Ownership model:
--   departments.head_user_id                  = default approver + business reviewer (BR-OWN-001/003).
--   jobs.recruiter_id                         = business owner of the job's applications; created_by is
--                                               an audit field and the legacy fallback (BR-OWN-002).
--   applications.assigned_recruiter_id        = jobs.recruiter_id ?? jobs.created_by (snapshot, BR-OWN-005).
--   applications.assigned_department_head_id  = departments.head_user_id ?? jobs.approved_by (snapshot).
-- This block is idempotent and safe to re-run.
-- ============================================================================

-- 1. Columns (nullable during the migration window)
ALTER TABLE public.departments  ADD COLUMN IF NOT EXISTS head_user_id uuid;
ALTER TABLE public.jobs         ADD COLUMN IF NOT EXISTS recruiter_id uuid;
ALTER TABLE public.applications ADD COLUMN IF NOT EXISTS assigned_recruiter_id uuid;
ALTER TABLE public.applications ADD COLUMN IF NOT EXISTS assigned_department_head_id uuid;

-- 2. Foreign keys to users — RESTRICT/NO ACTION (matching jobs_created_by_fkey etc.); deleting a user
--    must never cascade-delete a department/job/application.
-- Idempotent without a PL/pgSQL DO block (see note above): DROP CONSTRAINT IF EXISTS + ADD CONSTRAINT.
ALTER TABLE ONLY public.departments DROP CONSTRAINT IF EXISTS departments_head_user_id_fkey;
ALTER TABLE ONLY public.departments ADD CONSTRAINT departments_head_user_id_fkey FOREIGN KEY (head_user_id) REFERENCES public.users(id);
ALTER TABLE ONLY public.jobs DROP CONSTRAINT IF EXISTS jobs_recruiter_id_fkey;
ALTER TABLE ONLY public.jobs ADD CONSTRAINT jobs_recruiter_id_fkey FOREIGN KEY (recruiter_id) REFERENCES public.users(id);
ALTER TABLE ONLY public.applications DROP CONSTRAINT IF EXISTS applications_assigned_recruiter_id_fkey;
ALTER TABLE ONLY public.applications ADD CONSTRAINT applications_assigned_recruiter_id_fkey FOREIGN KEY (assigned_recruiter_id) REFERENCES public.users(id);
ALTER TABLE ONLY public.applications DROP CONSTRAINT IF EXISTS applications_assigned_department_head_id_fkey;
ALTER TABLE ONLY public.applications ADD CONSTRAINT applications_assigned_department_head_id_fkey FOREIGN KEY (assigned_department_head_id) REFERENCES public.users(id);

-- 3. Indexes
CREATE INDEX IF NOT EXISTS ix_departments_head_user_id
    ON public.departments USING btree (head_user_id);
CREATE INDEX IF NOT EXISTS ix_jobs_recruiter_id
    ON public.jobs USING btree (recruiter_id);
CREATE INDEX IF NOT EXISTS ix_applications_assigned_recruiter_id
    ON public.applications USING btree (assigned_recruiter_id);
CREATE INDEX IF NOT EXISTS ix_applications_assigned_department_head_id
    ON public.applications USING btree (assigned_department_head_id);

-- 4. Seed normalization — Department heads.
--    Demo simplification: Trần Trọng Tiến Đạt heads all seeded departments — he is the sole
--    DepartmentHead persona and already approves the main demo jobs (Engineering dept fafe312f...).
--    In production each department would have its own head.
UPDATE public.departments
SET head_user_id = '721b1851-349a-48aa-acae-feed1c1843ed'::uuid
WHERE head_user_id IS NULL;

-- 5. Backfill — Job recruiter from the audit created_by field (legacy fallback owner).
--    The main demo jobs are created_by = Nguyễn Thục Uyên (HR), so recruiter_id resolves to her.
UPDATE public.jobs
SET recruiter_id = created_by
WHERE recruiter_id IS NULL
  AND created_by IS NOT NULL;

-- 6. Backfill — Application ownership snapshot (BR-OWN-005).
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

-- ============================================================================
-- INV-014 — at most one ACTIVE application per (candidate, job).
-- DB-level defense-in-depth behind the service check (INV-003). Closed rows
-- (Hired, Rejected, OfferDeclined, Withdrawn) are history and excluded from the
-- filter so re-apply stays possible. Mirrors AppDbContext (ux_applications_active_user_job)
-- and migration 20260626023451_AddActiveApplicationUniqueIndex exactly. Created
-- after the seed above (which has zero duplicate active rows). Idempotent.
-- ============================================================================
CREATE UNIQUE INDEX IF NOT EXISTS ux_applications_active_user_job
ON public.applications (user_id, job_id)
WHERE status IN ('Applied', 'Screening', 'ManagerReview', 'Interview', 'Offer');

-- ============================================================================
-- Workflow correctness — DepartmentHead review hand-off timestamp.
-- Set when HR sends an application from Screening to ManagerReview (Head Review).
-- The DepartmentHead/Manager review queue shows THIS date as "received for review",
-- not applied_at. Mirrors AppDbContext (department_head_review_requested_at) and
-- migration 20260628000000_AddDepartmentHeadReviewRequestedAt. Idempotent.
-- ============================================================================
ALTER TABLE public.applications
    ADD COLUMN IF NOT EXISTS department_head_review_requested_at timestamp without time zone NULL;

-- Backfill — legacy rows already in ManagerReview have no recorded hand-off date; fall back to
-- applied_at so the review queue shows a sensible date instead of "no date" for existing demo data.
UPDATE public.applications
SET department_head_review_requested_at = applied_at
WHERE status = 'ManagerReview'
  AND department_head_review_requested_at IS NULL;

-- ============================================================================
-- v4 Workflow Automation + MCP (see db/patches/20260701-add-v4-*.sql). Idempotent so this file stays
-- safe to re-run on an existing DB. NO EF migrations are used in this project.
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.published_domain_events (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
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
CREATE UNIQUE INDEX IF NOT EXISTS ux_published_domain_events_dedup_key ON public.published_domain_events USING btree (dedup_key);
CREATE INDEX IF NOT EXISTS ix_published_domain_events_status_next_attempt ON public.published_domain_events USING btree (status, next_attempt_at);
CREATE INDEX IF NOT EXISTS ix_published_domain_events_event_type_occurred_at ON public.published_domain_events USING btree (event_type, occurred_at);

CREATE TABLE IF NOT EXISTS public.workflow_definitions (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    name character varying(200) NOT NULL,
    description text,
    is_enabled boolean DEFAULT true NOT NULL,
    active_version_id uuid,
    created_by uuid,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.workflow_definition_versions (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    workflow_definition_id uuid NOT NULL REFERENCES public.workflow_definitions(id) ON DELETE CASCADE,
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
CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_definition_versions_workflow_version ON public.workflow_definition_versions USING btree (workflow_definition_id, version_no);
ALTER TABLE ONLY public.workflow_definitions DROP CONSTRAINT IF EXISTS workflow_definitions_active_version_id_fkey;
ALTER TABLE ONLY public.workflow_definitions ADD CONSTRAINT workflow_definitions_active_version_id_fkey FOREIGN KEY (active_version_id) REFERENCES public.workflow_definition_versions(id);

CREATE TABLE IF NOT EXISTS public.workflow_executions (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
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
CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_executions_version_event ON public.workflow_executions USING btree (workflow_definition_version_id, event_dedup_key) WHERE event_dedup_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_workflow_executions_status_next_retry ON public.workflow_executions USING btree (status, next_retry_at);
CREATE INDEX IF NOT EXISTS ix_workflow_executions_definition_created ON public.workflow_executions USING btree (workflow_definition_id, created_at);

CREATE TABLE IF NOT EXISTS public.workflow_execution_steps (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    execution_id uuid NOT NULL REFERENCES public.workflow_executions(id) ON DELETE CASCADE,
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
CREATE INDEX IF NOT EXISTS ix_workflow_execution_steps_execution_step ON public.workflow_execution_steps USING btree (execution_id, step_no);

CREATE TABLE IF NOT EXISTS public.workflow_action_dead_letters (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    execution_id uuid NOT NULL REFERENCES public.workflow_executions(id) ON DELETE CASCADE,
    step_id uuid,
    action_type character varying(100) NOT NULL,
    payload_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    error_reason text NOT NULL,
    attempt_count integer DEFAULT 0 NOT NULL,
    next_retry_at timestamp without time zone,
    resolved_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_workflow_action_dead_letters_execution_id ON public.workflow_action_dead_letters USING btree (execution_id);

CREATE TABLE IF NOT EXISTS public.workflow_worker_heartbeats (
    worker_name character varying(100) NOT NULL PRIMARY KEY,
    last_beat_at timestamp without time zone NOT NULL,
    status character varying(50) DEFAULT 'Running' NOT NULL,
    detail text,
    updated_at timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.mcp_tool_audits (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    tool_name character varying(150) NOT NULL,
    caller_user_id uuid,
    input_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    output_summary_json jsonb,
    allowed boolean NOT NULL,
    denied_reason text,
    latency_ms integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_mcp_tool_audits_tool_created ON public.mcp_tool_audits USING btree (tool_name, created_at);

-- =============================================================================
-- v5 AI Ops + Talent Intelligence (2026-07-04). Kept in sync with
-- db/patches/20260704-add-v5-ai-ops.sql and AppDbContext.Ai.cs.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.ai_run_telemetry (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
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
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_created_at ON public.ai_run_telemetry USING btree (created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_feature_created_at ON public.ai_run_telemetry USING btree (feature, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_provider_model_created_at ON public.ai_run_telemetry USING btree (provider_name, model_name, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_success_created_at ON public.ai_run_telemetry USING btree (success, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_run_telemetry_correlation_id ON public.ai_run_telemetry USING btree (correlation_id);

CREATE TABLE IF NOT EXISTS public.prompt_template_versions (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
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
CREATE UNIQUE INDEX IF NOT EXISTS ux_prompt_template_versions_feature_version ON public.prompt_template_versions USING btree (feature_key, version_no);
CREATE UNIQUE INDEX IF NOT EXISTS ux_prompt_template_versions_active_feature ON public.prompt_template_versions USING btree (feature_key) WHERE is_active;

CREATE TABLE IF NOT EXISTS public.provider_routing_policies (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
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
CREATE UNIQUE INDEX IF NOT EXISTS ux_provider_routing_policies_feature ON public.provider_routing_policies USING btree (feature_key);

CREATE TABLE IF NOT EXISTS public.ai_evaluation_cases (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    feature_key character varying(100) NOT NULL,
    name character varying(200) NOT NULL,
    input_json jsonb DEFAULT '{}'::jsonb NOT NULL,
    expected_json jsonb,
    scoring_rubric_json jsonb,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_ai_evaluation_cases_feature_active ON public.ai_evaluation_cases USING btree (feature_key, is_active);

CREATE TABLE IF NOT EXISTS public.ai_evaluation_results (
    id uuid DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    run_id uuid NOT NULL,
    case_id uuid NOT NULL REFERENCES public.ai_evaluation_cases(id) ON DELETE CASCADE,
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
CREATE INDEX IF NOT EXISTS idx_ai_evaluation_results_run_created ON public.ai_evaluation_results USING btree (run_id, created_at);
CREATE INDEX IF NOT EXISTS idx_ai_evaluation_results_case ON public.ai_evaluation_results USING btree (case_id);


-- ============================================================================
-- DEMO SEED — v4 Workflow Automation (base date: 2026-07-04)
-- ============================================================================
-- Đặt CUỐI FILE vì các bảng workflow_* được tạo ở phần trên. Idempotent (UPSERT
-- theo ID cố định). 4 workflow đầu trùng TÊN với WorkflowTemplateSeeder (C#)
-- nên seeder bỏ qua khi khởi động — ID ổn định thuộc về init.sql.
-- Trạng thái demo: 3 workflow active (2 Live + 1 Shadow), 1 disabled (inactive),
-- 1 draft chưa publish; executions có đủ Success/Failed/Skipped + 1 dead letter.
-- Giữ đồng bộ với db/patches/20260704-refresh-demo-seed.sql.
-- ============================================================================

INSERT INTO public.workflow_definitions (id, name, description, is_enabled, active_version_id, created_by, created_at, updated_at) VALUES
    ('f1000000-0000-4000-8000-000000000001', 'Pass CV → Notify Head Review', 'Khi HR chuyển hồ sơ sang bước Trưởng bộ phận duyệt, thông báo cho Trưởng bộ phận phụ trách.', true, NULL, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:00:00', '2026-07-01 09:00:00'),
    ('f1000000-0000-4000-8000-000000000002', 'Head Review Overdue Reminder', 'Nhắc Trưởng bộ phận khi hồ sơ ở bước duyệt quá hạn (mặc định 3 ngày), có cooldown 24 giờ.', true, NULL, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:05:00', '2026-06-15 09:05:00'),
    ('f1000000-0000-4000-8000-000000000003', 'High-fit Candidate Alert', 'Khi ứng viên nộp hồ sơ và điểm phù hợp cao (>= 80), báo cho HR/recruiter phụ trách.', true, NULL, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:10:00', '2026-07-02 10:00:00'),
    ('f1000000-0000-4000-8000-000000000004', 'Interview Completed Follow-up', 'Khi phỏng vấn hoàn tất, gửi gợi ý bước tiếp theo (theo luật, không cần AI) cho HR/Manager.', false, NULL, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:15:00', '2026-07-03 08:00:00'),
    ('f1000000-0000-4000-8000-000000000005', 'Candidate Score Ready Digest', 'BẢN NHÁP: tổng hợp điểm AI của ứng viên mới cho HR mỗi khi chấm điểm xong. Chưa publish phiên bản nào.', true, NULL, 'a0000000-0000-4000-8000-000000000001', '2026-07-03 10:00:00', '2026-07-03 10:00:00')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    is_enabled = EXCLUDED.is_enabled,
    created_at = EXCLUDED.created_at,
    updated_at = EXCLUDED.updated_at;

INSERT INTO public.workflow_definition_versions (id, workflow_definition_id, version_no, trigger_json, conditions_json, actions_json, mode, is_active, published_by, published_at, created_at) VALUES
    ('f2000000-0000-4000-8000-000000000001', 'f1000000-0000-4000-8000-000000000001', 1,
     '{"eventType":"PassedToHeadReview"}',
     '[{"field":"departmentHeadId","operator":"exists","value":null}]',
     '[{"type":"notify_user","config":{"recipients":["assignedDepartmentHead"],"eventCode":"workflow_head_review","title":"Hồ sơ chờ bạn duyệt","body":"Có hồ sơ mới cần Trưởng bộ phận xem xét."}}]',
     'Live', true, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:00:00', '2026-06-15 09:00:00'),
    ('f2000000-0000-4000-8000-000000000002', 'f1000000-0000-4000-8000-000000000002', 1,
     '{"eventType":"HeadReviewOverdue"}',
     '[{"field":"overdueDays","operator":"greater_than_or_equal","value":"3"}]',
     '[{"type":"send_reminder","config":{"recipients":["assignedDepartmentHead"],"cooldownHours":24,"eventCode":"workflow_head_review_overdue","title":"Nhắc duyệt hồ sơ","body":"Có hồ sơ đang chờ bạn duyệt quá hạn."}}]',
     'Shadow', true, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:05:00', '2026-06-15 09:05:00'),
    ('f2000000-0000-4000-8000-000000000003', 'f1000000-0000-4000-8000-000000000003', 1,
     '{"eventType":"CandidateApplied"}',
     '[{"field":"finalScore","operator":"greater_than_or_equal","value":"80"}]',
     '[{"type":"notify_user","config":{"recipients":["assignedRecruiter"],"eventCode":"workflow_high_fit","title":"Ứng viên tiềm năng","body":"Có ứng viên điểm phù hợp cao vừa ứng tuyển."}}]',
     'Live', true, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:10:00', '2026-06-15 09:10:00'),
    ('f2000000-0000-4000-8000-000000000004', 'f1000000-0000-4000-8000-000000000004', 1,
     '{"eventType":"InterviewCompleted"}',
     '[]',
     '[{"type":"rule_based_next_step_suggestion","config":{"scoreField":"finalScore","recipients":["assignedRecruiter","assignedDepartmentHead"],"eventCode":"workflow_interview_followup","title":"Gợi ý sau phỏng vấn"}}]',
     'Shadow', true, 'a0000000-0000-4000-8000-000000000001', '2026-06-15 09:15:00', '2026-06-15 09:15:00'),
    ('f2000000-0000-4000-8000-000000000005', 'f1000000-0000-4000-8000-000000000005', 1,
     '{"eventType":"CandidateScoreReady"}',
     '[{"field":"finalScore","operator":"greater_than_or_equal","value":"70"}]',
     '[{"type":"notify_role","config":{"roles":["HR"],"eventCode":"workflow_score_digest","title":"Điểm ứng viên đã sẵn sàng","body":"Có ứng viên mới vừa được chấm điểm."}}]',
     'Shadow', false, NULL, NULL, '2026-07-03 10:00:00')
ON CONFLICT (id) DO UPDATE SET
    trigger_json = EXCLUDED.trigger_json,
    conditions_json = EXCLUDED.conditions_json,
    actions_json = EXCLUDED.actions_json,
    mode = EXCLUDED.mode,
    is_active = EXCLUDED.is_active,
    published_at = EXCLUDED.published_at,
    created_at = EXCLUDED.created_at;

-- Nối active_version_id sau khi cả hai bảng đã có dữ liệu (FK vòng).
UPDATE public.workflow_definitions SET active_version_id = 'f2000000-0000-4000-8000-000000000001' WHERE id = 'f1000000-0000-4000-8000-000000000001';
UPDATE public.workflow_definitions SET active_version_id = 'f2000000-0000-4000-8000-000000000002' WHERE id = 'f1000000-0000-4000-8000-000000000002';
UPDATE public.workflow_definitions SET active_version_id = 'f2000000-0000-4000-8000-000000000003' WHERE id = 'f1000000-0000-4000-8000-000000000003';
UPDATE public.workflow_definitions SET active_version_id = 'f2000000-0000-4000-8000-000000000004' WHERE id = 'f1000000-0000-4000-8000-000000000004';
-- f1...0005 giữ active_version_id = NULL (bản nháp chưa publish).

-- ---------------------------------------------------------------------------
-- Outbox events (lịch sử — Processed/Failed; không có Pending để dispatcher không chạy lại)
-- ---------------------------------------------------------------------------
INSERT INTO public.published_domain_events (id, event_type, aggregate_type, aggregate_id, dedup_key, payload_json, status, occurred_at, processed_at, next_attempt_at, attempt_count, error_reason, created_at) VALUES
    ('e0000000-0000-4000-8000-000000000001', 'CandidateApplied', 'Application', '41000000-0000-4000-8000-000000000001', 'CandidateApplied:41000000-0000-4000-8000-000000000001', '{"applicationId":"41000000-0000-4000-8000-000000000001","jobTitle":"QA Automation Engineer","finalScore":85.8}', 'Processed', '2026-07-01 10:46:00', '2026-07-01 10:46:05', NULL, 1, NULL, '2026-07-01 10:46:00'),
    ('e0000000-0000-4000-8000-000000000002', 'PassedToHeadReview', 'Application', '40000000-0000-4000-8000-000000000011', 'PassedToHeadReview:40000000-0000-4000-8000-000000000011', '{"applicationId":"40000000-0000-4000-8000-000000000011","jobTitle":"Java Backend Developer","departmentHeadId":"721b1851-349a-48aa-acae-feed1c1843ed"}', 'Processed', '2026-07-02 16:58:00', '2026-07-02 16:58:04', NULL, 1, NULL, '2026-07-02 16:58:00'),
    ('e0000000-0000-4000-8000-000000000003', 'CandidateApplied', 'Application', '40000000-0000-4000-8000-000000000008', 'CandidateApplied:40000000-0000-4000-8000-000000000008', '{"applicationId":"40000000-0000-4000-8000-000000000008","jobTitle":"Data Analyst","finalScore":null}', 'Processed', '2026-07-04 08:26:00', '2026-07-04 08:26:03', NULL, 1, NULL, '2026-07-04 08:26:00'),
    ('e0000000-0000-4000-8000-000000000004', 'PassedToHeadReview', 'Application', 'a1111111-1111-4111-8111-111111111112', 'PassedToHeadReview:a1111111-1111-4111-8111-111111111112', '{"applicationId":"a1111111-1111-4111-8111-111111111112","jobTitle":"Full Stack Engineer","departmentHeadId":null}', 'Processed', '2026-07-03 09:11:00', '2026-07-03 09:12:10', NULL, 2, NULL, '2026-07-03 09:11:00'),
    ('e0000000-0000-4000-8000-000000000005', 'InterviewCompleted', 'Interview', '50000000-0000-4000-8000-000000000008', 'InterviewCompleted:50000000-0000-4000-8000-000000000008', '{"interviewId":"50000000-0000-4000-8000-000000000008","applicationId":"40000000-0000-4000-8000-000000000012","finalScore":89.4}', 'Processed', '2026-06-30 11:30:00', '2026-06-30 11:30:06', NULL, 1, NULL, '2026-06-30 11:30:00'),
    ('e0000000-0000-4000-8000-000000000006', 'HeadReviewOverdue', 'Application', '40000000-0000-4000-8000-000000000011', 'HeadReviewOverdue:40000000-0000-4000-8000-000000000011:20260704', '{"applicationId":"40000000-0000-4000-8000-000000000011","overdueDays":2}', 'Processed', '2026-07-04 07:00:00', '2026-07-04 07:00:05', NULL, 1, NULL, '2026-07-04 07:00:00'),
    ('e0000000-0000-4000-8000-000000000007', 'CandidateApplied', 'Application', '41000000-0000-4000-8000-000000000004', 'CandidateApplied:41000000-0000-4000-8000-000000000004', '{"applicationId":"41000000-0000-4000-8000-000000000004","jobTitle":"QA Automation Engineer","finalScore":null}', 'Failed', '2026-07-04 09:06:00', NULL, NULL, 3, 'Notification channel timeout after 3 attempts.', '2026-07-04 09:06:00')
ON CONFLICT (id) DO UPDATE SET
    status = EXCLUDED.status,
    occurred_at = EXCLUDED.occurred_at,
    processed_at = EXCLUDED.processed_at,
    attempt_count = EXCLUDED.attempt_count,
    error_reason = EXCLUDED.error_reason,
    created_at = EXCLUDED.created_at;

-- ---------------------------------------------------------------------------
-- Executions — Success / Failed / Skipped gần mốc 2026-07-04 (terminal, không bị retry worker nhặt lại)
-- ---------------------------------------------------------------------------
INSERT INTO public.workflow_executions (id, workflow_definition_id, workflow_definition_version_id, event_id, event_dedup_key, status, mode, trigger_event_type, input_payload_json, output_json, error_reason, attempt_count, next_retry_at, started_at, finished_at, created_at) VALUES
    ('e1000000-0000-4000-8000-000000000001', 'f1000000-0000-4000-8000-000000000003', 'f2000000-0000-4000-8000-000000000003', 'e0000000-0000-4000-8000-000000000001', 'CandidateApplied:41000000-0000-4000-8000-000000000001', 'Success', 'Live', 'CandidateApplied', '{"applicationId":"41000000-0000-4000-8000-000000000001","jobTitle":"QA Automation Engineer","finalScore":85.8}', '{"notified":["e781ccd9-e6f8-4ce1-b15d-e142f8977a4e"],"eventCode":"workflow_high_fit"}', NULL, 1, NULL, '2026-07-01 10:46:05', '2026-07-01 10:46:06', '2026-07-01 10:46:05'),
    ('e1000000-0000-4000-8000-000000000002', 'f1000000-0000-4000-8000-000000000001', 'f2000000-0000-4000-8000-000000000001', 'e0000000-0000-4000-8000-000000000002', 'PassedToHeadReview:40000000-0000-4000-8000-000000000011', 'Success', 'Live', 'PassedToHeadReview', '{"applicationId":"40000000-0000-4000-8000-000000000011","jobTitle":"Java Backend Developer","departmentHeadId":"721b1851-349a-48aa-acae-feed1c1843ed"}', '{"notified":["721b1851-349a-48aa-acae-feed1c1843ed"],"eventCode":"workflow_head_review"}', NULL, 1, NULL, '2026-07-02 16:58:04', '2026-07-02 16:58:05', '2026-07-02 16:58:04'),
    ('e1000000-0000-4000-8000-000000000003', 'f1000000-0000-4000-8000-000000000003', 'f2000000-0000-4000-8000-000000000003', 'e0000000-0000-4000-8000-000000000003', 'CandidateApplied:40000000-0000-4000-8000-000000000008', 'Skipped', 'Live', 'CandidateApplied', '{"applicationId":"40000000-0000-4000-8000-000000000008","jobTitle":"Data Analyst","finalScore":null}', '{"reason":"Condition finalScore >= 80 not met (score missing)."}', NULL, 1, NULL, '2026-07-04 08:26:03', '2026-07-04 08:26:03', '2026-07-04 08:26:03'),
    ('e1000000-0000-4000-8000-000000000004', 'f1000000-0000-4000-8000-000000000001', 'f2000000-0000-4000-8000-000000000001', 'e0000000-0000-4000-8000-000000000004', 'PassedToHeadReview:a1111111-1111-4111-8111-111111111112', 'Failed', 'Live', 'PassedToHeadReview', '{"applicationId":"a1111111-1111-4111-8111-111111111112","jobTitle":"Full Stack Engineer","departmentHeadId":null}', NULL, 'Recipient resolution failed: assignedDepartmentHead is empty for this application.', 3, NULL, '2026-07-03 09:12:00', '2026-07-03 09:12:08', '2026-07-03 09:11:30'),
    ('e1000000-0000-4000-8000-000000000005', 'f1000000-0000-4000-8000-000000000004', 'f2000000-0000-4000-8000-000000000004', 'e0000000-0000-4000-8000-000000000005', 'InterviewCompleted:50000000-0000-4000-8000-000000000008', 'Success', 'Shadow', 'InterviewCompleted', '{"interviewId":"50000000-0000-4000-8000-000000000008","applicationId":"40000000-0000-4000-8000-000000000012","finalScore":89.4}', '{"shadow":true,"suggestion":"Điểm cao — đề xuất chuyển sang bước Offer.","wouldNotify":["10000000-0000-4000-8000-000000000001"]}', NULL, 1, NULL, '2026-06-30 11:30:06', '2026-06-30 11:30:07', '2026-06-30 11:30:06'),
    ('e1000000-0000-4000-8000-000000000006', 'f1000000-0000-4000-8000-000000000002', 'f2000000-0000-4000-8000-000000000002', 'e0000000-0000-4000-8000-000000000006', 'HeadReviewOverdue:40000000-0000-4000-8000-000000000011:20260704', 'Skipped', 'Shadow', 'HeadReviewOverdue', '{"applicationId":"40000000-0000-4000-8000-000000000011","overdueDays":2}', '{"reason":"Condition overdueDays >= 3 not met (2)."}', NULL, 1, NULL, '2026-07-04 07:00:05', '2026-07-04 07:00:05', '2026-07-04 07:00:05'),
    ('e1000000-0000-4000-8000-000000000007', 'f1000000-0000-4000-8000-000000000003', 'f2000000-0000-4000-8000-000000000003', NULL, 'CandidateApplied:manual-replay-20260629', 'Success', 'Live', 'CandidateApplied', '{"applicationId":"40000000-0000-4000-8000-000000000009","jobTitle":"DevOps Engineer","finalScore":91.8}', '{"notified":["10000000-0000-4000-8000-000000000002"],"eventCode":"workflow_high_fit"}', NULL, 1, NULL, '2026-06-29 15:00:00', '2026-06-29 15:00:01', '2026-06-29 15:00:00'),
    ('e1000000-0000-4000-8000-000000000008', 'f1000000-0000-4000-8000-000000000001', 'f2000000-0000-4000-8000-000000000001', NULL, 'PassedToHeadReview:manual-replay-20260628', 'Failed', 'Live', 'PassedToHeadReview', '{"applicationId":"40000000-0000-4000-8000-000000000005","jobTitle":"Data Analyst","departmentHeadId":"721b1851-349a-48aa-acae-feed1c1843ed"}', NULL, 'notify_user action failed: notification store rejected the payload (title too long).', 3, NULL, '2026-06-28 10:00:00', '2026-06-28 10:00:09', '2026-06-28 10:00:00')
ON CONFLICT (id) DO UPDATE SET
    status = EXCLUDED.status,
    mode = EXCLUDED.mode,
    input_payload_json = EXCLUDED.input_payload_json,
    output_json = EXCLUDED.output_json,
    error_reason = EXCLUDED.error_reason,
    attempt_count = EXCLUDED.attempt_count,
    next_retry_at = EXCLUDED.next_retry_at,
    started_at = EXCLUDED.started_at,
    finished_at = EXCLUDED.finished_at,
    created_at = EXCLUDED.created_at;

INSERT INTO public.workflow_execution_steps (id, execution_id, step_no, step_type, action_type, status, input_json, output_json, error_reason, started_at, finished_at, created_at) VALUES
    ('e2000000-0000-4000-8000-000000000001', 'e1000000-0000-4000-8000-000000000001', 1, 'condition', NULL, 'Success', '{"field":"finalScore","operator":"greater_than_or_equal","value":"80"}', '{"matched":true,"actual":85.8}', NULL, '2026-07-01 10:46:05', '2026-07-01 10:46:05', '2026-07-01 10:46:05'),
    ('e2000000-0000-4000-8000-000000000002', 'e1000000-0000-4000-8000-000000000001', 2, 'action', 'notify_user', 'Success', '{"recipients":["assignedRecruiter"]}', '{"notifiedUserIds":["e781ccd9-e6f8-4ce1-b15d-e142f8977a4e"]}', NULL, '2026-07-01 10:46:05', '2026-07-01 10:46:06', '2026-07-01 10:46:05'),
    ('e2000000-0000-4000-8000-000000000003', 'e1000000-0000-4000-8000-000000000002', 1, 'condition', NULL, 'Success', '{"field":"departmentHeadId","operator":"exists","value":null}', '{"matched":true}', NULL, '2026-07-02 16:58:04', '2026-07-02 16:58:04', '2026-07-02 16:58:04'),
    ('e2000000-0000-4000-8000-000000000004', 'e1000000-0000-4000-8000-000000000002', 2, 'action', 'notify_user', 'Success', '{"recipients":["assignedDepartmentHead"]}', '{"notifiedUserIds":["721b1851-349a-48aa-acae-feed1c1843ed"]}', NULL, '2026-07-02 16:58:04', '2026-07-02 16:58:05', '2026-07-02 16:58:04'),
    ('e2000000-0000-4000-8000-000000000005', 'e1000000-0000-4000-8000-000000000003', 1, 'condition', NULL, 'Skipped', '{"field":"finalScore","operator":"greater_than_or_equal","value":"80"}', '{"matched":false,"actual":null}', NULL, '2026-07-04 08:26:03', '2026-07-04 08:26:03', '2026-07-04 08:26:03'),
    ('e2000000-0000-4000-8000-000000000006', 'e1000000-0000-4000-8000-000000000004', 1, 'condition', NULL, 'Success', '{"field":"departmentHeadId","operator":"exists","value":null}', '{"matched":true,"note":"exists operator matched on key presence"}', NULL, '2026-07-03 09:12:00', '2026-07-03 09:12:00', '2026-07-03 09:12:00'),
    ('e2000000-0000-4000-8000-000000000007', 'e1000000-0000-4000-8000-000000000004', 2, 'action', 'notify_user', 'Failed', '{"recipients":["assignedDepartmentHead"]}', NULL, 'Recipient resolution failed: assignedDepartmentHead is empty for this application.', '2026-07-03 09:12:00', '2026-07-03 09:12:08', '2026-07-03 09:12:00'),
    ('e2000000-0000-4000-8000-000000000008', 'e1000000-0000-4000-8000-000000000005', 1, 'condition', NULL, 'Success', '[]', '{"matched":true}', NULL, '2026-06-30 11:30:06', '2026-06-30 11:30:06', '2026-06-30 11:30:06'),
    ('e2000000-0000-4000-8000-000000000009', 'e1000000-0000-4000-8000-000000000005', 2, 'action', 'rule_based_next_step_suggestion', 'Success', '{"scoreField":"finalScore"}', '{"shadow":true,"suggestion":"Điểm cao — đề xuất chuyển sang bước Offer."}', NULL, '2026-06-30 11:30:06', '2026-06-30 11:30:07', '2026-06-30 11:30:06'),
    ('e2000000-0000-4000-8000-000000000010', 'e1000000-0000-4000-8000-000000000008', 1, 'condition', NULL, 'Success', '{"field":"departmentHeadId","operator":"exists","value":null}', '{"matched":true}', NULL, '2026-06-28 10:00:00', '2026-06-28 10:00:00', '2026-06-28 10:00:00'),
    ('e2000000-0000-4000-8000-000000000011', 'e1000000-0000-4000-8000-000000000008', 2, 'action', 'notify_user', 'Failed', '{"recipients":["assignedDepartmentHead"]}', NULL, 'notify_user action failed: notification store rejected the payload (title too long).', '2026-06-28 10:00:00', '2026-06-28 10:00:09', '2026-06-28 10:00:00')
ON CONFLICT (id) DO UPDATE SET
    status = EXCLUDED.status,
    input_json = EXCLUDED.input_json,
    output_json = EXCLUDED.output_json,
    error_reason = EXCLUDED.error_reason,
    started_at = EXCLUDED.started_at,
    finished_at = EXCLUDED.finished_at,
    created_at = EXCLUDED.created_at;

INSERT INTO public.workflow_action_dead_letters (id, execution_id, step_id, action_type, payload_json, error_reason, attempt_count, next_retry_at, resolved_at, created_at) VALUES
    ('e3000000-0000-4000-8000-000000000001', 'e1000000-0000-4000-8000-000000000004', 'e2000000-0000-4000-8000-000000000007', 'notify_user', '{"recipients":["assignedDepartmentHead"],"applicationId":"a1111111-1111-4111-8111-111111111112"}', 'Recipient resolution failed after 3 attempts: assignedDepartmentHead is empty.', 3, NULL, NULL, '2026-07-03 09:12:08')
ON CONFLICT (id) DO UPDATE SET
    error_reason = EXCLUDED.error_reason,
    attempt_count = EXCLUDED.attempt_count,
    created_at = EXCLUDED.created_at;

-- Heartbeats demo — worker thật sẽ ghi đè khi API chạy.
INSERT INTO public.workflow_worker_heartbeats (worker_name, last_beat_at, status, detail, updated_at) VALUES
    ('workflow-dispatcher', '2026-07-04 09:00:00', 'Running', 'Demo seed heartbeat — replaced by the live worker on API startup.', '2026-07-04 09:00:00'),
    ('workflow-retry', '2026-07-04 09:00:00', 'Running', 'Demo seed heartbeat — replaced by the live worker on API startup.', '2026-07-04 09:00:00')
ON CONFLICT (worker_name) DO UPDATE SET
    last_beat_at = EXCLUDED.last_beat_at,
    status = EXCLUDED.status,
    detail = EXCLUDED.detail,
    updated_at = EXCLUDED.updated_at;

-- MCP tool audit demo cho màn System Admin → MCP Audit.
INSERT INTO public.mcp_tool_audits (id, tool_name, caller_user_id, input_json, output_summary_json, allowed, denied_reason, latency_ms, created_at) VALUES
    ('e4000000-0000-4000-8000-000000000001', 'jobs.search', 'a0000000-0000-4000-8000-000000000001', '{"query":"backend"}', '{"matches":3}', true, NULL, 42, '2026-07-03 10:00:00'),
    ('e4000000-0000-4000-8000-000000000002', 'applications.get', 'a0000000-0000-4000-8000-000000000001', '{"applicationId":"40000000-0000-4000-8000-000000000011"}', '{"status":"ManagerReview"}', true, NULL, 35, '2026-07-03 10:02:00'),
    ('e4000000-0000-4000-8000-000000000003', 'analytics.get_funnel_summary', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', '{"from":"2026-06-01","to":"2026-07-04"}', '{"stages":6}', true, NULL, 120, '2026-07-03 10:05:00'),
    ('e4000000-0000-4000-8000-000000000004', 'applications.get_fit_analysis', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '{"applicationId":"41000000-0000-4000-8000-000000000001"}', NULL, false, 'Caller lacks SystemAdmin role for MCP test invocation.', 8, '2026-07-02 15:30:00'),
    ('e4000000-0000-4000-8000-000000000005', 'interviews.get_schedule', 'a0000000-0000-4000-8000-000000000001', '{"from":"2026-07-04","to":"2026-07-31"}', '{"interviews":6}', true, NULL, 51, '2026-07-04 08:40:00')
ON CONFLICT (id) DO UPDATE SET
    input_json = EXCLUDED.input_json,
    output_summary_json = EXCLUDED.output_summary_json,
    allowed = EXCLUDED.allowed,
    denied_reason = EXCLUDED.denied_reason,
    latency_ms = EXCLUDED.latency_ms,
    created_at = EXCLUDED.created_at;

-- ===========================================================================
-- CV-matching demo data (added): 3 candidate accounts mirroring the CVs in the cv/ folder
-- (all Java/Spring Boot backend), Java/Spring Boot jobs that strongly match them, and a rich
-- Screening pool for the AI copilot. CVs stay EMPTY (candidates upload their own); skills carry
-- explicit years for matching. Password = Password@123 (login by username).
-- ===========================================================================

-- Extra skills referenced by the CVs
INSERT INTO public.skills (id, name) VALUES
    ('90000000-0000-4000-8000-000000000021', 'Vue.js'),
    ('90000000-0000-4000-8000-000000000022', 'MySQL'),
    ('90000000-0000-4000-8000-000000000023', 'JPA'),
    ('90000000-0000-4000-8000-000000000024', 'Thymeleaf')
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;

-- CV-owner candidate accounts (nhatquang already exists as the Phung Nhat Quang CV)
INSERT INTO public.users (id, username, email, password_hash, full_name, phone, avatar_url, status, created_at, updated_at) VALUES
    ('11000000-0000-4000-8000-000000000001', 'anhhuy', 'anhhuy.nguyen@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Nguyen Anh Huy', '0900000301', NULL, 'Active', '2026-07-05 09:00:00', '2026-07-05 09:00:00'),
    ('11000000-0000-4000-8000-000000000002', 'vanphong', 'vanphong.pham@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Pham Van Phong', '0900000302', NULL, 'Active', '2026-07-05 09:05:00', '2026-07-05 09:05:00'),
    ('11000000-0000-4000-8000-000000000003', 'nguyenliem', 'liem.nguyen@recruitpro.vn', crypt('Password@123', gen_salt('bf', 6)), 'Nguyen Liem', '0900000303', NULL, 'Active', '2026-07-05 09:10:00', '2026-07-05 09:10:00')
ON CONFLICT (id) DO UPDATE SET username = EXCLUDED.username, full_name = EXCLUDED.full_name, status = EXCLUDED.status;

INSERT INTO public.user_roles (user_id, role_id, assigned_at) VALUES
    ('11000000-0000-4000-8000-000000000001', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-07-05 09:00:00'),
    ('11000000-0000-4000-8000-000000000002', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-07-05 09:05:00'),
    ('11000000-0000-4000-8000-000000000003', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-07-05 09:10:00')
ON CONFLICT (user_id, role_id) DO NOTHING;

-- Candidate profiles -- resume EMPTY (upload required); github/linkedin only.
INSERT INTO public.candidate_profiles (id, user_id, current_position, experience_years, education, address, bio, resume_url, github_url, linkedin_url, resume_extracted_text, resume_parse_status) VALUES
    ('21000000-0000-4000-8000-000000000001', '11000000-0000-4000-8000-000000000001', 'Java Backend Developer (Fresher)', 1, 'Hanoi University of Industrial Economics and Technology', 'Dong Da, Ha Noi', 'Java Spring Boot backend fresher: Spring Boot, JPA, MySQL, RESTful API, Thymeleaf, Spring Security.', NULL, 'https://github.com/nah-coder', 'https://linkedin.com/in/anh-huy-nguyen', NULL, NULL),
    ('21000000-0000-4000-8000-000000000002', '11000000-0000-4000-8000-000000000002', 'Software Developer Intern', 1, 'FPT University', 'Ha Noi', 'Intern developer with Java, Spring Boot, SQL and REST API basics.', NULL, 'https://github.com/vanphong-dev', 'https://linkedin.com/in/van-phong-pham', NULL, NULL),
    ('21000000-0000-4000-8000-000000000003', '11000000-0000-4000-8000-000000000003', 'Java Backend Developer', 2, 'Hanoi University of Science and Technology', 'Ha Noi', 'Backend developer: Java, Spring Boot, microservices, MySQL/PostgreSQL, REST API.', NULL, 'https://github.com/liem-nguyen', 'https://linkedin.com/in/liem-nguyen', NULL, NULL)
ON CONFLICT (id) DO NOTHING;

-- Skills with explicit years (matching the CVs). Vue.js added to nhatquang to match the fullstack job.
INSERT INTO public.candidate_skills (candidate_id, skill_id, years_of_experience) VALUES
    ('21000000-0000-4000-8000-000000000001', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 1),
    ('21000000-0000-4000-8000-000000000001', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 1),
    ('21000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000023', 1),
    ('21000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000022', 1),
    ('21000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000016', 1),
    ('21000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000024', 1),
    ('21000000-0000-4000-8000-000000000002', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 1),
    ('21000000-0000-4000-8000-000000000002', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 1),
    ('21000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000007', 1),
    ('21000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000016', 1),
    ('21000000-0000-4000-8000-000000000003', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 2),
    ('21000000-0000-4000-8000-000000000003', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 2),
    ('21000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000022', 2),
    ('21000000-0000-4000-8000-000000000003', 'b1779c35-b7c4-47bb-a69b-43131c084a0f', 1),
    ('21000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000016', 2),
    ('21000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000015', 1),
    ('366cb75c-aecd-4f54-aedb-b76cf475d81a', '90000000-0000-4000-8000-000000000021', 1)
ON CONFLICT (candidate_id, skill_id) DO UPDATE SET years_of_experience = EXCLUDED.years_of_experience;

-- Java/Spring Boot jobs that strongly match the CVs (junior-friendly, Approved, Engineering dept).
INSERT INTO public.jobs (id, department_id, created_by, approved_by, title, short_pitch, description, requirements, benefits, location, work_mode, employment_type, min_experience_years, vacancy_count, salary_min, salary_max, deadline, status, created_at) VALUES
    ('31000000-0000-4000-8000-000000000001', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Java Backend Developer (Fresher)', 'Kick off your backend career building Spring Boot REST APIs.', 'Build backend REST APIs with Spring Boot; write SQL; use Git.', 'Java, Spring Boot, REST API, SQL, Git', 'Mentorship, training budget, hybrid', 'Ha Noi', 'Hybrid', 'FullTime', 0, 2, 12000000.00, 20000000.00, '2026-09-30 18:00:00', 'Approved', '2026-07-05 08:30:00'),
    ('31000000-0000-4000-8000-000000000002', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Spring Boot Backend Developer (Junior)', 'Own backend services with Spring Boot, JPA and MySQL.', 'Design and implement Spring Boot services with JPA/Hibernate and MySQL.', 'Java, Spring Boot, JPA, MySQL, REST API', 'Annual bonus, laptop allowance, hybrid', 'Ha Noi', 'Hybrid', 'FullTime', 1, 2, 18000000.00, 30000000.00, '2026-09-30 18:00:00', 'Approved', '2026-07-05 08:35:00'),
    ('31000000-0000-4000-8000-000000000003', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Java Web Developer (Spring Boot, Thymeleaf)', 'Build server-rendered Java web apps with Spring Boot + Thymeleaf.', 'Develop MVC web features with Spring Boot, Thymeleaf, Spring Security and MySQL.', 'Java, Spring Boot, Thymeleaf, MySQL, MVC', 'Onsite team, learning budget', 'Ha Noi', 'Onsite', 'FullTime', 0, 1, 14000000.00, 24000000.00, '2026-09-30 18:00:00', 'Approved', '2026-07-05 08:40:00'),
    ('31000000-0000-4000-8000-000000000004', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Java Fullstack Developer (Spring Boot + Vue.js)', 'Work across Spring Boot APIs and a Vue.js frontend.', 'Build end-to-end features with Spring Boot backend and Vue.js frontend.', 'Java, Spring Boot, Vue.js, REST API, PostgreSQL', 'Flexible hours, hybrid, annual bonus', 'Ha Noi', 'Hybrid', 'FullTime', 1, 2, 20000000.00, 34000000.00, '2026-09-30 18:00:00', 'Approved', '2026-07-05 08:45:00')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.job_skills (job_id, skill_id, min_years_experience, is_required) VALUES
    ('31000000-0000-4000-8000-000000000001', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 0, true),
    ('31000000-0000-4000-8000-000000000001', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 0, true),
    ('31000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000016', 0, true),
    ('31000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000007', 0, false),
    ('31000000-0000-4000-8000-000000000002', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 1, true),
    ('31000000-0000-4000-8000-000000000002', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 1, true),
    ('31000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000023', 0, true),
    ('31000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000022', 0, false),
    ('31000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000016', 1, false),
    ('31000000-0000-4000-8000-000000000003', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 0, true),
    ('31000000-0000-4000-8000-000000000003', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 0, true),
    ('31000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000024', 0, true),
    ('31000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000022', 0, false),
    ('31000000-0000-4000-8000-000000000004', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 1, true),
    ('31000000-0000-4000-8000-000000000004', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 1, true),
    ('31000000-0000-4000-8000-000000000004', '90000000-0000-4000-8000-000000000021', 0, true),
    ('31000000-0000-4000-8000-000000000004', '90000000-0000-4000-8000-000000000016', 1, false),
    ('31000000-0000-4000-8000-000000000004', 'b1779c35-b7c4-47bb-a69b-43131c084a0f', 0, false)
ON CONFLICT (job_id, skill_id) DO UPDATE SET min_years_experience = EXCLUDED.min_years_experience, is_required = EXCLUDED.is_required;

-- Rich Screening pool (status Screening, scored) across the matching jobs for the AI copilot.
INSERT INTO public.applications (id, user_id, job_id, reviewed_by, status, applied_at, cover_letter, rule_score, semantic_score, final_score, score_status, scored_at) VALUES
    ('42000000-0000-4000-8000-000000000001', '11000000-0000-4000-8000-000000000001', '31000000-0000-4000-8000-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:00:00', 'Spring Boot backend fresher.', 82.00, 80.00, 81.20, 'Completed', '2026-07-06 09:30:00'),
    ('42000000-0000-4000-8000-000000000002', '11000000-0000-4000-8000-000000000002', '31000000-0000-4000-8000-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:05:00', 'Intern Java, Spring Boot va SQL.', 74.00, 72.00, 72.80, 'Completed', '2026-07-06 09:35:00'),
    ('42000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000101', '31000000-0000-4000-8000-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:10:00', 'Java microservices backend.', 88.00, 85.00, 85.80, 'Completed', '2026-07-06 09:40:00'),
    ('42000000-0000-4000-8000-000000000004', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', '31000000-0000-4000-8000-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:15:00', 'Full stack Java, focus backend.', 79.00, 77.00, 77.80, 'Completed', '2026-07-06 09:45:00'),
    ('42000000-0000-4000-8000-000000000005', '11000000-0000-4000-8000-000000000003', '31000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:20:00', 'Java backend 2 years, Spring Boot + JPA + MySQL.', 90.00, 88.00, 88.80, 'Completed', '2026-07-06 09:50:00'),
    ('42000000-0000-4000-8000-000000000006', '4071353e-5816-4746-a8b6-c0bc3113c44d', '31000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:25:00', 'Java Developer, Spring Boot va Vue.js.', 84.00, 82.00, 82.80, 'Completed', '2026-07-06 09:55:00'),
    ('42000000-0000-4000-8000-000000000007', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', '31000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:30:00', 'DevOps voi nen tang Java.', 68.00, 66.00, 66.80, 'Completed', '2026-07-06 10:00:00'),
    ('42000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000103', '31000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:35:00', 'SQL, muon hoc backend Java.', 65.00, 63.00, 63.80, 'Completed', '2026-07-06 10:05:00'),
    ('42000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000105', '31000000-0000-4000-8000-000000000003', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:40:00', '.NET backend, OOP va SQL.', 70.00, 68.00, 68.80, 'Completed', '2026-07-06 10:10:00'),
    ('42000000-0000-4000-8000-000000000010', 'c3d4e5f6-a7b8-4903-9a04-001122334455', '31000000-0000-4000-8000-000000000003', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:45:00', 'Biet Java web co ban.', 60.00, 58.00, 58.80, 'Completed', '2026-07-06 10:15:00'),
    ('42000000-0000-4000-8000-000000000011', '10000000-0000-4000-8000-000000000106', '31000000-0000-4000-8000-000000000003', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:50:00', 'Java web MVC voi Thymeleaf.', 76.00, 74.00, 74.80, 'Completed', '2026-07-06 10:20:00'),
    ('42000000-0000-4000-8000-000000000012', '4071353e-5816-4746-a8b6-c0bc3113c44d', '31000000-0000-4000-8000-000000000004', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 09:55:00', 'Spring Boot + Vue.js.', 89.00, 87.00, 87.80, 'Completed', '2026-07-06 10:25:00'),
    ('42000000-0000-4000-8000-000000000013', '11000000-0000-4000-8000-000000000003', '31000000-0000-4000-8000-000000000004', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 10:00:00', 'Java fullstack, manh backend Spring Boot.', 80.00, 78.00, 78.80, 'Completed', '2026-07-06 10:30:00'),
    ('42000000-0000-4000-8000-000000000014', '10000000-0000-4000-8000-000000000107', '31000000-0000-4000-8000-000000000004', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 10:05:00', 'Hoc them lap trinh.', 55.00, 53.00, 53.80, 'Completed', '2026-07-06 10:35:00'),
    ('42000000-0000-4000-8000-000000000015', '11000000-0000-4000-8000-000000000001', '31000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 10:10:00', 'Spring Boot + JPA + MySQL.', 81.00, 79.00, 79.80, 'Completed', '2026-07-06 10:40:00'),
    ('42000000-0000-4000-8000-000000000016', '11000000-0000-4000-8000-000000000002', '31000000-0000-4000-8000-000000000003', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Screening', '2026-07-06 10:15:00', 'Java web co ban, Spring Boot + Thymeleaf.', 72.00, 70.00, 70.80, 'Completed', '2026-07-06 10:45:00')
ON CONFLICT (id) DO NOTHING;
