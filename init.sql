
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

CREATE TYPE public.application_status AS ENUM (
    'Pending',
    'Reviewing',
    'Interviewing',
    'ManagerReview',
    'Accepted',
    'Rejected'
);


ALTER TYPE public.application_status OWNER TO postgres;

--
-- TOC entry 911 (class 1247 OID 16410)
-- Name: employment_type; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.employment_type AS ENUM (
    'FullTime',
    'PartTime',
    'Remote',
    'Hybrid',
    'Internship',
    'Contract'
);


ALTER TYPE public.employment_type OWNER TO postgres;

--
-- TOC entry 917 (class 1247 OID 16438)
-- Name: interview_status; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.interview_status AS ENUM (
    'Scheduled',
    'Completed',
    'Cancelled'
);


ALTER TYPE public.interview_status OWNER TO postgres;

--
-- TOC entry 908 (class 1247 OID 16398)
-- Name: job_status; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.job_status AS ENUM (
    'Draft',
    'PendingApproval',
    'Approved',
    'Closed',
    'Rejected'
);


ALTER TYPE public.job_status OWNER TO postgres;

--
-- TOC entry 920 (class 1247 OID 16446)
-- Name: meeting_type; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.meeting_type AS ENUM (
    'Online',
    'Offline'
);


ALTER TYPE public.meeting_type OWNER TO postgres;

--
-- TOC entry 923 (class 1247 OID 16452)
-- Name: notification_type; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.notification_type AS ENUM (
    'System',
    'Job',
    'Interview',
    'Application'
);


ALTER TYPE public.notification_type OWNER TO postgres;

--
-- TOC entry 905 (class 1247 OID 16390)
-- Name: user_status; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.user_status AS ENUM (
    'Active',
    'Inactive',
    'Blocked'
);


ALTER TYPE public.user_status OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 231 (class 1259 OID 16692)
-- Name: applications; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.applications (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    candidate_id uuid NOT NULL,
    job_id uuid NOT NULL,
    reviewed_by uuid,
    status public.application_status DEFAULT 'Pending'::public.application_status,
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
    meeting_type public.meeting_type,
    meeting_link text,
    location text,
    notes text,
    status public.interview_status DEFAULT 'Scheduled'::public.interview_status
);


ALTER TABLE public.interviews OWNER TO postgres;

--
-- TOC entry 230 (class 1259 OID 16675)
-- Name: job_skills; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.job_skills (
    job_id uuid NOT NULL,
    skill_id uuid NOT NULL
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
    description text,
    requirements text,
    location character varying(255),
    salary_min numeric(15,2),
    salary_max numeric(15,2),
    employment_type public.employment_type,
    deadline timestamp without time zone,
    status public.job_status DEFAULT 'Draft'::public.job_status,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT salary_check CHECK (((salary_max IS NULL) OR (salary_min IS NULL) OR (salary_max >= salary_min)))
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
    type public.notification_type,
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
    status public.user_status DEFAULT 'Active'::public.user_status,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.users OWNER TO postgres;

--
-- TOC entry 5222 (class 0 OID 16692)
-- Dependencies: 231
-- Data for Name: applications; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.applications VALUES ('75afffb4-ad67-4974-807c-0308a90f07bf', '366cb75c-aecd-4f54-aedb-b76cf475d81a', '59a5221b-43bb-489f-83b7-d41c347e37f9', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', 'Reviewing', '2026-05-27 09:23:30.793214');


--
-- TOC entry 5217 (class 0 OID 16601)
-- Dependencies: 226
-- Data for Name: candidate_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.candidate_profiles VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', '4071353e-5816-4746-a8b6-c0bc3113c44d', 'Backend Developer', 2, 'FPT University', 'Ha Noi', 'Java backend developer', NULL, 'https://github.com/candidate', 'https://linkedin.com/in/candidate');


--
-- TOC entry 5219 (class 0 OID 16629)
-- Dependencies: 228
-- Data for Name: candidate_skills; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a');
INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67');
INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'b1779c35-b7c4-47bb-a69b-43131c084a0f');
INSERT INTO public.candidate_skills VALUES ('366cb75c-aecd-4f54-aedb-b76cf475d81a', 'f69bf36f-a210-4b76-8008-a5a1fefc6b4b');


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


--
-- TOC entry 5221 (class 0 OID 16675)
-- Dependencies: 230
-- Data for Name: job_skills; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.job_skills VALUES ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'b2f56bd0-c4b6-4442-b6e4-d341563c1e7a');
INSERT INTO public.job_skills VALUES ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'e70f15bc-9e3c-4dc1-92db-4b53028d5c67');
INSERT INTO public.job_skills VALUES ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'b1779c35-b7c4-47bb-a69b-43131c084a0f');


