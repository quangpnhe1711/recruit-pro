# Hướng dẫn test thông luồng nghiệp vụ — RecruitPro

Playbook test **end-to-end thủ công** cho các luồng nghiệp vụ core. Mục tiêu: chạy 1 lượt xuyên suốt (golden path) để chứng minh cả pipeline tuyển dụng thông, rồi test chi tiết từng luồng + các case tiêu cực.

> Sơ đồ luồng đi kèm: [flow-html/apply-flow.html](flow-html/apply-flow.html), [flow-html/ranking-flow.html](flow-html/ranking-flow.html), [flow-html/g10-automation-flow.html](flow-html/g10-automation-flow.html).

---

## 0. Chuẩn bị môi trường

### 0.1 Chạy hệ thống

| Cách | Lệnh |
|---|---|
| Docker (đủ bộ: API + Postgres + MinIO) | `docker compose up -d` tại `RecruitProInternal/` |
| Local (Postgres đã chạy ở cổng 5433) | `dotnet run --project RecruitPro.API` |
| Microservice chấm điểm (chỉ cần cho luồng external-score) | `dotnet run --project RecruitPro.ScoringService` |

Refresh dữ liệu demo (idempotent, re-anchor ngày về 2026-07-04):
```bash
docker exec -i recruitpro_postgres psql -U postgres -d recruit_pro < db/patches/20260704-refresh-demo-seed.sql
```

### 0.2 URL & cổng

| Thành phần | URL |
|---|---|
| API | `http://localhost:5013` |
| Swagger (cách test dễ nhất) | `http://localhost:5013/swagger` |
| Frontend dev | `http://localhost:5173` |
| Postgres | `localhost:5433` · db `recruit_pro` · postgres/123456 |
| MinIO | `localhost:9000` |
| ScoringService (gRPC) | `http://localhost:5210` |

### 0.3 Tài khoản test (seed) — mật khẩu chung: `Password@123`

| Vai trò | Username | Cổng đăng nhập |
|---|---|---|
| Candidate | `nhatquang` | `POST /api/auth/candidate/login` (username-only) |
| HR | `thucuyen` | `POST /api/auth/internal/login` |
| Manager **+ HeadDepartment** (duyệt job & review) | `tiendat` | `POST /api/auth/internal/login` |
| SystemAdmin | `admin` | `POST /api/auth/internal/login` |

> `tiendat` giữ **cả** Manager và HeadDepartment — đây là tài khoản duyệt job và đẩy `ManagerReview → Interview` (guard yêu cầu đúng DepartmentHead phụ trách).
> Tài khoản `yennhi` bị seed **Inactive** — dùng để test login bị chặn (401).

### 0.4 Lấy token & gắn Bearer

```http
POST /api/auth/login
{ "username": "thucuyen", "password": "Password@123" }
```
→ trả `accessToken` (JWT HS256, hạn 15 phút) + `refreshToken`. Mọi request sau gắn header:
```
Authorization: Bearer <accessToken>
```
Trên Swagger: bấm **Authorize** → dán `<accessToken>`. Token hết hạn → `POST /api/auth/refresh { "refreshToken": "..." }`.

**Role được nhúng trong token** dưới dạng claim `role` → `[Authorize(Roles=...)]` đọc trực tiếp. Cần token của đúng role cho mỗi bước dưới đây.

---

## A. Kịch bản xuyên suốt (golden path)

Chạy 1 ứng viên đi hết pipeline: **Applied → Screening → ManagerReview → Interview → Offer → Hired**. Đây là bài "test thông luồng" chính.

