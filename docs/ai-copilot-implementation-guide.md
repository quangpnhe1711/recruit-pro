# AI Copilot Implementation Guide

## 1. Tai lieu nay dung de lam gi

Tai lieu nay mo ta chi tiet cach chuc nang `AI Recruitment Copilot` da duoc implement trong backend `RecruitProInternal`.

Muc tieu cua tai lieu nay khong chi la ke ten file hay endpoint, ma la giai thich ro:

- He thong dang giai bai toan gi
- Du lieu di qua cac tang nhu the nao
- Vi sao chon huong implement hien tai
- Vi sao can them tung class, interface, config, package
- Khi deploy can cau hinh gi de chay that
- Sau nay neu can mo rong thi nen mo rong o dau

Noi ngan gon: day la file de ong co the doc lai sau nay va hieu chuc nang nay tu goc do kien truc cho den code.

## 2. Bai toan nghiep vu dang giai

AI Copilot duoc xay de ho tro HR loc ung vien theo tung Job dang tuyen.

Workflow tong quat:

1. HR chon mot Job
2. Backend lay danh sach ung vien da apply vao Job do
3. Backend chuan bi ho so ung vien, ky nang, kinh nghiem, tom tat CV
4. Neu CV la file PDF, backend doc them noi dung PDF
5. HR nhap prompt, vi du:
   - `Tim ung vien biet Java Spring Boot`
   - `Loai toan bo sinh vien FPT`
   - `Uu tien ung vien tren 2 nam kinh nghiem`
6. Backend ranking ung vien
7. Neu co OpenAI API key thi backend gui payload da chuan bi sang OpenAI de cai thien ket qua va loi giai thich
8. Backend luu lai session ranking + lich su chat
9. Frontend hien ket qua xep hang

Co hai nguyen tac thiet ke quan trong:

- AI khong duoc truy cap truc tiep database
- Backend luon nam quyen chuan bi du lieu va kiem soat ket qua

Dieu nay giup he thong:

- de debug
- de audit
- de kiem soat chi phi AI
- tranh de AI "tu do" suy dien tren du lieu goc

## 3. Tai sao khong de AI doc truc tiep database

Ve ly thuyet co the xay dung kieu "AI agent goi SQL", nhung trong bai toan ATS hien tai cach do khong phu hop.

Ly do:

- Database chua du lieu nhay cam cua ung vien
- HR can ket qua on dinh va co the giai thich
- Ranking can luu lai de audit
- Prompt cua nguoi dung mang tinh tu nhien, khong nen bien thanh quyen truy cap DB
- So luong ung vien co the len den hang tram hoac hon, can toi uu chi phi va latency

Vi vay, backend se:

- query du lieu ATS
- chuan hoa thanh `candidate pool`
- ranking theo logic xac dinh
- chi gui payload da rut gon sang OpenAI

Huong nay goi la `backend-prepared AI payload`.

## 4. Kien truc tong the cua implementation hien tai

Chuc nang nay dang duoc chia theo cac tang:

### 4.1 Domain

Luu cac entity phan nghiep vu cua Copilot:

- `CopilotConversation`
- `CopilotMessage`
- `CopilotRankingSession`
- `CopilotRankingResult`
- `CopilotSavedRule`
- `CopilotCandidateTag`

Tai sao can entity rieng:

- vi conversation AI khong nen chen lung tung vao bang ATS cu
- vi ranking session la du lieu nghiep vu rieng, can audit
- vi sau nay co the phat trien thanh lich su hoi thoai, save rule, tag candidate

### 4.2 Application

Tang nay chua:

- DTO request/response
- interface service
- interface repository
- logic nghiep vu chinh trong `CopilotService`

Tai sao dat logic o Application:

- de business flow khong bi dinh vao ASP.NET Controller
- de test service de hon
- de ha tang OpenAI, MinIO, EF co the thay the ma khong pha nghiep vu

### 4.3 Infrastructure

Tang nay chua:

- EF repository de doc/ghi database
- MinIO file storage service
- PDF text extractor
- OpenAI provider

Tai sao phai tach ra:

- `CopilotService` chi can biet "lay file", "doc text", "goi AI"
- no khong can biet cu the la MinIO hay S3, PdfPig hay thu vien khac, HttpClient hay SDK nao

### 4.4 API

