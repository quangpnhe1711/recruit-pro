-- ============================================================================
-- 20260717-add-interview-interviewer-and-duration.sql
-- Thêm 2 cột cho interviews trên DATABASE ĐANG TỒN TẠI:
--   - interviewer_id  : người phỏng vấn (HR/Manager) được gán cho buổi phỏng vấn.
--   - duration_minutes: thời lượng buổi phỏng vấn (phút), mặc định 60.
-- (DB mới tạo bằng docker compose đã có sẵn 2 cột này từ init.sql.)
-- Idempotent: ADD COLUMN IF NOT EXISTS + guard cho FK/index — chạy lại an toàn.
--
-- Trước đây interviewer/duration được UI gửi lên nhưng KHÔNG được lưu; lịch bận
-- cũng dùng chung (global) cho mọi interviewer. Sau patch này backend lưu đúng
-- người phỏng vấn + thời lượng, dựng lịch bận theo từng interviewer và chặn
-- double-book theo khung giờ thực tế.
--
-- Cách chạy:
--   docker exec -i recruitpro_postgres psql -U postgres -d recruit_pro < db/patches/20260717-add-interview-interviewer-and-duration.sql
-- ============================================================================

ALTER TABLE public.interviews
    ADD COLUMN IF NOT EXISTS interviewer_id uuid,
    ADD COLUMN IF NOT EXISTS duration_minutes integer NOT NULL DEFAULT 60;

-- The entity maps duration_minutes as a non-nullable int (NOT NULL in the EF/EnsureCreated schema).
-- Enforce the same on any DB where an earlier run of this patch added the column as nullable.
UPDATE public.interviews SET duration_minutes = 60 WHERE duration_minutes IS NULL;
ALTER TABLE public.interviews ALTER COLUMN duration_minutes SET DEFAULT 60;
ALTER TABLE public.interviews ALTER COLUMN duration_minutes SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'interviews_interviewer_id_fkey'
    ) THEN
        ALTER TABLE public.interviews
            ADD CONSTRAINT interviews_interviewer_id_fkey
            FOREIGN KEY (interviewer_id) REFERENCES public.users(id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_interviews_interviewer_id
    ON public.interviews (interviewer_id);
