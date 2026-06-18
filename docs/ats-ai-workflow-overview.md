# RecruitPro ATS AI Workflow Overview

## Mục tiêu tài liệu

Tài liệu này mô tả các tính năng AI/semantic matching đã được triển khai trong RecruitPro qua Phase 1, Phase 2 và Phase 3.

Tài liệu tập trung vào:

- Chức năng hệ thống hiện có
- Giá trị thực tế trong tuyển dụng
- Context nên dùng
- Workflow end-to-end
- Ý nghĩa vận hành cho HR, Candidate và team kỹ thuật

---

## 1. Tổng quan các phase đã triển khai

### Phase 1: Resume Parsing + Rule-Based Ranking

Đã triển khai:

- Upload CV với hỗ trợ `PDF`, `DOCX`, `TXT`
- Trích xuất text trước khi gọi AI
- Chặn file không hỗ trợ hoặc CV không đọc được
- Phân biệt scanned/image-based PDF
- Parse CV bằng AI sang dữ liệu structured
- Lưu parsed resume vào candidate profile
- Lưu parse status, parse error, provider/model metadata
- Tính `rule_score` ngay khi ứng viên apply job

Ý nghĩa thực tế:

- Candidate không cần nhập tay toàn bộ hồ sơ
- HR có dữ liệu chuẩn hóa để lọc ứng viên nhanh hơn
- Hệ thống vẫn chạy được dù AI parser lỗi
- Apply flow luôn phản hồi nhanh

### Phase 2: Async Semantic Matching

Đã triển khai:

- Queue nền in-process cho `ApplicationSubmitted`
- Background worker xử lý semantic score bất đồng bộ
- Sinh candidate embedding text từ structured resume
- Sinh job embedding text từ job data
- Hash-based cache cho candidate/job embedding
- Gọi embedding provider riêng
- Tính cosine similarity
- Cập nhật `semantic_score`, `final_score`, `score_status`
- Fallback nếu semantic lỗi, vẫn giữ `rule_score`

Ý nghĩa thực tế:

- HR không phải chờ AI mới xem được hồ sơ
- Semantic matching chỉ chạy khi thực sự cần
- Giảm cost embedding nhờ hash cache
- Tăng chất lượng ranking với các CV không match hoàn toàn bằng keyword

### Phase 3: Semantic Discovery + Recommendation

Đã triển khai:

- Vector storage cho candidate và job trong DB
- Talent Pool Search
- Similar Candidate Search
- Similar Job Search
- Candidate Recommendation theo job
- Job Recommendation theo candidate
- AI-powered Candidate Discovery từ natural language query

Ý nghĩa thực tế:

- Không chỉ xử lý hồ sơ đã apply, mà còn chủ động tìm ứng viên phù hợp
- Hỗ trợ sourcing nhanh hơn cho HR
- Gợi ý job phù hợp cho candidate
- Tái sử dụng data CV/job cho nhiều use case tuyển dụng thông minh

---

## 2. Các chức năng chính đã implement

## 2.1 Resume Upload & Text Extraction

### Hệ thống làm gì

- Nhận CV từ candidate
- Kiểm tra định dạng file
- Trích xuất text từ file
- Từ chối file không đọc được hoặc quá ít nội dung
- Trả message rõ nếu file là scanned PDF

### Có ích gì trong thực tế

- Tránh gọi AI vô ích cho file rỗng hoặc CV scan
- Giảm token cost
- Giảm lỗi parse sai do input kém chất lượng
- Candidate nhận lỗi đúng bản chất thay vì thông báo mơ hồ

### Context nên dùng

- Candidate portal
- Bulk resume intake
- Resume update trong profile

---

## 2.2 AI Resume Parsing

### Hệ thống làm gì

- Gọi AI parser sau khi text extraction thành công
- Chuẩn hóa CV thành JSON có cấu trúc
- Trích xuất:
  - profile
  - skills
  - experience
  - projects
  - education
  - certifications
  - languages
  - awards
  - activities
  - keywords
  - parser warnings

### Có ích gì trong thực tế