--
-- TOC entry 5220 (class 0 OID 16646)
-- Dependencies: 229
-- Data for Name: jobs; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.jobs VALUES ('59a5221b-43bb-489f-83b7-d41c347e37f9', 'fafe312f-0c2f-4e69-a009-4ae38fd1218f', 'e781ccd9-e6f8-4ce1-b15d-e142f8977a4e', '721b1851-349a-48aa-acae-feed1c1843ed', 'Java Backend Developer', 'Develop backend services using Spring Boot', 'Java, Spring Boot, PostgreSQL', 'Ha Noi', 1000.00, 2000.00, 'FullTime', '2026-06-26 09:23:30.793214', 'Approved', '2026-05-27 09:23:30.793214');


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
INSERT INTO public.permissions VALUES ('f0515aef-c29c-42d6-add4-9ef95b92da6d', 'View Jobs', 'JOB_VIEW', 'View jobs');
INSERT INTO public.permissions VALUES ('b98f8544-bce4-442d-ac70-ae5f942c4db7', 'Create Job', 'JOB_CREATE', 'Create jobs');
INSERT INTO public.permissions VALUES ('51d0f144-59ca-4b9b-b7b9-7a01b36d3c68', 'Update Job', 'JOB_UPDATE', 'Update jobs');
INSERT INTO public.permissions VALUES ('80c0ba49-f562-42b9-b09d-533db4b394a5', 'Delete Job', 'JOB_DELETE', 'Delete jobs');
INSERT INTO public.permissions VALUES ('eabc0d36-032d-4eb2-81f7-6d9a50c82b42', 'Approve Job', 'JOB_APPROVE', 'Approve jobs');
INSERT INTO public.permissions VALUES ('b33f762b-1a44-4292-9b88-1ce34f5387e8', 'View Applications', 'APPLICATION_VIEW', 'View applications');
INSERT INTO public.permissions VALUES ('3130c14f-bfe8-4d5f-84ac-7e6886104070', 'Apply Job', 'APPLICATION_APPLY', 'Apply jobs');
INSERT INTO public.permissions VALUES ('bdf8cb0d-b58d-400c-ad47-064f88054275', 'Review Application', 'APPLICATION_REVIEW', 'Review applications');
INSERT INTO public.permissions VALUES ('7d33a32f-737a-4d94-9271-3ce4bba57659', 'View Interviews', 'INTERVIEW_VIEW', 'View interviews');
INSERT INTO public.permissions VALUES ('b410e0de-f182-4288-b200-9b0eed42424d', 'Create Interview', 'INTERVIEW_CREATE', 'Create interviews');
INSERT INTO public.permissions VALUES ('753339d4-9adf-4fc5-b80f-268f77ca0849', 'Update Interview', 'INTERVIEW_UPDATE', 'Update interviews');
INSERT INTO public.permissions VALUES ('ee23f4cf-3ce2-4b43-a28a-8385ceb76e00', 'View Candidate Profile', 'CANDIDATE_PROFILE_VIEW', 'View candidate profile');
INSERT INTO public.permissions VALUES ('52b771f7-e9b4-4502-a7a0-afab7f1bd715', 'Update Candidate Profile', 'CANDIDATE_PROFILE_UPDATE', 'Update candidate profile');
INSERT INTO public.permissions VALUES ('4d8b48b2-6fc8-4f68-9d9d-3c84e6621f87', 'View Departments', 'DEPARTMENT_VIEW', 'View departments');
INSERT INTO public.permissions VALUES ('6ca18f1f-43ff-4602-a617-23d8543077a6', 'Manage Departments', 'DEPARTMENT_MANAGE', 'Manage departments');
INSERT INTO public.permissions VALUES ('b0a5f45f-53f3-4e09-9b7c-9d713135fe91', 'View Skills', 'SKILL_VIEW', 'View skills');
INSERT INTO public.permissions VALUES ('fc5bbc69-f2ac-48e3-b54a-5aac96c1d062', 'Manage Skills', 'SKILL_MANAGE', 'Manage skills');
INSERT INTO public.permissions VALUES ('99ea073d-fb48-45cc-9e5a-e07c43a0b8fa', 'View Notifications', 'NOTIFICATION_VIEW', 'View notifications');
INSERT INTO public.permissions VALUES ('cdda07cb-20f5-4b82-9842-dfd65057c425', 'View Logs', 'SYSTEM_LOG_VIEW', 'View system logs');


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
-- Name: applications applications_candidate_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.applications
    ADD CONSTRAINT applications_candidate_id_fkey FOREIGN KEY (candidate_id) REFERENCES public.candidate_profiles(id) ON DELETE CASCADE;


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


