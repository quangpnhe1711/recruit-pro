# Tài khoản mẫu — RecruitPro

> Nguồn: seed trong [`init.sql`](../init.sql) (users `:1080–1104`, gán role `:1118–1143`). DB tạo mới bằng
> `docker compose up` đã có sẵn các tài khoản này.

**Mật khẩu chung cho MỌI tài khoản: `Password@123`** — đăng nhập bằng **username** (không phải email).
- Portal nội bộ (HR / Manager / HeadDepartment / SystemAdmin): `POST /api/auth/internal/login`, màn hình `/internal/login`.
- Portal ứng viên (Candidate): `POST /api/auth/candidate/login`, màn hình `/login`.

## Tài khoản đại diện mỗi vai trò

| Vai trò | Username | Họ tên | Ghi chú |
|---|---|---|---|
| **SystemAdmin** | `admin` | Quản trị hệ thống | Quản lý user, RBAC, workflow automation |
| SystemAdmin | `minhkhoi` | Võ Minh Khôi | |
| **HR** | `thucuyen` | Nguyễn Thục Uyên | Recruiter chính trong seed (sở hữu nhiều job/application) |
| HR | `giahan` | Lê Gia Hân | |
| HR | `khanhlinh.pham` | Phạm Khánh Linh | |
| HR | `haiyen` | Đỗ Hải Yến | |
| **Manager** | `quocbao` | Vũ Quốc Bảo | Duyệt job, xem báo cáo, ra quyết định tuyển |
| Manager | `minhkhang` | Đặng Minh Khang | |
| **Manager + HeadDepartment** | `tiendat` | Trần Trọng Tiến Đạt | Có **2 vai trò**; trưởng bộ phận Engineering |
| **Candidate** | `nhatquang` | Phùng Nhật Quang | Ứng viên có hồ sơ + đơn ứng tuyển mẫu |
| Candidate | `haidang`, `ducminh`, `khanhnam`, `thuha` | … | Ứng viên bổ sung |
| Candidate (**Inactive**) | `yennhi` | Phạm Yến Nhi | **Bị vô hiệu hóa sẵn** — dùng để minh họa flow disable-user (login → 401 `ACCOUNT_DISABLED`) |

*(Còn nhiều Candidate khác trong seed: `minhquan`, `khanhlinh.tran`, `quocanh`, `minhthao`, `hoangnam`,
`ngocan`, `hoangphuc`, `quynhmai`, `anhkhoa`.)*

## Thử nhanh flow vô hiệu hóa tài khoản
1. Đăng nhập `admin` (SystemAdmin) → lấy access token.
2. Lấy danh sách user: `GET /api/sysadmin/users`.
3. Vô hiệu hóa một user: `PATCH /api/sysadmin/users/{id}/status` body `{ "status": "Inactive" }`
   → `token_version += 1`, xóa toàn bộ refresh token của user đó.
4. Access token cũ của user đó lập tức bị từ chối (**401**) ở request kế tiếp; `POST /api/auth/refresh`
   của họ cũng trả **401** → frontend tự logout.
5. (SystemAdmin không thể tự vô hiệu hóa chính mình → **409**; không thể vô hiệu hóa admin cuối cùng → **409**.)