Tang API lam nhiem vu:

- nhan HTTP request
- lay `userId` tu JWT
- goi `ICopilotService`
- tra response ve frontend

Tai sao controller rat mong:

- de controller dung nghia la adapter HTTP
- de logic ranking khong nam trong controller

## 5. Nhung file quan trong da duoc them hoac cap nhat

### 5.1 API layer

- `RecruitPro.API/Controllers/CopilotController.cs`
- `RecruitPro.API/Program.cs`
- `RecruitPro.API/appsettings.json`
- `RecruitPro.API/appsettings.Production.json`

### 5.2 Application layer

- `RecruitPro.Application/Configurations/OpenAiSettings.cs`
- `RecruitPro.Application/Interfaces/IServices/ICopilotService.cs`
- `RecruitPro.Application/Interfaces/IServices/IAiCopilotProvider.cs`
- `RecruitPro.Application/Interfaces/IRepositories/ICopilotRepository.cs`
- `RecruitPro.Application/Interfaces/IResumeTextExtractor.cs`
- `RecruitPro.Application/Interfaces/IFileStorageService.cs`
- `RecruitPro.Application/Services/CopilotService.cs`
- cac DTO trong `DTOs/Request/Copilot` va `DTOs/Response/Copilot`

### 5.3 Domain layer

- `RecruitPro.Domain/Entities/CopilotConversation.cs`
- `RecruitPro.Domain/Entities/CopilotMessage.cs`
- `RecruitPro.Domain/Entities/CopilotRankingSession.cs`
- `RecruitPro.Domain/Entities/CopilotRankingResult.cs`
- `RecruitPro.Domain/Entities/CopilotSavedRule.cs`
- `RecruitPro.Domain/Entities/CopilotCandidateTag.cs`

### 5.4 Infrastructure layer

