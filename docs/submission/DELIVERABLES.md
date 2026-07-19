# RecruitPro — Bàn giao tài liệu Project PRN232

> Sinh ra ngày 19/07/2026. Nội dung dưới đây phản ánh **hệ thống thực tế** (đã đối chiếu source
> code, database schema/Fluent config, và **chạy thử trực tiếp** trên API đang chạy `http://localhost:5013`
> + gRPC ScoringService `:5210`). Nguyên tắc: chỉ ghi những gì có căn cứ, không bịa; đánh dấu rõ
> Implemented / Partial / NotImplemented / Unverified.

## 1. Danh sách file bàn giao

| File | Vị trí | Ghi chú |
|---|---|---|
| **RecruitPro_Project_Report.docx** | `RecruitProInternal/docs/submission/` | Báo cáo project — 14 mục theo đề + Phụ lục A (coverage). Có mục lục, danh sách hình/bảng, header/footer, số trang. |
| **RecruitPro_Defense_Handbook.docx** | `RecruitProInternal/docs/submission/` | Cẩm nang vấn đáp — độc lập với báo cáo. Kịch bản pitch, kịch bản demo, 9 nhóm Q&A có căn cứ code. |
| Postman collection | `RecruitProInternal/docs/postman/RecruitPro.postman_collection.json` (+ `.postman_environment.json`) | Đã kiểm tra: Auth, Jobs, OData, Content Negotiation, Applications, Interviews, Offers, gRPC External Score, SysAdmin, Dashboards. |
| Tài khoản mẫu | `RecruitProInternal/docs/SAMPLE-ACCOUNTS.md` | Mật khẩu chung `Password@123`, đăng nhập bằng username. |
| ERD nguồn | `RecruitProInternal/docs/erd/RecruitPro-ERD.md` | |
| Schema + seed | `RecruitProInternal/init.sql`, `RecruitProInternal/db/patches/`, `RecruitProInternal/RecruitPro.Infrastructure/Migrations/` | |

**Xác nhận:** hai file Word là **hai tài liệu độc lập**; cả hai là **`.docx` thật** (định dạng OOXML,
chữ ký file `PK\x03\x04`, sinh bằng python-docx — **không** đổi đuôi từ định dạng khác). Diagram được
render thành ảnh PNG nhúng trực tiếp (không để mã Mermaid thô).

## 2. Coverage 14 mục yêu cầu của đề

| # | Mục | Trạng thái | Bằng chứng chính |
|---|---|---|---|
| 1 | Giới thiệu project | Implemented | §1 báo cáo; README, .csproj versions |
| 2 | Vai trò người dùng | Implemented | 5 role: Candidate/HR/Manager/HeadDepartment/SystemAdmin — `RoleNames.cs`, seed, `[Authorize]` |
| 3 | Use case | Implemented | 16 use case đối chiếu endpoint + service |
| 4 | ERD / DB schema | Implemented | 16 thực thể lõi + join n-n (JobSkill, CandidateSkill, UserRole, RolePermission) — `AppDbContext.cs` |
| 5 | Business rules (≥5) | Implemented | 14 rule xác minh từ code (dup-active 409 + unique index, workflow guard, apply eligibility, offer/interview gating, ownership) |
| 6 | Workflow trạng thái | Implemented | Máy trạng thái đơn ứng tuyển 9 trạng thái — `ApplicationStatusWorkflow.cs` |
| 7 | Kiến trúc hệ thống | Implemented | Onion 4 tầng + gRPC service — `Program.cs`, project refs |
| 8 | Service design | Implemented | ~30 service tách theo nghiệp vụ — `RecruitPro.Application/Services` |
| 9 | API endpoint list | Implemented | ~150 endpoint, 24 REST + 2 OData controller (bảng khổ ngang trong §9) |
| 10 | Security matrix | Implemented | 16 chức năng × 5 role; 3 lớp: role / RBAC / ownership; **chạy thử live 401/403/200** |
| 11 | OData (≥2) | Implemented | `/odata/Jobs`, `/odata/Applications`; **chạy thử live** $filter/$select/$orderby/$top/$skip/$count |
| 12 | Content negotiation | Implemented | `/api/xml-demo/jobs`; **chạy thử live** JSON & XML |
| 13 | gRPC / microservice | Implemented | `RecruitPro.ScoringService`; **chạy thử live** external-score = 200 |
| 14 | Hướng dẫn chạy | Implemented | §14 báo cáo; README, docker-compose |
| + | Postman / tài khoản mẫu / migration+seed / JS client | Implemented | xem mục 1 |