| # | Actor (token) | Hành động (endpoint) | Body tối thiểu | Kết quả mong đợi |
|---|---|---|---|---|
| 1 | — | `GET /api/jobs` | — | Lấy 1 job đang **Approved** còn hạn (ghi lại `jobId`). Nếu không có → làm mục **B1** để tạo + duyệt job. |
| 2 | Candidate `nhatquang` | `GET /api/jobs/{jobId}/apply-context` | — | Trả về eligibility. `canApply=true`; nếu false xem `blockers` (thiếu CV / hết hạn / job chưa Approved / đã ứng tuyển). |
| 3 | Candidate | `POST /api/jobs/{jobId}/apply` | `{ "coverLetter": "..." }` | **201**. `status=Applied`, có `ruleScore`, `scoreStatus=PendingSemantic`. |
| 4 | Candidate | `GET /api/jobs/{jobId}/apply-context` (đợi ~5–10s) | — | `finalScore` được cập nhật, `scoreStatus=SemanticCompleted` (chấm semantic chạy nền). |
| 5 | HR `thucuyen` | `GET /api/hr/applications` | — | Thấy đơn của nhatquang, `status=Applied`. Lấy `applicationId`. |
| 6 | HR | `PATCH /api/hr/applications/{applicationId}/decision` | `{ "targetStatus": "Screening" }` | Đơn → **Screening**. |
| 7 | HR | `POST /api/copilot/conversations` | `{ "jobId": "{jobId}" }` | Tạo conversation Copilot gắn job. Lấy `conversationId`. |
| 8 | HR | `POST /api/copilot/conversations/{conversationId}/rankings` | `{ "jobId":"{jobId}", "forceRanking": true, "prompt": "ưu tiên React, 2 năm KN" }` | Trả bảng xếp hạng (chỉ ứng viên **Screening**), có điểm + FitLabel tiếng Việt. Lấy `rankingSessionId`. |
| 9 | HR | `POST /api/copilot/ranking-sessions/{rankingSessionId}/pass-cv` | `{ "applicationIds": ["{applicationId}"] }` | `Updated` chứa đơn; đơn → **ManagerReview** (stamp `departmentHeadReviewRequestedAt`). |
| 10 | Head `tiendat` | `GET /api/manager/applications/review-queue` | — | Thấy đơn ở hàng chờ duyệt. |
| 11 | Head `tiendat` | `PATCH /api/hr/applications/{applicationId}/decision` | `{ "targetStatus": "Interview" }` | Đơn → **Interview**. (Bằng HR `thucuyen` sẽ **403/422** vì guard yêu cầu đúng DepartmentHead phụ trách.) |
| 12 | HR | `POST /api/hr/interviews` | (xem **B5** cho schema) | Tạo lịch phỏng vấn. Lấy `interviewId`. |
| 13 | HR | `PATCH /api/hr/interviews/{interviewId}/status` | `{ "status": "Completed" }` | Interview → Completed. |
| 14 | HR | `PUT /api/hr/interviews/{interviewId}/evaluation` | scorecard/feedback | Lưu feedback (chỉ hợp lệ khi Completed). |
| 15 | HR | `POST /api/hr/applications/{applicationId}/offer/send` | nội dung offer | Đơn → **Offer**. (Không đẩy Offer qua endpoint `/decision` được — sẽ 422 `EmailRequiredForOffer`.) |
| 16 | Candidate `nhatquang` | `GET /api/candidate/applications/{applicationId}/offer` | — | Xem offer đã gửi. |
| 17 | Candidate | `POST /api/candidate/applications/{applicationId}/accept-offer` | — | Đơn → **Hired**. ✅ hết pipeline. |

**Verify xuyên suốt:**
- Sau mỗi bước: `GET /api/hr/applications/{applicationId}` → `status` đúng như bảng.
- Mỗi chuyển trạng thái sinh **notification** cho đúng người (xem **B8**).
- Với SysAdmin: mỗi hành động ATS quan trọng tạo 1 **outbox event** (`GET /api/sysadmin/automation/events`) và 1 **execution** (xem **B9**).

---

## B. Chi tiết từng luồng core

### B1. Job lifecycle (HR tạo → Head duyệt → publish)
| Bước | Actor | Endpoint | Kết quả |
|---|---|---|---|
| Tạo job | HR `thucuyen` | `POST /api/hr/jobs` | Job `Draft`. |
| Gửi duyệt | HR | `PATCH /api/hr/jobs/{jobId}` với `approvalStatus` chờ duyệt | Vào hàng chờ Head. |
| Xem hàng chờ | Head `tiendat` | `GET /api/manager/jobs/approval-queue` | Thấy job. |
| Duyệt / publish | Head `tiendat` | `PATCH /api/hr/jobs/{jobId}/status` (set Approved) | Job **Approved** → hiện ở `GET /api/jobs` (public). |
- **Quy tắc:** chỉ DepartmentHead phụ trách phòng ban của job mới duyệt/từ chối (BR-OWN-003). Job phải Approved + còn hạn thì candidate mới apply được.

### B2. Ứng tuyển + chấm điểm
- Tiền đề: candidate phải có **CV hiện hành** + tên/email trong profile. Nếu thiếu → upload: `POST /api/candidate/resume`.
- Apply: `POST /api/jobs/{jobId}/apply` → 201, `ruleScore` tính **đồng bộ** (không AI), `scoreStatus=PendingSemantic`.
- **Chấm nền:** vài giây sau, worker semantic chạy → `FinalScore = RuleScore×0.85 + SemanticScore×0.15`, `scoreStatus=SemanticCompleted`. Verify bằng cách gọi lại apply-context / hr application detail.
- Case lỗi embedding: `scoreStatus=SemanticFailed`, `finalScore` fallback về `ruleScore` (không chặn luồng).

