
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
    status character varying(50) DEFAULT 'Pending',
    applied_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.applications OWNER TO postgres;

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
    meeting_type character varying(50),
    meeting_link text,
    location text,
    notes text,
    status character varying(50) DEFAULT 'Scheduled'
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

--
-- TOC entry 233 (class 1259 OID 16735)
-- Name: notifications; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.notifications (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    title character varying(255),
    content text,
    type character varying(50),
    is_read boolean DEFAULT false,
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
    email character varying(255) NOT NULL,
    password_hash text NOT NULL,
    full_name character varying(255) NOT NULL,
    phone character varying(20),
    avatar_url text,
    status character varying(50) DEFAULT 'Active',
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.users OWNER TO postgres;

--
-- TOC entry 5222 (class 0 OID 16692)
-- Dependencies: 231
-- Data for Name: applications; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.applications VALUES ('75afffb4-ad67-4974-807c-0308a90f07bf', '4071353e-5816-4746-a8b6-c0bc3113c44d', '59a5221b-43bb-489f-83b7-d41c347e37f9', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Reviewing', '2026-05-27 09:23:30.793214');
INSERT INTO public.applications VALUES ('a1111111-1111-4111-8111-111111111111', 'd8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', '8c1b65c9-1c9f-4a36-9e6a-111111111111', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Pending', '2026-05-29 09:00:00');
INSERT INTO public.applications VALUES ('a1111111-1111-4111-8111-111111111112', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', '7c2d3e4f-5a6b-4c7d-8e9f-000000000001', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Reviewing', '2026-05-29 09:15:00');
INSERT INTO public.applications VALUES ('a1111111-1111-4111-8111-111111111113', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '721b1851-349a-48aa-acae-feed1c1843ed', 'Interviewing', '2026-05-29 09:30:00');
INSERT INTO public.applications VALUES ('a1111111-1111-4111-8111-111111111114', 'c3d4e5f6-a7b8-4903-9a04-001122334455', '7c2d3e4f-5a6b-4c7d-8e9f-000000000003', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Pending', '2026-05-29 09:45:00');


--
-- TOC entry 5217 (class 0 OID 16601)
-- Dependencies: 226
-- Data for Name: candidate_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.candidate_profiles VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'Backend Developer', 2, 'FPT University', 'Ha Noi', 'Java backend developer', NULL, 'https://github.com/candidate', 'https://linkedin.com/in/candidate');
INSERT INTO public.candidate_profiles VALUES ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', 'd8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', 'Frontend Developer', 3, 'Hanoi University', 'Ha Noi', 'Frontend engineer focused on React and modern UI systems', NULL, 'https://github.com/frontend-dev', 'https://linkedin.com/in/frontend-dev');
INSERT INTO public.candidate_profiles VALUES ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', 'Full Stack Developer', 4, 'VNU', 'Ho Chi Minh City', 'Full stack developer with product mindset', NULL, 'https://github.com/fullstack-dev', 'https://linkedin.com/in/fullstack-dev');
INSERT INTO public.candidate_profiles VALUES ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', 'DevOps Engineer', 5, 'FPT Polytechnic', 'Da Nang', 'Infrastructure and delivery automation specialist', NULL, 'https://github.com/devops-dev', 'https://linkedin.com/in/devops-dev');
INSERT INTO public.candidate_profiles VALUES ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', 'c3d4e5f6-a7b8-4903-9a04-001122334455', 'Business Analyst', 3, 'NEU', 'Ha Noi', 'Business analyst with strong communication and process design skills', NULL, 'https://github.com/ba-dev', 'https://linkedin.com/in/ba-dev');


--
-- TOC entry 5219 (class 0 OID 16629)
-- Dependencies: 228
-- Data for Name: candidate_skills; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a');
INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67');
INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'b1779c35-b7c4-47bb-a69b-43131c084a0f');
INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b');
INSERT INTO public.candidate_skills VALUES ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d');
INSERT INTO public.candidate_skills VALUES ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('9ef6d6c3-2b94-4d9d-a0d2-9e6d2c8ce101', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a');
INSERT INTO public.candidate_skills VALUES ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d');
INSERT INTO public.candidate_skills VALUES ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'e1330bc9-5fe4-4e6d-a5f3-768670d8104a');
INSERT INTO public.candidate_skills VALUES ('7f6f6f6c-8f8a-4a77-9d2b-7fd3c0f41002', 'b1779c35-b7c4-47bb-a69b-43131c084a0f');
INSERT INTO public.candidate_skills VALUES ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b');
INSERT INTO public.candidate_skills VALUES ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', 'ee3fd9dd-15b1-45e0-875d-ebd18a48db41');
INSERT INTO public.candidate_skills VALUES ('3a6d5b4c-1a2b-4c3d-9e0f-1234567890ab', '984f6a6b-e625-449c-8eb4-526348f279ef');
INSERT INTO public.candidate_skills VALUES ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('5b4c3d2e-1f0a-4b5c-9d8e-0a1b2c3d4e5f', '984f6a6b-e625-449c-8eb4-526348f279ef');


--
-- TOC entry 5216 (class 0 OID 16589)
-- Dependencies: 225
-- Data for Name: departments; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.departments VALUES ('fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'Engineering', 'Engineering department');
INSERT INTO public.departments VALUES ('95db214f-5ce2-4e89-b350-93da747c7bdb', 'Human Resources', 'Human resources department');
INSERT INTO public.departments VALUES ('cc05372d-9ee5-4962-a071-74a8d7630279', 'Finance', 'Finance department');
INSERT INTO public.departments VALUES ('3067eaeb-3893-468f-b854-08f1319448c9', 'Marketing', 'Marketing department');


--
-- TOC entry 5223 (class 0 OID 16718)
-- Dependencies: 232
-- Data for Name: interviews; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.interviews VALUES ('ce4c916b-dfe9-455c-9fbc-f8f3bfa6c994', '75afffb4-ad67-4974-807c-0308a90f07bf', '2026-05-30 09:23:38.11334', 'Online', 'https://meet.google.com/sample-room', NULL, 'Technical interview round 1', 'Scheduled');
INSERT INTO public.interviews VALUES ('d2222222-2222-4222-8222-222222222221', 'a1111111-1111-4111-8111-111111111112', '2026-06-01 10:00:00', 'Online', 'https://meet.google.com/frontend-room', NULL, 'Frontend screening interview', 'Scheduled');
INSERT INTO public.interviews VALUES ('d2222222-2222-4222-8222-222222222222', 'a1111111-1111-4111-8111-111111111113', '2026-06-02 14:00:00', 'Offline', NULL, 'RecruitPro Office', 'HR and culture fit interview', 'Scheduled');


--
-- TOC entry 5221 (class 0 OID 16675)
-- Dependencies: 230
-- Data for Name: job_skills; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.job_skills VALUES
(
    '59a5221b-43bb-489f-83b7-d41c347e37f9',
    'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a',
    2,
    true
),
(
    '59a5221b-43bb-489f-83b7-d41c347e37f9',
    'e70f15bc-9e3c-4dc1-92db-4b53028d5c67',
    1,
    true
),
(
    '59a5221b-43bb-489f-83b7-d41c347e37f9',
    'b1779c35-b7c4-47bb-a69b-43131c084a0f',
    1,
    false
);
INSERT INTO public.job_skills VALUES
(
    '8c1b65c9-1c9f-4a36-9e6a-111111111111',
    'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d',
    3,
    true
),
(
    '8c1b65c9-1c9f-4a36-9e6a-111111111111',
    '23a4ab7e-04af-42b3-8f11-37bbeb036fb2',
    2,
    true
),
(
    '8c1b65c9-1c9f-4a36-9e6a-111111111111',
    '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4',
    2,
    false
),
(
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000001',
    'e1330bc9-5fe4-4e6d-a5f3-768670d8104a',
    4,
    true
),
(
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000001',
    'b1779c35-b7c4-47bb-a69b-43131c084a0f',
    2,
    true
),
(
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000001',
    'f69bf36f-a210-4b76-8008-a5a1fefc6b4b',
    1,
    false
),
(
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000002',
    'f69bf36f-a210-4b76-8008-a5a1fefc6b4b',
    5,
    true
),
(
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000002',
    'ee3fd9dd-15b1-45e0-875d-ebd18a48db41',
    4,
    true
),
(
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000002',
    '984f6a6b-e625-449c-8eb4-526348f279ef',
    3,
    false
);


--
-- TOC entry 5220 (class 0 OID 16646)
-- Dependencies: 229
-- Data for Name: jobs; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '59a5221b-43bb-489f-83b7-d41c347e37f9',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'Java Backend Developer',
    'Build scalable backend services for high-traffic recruiting workflows.',
    'Develop backend services using Spring Boot',
    'Java, Spring Boot, PostgreSQL',
    NULL,
    'Ha Noi',
    'Onsite',
    'FullTime',
    2,
    1,
    1000.00,
    2000.00,
    '2026-06-26 09:23:30.793214',
    'Approved',
    '2026-05-27 09:23:30.793214'
);
INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '8c1b65c9-1c9f-4a36-9e6a-111111111111',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'Senior Frontend Developer',
    'Own the user experience for recruiter and candidate-facing product flows.',
    'Build and maintain responsive frontend interfaces for the recruitment platform.',
    'ReactJS, TypeScript, CSS, API integration',
    'Hybrid work, premium laptop, learning budget',
    'Ha Noi',
    'Hybrid',
    'FullTime',
    3,
    2,
    1200.00,
    2200.00,
    '2026-07-10 09:00:00',
    'Approved',
    '2026-05-28 09:15:00'
);
INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000001',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'Full Stack Engineer',
    'Work across frontend and backend on a fast-moving product team.',
    'Build end-to-end features, from APIs to polished UI workflows.',
    'NodeJS, ReactJS, PostgreSQL, REST, Git',
    'Competitive package, flexible hours, onsite gym',
    'Ho Chi Minh City',
    'Hybrid',
    'FullTime',
    4,
    3,
    1400.00,
    2600.00,
    '2026-07-20 09:00:00',
    'Approved',
    '2026-05-29 10:00:00'
);
INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000002',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'DevOps Engineer',
    'Automate delivery, infrastructure, and observability across services.',
    'Own CI/CD pipelines, deployment strategy, and cloud infrastructure reliability.',
    'Docker, Kubernetes, CI/CD, Linux, Cloud',
    'Remote-friendly, certification support, modern stack',
    'Da Nang',
    'Remote',
    'FullTime',
    5,
    2,
    1500.00,
    2800.00,
    '2026-07-25 09:00:00',
    'Approved',
    '2026-05-29 11:00:00'
);
INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000003',
    '95db214f-5ce2-4e89-b350-93da747c7bdb',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'HR Operations Specialist',
    'Support hiring operations and candidate experience end-to-end.',
    'Coordinate interviews, manage records, and keep recruitment workflows moving.',
    'Communication, Excel, process management, English',
    'Stable team, process ownership, annual review',
    'Ha Noi',
    'Onsite',
    'FullTime',
    2,
    1,
    700.00,
    1200.00,
    '2026-07-05 09:00:00',
    'Approved',
    '2026-05-30 08:30:00'
);
INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '7c2d3e4f-5a6b-4c7d-8e9f-000000000004',
    '3067eaeb-3893-468f-b854-08f1319448c9',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'Marketing Specialist',
    'Drive product awareness and support growth campaigns.',
    'Plan and execute marketing campaigns, content, and analytics.',
    'Content writing, analytics, communication, social media',
    'Flexible scope, creative ownership, annual bonus',
    'Ho Chi Minh City',
    'Hybrid',
    'FullTime',
    2,
    1,
    800.00,
    1500.00,
    '2026-07-12 09:00:00',
    'Approved',
    '2026-05-30 09:00:00'
);
INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES (
    '8c1b65c9-1c9f-4a36-9e6a-111111111112',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'Java Backend Engineer',
    'Join a backend team shipping APIs and core business services.',
    'Design and implement reliable Java services for the recruiting domain.',
    'Java, Spring Boot, PostgreSQL, REST, OOP',
    'Hybrid model, laptop allowance, training budget',
    'Da Nang',
    'Hybrid',
    'FullTime',
    2,
    2,
    1100.00,
    2100.00,
    '2026-07-18 09:00:00',
    'Approved',
    '2026-05-31 09:00:00'
);