- HR nhìn được hồ sơ theo format thống nhất
- Dễ chấm điểm, so khớp kỹ năng, tìm kiếm semantic
- Phù hợp cho dashboard, review screen, talent pool
- Dễ re-parse khi đổi prompt/model sau này

### Context nên dùng

- Candidate profile enrichment
- ATS screening
- Copilot/recommendation/search

---

## 2.3 Parse Status & Failure Handling

### Hệ thống làm gì

- Lưu các trạng thái:
  - `NotStarted`
  - `TextExtractionFailed`
  - `Parsing`
  - `Completed`
  - `RetryPending`
  - `Failed`
- Retryable AI errors không bắt candidate upload lại
- Lưu extracted text, error, model, warnings

### Có ích gì trong thực tế

- Hỗ trợ vận hành và debug
- Không làm mất CV nếu AI provider tạm thời lỗi
- Có thể retry nền hoặc retry thủ công sau
- Phân biệt lỗi business với lỗi hạ tầng

### Context nên dùng

- Production monitoring
- Support/debug candidate issues
- Tối ưu độ ổn định hệ thống AI

---

## 2.4 Rule-Based Scoring Khi Apply Job

### Hệ thống làm gì

- Tạo `Application` ngay khi candidate apply
- Tính `rule_score` ngay lập tức
- Lưu `final_score` ban đầu bằng `rule_score`
- Gắn `score_status = PendingSemantic`

### Thành phần chấm điểm hiện tại

- Required skill match
- Experience match
- Nice-to-have skill match
- Keyword overlap
- Base scoring cho structured profile completeness

### Có ích gì trong thực tế

- HR xem được ứng viên ngay sau khi apply
- Không phụ thuộc AI semantic để vận hành ATS
- Dễ explain vì rule score minh bạch hơn semantic

### Context nên dùng

- Shortlisting ban đầu
- Sorting nhanh danh sách ứng viên
- SLA xử lý hồ sơ gần real-time

---

## 2.5 Async Semantic Matching

### Hệ thống làm gì

- Sau khi apply thành công, application được đưa vào queue
- Worker nền xử lý semantic score
- Nếu candidate/job vector thiếu hoặc hash đã đổi, hệ thống tạo embedding mới
- Nếu hash không đổi, tái sử dụng vector đã có
- Tính cosine similarity và cập nhật:
  - `semantic_score`
  - `final_score`
  - `score_status`

### Có ích gì trong thực tế

- Nhận ra ứng viên phù hợp dù wording CV khác JD
- Tăng chất lượng match cho profile không keyword-perfect
- Giảm cost nhờ chỉ embed khi cần
- Không chặn apply flow

### Context nên dùng

- Ranking quality improvement
- Large job pipelines
- Cases CV viết “business language” nhưng vẫn fit kỹ thuật

---

## 2.6 Talent Pool Search

### Hệ thống làm gì

- HR nhập free-text query hoặc dùng `jobId`
- Hệ thống embed query/job
- So khớp semantic với candidate vectors
- Trả về danh sách ứng viên phù hợp nhất

### Ví dụ query

- "Senior backend engineer with .NET, PostgreSQL, REST API"
- "Product designer with design system and candidate journey experience"
- "QA automation profile with API test and CI/CD exposure"

### Có ích gì trong thực tế

- HR có thể tìm ứng viên trong talent pool mà không cần filter cứng
- Tốt hơn search keyword thuần
- Dùng được cả khi CV viết không trùng exact keyword

### Context nên dùng

- Sourcing nội bộ
- Tái kích hoạt candidate cũ
- Talent database mining

---

## 2.7 Similar Candidate Search

### Hệ thống làm gì

- Chọn một candidate đã có
- Hệ thống tìm các candidate semantic tương tự

### Có ích gì trong thực tế

- Nếu HR thấy một hồ sơ đẹp, có thể tìm thêm hồ sơ cùng nhóm
- Tăng tốc shortlist
- Hữu ích khi một candidate đã pass screening tốt

### Context nên dùng

- Lookalike sourcing
- Build backup shortlist
- Replace candidate drop-off

---

## 2.8 Similar Job Search

### Hệ thống làm gì

- Chọn một job
- Hệ thống tìm các job semantic tương tự

### Có ích gì trong thực tế

