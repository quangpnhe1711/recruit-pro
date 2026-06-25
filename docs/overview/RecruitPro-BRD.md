# RecruitPro BRD

## 1. Mục tiêu tài liệu

Tài liệu này mô tả yêu cầu nghiệp vụ cấp cao của hệ thống RecruitPro.

Mục đích:

- Thống nhất phạm vi sản phẩm giữa business, HR, candidate và team kỹ thuật
- Mô tả hệ thống giải quyết bài toán tuyển dụng nào
- Xác định vai trò người dùng, luồng nghiệp vụ và giá trị mang lại
- Làm nền cho SRD, backlog và kế hoạch phát triển

## 2. Tổng quan sản phẩm

RecruitPro là một hệ thống ATS kết hợp AI cho tuyển dụng end-to-end:

- Candidate có thể đăng ký, tạo hồ sơ, upload CV, apply job và theo dõi ứng tuyển
- HR có thể đăng job, quản lý candidate, xử lý application, sắp lịch interview và gửi offer
- Manager có thể tham gia duyệt job, xem shortlist và theo dõi hiệu quả tuyển dụng
- Hệ thống AI hỗ trợ parse CV, semantic matching, recommendation và copilot ranking

## 3. Vấn đề kinh doanh

Hệ thống được thiết kế để giải quyết các pain point sau:

- CV đến từ nhiều format khác nhau, khó chuẩn hóa
- HR tốn thời gian lọc candidate bằng tay
- Keyword matching không đủ tốt cho hồ sơ và JD viết khác nhau
- Candidate cần trải nghiệm tự phục vụ, không phải nhập tay toàn bộ profile
- Doanh nghiệp cần luồng tuyển dụng có thể audit, đo lường và mở rộng

## 4. Phạm vi nghiệp vụ

### 4.1 In scope

- Candidate registration và login
- Candidate profile management
- CV upload và CV parsing
- Job posting và job browsing
- Application submission và tracking
- Interview scheduling
- Offer management
- Notification
- AI semantic scoring
- Candidate/job recommendation
- AI recruitment copilot ranking

### 4.2 Out of scope

- Payroll, chấm công, nhân sự nội bộ sau khi onboard
- Performance review
- Learning management
- Cổng candidate cho toàn bộ vòng đời nhân sự sau tuyển dụng

## 5. Đối tượng sử dụng

### Candidate

- Tạo tài khoản
- Upload CV
- Cập nhật profile
- Apply job
- Theo dõi trạng thái ứng tuyển
- Nhận gợi ý job phù hợp

### HR

- Tạo và quản lý job
- Quản lý candidate
- Xem ứng viên theo job
- Sắp xếp interview
- Gửi email và offer
- Dùng copilot để shortlist candidate

### Manager

- Duyệt job
- Xem danh sách ứng viên
- Tham gia đánh giá và theo dõi funnel tuyển dụng

### SystemAdmin

- Quản trị toàn bộ hệ thống nội bộ
- Có toàn quyền trên các chức năng internal

## 6. Mục tiêu nghiệp vụ

1. Giảm thời gian xử lý CV và hồ sơ ứng viên
2. Chuẩn hóa dữ liệu candidate thành một nguồn dữ liệu dùng chung
3. Tăng chất lượng match giữa job và candidate
4. Hỗ trợ HR tìm candidate nhanh hơn bằng AI và semantic search
5. Đảm bảo candidate có trải nghiệm self-service rõ ràng, nhanh, dễ dùng
6. Giữ hệ thống có thể vận hành ngay cả khi AI provider gặp lỗi

## 7. Phạm vi chức năng chính

### 7.1 Candidate Self-Service

- Đăng ký tài khoản
- Đăng nhập candidate
- Cập nhật profile cá nhân
- Quản lý skills, experience, education, project
- Upload và thay thế CV
- Theo dõi job đã apply
- Hủy apply nếu cần

### 7.2 Job Discovery

- Xem danh sách job công khai
- Lọc theo department, work mode, employment type, keyword
- Xem chi tiết job
- Apply job từ trang job detail hoặc job listing

### 7.3 Recruitment Operations

- HR tạo job mới
- HR cập nhật, đóng, xóa job
- HR xem application theo job
- HR xem candidate list và candidate profile
- HR gửi email cho candidate
- HR sắp lịch interview
- HR tạo và quản lý offer

### 7.4 AI-Assisted Hiring

- Parse CV thành dữ liệu chuẩn hóa
- Sinh embedding cho candidate/job
- Tính semantic score cho application
- Gợi ý job phù hợp cho candidate
- Gợi ý candidate phù hợp cho job
- Hỗ trợ copilot ranking dựa trên prompt tự nhiên