### B3. Ranking (Copilot v2) + idempotency + pass CV
- Chỉ ứng viên **Screening** của job mới vào pool ranking (screening-only).
- **Điểm & thứ tự là deterministic** (source of truth). AI (Gemini) chỉ enrich văn bản tiếng Việt, **không đổi điểm/thứ tự**.
- **Test idempotency:** gọi `.../rankings` lần 2 với **cùng input** (cùng prompt/criteria, pool không đổi) → hệ trả lại session cũ (`reusedRankingSession=true`), **không gọi AI**, không tạo dòng mới (dedup theo `input_hash`). Đổi prompt/criteria → tạo session mới.
- **Pass CV:** `POST /api/copilot/ranking-sessions/{id}/pass-cv` body `{ "applicationIds": [...] }`. Ứng viên không ở Screening sẽ nằm trong `Skipped` kèm lý do; hợp lệ → `Screening → ManagerReview` và **rời pool** ở lần refresh sau.

### B4. Pipeline quyết định + guard role
Endpoint chính: `PATCH /api/hr/applications/{id}/decision` `{ "targetStatus": "..." }`.

| Chuyển | Ai được phép | Ghi chú |
|---|---|---|
| Applied → Screening | HR/Manager | |
| Screening → ManagerReview | HR/Manager (hoặc pass-cv) | |
| ManagerReview → Interview | **đúng DepartmentHead phụ trách** (hoặc SystemAdmin; Manager chỉ khi không có head) | dùng `tiendat` |
| Interview → Offer | HR/Manager | **không** qua `/decision` — phải `offer/send` |
| → Rejected | HR/Manager | **không** qua `/decision` — phải `rejection-email` (gửi email xong mới set Rejected) |
| Offer → Hired / OfferDeclined | **chỉ Candidate** | reviewer không đẩy được ra khỏi Offer |
| → Withdrawn | **chỉ Candidate** | từ Applied/Screening/ManagerReview/Interview |

Chuyển sai luồng → **422** (`ApplicationStatusWorkflow.CanTransition` chặn).

### B5. Interview
| Bước | Endpoint | Ghi chú |
|---|---|---|
| Lấy data lịch | `GET /api/hr/interviews/schedule-data` | interviewer, slot… |
| Tạo lịch | `POST /api/hr/interviews` | body theo schema Swagger (applicationId, thời gian, hình thức, interviewer) |
| Candidate xác nhận | `POST /api/candidate/interviews/{id}/confirm` | |
| Hoàn tất | `PATCH /api/hr/interviews/{id}/status` `{ "status":"Completed" }` | |
| Feedback | `PUT /api/hr/interviews/{id}/evaluation` | **chỉ hợp lệ khi Completed** |
- Sự kiện `InterviewCompleted` kích hoạt workflow gợi ý bước tiếp theo (rule-based, không AI) — xem **B9**.

### B6. Offer + accept/decline
- Lưu nháp: `PUT /api/hr/applications/{id}/offer`. Gửi: `POST /api/hr/applications/{id}/offer/send` (đơn → Offer).
- Candidate: `GET /api/candidate/applications/{id}/offer` (không thấy bản Draft) → `accept-offer` (→ Hired) hoặc `decline-offer` (→ OfferDeclined).

### B7. Reject / Withdraw
- **Reject:** `POST /api/hr/applications/{id}/rejection-email` — chỉ set `Rejected` **sau khi gửi email thành công**. Từ Interview yêu cầu interview đã Completed.
- **Withdraw:** `POST /api/candidate/applications/{id}/withdraw` — candidate tự rút (không từ Offer), huỷ interview đang chờ. Đơn đã Withdrawn/Rejected/OfferDeclined được **apply lại** job đó (Hired thì không).

### B8. Notification realtime (SSE + seen/read)
| Bước | Endpoint | Verify |
|---|---|---|
| Mở SSE | `GET /api/notifications/stream` (đăng nhập) | giữ kết nối `text/event-stream`; có `: ping` mỗi 25s |
| Kích hoạt | làm 1 chuyển trạng thái ở tab khác | client SSE nhận event `notification.created` **realtime** |
| Badge | `GET /api/notifications/counts` | `{ unseen, unread }` |
| Đánh dấu đã xem | `POST /api/notifications/seen` | `unseen` về 0 |
| Đọc 1 cái | `POST /api/notifications/{id}/read` | `unread` giảm 1 |
- Phân biệt **seen** (đã mở chuông) vs **read** (đã đọc từng cái).

### B9. Workflow automation (SysAdmin) — [g10-automation-flow.html](flow-html/g10-automation-flow.html)
Đăng nhập `admin`. Sau khi làm 1 hành động ATS (vd apply, pass CV):

