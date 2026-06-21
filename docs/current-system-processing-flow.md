# Current System Processing Flow

## 1. Muc tieu tai lieu

Tai lieu nay mo ta luong xu ly hien tai cua he thong RecruitPro sau cac thay doi moi nhat:

- ranking uu tien du lieu profile luu trong he thong
- CV duoc dung de parse va dong bo vao profile cau truc
- thong tin candidate sau khi save tren he thong duoc xem la CV chuan cua ung vien
- profile candidate ho tro cac section linh hoat thay vi chi gom mot vai nhom co dinh
- AI provider duoc dat ten chung la `AiProvider`

Tai lieu tap trung vao 5 luong chinh:

1. Dang ky candidate va upload CV
2. Parse CV thanh du lieu profile
3. Quan ly profile candidate
4. Embedding va semantic scoring
5. Copilot ranking va copilot chat

## 2. Tong quan kien truc

He thong duoc chia thanh cac lop chinh:

- `RecruitPro.API`
  - nhan request HTTP
  - bind config
  - dang ky DI
  - chay background service semantic scoring
- `RecruitPro.Application`
  - chua business logic
  - dieu phoi flow ranking, profile, parse CV, scoring
- `RecruitPro.Infrastructure`
  - repository EF Core
  - file storage
  - AI provider
  - embedding provider
- `RecruitPro.Domain`
  - entity va enum
- `RecruitPro.API + React frontend`
  - FE goi API
  - hien thi profile, CV parse preview, copilot ranking

## 3. AI va config hien tai

He thong hien tai dung ten config chung:

- section config: `AiProvider`
- class config: `AiProviderSettings`

Config nay duoc bind trong:

- [Program.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.API/Program.cs)

He thong dang su dung cac implementation ha tang:

- `AiResumeParserProvider`
- `AiEmbeddingProvider`
- `AiCopilotProvider`
- `AiCompatibleApiHelper`

Luu y:

- Base URL co the tro toi Gemini OpenAI-compatible endpoint
- code khong con phu thuoc vao ten `OpenAi`
- nghiep vu chi biet den `AiProvider`, `IResumeParsingAiProvider`, `IEmbeddingProvider`, `IAiCopilotProvider`

## 4. Model du lieu candidate hien tai

### 4.1 Profile co dinh cu

He thong van giu cac cot/table cu de tuong thich nguoc:

- `candidate_profiles`
- `candidate_skills`
- `candidate_projects`
- cac JSON cu nhu:
  - `experience_entries_json`
  - `education_records_json`
  - `certification_records_json`
  - `language_records_json`

### 4.2 Model linh hoat moi

He thong da them model moi de luu profile theo cau truc linh hoat:

- `candidate_profile_sections`
- `candidate_profile_section_items`

Y nghia:

- `candidate_profile_sections`
  - dai dien cho mot dau muc lon
  - vi du: Experience, Education, Projects, Vinh danh, Hoat dong, Publications
- `candidate_profile_section_items`
  - la cac muc con ben trong tung section
  - moi item co the co title, subtitle, organization, location, description, tags, moc thoi gian, attributes mo rong

Loi ich:

- khong bi khoa cung vao cac nhom co dinh
- FE co the cho user tao dau muc lon moi
- ranking va semantic flow co mot nguon du lieu cau truc thong nhat hon

### 4.3 Quy uoc source of truth moi

Quy uoc hien tai cua he thong:

- CV file la nguon input de parse
- profile da duoc luu trong DB moi la nguon su that cuoi cung
- co the hieu ngan gon la:
  - `CV upload -> parse -> map vao he thong`
  - `du lieu tren he thong = CV chuan cua ung vien`

Y nghia:

- ranking, recommendation va semantic search deu uu tien profile da normalize
- user sua profile tren he thong nghia la dang sua CV chuan cua minh
- CV goc khong con la source of truth ve sau

## 5. Luong 1: Candidate dang ky va tao profile

Service chinh:

- [CandidateService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/CandidateService.cs)

Flow:

1. FE gui thong tin user va profile co ban.
2. `CandidateService.RegisterAsync(...)` tao:
   - `users`
   - `user_roles`
   - `candidate_profiles`
3. Neu candidate upload CV ngay luc dang ky:
   - file duoc day len storage
   - tao `candidate_resumes`
   - `resume_url` va metadata duoc cap nhat vao profile
4. Profile moi mac dinh co:
   - `ResumeParseStatus = NotStarted`
   - `CandidateEmbeddingStatus = NotStarted`

