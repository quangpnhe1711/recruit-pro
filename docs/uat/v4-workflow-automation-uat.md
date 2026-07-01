# UAT Checklist — v4 Workflow Automation + MCP

Phạm vi: kiểm thử chấp nhận thủ công cho hệ thống Tự động hóa tuyển dụng (v4). Quản trị đặt tại khu vực **SystemAdmin**. HR/Manager không quản lý workflow, chỉ nhận kết quả qua màn hình nghiệp vụ + thông báo.

Nguyên tắc: deterministic-first, AI không bắt buộc. Cutover an toàn theo từng sự kiện (Disabled / Shadow / Live). Không bao giờ gửi trùng thông báo (một nguồn gửi tại một thời điểm).

## Chuẩn bị
- [ ] DB đã chạy `init.sql` (fresh) hoặc các patch idempotent (DB có sẵn): `db/patches/20260701-add-v4-workflow-automation.sql` + `db/patches/20260701-add-v4-mcp-tool-audits.sql` + `db/patches/20260702-add-v4-worker-heartbeat.sql`. Không dùng EF migration.
- [ ] `appsettings` có mục `WorkflowAutomation` (mặc định `Shadow`).
- [ ] Đăng nhập bằng tài khoản **SystemAdmin**.

## A. SystemAdmin — Dashboard tự động hóa
- [ ] Vào `/system-admin/automation`: hiển thị thẻ tổng workflow, workflow đang bật, số thực thi hôm nay, số thất bại, số dead-letter, hành động lỗi phổ biến, danh sách thực thi gần đây.
- [ ] Có trạng thái loading, empty, error rõ ràng; layout không vỡ trên màn hình laptop.

## B. Quản lý workflow
- [ ] `/system-admin/automation/workflows`: thấy 4 template mặc định (Pass CV → Notify Head Review, Head Review Overdue Reminder, High-fit Candidate Alert, Interview Completed Follow-up).
- [ ] Badge Đã bật/Đã tắt và Shadow/Live rõ ràng; Live có cảnh báo trực quan.
- [ ] Lọc theo trạng thái / trigger / chế độ hoạt động.
- [ ] Mở chi tiết một workflow: thấy trigger, điều kiện, hành động, chế độ, lịch sử phiên bản, thực thi gần đây.
- [ ] **Bật/Tắt** workflow: có xác nhận, cập nhật trạng thái ngay.
- [ ] **Sửa nháp** trong trình soạn thảo có cấu trúc (trigger select, điều kiện, hành động, chế độ). Có xem trước “Khi X, nếu Y, thì Z”. Chặn lưu/xuất bản khi thiếu trường bắt buộc.
- [ ] **Xuất bản phiên bản**: có xác nhận; nếu chọn Live phải thấy cảnh báo “Chế độ Live sẽ gửi thông báo thật…”. Phiên bản đã xuất bản là bất biến (sửa tạo phiên bản nháp mới).

## C. Luồng nghiệp vụ + cutover
- [ ] HR chuyển 1 hồ sơ từ Screening → ManagerReview (Pass CV).
- [ ] Với event `PassedToHeadReview` ở chế độ **Shadow**: có bản ghi thực thi “wouldNotify”, KHÔNG gửi thông báo thật; thông báo trực tiếp cũ vẫn gửi bình thường.
- [ ] Chuyển cùng event sang **Live**: workflow gửi đúng **một** thông báo; thông báo trực tiếp cũ bị bỏ qua (không trùng).
- [ ] `CandidateApplied`, `InterviewCompleted`, `JobApproved` sinh event trong outbox (mục Sự kiện/Outbox debug), không sinh trùng khi lặp lại cùng chuyển trạng thái.

## D. Head Review Overdue (scheduler)
- [ ] Có hồ sơ ở ManagerReview quá hạn (mặc định ≥ 3 ngày): scheduler sinh event `HeadReviewOverdue` và workflow nhắc Trưởng bộ phận.
- [ ] Chạy lại trong cùng cửa sổ 24 giờ: KHÔNG nhắc trùng (dedup theo cửa sổ ngày).

## E. Lịch sử & chi tiết thực thi
- [ ] `/system-admin/automation/executions`: lọc theo trạng thái / workflow / event / khoảng ngày / chế độ; có phân trang.
- [ ] Mở chi tiết: thấy tóm tắt, payload sự kiện (thu gọn), snapshot phiên bản, dòng thời gian từng bước (input/output/error), ID có thể copy.
- [ ] Chế độ Shadow: chi tiết cho thấy “would run / would notify” và không có dấu hiệu gửi thật.