| Kiểm tra | Endpoint | Mong đợi |
|---|---|---|
| Outbox | `GET /api/sysadmin/automation/events` | có event (vd `CandidateApplied`, `PassedToHeadReview`), status `Processed` |
| Execution | `GET /api/sysadmin/automation/executions` | có execution tương ứng; bấm chi tiết xem từng step (condition/action) |
| Dashboard | `GET /api/sysadmin/automation/dashboard` | tổng số workflow/execution/fail/dead-letter |
| Chẩn đoán | `GET /api/sysadmin/automation/diagnostics` | worker `dispatcher` heartbeat còn sống; mode hiệu lực |
| Retry | `POST /api/sysadmin/automation/executions/{id}/retry` | chỉ chạy khi execution Failed/DeadLetter |

- **Shadow vs Live:** mặc định tất cả event ở **Shadow** (`appsettings.json → WorkflowAutomation.EventModes`) → workflow chạy & ghi log nhưng **không gửi**, notification vẫn do đường trực tiếp cũ gửi. Muốn test **Live**: đổi 1 event thành `"Live"` → restart API → notification do workflow gửi và đường cũ tự tắt (chỉ 1 nguồn).
- **Diễn tập overdue:** để hồ sơ ở ManagerReview quá `HeadReviewOverdueDays` (mặc định 3 ngày) → scheduler (chạy mỗi giờ) publish `HeadReviewOverdue` (dedup theo cửa sổ 24h).

### B10. MCP tools (SysAdmin)
| Bước | Endpoint | Mong đợi |
|---|---|---|
| Danh mục | `GET /api/sysadmin/mcp/tools` | 6 tool read-only (jobs/applications/interviews/analytics) |
| Gọi thử | `POST /api/sysadmin/mcp/tools/jobs.search/test` `{ "inputJson": "{}" }` | trả tóm tắt trạng thái (không dump dữ liệu nhạy cảm) |
| Audit | `GET /api/sysadmin/mcp/audits` | mọi lần gọi đều có dòng audit (allow/deny, latency) |
| RBAC | gọi tool bằng token HR | bị từ chối (chỉ SystemAdmin) — vẫn ghi audit `denied` |

### B11. External score (gRPC demo)
- Cần `RecruitPro.ScoringService` chạy ở cổng 5210.
- HR: `GET /api/hr/applications/{id}/external-score` → trả `score`, `label`, `matchedSkills`, `missingSkills`, `source="RecruitPro.ScoringService (gRPC)"`.
- **Lưu ý:** đây là hệ **độc lập** với ranking Copilot (chỉ demo service-to-service, không ghi bảng ranking).

---

## C. Test tiêu cực / biên (bắt buộc chạy)

| Case | Cách | Mong đợi |
|---|---|---|
| Tài khoản Inactive | login `yennhi` | **401** `AccountDisabled` |
| Sai role | HR gọi `GET /api/sysadmin/automation/dashboard` | **403** |
| Ứng tuyển trùng | apply lần 2 khi đang có đơn active | **409** `ApplicationAlreadyActive` |
| Thiếu điều kiện apply | apply khi chưa có CV / job chưa Approved / hết hạn | **422** kèm `blockers` |
| Chuyển trạng thái sai luồng | `/decision` `Applied → Interview` | **422** |
| Đẩy Offer sai cửa | `/decision` `targetStatus=Offer` | **422** `EmailRequiredForOffer` |
| Reviewer ép Hired | HR gọi accept-offer thay candidate | **403/422** (chỉ Candidate) |
| ManagerReview→Interview sai người | HR `thucuyen` (không phải head) đẩy | bị guard chặn |
| Token hết hạn | đợi >15’ rồi gọi API | **401** → phải `refresh` |

---

## D. Checklist tổng (định nghĩa "thông luồng")

- [ ] Đăng nhập được cả 4 role, lấy được token.
- [ ] Job: tạo → duyệt → hiện public.
- [ ] Apply 201, rule score có ngay, final/semantic score cập nhật nền.
- [ ] Ranking chạy; re-click cùng input → reuse (idempotent); pass CV → ManagerReview.
- [ ] Pipeline đi hết: Screening → ManagerReview → Interview → Offer → Hired (đúng role mỗi bước).
- [ ] Interview: schedule → complete → feedback.
- [ ] Offer: send → candidate accept/decline.
- [ ] Reject (email-gated) & Withdraw hoạt động, chặn đúng luật.
- [ ] Notification realtime qua SSE + seen/read + counts đúng.
- [ ] Automation: mỗi hành động sinh outbox event + execution; diagnostics thấy worker sống.
- [ ] MCP: list/test/audit + RBAC chặn non-admin.
- [ ] Toàn bộ case tiêu cực ở mục C trả đúng mã lỗi.