- Tránh tạo JD trùng lặp
- Gợi ý các role gần nhau cho candidate
- Giúp team product/HR tái dùng mẫu tuyển dụng

### Context nên dùng

- Candidate discovery
- Job recommendation
- Internal cataloging

---

## 2.9 Candidate Recommendation Theo Job

### Hệ thống làm gì

- Dựa trên 1 job cụ thể
- Xếp hạng các candidate phù hợp nhất

### Có ích gì trong thực tế

- HR có danh sách ưu tiên để chủ động contact
- Tăng tốc mở job mới
- Giảm thời gian manual search

### Context nên dùng

- Opening mới cần shortlist nhanh
- Manager yêu cầu candidate đề xuất trong ngày
- High-volume tuyển dụng

---

## 2.10 Job Recommendation Theo Candidate

### Hệ thống làm gì

- Dựa trên candidate profile/vector
- Gợi ý các job phù hợp nhất

### Có ích gì trong thực tế

- Tăng engagement candidate
- Tăng apply conversion
- Hỗ trợ candidate portal thông minh hơn

### Context nên dùng

- Candidate dashboard
- Re-engagement email
- Cross-role recommendation

---

## 2.11 AI-Powered Candidate Discovery

### Hệ thống làm gì

- HR mô tả ứng viên mong muốn bằng ngôn ngữ tự nhiên
- Hệ thống semantic search trong pool candidate

### Ví dụ

- "Tìm giúp mình ứng viên từng làm ATS hoặc HR tech, mạnh backend API và đã có kinh nghiệm với ranking systems"
- "Tìm profile có product sense, React tốt, từng làm internal dashboard"

### Có ích gì trong thực tế

- Không cần ép HR hiểu schema filter kỹ thuật
- Giảm rào cản giữa business language và candidate database
- Là nền tảng cho AI sourcing assistant sau này

### Context nên dùng

- Hiring manager brief không rõ keyword
- Recruiter mới chưa thuộc hết skill taxonomy
- Tìm kiếm exploratory

---

## 3. Workflow end-to-end

## 3.1 Candidate Resume Workflow

1. Candidate upload CV
2. Hệ thống kiểm tra file type
3. Hệ thống extract text
4. Nếu text rỗng hoặc quá ngắn:
   - đánh dấu `TextExtractionFailed`
   - trả message rõ cho candidate
5. Nếu text hợp lệ:
   - gọi AI parser
6. Nếu AI parse thành công:
   - lưu parsed resume JSON
   - cập nhật candidate profile structured data
   - lưu warnings/model/status
7. Nếu AI parse retryable fail:
   - lưu CV và extracted text
   - đánh dấu `RetryPending`
8. Nếu AI parse fail không retryable:
   - lưu error
   - đánh dấu `Failed`

## 3.2 Application & Ranking Workflow

1. Candidate apply job
2. Hệ thống tạo `Application`
3. Tính `rule_score` ngay
4. Trả apply success ngay cho frontend
5. Enqueue application vào semantic processing queue
6. Worker nền xử lý:
   - load application
   - load candidate profile
   - load job
   - build embedding texts
   - check hash/vector cache
   - generate embeddings nếu cần
   - tính cosine similarity
   - cập nhật `semantic_score`
   - cập nhật `final_score`
7. Nếu semantic fail:
   - giữ `rule_score`
   - set `score_status = SemanticFailed`

## 3.3 Talent Discovery Workflow

1. HR nhập query hoặc chọn job/candidate gốc
2. Hệ thống build semantic query text
3. Hệ thống lấy/generate vector
4. So khớp với candidate/job vectors trong DB
5. Tính similarity
6. Sắp xếp kết quả
7. Trả danh sách đề xuất

---

## 4. Giá trị theo từng vai trò

## 4.1 Với Candidate

- Upload CV nhanh hơn
- Không phải nhập tay toàn bộ profile
- Nhận gợi ý job tốt hơn
- Apply nhanh và không phải chờ AI

## 4.2 Với HR/Recruiter

- Có ranking ngay sau khi ứng viên nộp hồ sơ
- Có semantic shortlist tốt hơn keyword matching
- Có talent pool search và candidate discovery
- Tìm candidate tương tự hoặc recommended candidate nhanh hơn