--
-- TOC entry 5224 (class 0 OID 16735)
-- Dependencies: 233
-- Data for Name: notifications; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- TOC entry 5213 (class 0 OID 16542)
-- Dependencies: 222
-- Data for Name: permissions; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.permissions VALUES ('0c84117e-ce89-430b-9d17-073e83e6a8b9', 'View Users', 'USER_VIEW', 'View users');
INSERT INTO public.permissions VALUES ('b2ac9a5a-014d-4952-8965-f947f3b5fec2', 'Create User', 'USER_CREATE', 'Create users');
INSERT INTO public.permissions VALUES ('9beaf30e-5c57-4c14-a160-89beca9a27eb', 'Update User', 'USER_UPDATE', 'Update users');
INSERT INTO public.permissions VALUES ('346841cb-b9c6-4d41-9f2f-ddad38755b52', 'Delete User', 'USER_DELETE', 'Delete users');
INSERT INTO public.permissions VALUES ('679b403b-c773-498e-92a2-eb65e9b73472', 'View Roles', 'ROLE_VIEW', 'View roles');
INSERT INTO public.permissions VALUES ('ca9ca784-c1e0-4e8f-99ce-a80ee5db03c5', 'Manage Roles', 'ROLE_MANAGE', 'Manage roles');
INSERT INTO public.permissions VALUES ('7b2702b7-7a1a-4249-81bb-ca7ca614dc4a', 'View Permissions', 'PERMISSION_VIEW', 'View permissions');
INSERT INTO public.permissions VALUES ('a771bd2e-bbed-4b27-ac94-d9bb951a415d', 'Manage Permissions', 'PERMISSION_MANAGE', 'Manage permissions');
INSERT INTO public.permissions VALUES ('f0515aef-c29c-42d6-add4-9ef95b92da6d', 'View Jobs', 'Job_VIEW', 'View jobs');
INSERT INTO public.permissions VALUES ('b98f8544-bce4-442d-ac70-ae5f942c4db7', 'Create Job', 'Job_CREATE', 'Create jobs');
INSERT INTO public.permissions VALUES ('51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', 'Update Job', 'Job_UPDATE', 'Update jobs');
INSERT INTO public.permissions VALUES ('80c0ba49-f562-42b9-b09d-533db4b394a5', 'Delete Job', 'Job_DELETE', 'Delete jobs');
INSERT INTO public.permissions VALUES ('eabc0d36-032d-4eb2-81f7-6d9a50c82b42', 'Approve Job', 'Job_APPROVE', 'Approve jobs');
INSERT INTO public.permissions VALUES ('b33f762b-1a44-4292-9b88-1ce34f5387e8', 'View Applications', 'Application_VIEW', 'View applications');
INSERT INTO public.permissions VALUES ('3130c14f-bfe8-4d5f-84ac-7e6886104070', 'Apply Job', 'Application_APPLY', 'Apply jobs');
INSERT INTO public.permissions VALUES ('bdf8cb0d-b58d-400c-ad47-064f88054275', 'Review Application', 'Application_REVIEW', 'Review applications');
INSERT INTO public.permissions VALUES ('7d33a32f-737a-4d94-9271-3ce4bba57659', 'View Interviews', 'Interview_VIEW', 'View interviews');
INSERT INTO public.permissions VALUES ('b410e0de-f182-4288-b200-9b0eed42424d', 'Create Interview', 'Interview_CREATE', 'Create interviews');
INSERT INTO public.permissions VALUES ('753339d4-9adf-4fc5-b80f-268f77ca0849', 'Update Interview', 'Interview_UPDATE', 'Update interviews');
INSERT INTO public.permissions VALUES ('ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', 'View Candidate Profile', 'CANDIDATE_PROFILE_VIEW', 'View candidate profile');
INSERT INTO public.permissions VALUES ('52b771f7-e9b4-4502-a7a0-afab7f1bd715', 'Update Candidate Profile', 'CANDIDATE_PROFILE_UPDATE', 'Update candidate profile');
INSERT INTO public.permissions VALUES ('4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', 'View Departments', 'DEPARTMENT_VIEW', 'View departments');
INSERT INTO public.permissions VALUES ('6ca18f1f-43ff-4602-a617-23d8543077a6', 'Manage Departments', 'DEPARTMENT_MANAGE', 'Manage departments');
INSERT INTO public.permissions VALUES ('b0a5f45f-53f3-4e09-9b7c-9d713135fe91', 'View Skills', 'SKILL_VIEW', 'View skills');
INSERT INTO public.permissions VALUES ('fc5bbc69-f2ac-48e3-b54a-5aac96c1d062', 'Manage Skills', 'SKILL_MANAGE', 'Manage skills');
INSERT INTO public.permissions VALUES ('99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', 'View Notifications', 'NOTIFICATION_VIEW', 'View notifications');
INSERT INTO public.permissions VALUES ('cdda07cb-20f5-4b82-9842-dfd65057c425', 'View Logs', 'System_LOG_VIEW', 'View system logs');


