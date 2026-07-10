# RecruitPro — Hệ thống tuyển dụng (PRN232)

RecruitPro là một hệ thống tuyển dụng end-to-end: đăng tin → duyệt tin → ứng viên nộp hồ sơ → sàng lọc
(AI Copilot xếp hạng CV) → phỏng vấn → gửi offer → tuyển. Backend ASP.NET Core (kiến trúc onion nhiều
tầng) + client React/TypeScript + một service gRPC riêng để chấm điểm.

- **Backend (repo này):** `RecruitProInternal/` — ASP.NET Core 8, EF Core, PostgreSQL, gRPC, OData, JWT.
- **Frontend:** `recruit-pro-internal/` — React + Vite + TypeScript + Redux Toolkit.

> Tài liệu nộp kèm: **[ERD](docs/erd/RecruitPro-ERD.md)** · **[Postman collection](docs/postman/)** ·
> **[Sơ đồ luồng (diagrams)](docs/diagrams/)** · **[Tài khoản mẫu](docs/SAMPLE-ACCOUNTS.md)** ·
> **[Tổng quan hệ thống](docs/overview/)**.

---

## 1. Tech stack

| Tầng | Công nghệ |
|---|---|
| API | ASP.NET Core 8 (Controllers), RESTful, OData 8, XML/JSON content negotiation |
| Kiến trúc | Onion: `Domain → Application → Infrastructure → API` (controller mỏng, service, repository + Unit of Work) |
| ORM / DB | EF Core 8 + **PostgreSQL** (Fluent API, 8 migration + `init.sql`) |
| Service-to-service | **gRPC** — `RecruitPro.ScoringService` (chấm điểm match ứng viên) |
| Auth | JWT (access 15 phút + `token_version`) + refresh token xoay vòng lưu DB (hash SHA-256) |
| Bổ trợ | AutoMapper, FluentValidation, global exception middleware, response envelope, SSE realtime notification, MinIO (lưu file), AI Copilot (xếp hạng CV / semantic) |
| Frontend | React 18, Vite, TypeScript, Redux Toolkit, Axios |

## 2. Cấu trúc solution (backend)

```
RecruitProInternal/
├── RecruitPro.API/            # Controllers, Program.cs, filters, middlewares, OData model
├── RecruitPro.Application/    # Services, DTOs, interfaces, mappings, validators (business logic)
├── RecruitPro.Infrastructure/ # EF Core DbContext, repositories, JWT, MinIO, email, migrations
├── RecruitPro.Domain/         # Entities, enums, workflow state machines, constants (không phụ thuộc)
├── RecruitPro.ScoringService/ # gRPC service độc lập (Protos/scoring.proto)
├── RecruitPro.Tests/          # xUnit unit + integration tests (Testcontainers Postgres)
├── init.sql                   # Schema + seed dữ liệu mẫu (mount vào postgres container)
├── db/patches/                # Patch SQL idempotent cho DB đang tồn tại
├── docker-compose.yml         # backend + scoring + postgres + minio
└── docs/                      # ERD, Postman, diagrams, overview, audit
```

## 3. Yêu cầu môi trường
- .NET SDK 8+ (`dotnet --version`)
- Docker + Docker Compose (chạy Postgres, MinIO, và toàn hệ nếu muốn)
- Node.js 18+ (chạy frontend)

## 4. Chạy dự án

### Cách A — Dev nhanh (khuyên dùng khi phát triển)
1. **Hạ tầng (Postgres + MinIO)** — từ thư mục `RecruitProInternal/`:
   ```bash
   docker compose up -d postgres minio
   ```
   Postgres tự nạp `init.sql` (schema + tài khoản mẫu) ở lần khởi tạo volume đầu tiên. Postgres lắng nghe
   `localhost:5433`, MinIO `localhost:9000` (console `9001`).
2. **Backend API:**
   ```bash
   dotnet run --project RecruitPro.API
   ```
   API chạy tại `http://localhost:5013` — Swagger UI: `http://localhost:5013/swagger`.
   (gRPC ScoringService: `dotnet run --project RecruitPro.ScoringService` nếu cần chấm điểm external.)
3. **Frontend** — từ thư mục `recruit-pro-internal/`:
   ```bash
   npm install
   npm run dev
   ```
   FE chạy tại `http://localhost:5173`. Base URL API lấy từ `.env.development` (`VITE_API_BASE_URL`).

### Cách B — Toàn bộ bằng Docker
```bash
cd RecruitProInternal
docker compose up --build
```
API: `http://localhost:5000` (map 8080 trong container) · Postgres `5433` · MinIO `9000/9001`.
> `docker-compose.yml` nạp secret production qua `env_file: /opt/recruitpro/api.env` (xem §5). Khi chạy
> local, hoặc tạo file đó với các biến ở §5, hoặc dùng **Cách A** (`dotnet run` đọc `appsettings.json`).