## 4.3 Với Hiring Manager

- Nhìn shortlist chất lượng hơn
- Có final score phản ánh cả rule + semantic
- Dễ tìm ứng viên gần với profile thành công trước đó

## 4.4 Với Team Kỹ Thuật / Sản Phẩm

- Hệ thống resilient hơn khi provider AI lỗi
- Có parse status, score status, metadata để monitoring
- Hash cache giúp tối ưu cost
- Có nền tảng để nâng cấp lên `pgvector`, recommendation engine hoặc copilot sourcing

---

## 5. Khi nào tính năng này đặc biệt hữu ích

### Context 1: Tuyển nhiều role kỹ thuật

Semantic matching giúp giảm việc phụ thuộc vào exact keyword như:

- `.NET Core` vs `ASP.NET Core`
- `ReactJS` vs `React`
- `REST API development` vs `backend service development`

### Context 2: HR không rành sâu kỹ thuật

AI-powered candidate discovery cho phép mô tả nhu cầu bằng business language thay vì filter kỹ thuật cứng.

### Context 3: Công ty có talent pool lớn

Talent pool search và similar candidate search giúp khai thác candidate cũ hiệu quả hơn thay vì chỉ chờ apply mới.

### Context 4: Muốn tăng conversion candidate portal

Job recommendation giúp candidate thấy role phù hợp hơn, tăng khả năng apply thêm.

### Context 5: Hệ thống cần ổn định trong production

Flow hiện tại không phụ thuộc cứng vào AI:

- parse lỗi vẫn giữ CV
- semantic lỗi vẫn giữ rule score
- apply flow không bị block

---

## 6. Điểm mạnh của thiết kế hiện tại

- Fast path và AI path được tách rõ
- Có graceful fallback
- Có metadata để audit/debug
- Có async enrichment thay vì blocking sync flow
- Có thể mở rộng tiếp sang:
  - `pgvector`
  - recruiter copilot
  - similar talent clustering
  - recommendation email campaigns
  - semantic internal sourcing dashboard

---

## 7. Hạn chế hiện tại

Đây là các điểm cần lưu ý trong thực tế:

- Vector hiện đang lưu `jsonb`, chưa query similarity trực tiếp trong PostgreSQL
- Similarity hiện tính trong application layer
- Queue hiện là in-process, chưa phải distributed queue
- Chưa có UI đầy đủ cho talent pool / similar / semantic discovery
- Candidate recommendation hiện mới ở mức API/backend-first

Các hạn chế này không làm hỏng flow chính, nhưng sẽ ảnh hưởng khi scale lớn.

---

## 8. Hướng mở rộng tiếp theo

### Ngắn hạn

- Dựng UI HR cho Talent Pool Search
- Hiển thị semantic explanations trong kết quả
- Hiển thị `Rule Score / Semantic Score / Final Score`

### Trung hạn

- Chuyển vector storage từ `jsonb` sang `pgvector`
- Đưa similarity ranking xuống DB
- Thêm retry queue thật như Redis/RabbitMQ/Hangfire

### Dài hạn

- Candidate clustering
- Similar talent graph
- AI sourcing assistant
- Smart campaign recommendation
- Matching explainability nâng cao

---

## 9. Kết luận

Những gì đã implement biến RecruitPro từ một ATS cơ bản thành một ATS có năng lực AI thực dụng:

- Parse CV tự động
- Ranking ngay khi apply
- Semantic enrichment bất đồng bộ
- Talent pool search
- Similarity search
- Candidate/job recommendation
- Candidate discovery bằng ngôn ngữ tự nhiên

Điểm quan trọng nhất trong thực tế là:

- business flow vẫn usable ngay cả khi AI lỗi
- HR có giá trị ngay lập tức từ `rule_score`
- AI được dùng như một lớp tăng cường chất lượng, không phải dependency bắt buộc của toàn bộ hệ thống

Đây là nền tảng tốt để tiếp tục nâng cấp RecruitPro theo hướng ATS thông minh, có khả năng sourcing và recommendation ở mức sản phẩm thực tế.