## 3. Kết quả chạy thử thực tế (live)

| Nhóm | Kết quả |
|---|---|
| Auth | `POST /api/auth/internal/login` (thucuyen/admin) → 200 + JWT; candidate `haidang` → 200 |
| OData | `$filter`/`contains`/`$select`/`$orderby`/`$top`/`$skip`/`$count(=12)` → 200; `$expand=Applications` → **400** (flat DTO) |
| Content negotiation | `Accept: application/json` → 200 JSON; `application/xml` → 200 XML `<ArrayOfJobODataDto>`; `application/pdf` → **200 JSON (fallback, KHÔNG 406)** |
| Security | không token → **401**; Candidate gọi endpoint HR → **403**; HR → **200**; tài khoản bị khóa → **401 ACCOUNT_DISABLED** |
| gRPC | ScoringService chạy → **200** `{score:32,label:"Weak",source:"...(gRPC)"}`; service tắt → **500 RpcException**; id sai → **404** |

## 4. Phần chưa làm / làm một phần / chưa demo (trung thực)

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| OData `$expand` | NotImplemented | DTO OData phẳng (không navigation) → trả 400. Đề chỉ yêu cầu "$expand nếu phù hợp" → chấp nhận được. |
| Trả 406 khi media type không hỗ trợ | NotImplemented | `ReturnHttpNotAcceptable` chưa bật → fallback về JSON 200. |
| gRPC retry / timeout / fallback | NotImplemented | Service tắt → 500 (RpcException bắt bởi global exception middleware). Chưa có degrade mềm. |
| Audit log | Partial | Có `SystemLog` + endpoint `GET /api/sysadmin/audit-logs` + audit cho MCP tool; chưa phải audit trail cross-cutting toàn hệ. |
| Soft delete | Partial | Chỉ một số entity (vd `CopilotSavedRule.IsDeleted`) + nhiều cờ `IsActive`; chưa có global query filter. |
| Serilog | NotImplemented | Dùng `ILogger` built-in. Đề cho phép "Serilog **hoặc cơ chế tương đương**" → đạt. |

## 5. Khác biệt giữa yêu cầu / tài liệu cũ / implementation (discrepancies)

1. **CSDL: đề ghi SQL Server, hệ thống dùng PostgreSQL (Npgsql).** Đề tài tự đề xuất nên thường được
   chấp nhận, nhưng nên chủ động giải thích khi bảo vệ.
2. **Secret trong `RecruitPro.API/appsettings.json`:** `Jwt:Key`, mật khẩu DB, MinIO secret đã là
   placeholder dev (khớp README §5). **NHƯNG `AiProvider:ApiKey` đang là một API key Google/Gemini
   thật** (bắt đầu `AIza…`) — mâu thuẫn với README §5 ("chỉ placeholder"). **Khuyến nghị: revoke key đó
   ngay và chuyển sang user-secrets / biến môi trường trước khi nộp.** (Không in giá trị key ở đây.)
3. **README ghi React 18, thực tế `package.json` là React 19.x** — code là chuẩn.
4. **Tài liệu overview ghi SignalR, thực tế realtime notification dùng SSE** — code là chuẩn.

## 6. Kết quả kiểm tra mở file (.docx)

- Cả hai file: `zip integrity: OK`, **18/18** XML part parse không lỗi, có header + footer + số trang
  (PAGE/NUMPAGES), có TOC + danh sách hình + danh sách bảng (Word field, tự cập nhật khi mở / nhấn F9),
  ảnh nhúng hợp lệ, báo cáo có 1 section khổ ngang (bảng API).
- Không có công cụ Microsoft Word trong môi trường sinh file để mở bằng UI; đã xác thực bằng kiểm tra
  cấu trúc OOXML (zip + toàn bộ XML part). **Khi nhận file, mở bằng Word và nhấn Ctrl+A → F9 một lần để
  cập nhật mục lục / danh sách hình / danh sách bảng và số trang.**

## 7. Cách render lại (nếu cần sửa)

Toàn bộ pipeline sinh tài liệu nằm ngoài repo (scratchpad phiên làm việc): fact JSON đã trích xuất +
`build/render_diagrams.py` (matplotlib) + `build/docx_engine.py` (python-docx) + `build/build_report.py`
/ `build/build_handbook.py`. Chạy: `py build_report.py` và `py build_handbook.py`.