--
-- TOC entry 5225 (class 0 OID 16752)
-- Dependencies: 234
-- Data for Name: refresh_tokens; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- TOC entry 5215 (class 0 OID 16572)
-- Dependencies: 224
-- Data for Name: role_permissions; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '3130c14f-bfe8-4d5f-84ac-7e6886104070', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '52b771f7-e9b4-4502-a7a0-afab7f1bd715', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b98f8544-bce4-442d-ac70-ae5f942c4db7', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', '51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'bdf8cb0d-b58d-400c-ad47-064f88054275', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b410e0de-f182-4288-b200-9b0eed42424d', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', '753339d4-9adf-4fc5-b80f-268f77ca0849', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-05-27 09:22:45.423186+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'eabc0d36-032d-4eb2-81f7-6d9a50c82b42', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'bdf8cb0d-b58d-400c-ad47-064f88054275', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'cdda07cb-20f5-4b82-9842-dfd65057c425', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '0c84117e-ce89-430b-9d17-073e83e6a8b9', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b2ac9a5a-014d-4952-8965-f947f3b5fec2', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '9beaf30e-5c57-4c14-a160-89beca9a27eb', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '346841cb-b9c6-4d41-9f2f-ddad38755b52', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '679b403b-c773-498e-92a2-eb65e9b73472', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'ca9ca784-c1e0-4e8f-99ce-a80ee5db03c5', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '7b2702b7-7a1a-4249-81bb-ca7ca614dc4a', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'a771bd2e-bbed-4b27-ac94-d9bb951a415d', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'f0515aef-c29c-42d6-add4-9ef95b92da6d', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b98f8544-bce4-442d-ac70-ae5f942c4db7', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '80c0ba49-f562-42b9-b09d-533db4b394a5', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'eabc0d36-032d-4eb2-81f7-6d9a50c82b42', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b33f762b-1a44-4292-9b88-1ce34f5387e8', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '3130c14f-bfe8-4d5f-84ac-7e6886104070', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'bdf8cb0d-b58d-400c-ad47-064f88054275', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '7d33a32f-737a-4d94-9271-3ce4bba57659', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b410e0de-f182-4288-b200-9b0eed42424d', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '753339d4-9adf-4fc5-b80f-268f77ca0849', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '52b771f7-e9b4-4502-a7a0-afab7f1bd715', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '6ca18f1f-43ff-4602-a617-23d8543077a6', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'b0a5f45f-53f3-4e09-9b7c-9d713135fe91', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'fc5bbc69-f2ac-48e3-b54a-5aac96c1d062', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', '99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', '2026-05-27 09:22:58.875748+07');
INSERT INTO public.role_permissions VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'cdda07cb-20f5-4b82-9842-dfd65057c425', '2026-05-27 09:22:58.875748+07');


--
-- TOC entry 5212 (class 0 OID 16530)
-- Dependencies: 221
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.roles VALUES ('ef574bf3-08e1-4f25-932c-2d83ed8afd88', 'Candidate', 'Candidate role');
INSERT INTO public.roles VALUES ('e28e9442-682d-4e1a-b11f-663af56eb730', 'HR', 'HR role');
INSERT INTO public.roles VALUES ('5f5350dc-a77f-4a68-88f8-69e27116aa8f', 'Manager', 'Manager role');
INSERT INTO public.roles VALUES ('0137e9bc-7ee4-463c-b760-1580ac17cdb6', 'SystemAdmin', 'System administrator role');