Ket qua:

- candidate da co tai khoan
- da co profile rong hoac profile co CV
- chua ranking ngay tai buoc nay

## 6. Luong 2: Upload CV va parse CV

### 6.1 Upload CV

Khi user upload CV:

1. API nhan file.
2. file duoc luu thong qua `IFileStorageService`
3. tao them mot record trong `candidate_resumes`
4. danh dau version hien tai
5. cap nhat thong tin parse status

### 6.2 Parse CV

Parse CV co 2 tang:

1. text extraction
2. AI parsing hoac heuristic fallback

Service lien quan:

- `IResumeTextExtractor`
- `IResumeParsingAiProvider`
- `CandidateService.ParseResumeAsync(...)`

Flow chi tiet:

1. He thong doc file CV.
2. `IResumeTextExtractor` trich text tu PDF/DOC/DOCX.
3. Neu trich text that bai:
   - status parse that bai
   - tra message cho FE
4. Neu trich text thanh cong:
   - goi `AiResumeParserProvider`
   - AI tra ve profile + skills + experiences + projects + educations + certifications + languages + awards + activities
5. Neu AI that bai hoac output khong hop le:
   - fallback sang heuristic parser trong `CandidateService`

### 6.3 Parse preview va apply vao profile

Sau khi parse xong:

1. FE nhan `CandidateResumeParseResponseDto`
2. response gom:
   - profile co ban
   - skills
   - cac list co dinh
   - `sections`
3. user xem preview
4. user bam apply/save
5. `CandidateService` se:
   - cap nhat profile field co ban
   - replace skills
   - replace cac list cu
   - dong bo/replace `candidate_profile_sections`

Luu y moi:

- parse CV khong con la dich den cuoi cung
- dich den cuoi cung la du lieu profile da duoc luu co cau truc trong DB

### 6.4 Embedding sau khi candidate data thay doi

Sau moi lan candidate data duoc save thanh cong, he thong se co gang refresh embedding ngay:

- update profile
- update skills
- create/update/delete experience
- apply parsed resume vao profile

Muc tieu:

- candidate vector luon bam sat profile moi nhat
- semantic recommendation co vector moi som
- dashboard recommendation khong can doi luc doc moi refresh lan dau

## 7. Luong 3: Quan ly profile candidate

### 7.1 API va service

Service trung tam:

- [CandidateService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/CandidateService.cs)

Repository:

- `CandidateProfileRepository`

### 7.2 Cach he thong doc profile

Khi load profile:

1. repository include:
   - `User`
   - `CandidateSkills`
   - `Projects`
   - `Resumes`
   - `Sections`
   - `Section.Items`
2. `CandidateProfileSectionHelper.BuildSections(profile)` duoc dung de:
   - uu tien section moi neu da ton tai
   - neu chua co thi derive tu data cu

Y nghia:

- FE luon co the nhan `sections`
- he thong van chay duoc voi data cu

### 7.3 Cach he thong save profile

Khi user save profile:

1. FE gui:
   - thong tin profile co ban
   - skills
   - cac list cu
   - `sections`
2. `CandidateService.ApplyProfileUpdateAsync(...)` xu ly:
   - update field candidate
   - replace skills
   - replace projects
   - cap nhat cac JSON cu
   - replace `candidate_profile_sections` neu request co `sections`
3. Neu request khong gui `sections`:
   - he thong sync section tu du lieu cu qua `SyncLegacySectionsAsync(...)`

### 7.4 FE candidate profile

Man hinh chinh:

- `CandidateProfileAndCVManagementScreen.tsx`

FE hien tai ho tro:

- sua profile co ban
- quan ly skills
- parse CV preview
- apply CV parse vao form
- tao them `Flexible Sections`
  - tao mot dau muc lon moi
  - them nhieu muc con ben trong

Vi du dau muc lon tu user:

- Vinh danh
- Hoat dong
- Publications
- Speaking
- Volunteer

## 8. Luong 4: Embedding va semantic scoring

### 8.1 Muc tieu

Semantic flow dung de:

- tim ung vien phu hop theo nghia
- tinh diem semantic cho application
- giam phu thuoc vao so khop ky nang keyword don thuan

### 8.2 Text dung de embedding

He thong hien tai khong nen embedding dua vao CV text thuan nua.

Text embedding cho candidate duoc build tu:

- profile field co ban
- skills
- projects
- va dac biet la `CandidateProfileSectionHelper.BuildStructuredNarrative(profile)`

