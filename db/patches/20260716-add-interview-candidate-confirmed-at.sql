-- ============================================================================
-- 20260716-add-interview-candidate-confirmed-at.sql
-- Thêm cột interviews.candidate_confirmed_at cho DATABASE ĐANG TỒN TẠI.
-- (DB mới tạo bằng docker compose đã có sẵn cột này từ init.sql.)
-- Idempotent: ADD COLUMN IF NOT EXISTS — chạy lại bao nhiêu lần cũng an toàn.
--
-- candidate_confirmed_at ghi nhận thời điểm ứng viên xác nhận tham dự buổi
-- phỏng vấn (quyền interview:confirm-own, endpoint
-- POST /api/candidate/interviews/{id}/confirm). NULL = chưa xác nhận.
-- HR xem giá trị này để biết lịch đã được ứng viên ghi nhận hay chưa.
--
-- Cách chạy:
--   docker exec -i recruitpro_postgres psql -U postgres -d recruit_pro < db/patches/20260716-add-interview-candidate-confirmed-at.sql
-- ============================================================================

ALTER TABLE public.interviews
    ADD COLUMN IF NOT EXISTS candidate_confirmed_at timestamp without time zone;