## F. Thất bại, retry, dead-letter
- [ ] Một hành động thất bại tạo thực thi trạng thái **Thất bại** + bản ghi dead-letter; giao dịch ATS KHÔNG bị rollback.
- [ ] Bấm **Thử lại** (có xác nhận): thực thi hợp lệ chuyển sang **Thành công**.
- [ ] Vượt số lần thử tối đa (mặc định 3): chuyển sang **DeadLetter**.

## G. MCP prototype
- [ ] `/system-admin/mcp/tools`: liệt kê 6 công cụ chỉ-đọc (jobs.search, jobs.get, applications.get, applications.get_fit_analysis, interviews.get_schedule, analytics.get_funnel_summary) với mô tả, quyền yêu cầu, phân loại read/write, lần gọi gần nhất.
- [ ] Gọi thử một công cụ: ghi 1 bản ghi audit **allowed = true**; output là tóm tắt an toàn (không lộ CV/dữ liệu nhạy cảm đầy đủ).
- [ ] Công cụ tuân thủ quyền sở hữu của application service (SystemAdmin không vượt quyền dữ liệu nghiệp vụ).
- [ ] `/system-admin/mcp/audits`: có bản ghi **denied** khi gọi bởi vai trò không đủ quyền.

## H. Phân quyền (RBAC)
- [ ] HR KHÔNG truy cập được màn hình/API SystemAdmin automation (403/redirect).
- [ ] Manager/HeadDepartment KHÔNG truy cập được.
- [ ] Candidate KHÔNG truy cập được.
- [ ] Người dùng chưa đăng nhập nhận 401.
- [ ] SystemAdmin truy cập, xuất bản, bật/tắt, thử lại thành công.

## J. Chẩn đoán & runtime (mới)
- [ ] `/system-admin/automation/diagnostics`: thấy trạng thái tự động hóa (Enabled/Disabled), trạng thái worker dispatcher (Đang chạy / quá hạn heartbeat / chưa chạy), số sự kiện chờ/lỗi/dead-letter, sự kiện & thực thi gần nhất, danh sách cảnh báo.
- [ ] Với mỗi workflow: thấy chế độ hiệu lực, và nếu chưa tạo execution thì có **lý do bằng tiếng Việt** (workflow tắt / chưa có phiên bản active / mode Disabled / chưa có sự kiện / worker chưa xử lý / điều kiện không thỏa).
- [ ] Dashboard `/system-admin/automation` hiển thị dải trạng thái worker + banner cảnh báo khi có vấn đề.

## Kịch bản UAT thủ công

### UAT-01: Pass CV tạo execution mới
- Login HR → Pass CV một application ở Screening (→ ManagerReview).
- Login SystemAdmin → Tự động hóa tuyển dụng → Lịch sử chạy.
- Kỳ vọng: thấy execution mới của workflow "Pass CV → Notify Head Review". Mở detail thấy event, condition, action, mode, recipient/wouldNotify.

### UAT-02: Không có execution thì chẩn đoán được lý do
- Tắt workflow hoặc để không có active version → thực hiện event.
- Mở Chẩn đoán → Kỳ vọng: UI nói rõ workflow disabled / no active version / no matching trigger / worker chưa xử lý.

### UAT-Toast-01: System notification toast
- Kích hoạt một thông báo đổi trạng thái hồ sơ (hoặc workflow notification Live).
- Kỳ vọng: thẻ thông báo bo góc xuất hiện ở **góc dưới-phải** trên desktop; trên mobile nằm gọn trong màn hình (không tràn).
- Kỳ vọng: tiêu đề/nội dung tiếng Việt, KHÔNG phải `toast.info` mặc định; bấm "Xem chi tiết" mở trang liên quan nếu có URL; chuông thông báo vẫn nhận thông báo.

### UAT-Mobile-01: SystemAdmin mobile drawer
- Mở SystemAdmin Automation ở viewport mobile.
- Kỳ vọng: có nút hamburger; sidebar mở dạng drawer từ trái; có backdrop, bấm backdrop/chọn mục điều hướng đều đóng drawer; không tràn ngang.

## I. Chất lượng UI/UX
- [ ] Không còn màn hình placeholder “coming soon” cho v4.
- [ ] Trạng thái loading / empty / error / success / failed đều có.
- [ ] Cảnh báo Live và xác nhận xuất bản/thử lại/đổi trạng thái hoạt động đúng.
- [ ] JSON kỹ thuật ẩn trong khu vực mở rộng, không phải nội dung chính.
- [ ] Toàn bộ chữ giao diện bằng tiếng Việt; badge rõ ràng; responsive laptop.
