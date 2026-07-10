-- ============================================================================
-- 20260711-add-user-token-version.sql
-- Thêm cột users.token_version cho DATABASE ĐANG TỒN TẠI.
-- (DB mới tạo bằng docker compose đã có sẵn cột này từ init.sql.)
-- Idempotent: ADD COLUMN IF NOT EXISTS — chạy lại bao nhiêu lần cũng an toàn.
--
-- token_version là "thế hệ" token của tài khoản: mỗi access token phát ra nhúng
-- giá trị hiện tại; khi vô hiệu hóa tài khoản, giá trị này +1 nên mọi access token
-- đã phát ra lập tức fail (JwtExtension.OnTokenValidated) — vô hiệu hóa có hiệu lực
-- ngay, không phải chờ token hết hạn.
--
-- Cách chạy:
--   docker exec -i recruitpro_postgres psql -U postgres -d recruit_pro < db/patches/20260711-add-user-token-version.sql
-- ============================================================================

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS token_version integer NOT NULL DEFAULT 0;