Service lien quan:

- `ApplicationSemanticScoringService`
- `SemanticDiscoveryService`

Y nghia:

- nguon embedding la profile cau truc da luu trong DB
- CV chi la nguon input de parse, khong phai nguon chinh de ranking lau dai

### 8.2.1 Candidate embedding

Candidate embedding duoc xem la vector dai dien cho CV chuan cua ung vien.

Nguon de sinh candidate vector:

- headline
- bio
- skills
- projects
- experience
- education
- certifications
- languages
- structured sections

Noi ngan gon:

- `candidate profile normalized in DB -> candidate embedding vector`

### 8.2.2 Job embedding

Job embedding duoc xem la vector dai dien cho JD chuan cua tin tuyen dung.

Nguon de sinh job vector:

- title
- short pitch
- description
- requirements
- required skills
- nice-to-have skills
- work mode
- employment type
- location
- min experience

Noi ngan gon:

- `job data normalized in DB -> job embedding vector`

### 8.3 Background scoring

API chay:

- `SemanticScoringBackgroundService`

Flow:

1. He thong queue application can scoring.
2. Background service lay application tu queue.
3. `ApplicationSemanticScoringService`:
   - build embedding text cho job
   - build embedding text cho candidate
   - goi `IEmbeddingProvider`
   - tinh similarity score
   - cap nhat:
     - `semantic_score`
     - `final_score`
     - `score_status`
     - `scored_at`

### 8.4 Embedding trigger moi

Ngoai semantic scoring background, he thong hien tai da bo sung trigger refresh embedding ngay sau khi data doi:

- candidate profile save xong -> refresh candidate embedding
- parsed CV apply xong -> refresh candidate embedding
- job create xong -> refresh job embedding
- job patch/update xong -> refresh job embedding
- job status update xong -> refresh job embedding

Nguyen tac:

- business data commit truoc
- embedding refresh sau
- neu embedding fail thi khong rollback nghiep vu chinh
- recommendation/search van co the tu refresh lai sau do neu can

## 9. Luong 5: Copilot ranking

### 9.1 Muc tieu

Copilot ranking danh sach ung vien theo job va prompt cua recruiter/HR.

Service chinh:

- [CopilotService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/CopilotService.cs)

Repository chinh:

- [CopilotRepository.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Infrastructure/Repositories/CopilotRepository.cs)

### 9.2 Candidate pool cho ranking

Thay doi quan trong hien tai:

- ranking khong con uu tien CV text raw
- ranking uu tien profile data da duoc luu trong he thong

`CopilotRepository.GetCandidatePoolAsync(jobId)` hien tai load:

- application
- candidate profile
- skills
- projects
- sections
- resumes

Sau do build `CopilotCandidateDto` voi:

- `Skills`
- `Education`
- `ExperienceYears`
- `CvSummary`

Trong do:

- `CvSummary` duoc sinh tu `CandidateProfileSectionHelper.BuildStructuredNarrative(profile)`

### 9.3 Flow ranking

Flow tong quat:

1. HR chon job.
2. HR nhap prompt/rules.
3. `CopilotService.CreateRankingAsync(...)`:
   - lay candidate pool
   - parse deterministic rules
   - scoring/ranking deterministic truoc
   - neu du dieu kien va AI duoc bat:
     - goi `IAiCopilotProvider`
     - refine ket qua
4. ket qua duoc luu vao:
   - `copilot_ranking_sessions`
   - `copilot_ranking_results`

### 9.4 Dieu gi khac voi truoc

Truoc day:

- ranking co xu huong bo sung resume text vao pool

Hien tai:

- luong ranking da bo viec enrich bang CV text thuan
- ranking lay profile da parse, da normalize, da luu trong DB

Ket qua:

- on dinh hon
- it phu thuoc vao format CV
- dung du lieu he thong lam source of truth

## 10. Luong 6: Job recommendation bang vector search

Job recommendation hien tai nen duoc hieu theo flow sau:

1. candidate upload CV hoac sua profile
2. he thong parse va luu profile cau truc
3. profile cau truc duoc xem la CV chuan
4. he thong refresh candidate embedding
5. HR tao/sua job
6. he thong refresh job embedding
7. dashboard candidate goi `GetRecommendedJobsForCandidateAsync(...)`
8. he thong tinh cosine similarity giua:
   - candidate vector
   - job vector
9. sap xep giam dan similarity
10. tra ve danh sach recommended jobs

Dieu nay co nghia:

- candidate embedding va job embedding la search vector
- job recommendation la bai toan vector search job-candidate
- semantic recommendation va copilot ranking dang bat dau dung cung mot nen tang du lieu normalized

## 11. Luong 7: Copilot chat

Copilot chat van la luong rieng voi ranking.

Muc tieu:

- hoi dap ve pool ung vien
- xin giai thich ranking
- xin tom tat candidate

Khac voi ranking:

- chat co the van enrich them bang resume text trong mot so tinh huong
- ranking thi khong nen dua vao CV raw nua

Noi ngan gon:

- `ranking = system profile first`
- `chat = system profile first, co the tham chieu them CV neu can`

## 12. Luong du lieu end-to-end de doi team de nho

Luot 1:

1. User upload CV
2. He thong extract text
3. AI/heuristic parse CV
4. Parse preview tra ve FE
5. User xac nhan
6. He thong save thanh profile cau truc
7. Section/item tro thanh nguon du lieu chinh
8. He thong refresh candidate embedding

Luot 2:

1. Candidate update profile tren FE
2. Them skills
3. Them projects
4. Them section linh hoat nhu Vinh danh
5. He thong save vao DB
6. He thong refresh candidate embedding

Luot 3:

1. HR tao hoac sua job
2. He thong save job vao DB
3. He thong refresh job embedding
4. Embedding va semantic scoring dung profile cau truc
5. Job recommendation dung candidate vector va job vector
6. Copilot ranking dung profile cau truc
7. CV chi con vai tro input va tham chieu phu

## 13. Bang tom tat source of truth

### 12.1 Candidate profile

Nguon uu tien:

1. `candidate_profile_sections` + `candidate_profile_section_items`
2. data legacy de backfill/compatibility
3. CV file khong phai source of truth

### 12.2 Ranking

Nguon uu tien:

1. `CopilotCandidateDto` sinh tu profile trong DB
2. deterministic scoring
3. AI refine neu bat

### 12.3 Semantic scoring

Nguon uu tien:

1. embedding text sinh tu profile cau truc
2. khong dua vao CV raw la nguon chinh

### 12.4 Job recommendation

Nguon uu tien:

1. candidate embedding vector
2. job embedding vector
3. cosine similarity
4. fallback keyword search chi la du phong

## 14. File quan trong nen doc khi can debug

### API

- [Program.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.API/Program.cs)
- [SemanticScoringBackgroundService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.API/SemanticScoringBackgroundService.cs)

### Application

- [CandidateService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/CandidateService.cs)
- [CopilotService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/CopilotService.cs)
- [ApplicationSemanticScoringService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/ApplicationSemanticScoringService.cs)
- [SemanticDiscoveryService.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Services/SemanticDiscoveryService.cs)
- [CandidateProfileSectionHelper.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Application/Common/CandidateProfileSectionHelper.cs)

### Infrastructure

- [CopilotRepository.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Infrastructure/Repositories/CopilotRepository.cs)
- [CandidateProfileRepository.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Infrastructure/Repositories/CandidateProfileRepository.cs)
- [AiResumeParserProvider.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Infrastructure/Service/AiResumeParserProvider.cs)
- [AiEmbeddingProvider.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Infrastructure/Service/AiEmbeddingProvider.cs)
- [AiCopilotProvider.cs](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/RecruitPro.Infrastructure/Service/AiCopilotProvider.cs)

### DB

- [init.sql](/abs/path/D:/NET/PRN232/recruit-pro/RecruitProInternal/init.sql)

### Frontend

- [candidateService.ts](/abs/path/D:/NET/PRN232/recruit-pro/recruit-pro-internal/src/services/candidate/candidateService.ts)
- [CandidateProfileAndCVManagementScreen.tsx](/abs/path/D:/NET/PRN232/recruit-pro/recruit-pro-internal/src/pages/candidate/CandidateProfileAndCVManagementScreen.tsx)

## 15. De xuat huong don gian hoa tiep theo

He thong hien tai da chay on voi model lai:

- data cu
- data section moi

Neu muon clean hon nua o buoc sau, nen lam theo thu tu:

1. chuyen toan bo read/write sang `candidate_profile_sections`
2. giam dan phu thuoc vao cac JSON legacy
3. tao migration don data mot chieu
4. sau cung moi xoa cot/table cu khong can thiet

Neu lam theo cach nay:

- it rui ro
- khong lam vo FE/BE dang chay
- van giu duoc backward compatibility trong giai doan chuyen doi