- `RecruitPro.Infrastructure/Repositories/CopilotRepository.cs`
- `RecruitPro.Infrastructure/Service/MinioFileStorageService.cs`
- `RecruitPro.Infrastructure/Service/PdfResumeTextExtractor.cs`
- `RecruitPro.Infrastructure/Service/OpenAiCopilotProvider.cs`
- `RecruitPro.Infrastructure/Data/AppDbContext.cs`
- `RecruitPro.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- `RecruitPro.Infrastructure/RecruitPro.Infrastructure.csproj`

### 5.5 Database bootstrap

- `init.sql`

### 5.6 Tai lieu

- `docs/ai-recruitment-copilot-design.md`
- `docs/ai-copilot-implementation-guide.md`

## 6. Cac bang du lieu duoc them vao database

Copilot can luu du lieu rieng. Neu khong luu, frontend co the hien ket qua mot lan, nhung:

- khong co lich su hoi thoai
- khong biet prompt nao tao ra ranking nao
- khong audit duoc vi sao candidate bi reject
- khong tai su dung duoc context cho hoi thoai tiep theo

Vi vay da them cac bang sau vao `init.sql`:

### 6.1 `copilot_conversations`

Luu mot hoi thoai theo `job + HR`.

Dung de:

- resume conversation
- gan session ranking moi nhat vao conversation
- mo rong tiep cho chat theo ngu canh

### 6.2 `copilot_messages`

Luu tung message trong hoi thoai.

Dung de:

- luu prompt cua HR
- luu tom tat response cua assistant
- debug
- audit

### 6.3 `copilot_ranking_sessions`

Moi lan HR an danh gia candidate se tao mot session moi.

Dung de:

- luu prompt dau vao
- luu rules da chuan hoa
- luu tong so candidate da danh gia
- luu model da dung

### 6.4 `copilot_ranking_results`

Moi candidate trong moi session se co mot dong ket qua.

Dung de:

- luu diem
- luu thu hang
- luu recommendation
- luu reject reason
- luu strengths / weaknesses

### 6.5 `copilot_saved_rules`

Bang nay da duoc tao san cho huong mo rong.

Dung de:

- luu rule HR muon tai su dung
- vi du: `Reject FPT`, `Require Java`, `Min 2 years exp`

### 6.6 `copilot_candidate_tags`

Bang nay da duoc tao san cho huong mo rong.

Dung de:

- danh tag cho candidate
- su dung cho filter chip, labels, audit

## 7. Tai sao phai them entity va mapping trong EF Core

Khi da co bang database, backend can entity va mapping de:

- query bang LINQ/EF
- quan ly relation
- luu object graph
- giu code repository sach hon

Neu chi viet SQL thu cong cho tung bang:

- code se roi rac
- kho maintain
- kho mo rong relation
- kho unit test / integration test

Vi vay trong `AppDbContext` da them:

- `DbSet` cho cac bang Copilot
- Fluent API mapping
- constraint, precision, relation

Tai sao dung Fluent API thay vi data annotation:

- mapping database tap trung mot noi
- de doc khi schema phuc tap
- de custom relation va index ro hon

## 8. Luong xu ly backend tu dau den cuoi

Day la phan quan trong nhat.

### 8.1 B1 - Frontend goi `GET /api/copilot/jobs`

Muc dich:

- load danh sach Job cho HR chon

Backend:

- controller goi `ICopilotService.GetJobsAsync()`
- service goi `ICopilotRepository.GetJobOptionsAsync()`

Tai sao co endpoint rieng:

- UI Copilot chi can job dang tuyen va thong tin tom tat
- khong nen dung API job list tong quat neu du lieu khac nhu cau

### 8.2 B2 - Frontend goi `POST /api/copilot/conversations`

Muc dich:

- tao moi hoac resume hoi thoai theo `jobId`

Backend:

- check da ton tai conversation moi nhat cua user voi job nay chua
- neu co thi tra conversation cu
- neu chua co thi tao moi

Tai sao lam theo `job-scoped conversation`:

- prompt AI can gan voi mot job cu the
- tranh HR hoi lang nhang qua nhieu job trong cung context
- sau nay follow-up prompt de xu ly hon

### 8.3 B3 - Frontend goi `GET /api/copilot/jobs/{jobId}/candidates`

Muc dich:

- load candidate pool cho Job da chon

Backend se query:

- thong tin Job
- danh sach application cua Job
- thong tin user / candidate
- education
- experience
- skill
- CV summary / resume url

Tai sao load candidate pool truoc khi goi AI:

- de UI co the hien danh sach raw ngay
- de backend kiem soat toan bo payload
- de debug de hon neu ranking sai

### 8.4 B4 - Frontend goi `POST /api/copilot/conversations/{conversationId}/rankings`

Day la diem trung tam cua he thong.

Backend trong `CopilotService.CreateRankingAsync()` se lam lan luot:

1. Xac thuc conversation co thuoc user va job hay khong
2. Load lai candidate pool
3. Enrich candidate bang text doc tu PDF neu co
4. Phan tich prompt thanh `normalized rules`
5. Ranking deterministic tren backend
6. Neu co OpenAI API key thi gui payload sang OpenAI de refine ket qua
7. Tao `CopilotRankingSession`
8. Tao `CopilotRankingResult`
9. Tao `CopilotMessage` cho user va assistant
10. Luu session id moi nhat vao `copilot_conversations`

Day la workflow `hybrid ranking`.

## 9. Phan prompt parsing deterministic trong `CopilotService`

Hien tai service co method `BuildRules()`.

Method nay dang lam gi:

- dua prompt ve lowercase
- tim skill nao xuat hien trong prompt
- ket hop skill tu job requirements va danh sach common skills
- nhan dien rule reject FPT
- nhan dien `minExperienceYears` bang regex

Vi du:

- prompt `Tim ung vien biet Java Spring Boot`
  - se tim ra `Java`, `Spring Boot`
- prompt `Loai toan bo sinh vien FPT`
  - se tao auto reject rule theo `education contains FPT`
- prompt `Uu tien tren 2 nam kinh nghiem`
  - se tim ra `minExperienceYears = 2`

Tai sao van can parser deterministic nay du da co OpenAI:

- he thong van chay duoc khi chua co API key
- he thong van chay duoc neu OpenAI loi
- frontend van co ket qua on dinh
- de chi phi AI khong tro thanh single point of failure

Noi cach khac: day la lop `safety net`.

## 10. Phan ranking deterministic trong `CopilotService`

Method `RankCandidates()` dang tinh diem theo 4 nhom:

- `SkillScore` toi da 40
- `ExperienceScore` toi da 30
- `EducationScore` toi da 20 gan dung
- `ProjectScore` toi da 10

Tong diem se duoc tinh thanh `TotalScore`.

Auto reject duoc check truoc:

- neu candidate match rule reject, `IsAutoRejected = true`
- `RejectReason` se duoc gan
- `Recommendation` se thanh `Reject`

Ket qua se duoc sap xep:

1. Candidate khong bi auto reject
2. `TotalScore` giam dan
3. `SkillScore` giam dan
4. `ExperienceScore` giam dan

Tai sao ranking deterministic van la cot song chinh:

- nhanh
- re
- de giai thich
- de test
- phu hop khi co nhieu candidate

Neu de AI scoring tung candidate tu do:

- chi phi cao
- ket qua de dao dong
- kho giai thich vi sao hom nay 82, mai 76

## 11. Tai sao can doc CV PDF

User da chot mot yeu cau rat quan trong: `CV la PDF`.

Neu khong doc PDF, he thong chi dua vao:

- skill da map trong DB
- education
- experience
- summary co san

Nhu vay se bo sot kha nhieu thong tin nam trong file CV:

- project details
- tech stack khong duoc map het vao db
- tu khoa nhu `Docker`, `Redis`, `Microservices`, `PostgreSQL`
- mo ta vai tro trong du an

Vi vay da them co che doc text tu PDF.

## 12. Luong doc PDF hien tai

### 12.1 Interface `IFileStorageService`

Da mo rong interface bang method:

- `DownloadFileAsync(string objectName)`

Tai sao can method nay:

- truoc day storage service chu yeu de upload, delete, presigned url
- AI Copilot can doc noi dung file tu backend
- backend phai tai file private tu MinIO ve dang `Stream`

### 12.2 `MinioFileStorageService.DownloadFileAsync()`

Method nay:

- nhan `objectName`
- normalize object name
- goi MinIO de tai object
- copy stream vao `MemoryStream`
- tra stream ve cho caller

Tai sao khong tra truc tiep stream tu MinIO:

- callback cua MinIO tiep can stream theo kieu async callback
- de backend co stream co the seek lai de parser PDF doc de dang
- `MemoryStream` de xu ly don gian hon

Trade-off:

- ton RAM hon neu file lon
- nhung doi lai code de kiem soat va on dinh cho phien ban hien tai

Voi ATS thong thuong, CV PDF thuong khong qua lon, nen trade-off nay chap nhan duoc.

### 12.3 Interface `IResumeTextExtractor`

Da them interface:

- `ExtractTextAsync(Stream resumeStream, CancellationToken cancellationToken = default)`

Tai sao can interface rieng thay vi nhung code doc PDF thang vao `CopilotService`:

- tach nghiep vu ranking khoi logic parser file
- sau nay co the them:
  - Word extractor
  - OCR extractor
  - cached extractor
- de test mock de hon

### 12.4 `PdfResumeTextExtractor`

Class nay dung package `UglyToad.PdfPig`.

No lam gi:

- reset stream ve dau neu seek duoc
- mo file PDF
- duyet tung page
- ghep text cua tung page vao `StringBuilder`
- tra ve chuoi text cuoi cung

Tai sao chon `PdfPig`:

- de dung
- phu hop nhu cau text extraction co ban
- khong can OCR server-side cho file PDF text thong thuong
- nhe hon so voi viec tu xay parser

Tai sao package version trong project la `1.7.0-custom-5`:

- vi day la version NuGet source hien tai resolve duoc trong moi truong build cua du an
- ban `1.7.0` thuong khong restore duoc o local environment hien tai

Noi thang ra: day la quyet dinh thuc dung de build pass va deploy duoc.

## 13. `EnrichPoolWithResumeTextAsync()` dang lam gi

Day la diem noi giua candidate pool va PDF extraction.

Voi moi candidate:

1. Lay `ResumeUrl`
2. Dung `IFileStorageService.DownloadFileAsync()` de tai file
3. Dung `IResumeTextExtractor.ExtractTextAsync()` de doc text
4. Cat text theo `MaxResumeCharsPerCandidate`
5. Ghep vao `CvSummary`

Tai sao ghep vao `CvSummary` thay vi them field moi khac:

- DTO hien tai da co `CvSummary`
- ranking deterministic da doc `CvSummary`
- OpenAI payload da co san field tom tat
- giam so file va DTO phai sua o version dau

Tai sao phai cat theo `MaxResumeCharsPerCandidate`:

- tranh payload qua lon
- tranh ton token OpenAI
- tranh latency tang vot
- tranh CV qua dai lam prompt bi loang

Day la mot config rat quan trong khi deploy that.

## 14. Tich hop OpenAI trong backend

Chuc nang AI that duoc dong goi trong:

- `IAiCopilotProvider`
- `OpenAiCopilotProvider`

Tai sao khong goi OpenAI truc tiep trong `CopilotService`:

- `CopilotService` dang la business orchestration
- OpenAI la external provider
- can de thay the sau nay neu doi model, doi vendor, hoac viet fake provider test

Do do phai tao abstraction.

## 15. `OpenAiSettings` dung de lam gi

Class `OpenAiSettings` gom:

- `ApiKey`
- `BaseUrl`
- `Model`
- `MaxCandidatesForAi`
- `MaxResumeCharsPerCandidate`
- `Enabled`

Tai sao can config hoa thay vi hard-code:

- deploy moi moi truong khac nhau
- co the tat AI ma khong can sua code
- doi model ma khong can build lai
- giam chi phi bang cach giam `MaxCandidatesForAi`
- giam token bang cach giam `MaxResumeCharsPerCandidate`

Day la mot trong nhung phan quan trong nhat de "chi can them API key la chay".

## 16. `OpenAiCopilotProvider` dang lam gi

Class nay:

1. Doc config tu `OpenAiSettings`
2. Neu `Enabled = false` hoac `ApiKey` rong thi return `null`
3. Gioi han so candidate gui sang AI theo `MaxCandidatesForAi`
4. Tao payload gom:
   - Job
   - Prompt cua HR
   - Normalized rules tu backend
   - Candidate list
   - Ket qua ranking deterministic
5. Goi OpenAI Responses API bang `HttpClient`
6. Parse output text
7. Deserialize ve `CopilotPromptResponseDto`
8. Neu co loi thi log warning va fallback

Tai sao khong de OpenAI tu lam tat ca tu dau:

- deterministic ranking da la baseline rat tot
- AI khong nen la noi duy nhat sinh ra ket qua
- AI hien tai dong vai tro:
  - refine normalized rules
  - dieu chinh ranking neu can
  - tra explanation, strengths, weaknesses

Noi cach khac: AI hien tai duoc dung theo kieu `assist`, khong dung theo kieu `replace everything`.

## 17. Tai sao dung Responses API va `HttpClient`

Class `OpenAiCopilotProvider` dang goi:

- `POST {BaseUrl}/responses`

Tai sao dung `HttpClient`:

- de project it phu thuoc hon
- de payload/request-response minh bach
- de de log, debug, thay doi
- de khong phai mang them SDK neu nhu cau hien tai con don gian

Tai sao dung Responses API:

- phu hop cho bai toan prompt -> output co cau truc
- de gom input theo kieu hoi thoai
- khong can xay abstraction phuc tap hon o version dau

## 18. Tai sao van fallback neu OpenAI loi

Trong `OpenAiCopilotProvider`, neu:

- khong co API key
- OpenAI tra loi HTTP error
- output parse that bai
- co exception bat ky

thi provider tra `null`.

Sau do `CopilotService` se giu ket qua deterministic da tinh san.

Tai sao fallback la bat buoc:

- HR khong the bi block chi vi OpenAI tam thoi loi
- deploy production can uu tien system availability
- user co the chua nap API key ngay tu dau
- trong giai doan demo/dev, he thong van phai chay

Day la cach lam "safe by default".

## 19. Tai sao `ModelName` hien tai van la `deterministic-copilot-v1`

Hien tai `CopilotService` dang luu:

- `ModelName = "deterministic-copilot-v1"`

Ly do:

- ranking baseline hien van do backend sinh ra
- du co OpenAI tham gia, orchestration chinh van mang tinh deterministic-first

Neu muon chinh xac hon sau nay, co the doi thanh:

- model OpenAI that khi AI co tham gia
- hoac luu ca:
  - deterministic engine name
  - ai model name

Version hien tai uu tien chay on dinh truoc.

## 20. Tai sao chua luu text CV da extract vao database

Hien tai text tu PDF duoc doc `on demand` moi lan ranking.

Tai sao chua luu:

- de version dau implement nhanh
- tranh sua them bang snapshot luc chua can
- de backend tap trung chay end-to-end truoc

Nhuoc diem:

- moi lan ranking phai tai PDF va parse lai
- ton thoi gian hon

Khi nao nen toi uu:

- khi so candidate tang
- khi cung mot CV bi doc nhieu lan
- khi muon ranking nhanh hon nua

Huong toi uu sau nay:

- tao bang `candidate_resume_snapshots`
- luu plain text da extract
- cap nhat khi candidate upload CV moi

## 21. Tai sao chi gui top N candidate sang OpenAI

Config:

- `OpenAi__MaxCandidatesForAi`

Dung de gioi han candidate gui sang AI.

Tai sao phai gioi han:

- tranh vuot token
- tranh request qua nang
- tranh latency cao
- tranh chi phi tang khong kiem soat

Backend van co the ranking toan bo candidate bang deterministic.
AI chi can tham gia tren tap da duoc cat gon hop ly.

Day la quyet dinh rat quan trong cho he thong ATS co the mo rong tot.

## 22. Tai sao merge AI result vao deterministic result thay vi thay the hoan toan

Trong `CreateRankingAsync()`:

- backend ranking truoc
- sau do moi goi AI
- neu AI tra ket qua hop le thi ket qua AI duoc dung de cap nhat `rules` va `results`

Tai sao khong goi AI truoc roi moi ranking sau:

- deterministic ranking la fallback
- backend can baseline truoc khi AI that bai
- AI co them context la ket qua ranking baseline, nhin prompt de hon

Noi ngan gon:

- backend danh khung
- AI danh bong

Kieu nay an toan hon rat nhieu.

## 23. Controller duoc viet mong co y nghia gi

`CopilotController` chi gom 4 endpoint chinh:

- `GET /api/copilot/jobs`
- `POST /api/copilot/conversations`
- `GET /api/copilot/jobs/{jobId}/candidates`
- `POST /api/copilot/conversations/{conversationId}/rankings`

Controller chi:

- nhan request
- lay `userId` tu JWT
- goi service
- tra status code

Tai sao nen giu controller mong:

- de test business logic o service
- de sau nay doi transport layer van it anh huong
- de file controller de doc

Day la mot thoi quen kien truc rat nen giu.

## 24. Program.cs da duoc cap nhat nhu the nao

Trong `Program.cs` da bo sung:

- `builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAi"));`

