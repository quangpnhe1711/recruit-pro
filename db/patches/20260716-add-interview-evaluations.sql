-- ============================================================================
-- 20260716-add-interview-evaluations.sql
-- Tạo bảng interview_evaluations (scorecard đánh giá sau phỏng vấn) cho
-- DATABASE ĐANG TỒN TẠI.
-- (DB mới tạo bằng docker compose đã có sẵn bảng này từ init.sql.)
-- Idempotent: CREATE TABLE IF NOT EXISTS — chạy lại bao nhiêu lần cũng an toàn.
--
-- Mỗi buổi phỏng vấn có tối đa MỘT bản đánh giá (UNIQUE interview_id):
--   - 4 tiêu chí chấm 1..5: technical / communication / problem_solving / culture_fit
--   - overall_score 0..100 (suy ra từ tổng tiêu chí, lưu sẵn để hiển thị danh sách)
--   - recommendation: StrongHire | Hire | NoHire | StrongNoHire
--   - strengths / concerns / notes: nhận xét chi tiết
-- HR/Manager ghi nhận sau khi interview Completed (endpoint
-- PUT /api/hr/interviews/{id}/evaluation) — lấp khoảng trống "Completed xong
-- là hết" trước khi ra quyết định Offer / Rejected.
--
-- Cách chạy:
--   docker exec -i recruitpro_postgres psql -U postgres -d recruit_pro < db/patches/20260716-add-interview-evaluations.sql
-- ============================================================================

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