--
-- TOC entry 5218 (class 0 OID 16619)
-- Dependencies: 227
-- Data for Name: skills; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.skills VALUES ('b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 'Java');
INSERT INTO public.skills VALUES ('e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 'Spring Boot');
INSERT INTO public.skills VALUES ('d29e5aff-c3e3-40c4-8440-8ddc7040bf0d', 'ReactJS');
INSERT INTO public.skills VALUES ('e1330bc9-5fe4-4e6d-a5f3-768670d8104a', 'NodeJS');
INSERT INTO public.skills VALUES ('b1779c35-b7c4-47bb-a69b-43131c084a0f', 'PostgreSQL');
INSERT INTO public.skills VALUES ('f69bf36f-a210-4b76-8008-a5a1fefc6b4b', 'Docker');
INSERT INTO public.skills VALUES ('ee3fd9dd-15b1-45e0-875d-ebd18a48db41', 'Kubernetes');
INSERT INTO public.skills VALUES ('984f6a6b-e625-449c-8eb4-526348f279ef', 'Redis');
INSERT INTO public.skills VALUES ('23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 'Communication');
INSERT INTO public.skills VALUES ('2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 'English');


--
-- TOC entry 5226 (class 0 OID 16769)
-- Dependencies: 235
-- Data for Name: system_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- TOC entry 5214 (class 0 OID 16555)
-- Dependencies: 223
-- Data for Name: user_roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.user_roles VALUES ('4071353e-5816-4746-a8b6-c0bc3113c44d', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-27 09:23:18.932331');
INSERT INTO public.user_roles VALUES ('e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-05-27 09:23:18.932331');
INSERT INTO public.user_roles VALUES ('721b1851-349a-48aa-acae-feed1c1843ed', '5f5350dc-a77f-4a68-88f8-69e27116aa8f', '2026-05-27 09:23:18.932331');
INSERT INTO public.user_roles VALUES ('92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', '0137e9bc-7ee4-463c-b760-1580ac17cdb6', '2026-05-27 09:23:18.932331');


--
-- TOC entry 5211 (class 0 OID 16513)
-- Dependencies: 220
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.users VALUES ('4071353e-5816-4746-a8b6-c0bc3113c44d', 'candidate@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Candidate User', '0900000001', NULL, 'Active', '2026-05-27 09:23:08.229887', '2026-05-27 09:23:08.229887');
INSERT INTO public.users VALUES ('e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'hr@recruitpro.com', '$2a$06$CTRpfzxL3P9C3r9yf7VxDOJC5N/0zHkJyb9UXUFID9WqDVJb/Xevm', 'HR User', '0900000002', NULL, 'Active', '2026-05-27 09:23:08.229887', '2026-05-27 09:23:08.229887');
INSERT INTO public.users VALUES ('721b1851-349a-48aa-acae-feed1c1843ed', 'manager@recruitpro.com', '$2a$06$XAl77ynjzT2/NgvgpqOIhuQZFPWF2OD62LwHiarMb0tsteso4xUe.', 'Manager User', '0900000003', NULL, 'Active', '2026-05-27 09:23:08.229887', '2026-05-27 09:23:08.229887');
INSERT INTO public.users VALUES ('92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'admin@recruitpro.com', '$2a$06$ZJJc7qoUG81fppCQLRBtU.Gun0NYxBfaWz8L6NAhvV59Jkui5RHZ2', 'System Admin', '0900000004', NULL, 'Active', '2026-05-27 09:23:08.229887', '2026-05-27 09:23:08.229887');
INSERT INTO public.users VALUES ('d8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', 'frontend@recruitpro.com', '$2a$06$X3N6Q3QW7jYqK4dGQx0xVuvY8f7uK7wP6k2x2Y5wq1pM9R7Z2xYy6', 'Frontend Candidate', '0900000011', NULL, 'Active', '2026-05-28 09:00:00', '2026-05-28 09:00:00');
INSERT INTO public.users VALUES ('a1b2c3d4-e5f6-4701-9802-abcdefabcdef', 'fullstack@recruitpro.com', '$2a$06$Qp0Hn3gV8u4Dq8g0g6Q3nO9hG8hQv6O1T6qgK3lW7pX1mB9cD2eF4', 'Full Stack Candidate', '0900000012', NULL, 'Active', '2026-05-28 09:05:00', '2026-05-28 09:05:00');
INSERT INTO public.users VALUES ('b2c3d4e5-f6a7-4802-9903-fedcbafedcba', 'devops@recruitpro.com', '$2a$06$J8kP2oL5sW1xV3nR6qT9uY0aB2cD4eF6gH8iJ0kL2mN4pQ6rS8tU', 'DevOps Candidate', '0900000013', NULL, 'Active', '2026-05-28 09:10:00', '2026-05-28 09:10:00');
INSERT INTO public.users VALUES ('c3d4e5f6-a7b8-4903-9a04-001122334455', 'ba@recruitpro.com', '$2a$06$V7nM2bQ9xT5cH1kL4pR8sW0yZ3aD6fG9hJ2kL5mN8pQ1rS4tU7vX', 'Business Analyst Candidate', '0900000014', NULL, 'Active', '2026-05-28 09:15:00', '2026-05-28 09:15:00');
INSERT INTO public.departments VALUES ('d1000000-0000-4000-8000-000000000001', 'Product', 'Product management and design department');
INSERT INTO public.departments VALUES ('d1000000-0000-4000-8000-000000000002', 'Data & Analytics', 'Data analysis, reporting, and business intelligence department');
INSERT INTO public.departments VALUES ('d1000000-0000-4000-8000-000000000003', 'Operations', 'Operations, support, and internal service excellence department');

INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000001', 'TypeScript');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000002', 'NextJS');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000003', 'C#');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000004', '.NET');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000005', 'AWS');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000006', 'Python');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000007', 'SQL');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000008', 'Power BI');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000009', 'Selenium');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000010', 'Figma');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000011', 'SEO');
INSERT INTO public.skills VALUES ('90000000-0000-4000-8000-000000000012', 'Excel');

INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000001', 'talent.lead@recruitpro.com', '$2a$06$CTRpfzxL3P9C3r9yf7VxDOJC5N/0zHkJyb9UXUFID9WqDVJb/Xevm', 'Talent Acquisition Lead', '0900000101', NULL, 'Active', '2026-05-28 08:00:00', '2026-05-28 08:00:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000002', 'recruiter.north@recruitpro.com', '$2a$06$CTRpfzxL3P9C3r9yf7VxDOJC5N/0zHkJyb9UXUFID9WqDVJb/Xevm', 'Recruiter North', '0900000102', NULL, 'Active', '2026-05-28 08:05:00', '2026-05-28 08:05:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000003', 'recruiter.south@recruitpro.com', '$2a$06$CTRpfzxL3P9C3r9yf7VxDOJC5N/0zHkJyb9UXUFID9WqDVJb/Xevm', 'Recruiter South', '0900000103', NULL, 'Active', '2026-05-28 08:10:00', '2026-05-28 08:10:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000004', 'eng.director@recruitpro.com', '$2a$06$XAl77ynjzT2/NgvgpqOIhuQZFPWF2OD62LwHiarMb0tsteso4xUe.', 'Engineering Director', '0900000104', NULL, 'Active', '2026-05-28 08:15:00', '2026-05-28 08:15:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000005', 'product.director@recruitpro.com', '$2a$06$XAl77ynjzT2/NgvgpqOIhuQZFPWF2OD62LwHiarMb0tsteso4xUe.', 'Product Director', '0900000105', NULL, 'Active', '2026-05-28 08:20:00', '2026-05-28 08:20:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000101', 'minh.nguyen@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Minh Nguyen', '0900000201', NULL, 'Active', '2026-05-28 09:20:00', '2026-05-28 09:20:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000102', 'linh.tran@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Linh Tran', '0900000202', NULL, 'Active', '2026-05-28 09:22:00', '2026-05-28 09:22:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000103', 'quang.pham@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Quang Pham', '0900000203', NULL, 'Active', '2026-05-28 09:24:00', '2026-05-28 09:24:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000104', 'thao.le@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Thao Le', '0900000204', NULL, 'Active', '2026-05-28 09:26:00', '2026-05-28 09:26:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000105', 'nam.ho@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Nam Ho', '0900000205', NULL, 'Active', '2026-05-28 09:28:00', '2026-05-28 09:28:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000106', 'an.vo@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'An Vo', '0900000206', NULL, 'Active', '2026-05-28 09:30:00', '2026-05-28 09:30:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000107', 'phuc.bui@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Phuc Bui', '0900000207', NULL, 'Active', '2026-05-28 09:32:00', '2026-05-28 09:32:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000108', 'mai.do@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Mai Do', '0900000208', NULL, 'Active', '2026-05-28 09:34:00', '2026-05-28 09:34:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000109', 'khoa.dang@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Khoa Dang', '0900000209', NULL, 'Active', '2026-05-28 09:36:00', '2026-05-28 09:36:00');
INSERT INTO public.users VALUES ('10000000-0000-4000-8000-000000000110', 'yen.pham@recruitpro.com', '$2a$06$kfKji00bWzacW3O75zipqeuZ.8ACaLYhuQZdGMcP6HGg4mbBqxlsG', 'Yen Pham', '0900000210', NULL, 'Active', '2026-05-28 09:38:00', '2026-05-28 09:38:00');

INSERT INTO public.user_roles VALUES ('d8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:00:00');
INSERT INTO public.user_roles VALUES ('a1b2c3d4-e5f6-4701-9802-abcdefabcdef', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:05:00');
INSERT INTO public.user_roles VALUES ('b2c3d4e5-f6a7-4802-9903-fedcbafedcba', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:10:00');
INSERT INTO public.user_roles VALUES ('c3d4e5f6-a7b8-4903-9a04-001122334455', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:15:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000001', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-05-28 08:00:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000002', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-05-28 08:05:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000003', 'e28e9442-682d-4e1a-b11f-663af56eb730', '2026-05-28 08:10:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000004', '5f5350dc-a77f-4a68-88f8-69e27116aa8f', '2026-05-28 08:15:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000005', '5f5350dc-a77f-4a68-88f8-69e27116aa8f', '2026-05-28 08:20:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000101', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:20:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000102', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:22:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000103', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:24:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000104', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:26:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000105', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:28:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000106', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:30:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000107', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:32:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000108', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:34:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000109', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:36:00');
INSERT INTO public.user_roles VALUES ('10000000-0000-4000-8000-000000000110', 'ef574bf3-08e1-4f25-932c-2d83ed8afd88', '2026-05-28 09:38:00');

INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000101', '10000000-0000-4000-8000-000000000101', 'Senior Java Developer', 4, 'Hanoi University of Science and Technology', 'Ha Noi', 'Backend engineer focused on Java microservices, clean APIs, and performance tuning.', 'https://cv.recruitpro.local/minh-nguyen.pdf', 'https://github.com/minh-nguyen-dev', 'https://linkedin.com/in/minh-nguyen-dev');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000102', '10000000-0000-4000-8000-000000000102', 'Frontend Engineer', 3, 'University of Engineering and Technology', 'Ha Noi', 'Frontend engineer building polished recruiter workflows with React and TypeScript.', 'https://cv.recruitpro.local/linh-tran.pdf', 'https://github.com/linh-tran-ui', 'https://linkedin.com/in/linh-tran-ui');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000103', '10000000-0000-4000-8000-000000000103', 'Data Analyst', 2, 'Foreign Trade University', 'Ho Chi Minh City', 'Data analyst with dashboard design, SQL modeling, and business reporting experience.', 'https://cv.recruitpro.local/quang-pham.pdf', 'https://github.com/quang-pham-data', 'https://linkedin.com/in/quang-pham-data');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000104', '10000000-0000-4000-8000-000000000104', 'QA Automation Engineer', 3, 'Posts and Telecommunications Institute of Technology', 'Da Nang', 'QA engineer automating regression suites and stabilizing release quality.', 'https://cv.recruitpro.local/thao-le.pdf', 'https://github.com/thao-le-qa', 'https://linkedin.com/in/thao-le-qa');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000105', '10000000-0000-4000-8000-000000000105', '.NET Backend Developer', 4, 'FPT University', 'Ho Chi Minh City', 'Backend developer delivering REST APIs with .NET, SQL, and cloud deployment practices.', 'https://cv.recruitpro.local/nam-ho.pdf', 'https://github.com/nam-ho-dotnet', 'https://linkedin.com/in/nam-ho-dotnet');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000106', '10000000-0000-4000-8000-000000000106', 'Product Designer', 3, 'RMIT Vietnam', 'Ho Chi Minh City', 'Product designer translating user research into production-ready flows and design systems.', 'https://cv.recruitpro.local/an-vo.pdf', 'https://github.com/an-vo-design', 'https://linkedin.com/in/an-vo-design');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000107', '10000000-0000-4000-8000-000000000107', 'Talent Acquisition Specialist', 2, 'National Economics University', 'Ha Noi', 'Recruitment operations specialist with sourcing, interview coordination, and candidate care experience.', 'https://cv.recruitpro.local/phuc-bui.pdf', 'https://github.com/phuc-bui-ops', 'https://linkedin.com/in/phuc-bui-ops');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000108', '10000000-0000-4000-8000-000000000108', 'Finance Analyst', 4, 'Academy of Finance', 'Ha Noi', 'Finance analyst experienced in planning, budgeting, and stakeholder reporting.', 'https://cv.recruitpro.local/mai-do.pdf', 'https://github.com/mai-do-finance', 'https://linkedin.com/in/mai-do-finance');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000109', '10000000-0000-4000-8000-000000000109', 'Cloud and DevOps Engineer', 6, 'Duy Tan University', 'Da Nang', 'DevOps engineer specializing in Kubernetes, AWS, CI/CD, and reliability engineering.', 'https://cv.recruitpro.local/khoa-dang.pdf', 'https://github.com/khoa-dang-cloud', 'https://linkedin.com/in/khoa-dang-cloud');
INSERT INTO public.candidate_profiles VALUES ('20000000-0000-4000-8000-000000000110', '10000000-0000-4000-8000-000000000110', 'Content Marketing Executive', 2, 'University of Social Sciences and Humanities', 'Ho Chi Minh City', 'Content marketer focused on SEO, campaign execution, and audience growth.', 'https://cv.recruitpro.local/yen-pham.pdf', 'https://github.com/yen-pham-content', 'https://linkedin.com/in/yen-pham-content');

INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000101', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000101', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000101', 'b1779c35-b7c4-47bb-a69b-43131c084a0f');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000101', '90000000-0000-4000-8000-000000000007');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000102', 'd29e5aff-c3e3-40c4-8440-8ddc7040bf0d');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000102', '90000000-0000-4000-8000-000000000001');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000102', '90000000-0000-4000-8000-000000000002');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000102', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000006');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000007');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000103', '90000000-0000-4000-8000-000000000008');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000103', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000104', '90000000-0000-4000-8000-000000000009');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000104', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000104', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000104', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000003');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000004');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000007');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000105', '90000000-0000-4000-8000-000000000005');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000106', '90000000-0000-4000-8000-000000000010');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000106', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000106', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000107', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000107', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000107', '90000000-0000-4000-8000-000000000012');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000108', '90000000-0000-4000-8000-000000000012');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000108', '90000000-0000-4000-8000-000000000007');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000108', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000109', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000109', 'ee3fd9dd-15b1-45e0-875d-ebd18a48db41');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000109', '90000000-0000-4000-8000-000000000005');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000109', '984f6a6b-e625-449c-8eb4-526348f279ef');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000110', '90000000-0000-4000-8000-000000000011');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000110', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2');
INSERT INTO public.candidate_skills VALUES ('20000000-0000-4000-8000-000000000110', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4');

INSERT INTO public.jobs (
    id,
    department_id,
    created_by,
    approved_by,
    title,
    short_pitch,
    description,
    requirements,
    benefits,
    location,
    work_mode,
    employment_type,
    min_experience_years,
    vacancy_count,
    salary_min,
    salary_max,
    deadline,
    status,
    created_at
) VALUES
(
    '30000000-0000-4000-8000-000000000001',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    '10000000-0000-4000-8000-000000000001',
    '10000000-0000-4000-8000-000000000004',
    'Senior Java Engineer',
    'Lead backend feature delivery for high-volume recruitment services.',
    'Design resilient Java services, mentor mid-level engineers, and improve service observability.',
    'Strong Java, Spring Boot, SQL, system design, and API security fundamentals.',
    'Quarterly bonus, hybrid policy, healthcare package, and annual learning budget.',
    'Ha Noi',
    'Hybrid',
    'FullTime',
    4,
    2,
    1800.00,
    2800.00,
    '2026-07-30 18:00:00',
    'Approved',
    '2026-05-31 08:30:00'
),
(
    '30000000-0000-4000-8000-000000000002',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    '10000000-0000-4000-8000-000000000002',
    '10000000-0000-4000-8000-000000000004',
    'QA Automation Engineer',
    'Own automation quality gates across web and API releases.',
    'Build regression suites, collaborate with developers, and raise release confidence for the platform.',
    'Selenium, API testing, Docker, communication, and release discipline.',
    'Remote work allowance, test lab budget, and flexible review cycles.',
    'Da Nang',
    'Hybrid',
    'FullTime',
    3,
    2,
    1100.00,
    1900.00,
    '2026-07-28 18:00:00',
    'Approved',
    '2026-05-31 09:00:00'
),
(
    '30000000-0000-4000-8000-000000000003',
    'd1000000-0000-4000-8000-000000000002',
    '10000000-0000-4000-8000-000000000002',
    '10000000-0000-4000-8000-000000000005',
    'Data Analyst',
    'Turn hiring and funnel data into clear business decisions.',
    'Model recruiting data, maintain dashboards, and present actionable insights to leaders.',
    'SQL, Power BI, Excel, statistics, and strong stakeholder communication.',
    'Performance bonus, hybrid office schedule, and mentoring from product leadership.',
    'Ho Chi Minh City',
    'Hybrid',
    'FullTime',
    2,
    1,
    1000.00,
    1700.00,
    '2026-07-22 18:00:00',
    'Approved',
    '2026-05-31 09:30:00'
),
(
    '30000000-0000-4000-8000-000000000004',
    'd1000000-0000-4000-8000-000000000001',
    '10000000-0000-4000-8000-000000000001',
    '10000000-0000-4000-8000-000000000005',
    'Product Designer',
    'Shape end-to-end candidate and recruiter experiences.',
    'Own product discovery artifacts, wireframes, handoff quality, and interface consistency.',
    'Figma, design systems, communication, and usability thinking.',
    'Modern design tooling, hybrid work, and product discovery exposure.',
    'Ho Chi Minh City',
    'Hybrid',
    'FullTime',
    3,
    1,
    1200.00,
    2000.00,
    '2026-07-24 18:00:00',
    'Approved',
    '2026-05-31 10:00:00'
),
(
    '30000000-0000-4000-8000-000000000005',
    '95db214f-5ce2-4e89-b350-93da747c7bdb',
    '10000000-0000-4000-8000-000000000003',
    '721b1851-349a-48aa-acae-feed1c1843ed',
    'Talent Acquisition Executive',
    'Scale candidate sourcing and scheduling for fast-growing teams.',
    'Source candidates, keep pipelines moving, and partner closely with hiring managers on interview operations.',
    'Communication, English, Excel, ATS discipline, and sourcing mindset.',
    'Clear promotion path, KPI bonus, and supportive team rituals.',
    'Ha Noi',
    'Onsite',
    'FullTime',
    2,
    2,
    800.00,
    1300.00,
    '2026-07-19 18:00:00',
    'Approved',
    '2026-05-31 10:30:00'
),
(
    '30000000-0000-4000-8000-000000000006',
    'cc05372d-9ee5-4962-a071-74a8d7630279',
    '10000000-0000-4000-8000-000000000001',
    NULL,
    'Finance Analyst',
    'Strengthen planning and business finance visibility for the company.',
    'Support budgeting, cost reporting, and operational finance analysis for leadership teams.',
    'Excel, SQL, financial modeling, and attention to reporting accuracy.',
    'Meal allowance, healthcare support, and clear annual review process.',
    'Ha Noi',
    'Onsite',
    'FullTime',
    3,
    1,
    900.00,
    1500.00,
    '2026-07-21 18:00:00',
    'PendingApproval',
    '2026-06-01 08:45:00'
),
(
    '30000000-0000-4000-8000-000000000007',
    'd1000000-0000-4000-8000-000000000001',
    '10000000-0000-4000-8000-000000000001',
    '10000000-0000-4000-8000-000000000005',
    'Product Manager',
    'Drive roadmap execution for recruiter productivity features.',
    'Lead discovery, prioritization, and release planning across product, design, and engineering.',
    'Product thinking, communication, analytics literacy, and stakeholder management.',
    'Leadership coaching, annual performance bonus, and modern work environment.',
    'Ha Noi',
    'Hybrid',
    'FullTime',
    5,
    1,
    1800.00,
    2600.00,
    '2026-05-25 18:00:00',
    'Closed',
    '2026-05-01 09:00:00'
),
(
    '30000000-0000-4000-8000-000000000008',
    'd1000000-0000-4000-8000-000000000003',
    '10000000-0000-4000-8000-000000000003',
    NULL,
    'Technical Support Specialist',
    'Prepare internal support coverage for the next platform rollout.',
    'Handle issue triage, knowledge base upkeep, and first-line support coordination.',
    'Communication, English, process ownership, and customer mindset.',
    'Shift allowance, onboarding roadmap, and structured coaching.',
    'Da Nang',
    'Onsite',
    'FullTime',
    1,
    2,
    650.00,
    1000.00,
    '2026-08-05 18:00:00',
    'Draft',
    '2026-06-01 09:15:00'
),
(
    '30000000-0000-4000-8000-000000000009',
    'd1000000-0000-4000-8000-000000000002',
    '10000000-0000-4000-8000-000000000002',
    '10000000-0000-4000-8000-000000000005',
    'Business Intelligence Analyst',
    'Build executive reporting across growth, hiring, and retention metrics.',
    'Maintain BI models, define core metrics, and automate recurring leadership reports.',
    'SQL, Power BI, Excel, business communication, and data quality awareness.',
    'Hybrid schedule, analytics guild, and cross-functional exposure.',
    'Ha Noi',
    'Hybrid',
    'FullTime',
    3,
    1,
    1300.00,
    2100.00,
    '2026-07-29 18:00:00',
    'Approved',
    '2026-06-01 10:00:00'
),
(
    '30000000-0000-4000-8000-000000000010',
    'fafe312f-0c2f-4e69-a009-4ae38fd1218f',
    '10000000-0000-4000-8000-000000000003',
    '10000000-0000-4000-8000-000000000004',
    '.NET Backend Developer',
    'Ship internal service APIs used by operations and finance teams.',
    'Develop .NET services, integrate internal workflows, and improve deployment reliability.',
    'C#, .NET, SQL, AWS, and REST API fundamentals.',
    'Cloud allowance, hybrid setup, and annual technical certification support.',
    'Ho Chi Minh City',
    'Hybrid',
    'FullTime',
    3,
    2,
    1400.00,
    2300.00,
    '2026-07-27 18:00:00',
    'Approved',
    '2026-06-01 10:30:00'
),
(
    '30000000-0000-4000-8000-000000000011',
    '3067eaeb-3893-468f-b854-08f1319448c9',
    '10000000-0000-4000-8000-000000000001',
    '10000000-0000-4000-8000-000000000005',
    'Content Marketing Executive',
    'Own SEO-driven content execution for employer branding and product growth.',
    'Plan content calendars, optimize landing pages, and report campaign performance.',
    'SEO, content writing, English, and communication skills.',
    'Creative budget, hybrid work model, and campaign ownership.',
    'Ho Chi Minh City',
    'Hybrid',
    'FullTime',
    2,
    1,
    850.00,
    1350.00,
    '2026-07-23 18:00:00',
    'Approved',
    '2026-06-01 11:00:00'
);

INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000001', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a', 4, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000001', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000001', '90000000-0000-4000-8000-000000000007', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000001', 'b1779c35-b7c4-47bb-a69b-43131c084a0f', 2, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000002', '90000000-0000-4000-8000-000000000009', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000002', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b', 1, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000002', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000007', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000008', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000003', '90000000-0000-4000-8000-000000000012', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000003', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 1, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000004', '90000000-0000-4000-8000-000000000010', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000004', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000004', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 1, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000005', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000005', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000005', '90000000-0000-4000-8000-000000000012', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000006', '90000000-0000-4000-8000-000000000012', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000006', '90000000-0000-4000-8000-000000000007', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000006', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 1, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000007', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 4, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000007', '90000000-0000-4000-8000-000000000008', 2, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000008', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 1, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000008', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 1, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000009', '90000000-0000-4000-8000-000000000007', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000009', '90000000-0000-4000-8000-000000000008', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000009', '90000000-0000-4000-8000-000000000012', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000009', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000010', '90000000-0000-4000-8000-000000000003', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000010', '90000000-0000-4000-8000-000000000004', 3, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000010', '90000000-0000-4000-8000-000000000007', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000010', '90000000-0000-4000-8000-000000000005', 1, false);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000011', '90000000-0000-4000-8000-000000000011', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000011', '23a4ab7e-04af-42b3-8f11-37bbeb036fb2', 2, true);
INSERT INTO public.job_skills VALUES ('30000000-0000-4000-8000-000000000011', '2d6e3402-1f74-449a-b4ea-8bb2470e4fa4', 1, false);

INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000101', '30000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000002', 'Accepted', '2026-06-01 08:00:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000102', '8c1b65c9-1c9f-4a36-9e6a-111111111111', '10000000-0000-4000-8000-000000000002', 'Interviewing', '2026-06-01 08:15:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000103', '30000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000001', 'ManagerReview', '2026-06-01 08:30:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000104', '30000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000003', 'Interviewing', '2026-06-01 08:45:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000105', '30000000-0000-4000-8000-000000000010', '10000000-0000-4000-8000-000000000002', 'Reviewing', '2026-06-01 09:00:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000106', '30000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000001', 'Accepted', '2026-06-01 09:10:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000107', '30000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000003', 'Interviewing', '2026-06-01 09:20:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000108', '30000000-0000-4000-8000-000000000006', NULL, 'Pending', '2026-06-01 09:25:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000109', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '10000000-0000-4000-8000-000000000002', 'Interviewing', '2026-06-01 09:30:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000010', '10000000-0000-4000-8000-000000000110', '30000000-0000-4000-8000-000000000011', '10000000-0000-4000-8000-000000000003', 'Interviewing', '2026-06-01 09:35:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000011', 'a1b2c3d4-e5f6-4701-9802-abcdefabcdef', '30000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000001', 'ManagerReview', '2026-06-01 09:40:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000012', 'b2c3d4e5-f6a7-4802-9903-fedcbafedcba', '7c2d3e4f-5a6b-4c7d-8e9f-000000000002', '10000000-0000-4000-8000-000000000001', 'Accepted', '2026-06-01 09:45:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000013', 'c3d4e5f6-a7b8-4903-9a04-001122334455', '7c2d3e4f-5a6b-4c7d-8e9f-000000000003', '10000000-0000-4000-8000-000000000003', 'Accepted', '2026-06-01 09:50:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000014', '4071353e-5816-4746-a8b6-c0bc3113c44d', '8c1b65c9-1c9f-4a36-9e6a-111111111112', '10000000-0000-4000-8000-000000000002', 'Reviewing', '2026-06-01 09:55:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000015', 'd8f2b5c8-44f7-4f8d-8c4a-5a1f4c2e1001', '7c2d3e4f-5a6b-4c7d-8e9f-000000000001', '10000000-0000-4000-8000-000000000002', 'Reviewing', '2026-06-01 10:00:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000016', '10000000-0000-4000-8000-000000000103', '30000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000001', 'Interviewing', '2026-06-01 10:05:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000017', '10000000-0000-4000-8000-000000000108', '30000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000003', 'Reviewing', '2026-06-01 10:10:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000018', '10000000-0000-4000-8000-000000000107', '7c2d3e4f-5a6b-4c7d-8e9f-000000000003', '10000000-0000-4000-8000-000000000003', 'Accepted', '2026-06-01 10:15:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000019', '10000000-0000-4000-8000-000000000106', '7c2d3e4f-5a6b-4c7d-8e9f-000000000004', '10000000-0000-4000-8000-000000000003', 'Reviewing', '2026-06-01 10:20:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000020', '10000000-0000-4000-8000-000000000109', '30000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000002', 'Rejected', '2026-06-01 10:25:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000021', '10000000-0000-4000-8000-000000000102', '30000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000001', 'Rejected', '2026-06-01 10:30:00');
INSERT INTO public.applications VALUES ('40000000-0000-4000-8000-000000000022', '10000000-0000-4000-8000-000000000110', '7c2d3e4f-5a6b-4c7d-8e9f-000000000004', '10000000-0000-4000-8000-000000000003', 'Accepted', '2026-06-01 10:35:00');

INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000001', '40000000-0000-4000-8000-000000000001', '2026-06-03 09:00:00', 'Online', 'https://meet.google.com/rp-java-final', NULL, 'Final technical round completed successfully.', 'Completed');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000002', '40000000-0000-4000-8000-000000000002', '2026-06-10 14:00:00', 'Online', 'https://meet.google.com/rp-frontend-1', NULL, 'Portfolio walkthrough and frontend architecture discussion.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000003', '40000000-0000-4000-8000-000000000003', '2026-06-11 09:30:00', 'Offline', NULL, 'RecruitPro HCM Office', 'Manager review session with product and analytics stakeholders.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000004', '40000000-0000-4000-8000-000000000004', '2026-06-09 10:00:00', 'Online', 'https://meet.google.com/rp-qa-automation', NULL, 'Automation strategy discussion.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000005', '40000000-0000-4000-8000-000000000006', '2026-06-02 15:00:00', 'Offline', NULL, 'RecruitPro HCM Office', 'Design case study review completed.', 'Completed');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000006', '40000000-0000-4000-8000-000000000007', '2026-06-12 09:00:00', 'Offline', NULL, 'RecruitPro Ha Noi Office', 'Culture and process interview with HR leadership.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000007', '40000000-0000-4000-8000-000000000009', '2026-06-12 16:00:00', 'Online', 'https://meet.google.com/rp-devops-cloud', NULL, 'Cloud architecture and incident response discussion.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000008', '40000000-0000-4000-8000-000000000010', '2026-06-13 10:00:00', 'Online', 'https://meet.google.com/rp-content-final', NULL, 'Content strategy and campaign planning round.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000009', '40000000-0000-4000-8000-000000000012', '2026-06-04 11:00:00', 'Online', 'https://meet.google.com/rp-devops-final', NULL, 'Offer readiness confirmed after technical closeout.', 'Completed');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000010', '40000000-0000-4000-8000-000000000013', '2026-06-03 13:30:00', 'Offline', NULL, 'RecruitPro Ha Noi Office', 'Operations and process ownership interview completed.', 'Completed');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000011', '40000000-0000-4000-8000-000000000016', '2026-06-13 14:30:00', 'Online', 'https://meet.google.com/rp-bi-round1', NULL, 'Dashboard case review and stakeholder storytelling assessment.', 'Scheduled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000012', '40000000-0000-4000-8000-000000000020', '2026-06-03 08:30:00', 'Online', 'https://meet.google.com/rp-java-screen', NULL, 'Candidate did not meet backend depth expected for the role.', 'Canceled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000013', '40000000-0000-4000-8000-000000000021', '2026-06-02 17:00:00', 'Online', 'https://meet.google.com/rp-design-screen', NULL, 'Candidate profile was redirected after initial discussion.', 'Canceled');
INSERT INTO public.interviews VALUES ('50000000-0000-4000-8000-000000000014', '75afffb4-ad67-4974-807c-0308a90f07bf', '2026-06-06 09:30:00', 'Online', 'https://meet.google.com/sample-room-final', NULL, 'Follow-up technical round added after initial screening.', 'Scheduled');

INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000101', 'Application accepted', 'Your application for Senior Java Engineer has progressed to offer preparation.', 'Application', true, '2026-06-03 10:00:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000102', 'Interview scheduled', 'Your interview for Senior Frontend Developer is scheduled on 2026-06-10 at 14:00.', 'Interview', false, '2026-06-04 08:00:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000103', 'Manager review', 'Your Data Analyst application has moved to manager review.', 'Application', false, '2026-06-04 08:10:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000104', 'Interview scheduled', 'Your QA Automation Engineer interview is scheduled on 2026-06-09 at 10:00.', 'Interview', false, '2026-06-04 08:15:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000105', 'Application in review', 'Your .NET Backend Developer application is under recruiter review.', 'Application', false, '2026-06-04 08:20:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000106', 'Application accepted', 'You passed the Product Designer process and HR will contact you for the offer.', 'Application', true, '2026-06-03 16:00:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000107', 'Interview scheduled', 'Your Talent Acquisition Executive interview is scheduled on 2026-06-12 at 09:00.', 'Interview', false, '2026-06-04 08:25:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000108', 'Application received', 'We received your Finance Analyst application and will review it soon.', 'Application', false, '2026-06-04 08:30:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000109', 'Interview scheduled', 'Your DevOps Engineer interview is scheduled on 2026-06-12 at 16:00.', 'Interview', false, '2026-06-04 08:35:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000010', '10000000-0000-4000-8000-000000000110', 'Content Marketing interview', 'Your Content Marketing Executive interview is scheduled on 2026-06-13 at 10:00.', 'Interview', false, '2026-06-04 08:40:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000011', '10000000-0000-4000-8000-000000000001', 'New approved jobs', 'Five new approved jobs went live this morning. Monitor inbound applications.', 'Job', true, '2026-06-01 11:30:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000012', '10000000-0000-4000-8000-000000000002', 'Candidate pipeline updated', 'Three engineering applications moved to reviewing and interviewing today.', 'Application', false, '2026-06-04 09:00:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000013', '10000000-0000-4000-8000-000000000003', 'Interview backlog', 'Two HR-related interview schedules still need confirmation from candidates.', 'Interview', false, '2026-06-04 09:05:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000014', '10000000-0000-4000-8000-000000000004', 'Approval requested', 'Finance Analyst is waiting for manager approval.', 'Job', false, '2026-06-04 09:10:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000015', '10000000-0000-4000-8000-000000000005', 'Dashboard update', 'Data Analyst and Product Designer pipelines both have active finalists.', 'Application', true, '2026-06-04 09:15:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000016', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'New applications received', 'Engineering and marketing jobs received 6 new applications today.', 'Application', false, '2026-06-04 09:20:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000017', '721b1851-349a-48aa-acae-feed1c1843ed', 'Jobs pending review', 'One finance job is pending approval and one product role is already closed.', 'Job', false, '2026-06-04 09:25:00');
INSERT INTO public.notifications VALUES ('60000000-0000-4000-8000-000000000018', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'System usage summary', 'Seed data now includes active records across candidate, job, interview, and audit tables.', 'System', true, '2026-06-04 09:30:00');

INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000001', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'rt_candidate_20260604_0001', '2026-07-04 09:00:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000002', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'rt_hr_20260604_0001', '2026-07-04 09:05:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000003', '721b1851-349a-48aa-acae-feed1c1843ed', 'rt_manager_20260604_0001', '2026-07-04 09:10:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000004', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'rt_admin_20260604_0001', '2026-07-04 09:15:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000001', 'rt_talentlead_20260604_0001', '2026-07-04 09:20:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000002', 'rt_recruiternorth_20260604_0001', '2026-07-04 09:25:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000003', 'rt_recruitersouth_20260604_0001', '2026-07-04 09:30:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000004', 'rt_engdirector_20260604_0001', '2026-07-04 09:35:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000005', 'rt_productdirector_20260604_0001', '2026-07-04 09:40:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000010', '10000000-0000-4000-8000-000000000101', 'rt_minh_20260604_0001', '2026-07-04 09:45:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000011', '10000000-0000-4000-8000-000000000102', 'rt_linh_20260604_0001', '2026-07-04 09:50:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000012', '10000000-0000-4000-8000-000000000103', 'rt_quang_20260604_0001', '2026-07-04 09:55:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000013', '10000000-0000-4000-8000-000000000104', 'rt_thao_20260604_0001', '2026-07-04 10:00:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000014', '10000000-0000-4000-8000-000000000109', 'rt_khoa_20260604_0001', '2026-07-04 10:05:00');
INSERT INTO public.refresh_tokens VALUES ('70000000-0000-4000-8000-000000000015', '10000000-0000-4000-8000-000000000110', 'rt_yen_20260604_0001', '2026-07-04 10:10:00');

INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000001', 'JOB_CREATED', 'Created Senior Java Engineer job posting.', '2026-05-31 08:31:00');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000004', 'JOB_APPROVED', 'Approved Senior Java Engineer job posting.', '2026-05-31 08:40:00');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000002', 'JOB_CREATED', 'Created QA Automation Engineer job posting.', '2026-05-31 09:01:00');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000005', 'JOB_APPROVED', 'Approved Data Analyst job posting.', '2026-05-31 09:35:00');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000005', '10000000-0000-4000-8000-000000000101', 'APPLICATION_SUBMITTED', 'Submitted application for Senior Java Engineer.', '2026-06-01 08:00:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000006', '10000000-0000-4000-8000-000000000102', 'APPLICATION_SUBMITTED', 'Submitted application for Senior Frontend Developer.', '2026-06-01 08:15:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000103', 'APPLICATION_SUBMITTED', 'Submitted application for Data Analyst.', '2026-06-01 08:30:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000008', '10000000-0000-4000-8000-000000000003', 'INTERVIEW_SCHEDULED', 'Scheduled QA Automation Engineer interview for Thao Le.', '2026-06-04 08:15:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000003', 'INTERVIEW_SCHEDULED', 'Scheduled Content Marketing Executive interview for Yen Pham.', '2026-06-04 08:40:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000010', '10000000-0000-4000-8000-000000000004', 'JOB_REVIEWED', 'Marked Finance Analyst job as waiting for approval feedback.', '2026-06-04 09:10:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000011', '10000000-0000-4000-8000-000000000005', 'PIPELINE_REVIEWED', 'Reviewed finalist pipeline for Product Designer and Data Analyst.', '2026-06-04 09:15:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000012', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'DASHBOARD_VIEWED', 'Viewed HR dashboard after daily application sync.', '2026-06-04 09:20:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000013', '721b1851-349a-48aa-acae-feed1c1843ed', 'APPROVAL_QUEUE_VIEWED', 'Reviewed pending approval queue.', '2026-06-04 09:25:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000014', '92e1a5c1-d3bd-4512-b1df-c6d69d4a41e0', 'SYSTEM_AUDIT_VIEWED', 'Viewed system usage summary and audit coverage.', '2026-06-04 09:30:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000015', '10000000-0000-4000-8000-000000000106', 'PROFILE_UPDATED', 'Updated product designer portfolio links.', '2026-06-02 18:05:00');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000016', '10000000-0000-4000-8000-000000000109', 'PROFILE_UPDATED', 'Updated cloud and DevOps resume information.', '2026-06-02 19:10:00');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000017', '10000000-0000-4000-8000-000000000001', 'JOB_CREATED', 'Created Content Marketing Executive job posting.', '2026-06-01 11:00:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000018', '10000000-0000-4000-8000-000000000005', 'JOB_APPROVED', 'Approved Content Marketing Executive job posting.', '2026-06-01 11:05:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000019', '10000000-0000-4000-8000-000000000107', 'APPLICATION_SUBMITTED', 'Submitted application for HR Operations Specialist.', '2026-06-01 10:15:10');
INSERT INTO public.system_logs VALUES ('80000000-0000-4000-8000-000000000020', '10000000-0000-4000-8000-000000000110', 'APPLICATION_SUBMITTED', 'Submitted application for Marketing Specialist.', '2026-06-01 10:35:10');


--
-- TOC entry 5036 (class 2606 OID 16702)
-- Name: applications applications_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.applications
    ADD CONSTRAINT applications_pkey PRIMARY KEY (id);


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