## 5. Cấu hình & bí mật (secrets)
Không commit secret thật. `appsettings.json` chỉ chứa **placeholder dev**; môi trường thật override bằng
biến môi trường (dấu `__` = lồng cấp):

| Config path | Env var | Ghi chú |
|---|---|---|
| `Jwt:Key` | `Jwt__Key` | **Bắt buộc rotate ở prod.** Chuỗi ngẫu nhiên ≥ 32 byte (`openssl rand -base64 48`). |
| `ConnectionStrings:Mycnn` | `ConnectionStrings__Mycnn` | Chuỗi kết nối Postgres |
| `MinioSettings:SecretKey` | `MinioSettings__SecretKey` | Khóa MinIO |
| `AiProvider:ApiKey` | `AiProvider__ApiKey` | (tùy chọn) khóa AI provider |

Dev không muốn để lộ: `dotnet user-secrets set "Jwt:Key" "<key-dev>"` trong `RecruitPro.API`
(User Secrets chỉ nạp ở môi trường Development). `appsettings.Production.json` đã là template placeholder.

## 6. Tài khoản mẫu
Mật khẩu chung cho **mọi** tài khoản: **`Password@123`** — đăng nhập bằng **username**. Chi tiết đầy đủ:
**[docs/SAMPLE-ACCOUNTS.md](docs/SAMPLE-ACCOUNTS.md)**.

| Vai trò | Username | Mật khẩu |
|---|---|---|
| Candidate | `nhatquang` | `Password@123` |
| HR | `thucuyen` | `Password@123` |
| Manager / HeadDepartment | `tiendat` | `Password@123` |
| SystemAdmin | `admin` | `Password@123` |

## 7. Xác thực, phiên & vô hiệu hóa tài khoản
- **Access token** JWT sống **15 phút**, mang claim `token_version`.
- **Refresh token** là chuỗi ngẫu nhiên, sống 7 ngày, **lưu DB dưới dạng hash SHA-256** (giá trị gốc chỉ
  trả cho client). `POST /api/auth/refresh` đổi (rotate, single-use) lấy cặp token mới; client tự động
  gọi khi access token hết hạn (silent refresh trong `api-client.ts`), thất bại thì logout.
- **Vô hiệu hóa tài khoản** (`PATCH /api/sysadmin/users/{id}/status` → `Inactive`/`Blocked`):
  1. `status = INACTIVE`
  2. `token_version += 1`
  3. xóa toàn bộ refresh token của user
  4. mọi access token đang sống lập tức fail (kiểm tra mỗi request ở `JwtExtension.OnTokenValidated`) →
     frontend nhận **401** → tự logout.
- Không có endpoint `/api/auth/me` hay `/api/auth/logout` (logout là client-side). Phân quyền: role-based
  (`[Authorize(Roles=...)]`) cho nghiệp vụ; permission fine-grained chỉ trong khu SysAdmin.

## 8. Test API
- **Swagger UI:** `http://localhost:5013/swagger`.
- **Postman:** import [`docs/postman/RecruitPro.postman_collection.json`](docs/postman/) +
  environment. Chạy request login trước (tự lưu `accessToken`/`refreshToken` vào biến collection), rồi
  chạy các flow: login → apply → screening → offer, OData, XML, external-score (gRPC), disable-user.

## 9. Chạy test
```bash
dotnet test                       # cần Docker (integration test dùng Testcontainers Postgres)
dotnet test --filter "FullyQualifiedName~AuthServiceUnitTests"   # chỉ unit test, không cần Docker
```

## 10. Đối chiếu "Sản phẩm cần nộp"
- [x] Source đầy đủ (BE + FE + gRPC scoring)
- [x] EF Core + migration + `init.sql` + dữ liệu mẫu · [x] **ERD** ([docs/erd](docs/erd/RecruitPro-ERD.md))
- [x] RESTful API + status code + DTO · [x] OData ≥2 · [x] Content negotiation JSON/XML
- [x] Security: JWT + role + ownership + hash mật khẩu · [x] gRPC service-to-service
- [x] Client JavaScript (React) · [x] **Postman collection** ([docs/postman](docs/postman/))
- [x] Business rules, workflow, kiến trúc, service design (xem [docs/overview](docs/overview/))
- [x] **Tài khoản mẫu** ([docs/SAMPLE-ACCOUNTS.md](docs/SAMPLE-ACCOUNTS.md)) · [x] Hướng dẫn chạy (file này)