Tai sao can dong nay:

- de bind config tu `appsettings` va env var vao `OpenAiSettings`
- de `IOptions<OpenAiSettings>` inject vao service/provider

Neu thieu dong nay:

- `OpenAiCopilotProvider` khong co config
- khong doc duoc API key

## 25. Service registration da duoc cap nhat nhu the nao

Trong DI container da them:

- `ICopilotService -> CopilotService`
- `ICopilotRepository -> CopilotRepository`
- `IResumeTextExtractor -> PdfResumeTextExtractor`
- `IAiCopilotProvider -> OpenAiCopilotProvider` qua `AddHttpClient`

Tai sao `IAiCopilotProvider` dang dung `AddHttpClient`:

- provider can `HttpClient`
- .NET se quan ly connection pool tot hon
- tranh tao `HttpClient` thu cong sai cach

Day la pattern chuan cho outbound HTTP trong ASP.NET Core.

## 26. Cac package duoc cai va ly do

### 26.1 `UglyToad.PdfPig`

Da them vao `RecruitPro.Infrastructure.csproj`.

Ly do:

- doc text tu PDF
- phu hop bai toan CV text-based
- khong can OCR o version dau

### 26.2 Khong them OpenAI SDK

Hien tai chua them package OpenAI SDK.

Ly do:

- nhu cau hien tai chi can 1 request `responses`
- `HttpClient` la du
- giam dependency
- de payload/request de debug hon

Neu sau nay can:

- streaming
- structured outputs nang hon
- uploads phuc tap

thi co the can nhac SDK.

## 27. Cac config can co khi deploy

Trong backend da chuan bi san:

- `appsettings.json`
- `appsettings.Production.json`
- `.env.railway.example`

Nhom config can co:

### 27.1 Database

- `ConnectionStrings__Mycnn`

### 27.2 JWT

- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__ExpiryMinutes`
- `Jwt__RefreshTokenExpiryMinutes`

### 27.3 CORS

- `Cors__AllowedOrigins__0`
- `Cors__AllowedOrigins__1`

### 27.4 MinIO

- `MinioSettings__Endpoint`
- `MinioSettings__AccessKey`
- `MinioSettings__SecretKey`
- `MinioSettings__BucketName`
- `MinioSettings__UseSsl`
- `MinioSettings__BaseUrl`
- `MinioSettings__PresignedUrlExpiryInSeconds`

### 27.5 OpenAI

- `OpenAi__Enabled`
- `OpenAi__ApiKey`
- `OpenAi__BaseUrl`
- `OpenAi__Model`
- `OpenAi__MaxCandidatesForAi`
- `OpenAi__MaxResumeCharsPerCandidate`

Tai sao config duoc chuan bi san:

- ong chi can nap API key la bat AI that
- giam rui ro quên config luc deploy
- moi truong dev/prod de doi de hon

## 28. Khi chua co OpenAI API key thi he thong se ra sao

Neu `OpenAi__ApiKey` rong:

- endpoint ranking van chay
- backend van parse prompt co ban
- backend van tinh diem
- backend van luu ranking session
- frontend van co ket qua hien thi

Dieu nay rat quan trong cho giai doan:

- local dev
- demo nhanh
- production tam thoi khi key bi het han

No co nghia la AI khong phai "nut song con" cua he thong.

## 29. Gioi han cua implementation hien tai

Can noi that de sau nay de nang cap:

### 29.1 Prompt parsing deterministic con don gian

Hien tai moi detect tot:

- mot so skill pho bien
- rule reject FPT
- so nam kinh nghiem

Neu prompt phuc tap hon, can mo rong parser hoac dua nhieu hon cho AI.

### 29.2 Chua co resume snapshot cache

Moi lan ranking lai tai PDF va parse lai.

### 29.3 Chua co save rule flow day du

Bang va entity da co, nhung luong API save/toggle/list rule chua hoan tat.

### 29.4 Chua co conversation history API day du

Bang message da co, nhung luong doc lich su va feed ngu canh dai han cho AI con co the mo rong tiep.

### 29.5 Chua co OCR

Neu PDF la scan image, `PdfPig` co the khong lay duoc text tot.

Khi gap truong hop do, can bo sung OCR pipeline.

## 30. Neu muon nang cap tiep thi nen di theo huong nao

Thu tu nang cap hop ly:

1. Luu `resume_text_snapshot` vao DB de tranh parse PDF nhieu lan
2. Them API save/list/toggle saved rules
3. Them API lay conversation history va ranking session detail
4. Cai thien parser prompt hoac dung AI de parse rules manh hon
5. Tach rie ng `summary`, `resumeText`, `projects` thanh field ro rang thay vi nhung vao `CvSummary`
6. Them OCR cho scanned PDF neu can
7. Ghi nhan token usage that tu OpenAI response vao DB

Day la lo trinh nang cap tot vi no di tu:

- performance
- audit
- UX
- AI quality

## 31. Cach hieu ngan gon ve kien truc hien tai

Neu phai mo ta chuc nang nay trong 5 dong:

1. Backend lay candidate pool theo job
2. Backend doc them CV PDF tu MinIO
3. Backend ranking deterministic truoc
4. OpenAI refine ket qua neu co API key
5. Tat ca duoc luu thanh conversation + ranking session de audit va tai su dung

Do la tinh than chinh cua implementation nay.

## 32. Ket luan

Implementation hien tai chon huong:

- practical truoc
- stable truoc
- deploy duoc truoc
- AI ho tro, khong chi huy toan bo

Tai sao day la huong hop ly:

- phu hop ATS thuc te
- de demo
- de debug
- de mo rong
- khong bi khoa chat vao mot nha cung cap AI hay mot kie u parser file

Neu sau nay ong quay lai nhin code va thay nhieu class duoc tach ra, ly do la vi:

- muon luong nghiep vu ro rang
- muon fallback an toan
- muon deploy production khong vo tran
- muon sau nay nang cap ma khong pha tat ca

Noi ngan gon: day khong phai la cach "AI nhay vao code cho vui", ma la mot implementation co chu y de he thong ATS chay ben va mo rong duoc.