## 8. Luồng nghiệp vụ chính

### 8.1 Candidate đăng ký và tạo hồ sơ

1. Candidate điền thông tin đăng ký
2. Hệ thống tạo user và candidate profile
3. Nếu có upload CV, hệ thống lưu file và tạo resume record
4. Profile ban đầu sẵn sàng để candidate tiếp tục hoàn thiện

### 8.2 Upload CV và parse CV

1. Candidate upload file CV
2. Hệ thống trích xuất text
3. Hệ thống parse sang dữ liệu cấu trúc
4. Candidate xem preview kết quả parse
5. Candidate xác nhận để lưu vào profile
6. Hệ thống đồng bộ profile và các phần dữ liệu liên quan

### 8.3 Apply job

1. Candidate xem job detail
2. Candidate gửi application
3. Hệ thống tạo application
4. Hệ thống chấm điểm theo rule trước
5. Semantic scoring chạy bổ sung sau
6. HR xem application trong dashboard hoặc job view

### 8.4 HR xử lý tuyển dụng

1. HR tạo job và publish
2. Candidate apply vào job
3. HR review danh sách applicant
4. HR chọn candidate để interview
5. HR gửi email, offer hoặc reject
6. Hệ thống cập nhật trạng thái và gửi notification

### 8.5 AI Copilot ranking

1. HR chọn job cần screening
2. HR nhập prompt hoặc rule
3. Hệ thống chuẩn bị candidate pool
4. AI và backend cùng đánh giá candidate
5. Kết quả ranking được lưu lại để audit và follow-up

## 9. Quy tắc nghiệp vụ

### Candidate

- Candidate chỉ được sửa hồ sơ của chính mình
- Candidate chỉ được xem application của chính mình
- Candidate chỉ được thao tác với offer/application của chính mình

### HR và nội bộ

- Internal user chỉ thao tác trên dữ liệu được phân quyền
- Job, application, interview và candidate data phải tuân thủ RBAC
- Các hành động nhạy cảm như approve, reject, delete phải có quyền riêng

### AI

- AI là lớp hỗ trợ, không được làm nghẽn luồng nghiệp vụ chính
- Nếu AI lỗi, hệ thống vẫn cho phép lưu dữ liệu và tiếp tục vận hành
- Semantic và copilot chỉ là lớp tăng chất lượng, không thay thế business rule cơ bản

## 10. Năng lực nghiệp vụ theo vai trò

### Candidate

- Đăng ký, đăng nhập, xem và cập nhật profile
- Upload CV
- Xem job và apply
- Theo dõi ứng tuyển
- Nhận recommendation

### HR

- Quản lý job
- Quản lý candidate
- Quản lý application
- Quản lý interview
- Gửi email, offer
- Dùng AI copilot để shortlist

### Manager

- Duyệt job và xem funnel
- Tham gia đánh giá candidate

### SystemAdmin

- Quản trị toàn bộ luồng internal

## 11. KPI kinh doanh kỳ vọng

- Giảm thời gian review CV ban đầu
- Giảm thời gian tìm candidate phù hợp
- Tăng tỷ lệ candidate hoàn thiện hồ sơ
- Tăng tỷ lệ apply từ candidate dashboard recommendation
- Tăng chất lượng shortlist nhờ semantic matching và copilot

## 12. Giả định và ràng buộc

- Hệ thống phục vụ cả public candidate flow và internal HR flow
- Candidate profile là nguồn dữ liệu chuẩn sau khi CV được parse và xác nhận
- AI provider có thể thay đổi, nên cấu hình phải trừu tượng hóa
- Hệ thống phải hỗ trợ backward compatibility cho dữ liệu legacy

## 13. Tiêu chí thành công

- Candidate có thể tự đăng ký, upload CV và apply job mà không cần HR can thiệp
- HR có thể quản lý pipeline tuyển dụng end-to-end
- AI giúp tăng chất lượng match nhưng không làm hệ thống phụ thuộc cứng
- Dữ liệu tuyển dụng được chuẩn hóa để tái sử dụng cho search, ranking và recommendation

## 14. Danh sách tài liệu kỹ thuật liên quan

- [RecruitPro-SRD.md](./RecruitPro-SRD.md)
- [current-system-processing-flow.md](./current-system-processing-flow.md)
- [ats-ai-workflow-overview.md](./ats-ai-workflow-overview.md)
- [ai-recruitment-copilot-design.md](./ai-recruitment-copilot-design.md)
- [ai-rules/rbac-matrix.md](./ai-rules/rbac-matrix.md)

