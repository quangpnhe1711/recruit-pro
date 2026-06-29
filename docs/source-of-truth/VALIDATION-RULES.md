# VALIDATION-RULES

## Mục tiêu

File này là source of truth cho toàn bộ rule validate create/update giữa FE và BE.

Nguyên tắc bắt buộc:

- FE phải dùng schema validator (`yup` hoặc `zod`) cho mọi form create/update.
- FE phải hiển thị lỗi ngay dưới field tương ứng ngay khi người dùng nhập/chọn sai hoặc khi submit.
- FE chỉ dùng rule đã được định nghĩa rõ ở BE; không tự sáng tạo rule riêng.
- BE là nguồn sự thật cuối cùng và phải có validator tường minh cho request DTO create/update.

## Chuẩn hiển thị lỗi FE

- Error text đặt ngay dưới field.
- Border field lỗi dùng màu lỗi (`#ba1a1a` hoặc tương đương hệ hiện tại).
- Nếu form có nhiều lỗi, submit có thể giữ `toast` tổng quát, nhưng không được thay thế field error.
- Với field dạng list/picker, hiển thị lỗi dưới block field đó.

## Mapping FE -> BE

### Candidate Register

FE schema: `src/common/validation/formValidation.ts` -> `candidateRegisterSchema`

BE contract:

- `UserInfo.Username`: bắt buộc, 4-50 ký tự, regex `^[a-zA-Z0-9._-]+$`
- `UserInfo.FullName`: bắt buộc, tối đa 100 ký tự
- `UserInfo.Email`: bắt buộc, email hợp lệ
- `UserInfo.Phone`: tùy chọn, nếu có phải match `^0\d{9}$`
- `UserInfo.PasswordHash`: bắt buộc, 6-100 ký tự

### Job Create

FE schema:

- `jobCreateStep1Schema`
- `jobCreateStep2Schema`
- `jobCreateStep3Schema`

BE contract:

- `Title`: bắt buộc, tối đa 255 ký tự
- `Department`: tùy chọn, tối đa 100 ký tự
- `DepartmentId`: nếu có phải là GUID hợp lệ
- `Location`: bắt buộc, tối đa 255 ký tự
- `ShortPitch`: tùy chọn ở BE, FE hiện giới hạn tối đa 255 ký tự
- `Description`: bắt buộc
- `Requirements`: bắt buộc, ít nhất 1 item
- `VacancyCount`: > 0
- `MinExperienceYears`: nếu có thì >= 0
- `SalaryMax`: nếu có cùng `SalaryMin` thì phải `>= SalaryMin`
- `SkillRequirements[].SkillType`: `Required` hoặc `NiceToHave`
- `SkillRequirements[].MinimumYearsOfExperience`: nếu có thì >= 0

### Job Update

FE schema: `jobEditSchema`

BE contract:

- Ít nhất một field phải được gửi
- `Title`: nếu có, tối đa 255 ký tự
- `Department`: nếu có, tối đa 100 ký tự
- `DepartmentId`: nếu có, GUID hợp lệ
- `Location`: nếu có, tối đa 255 ký tự
- `Description`: nếu gửi thì không được rỗng
- `Requirements`: nếu gửi thì không được rỗng
- `VacancyCount`: nếu có thì > 0
- `MinExperienceYears`: nếu có thì >= 0
- `SalaryMax`: nếu có cùng `SalaryMin` thì phải `>= SalaryMin`
- `SkillRequirements[]`: cùng rule như create

### Apply Job

FE schema: `applyJobSchema`

BE contract:

- `CoverLetter`: tùy chọn, tối đa 2000 ký tự

### Interview Create

FE schema: `interviewScheduleSchema`

BE contract:

- `ApplicationId`: bắt buộc, GUID hợp lệ
- `CandidateId`: bắt buộc, GUID hợp lệ
- `JobId`: bắt buộc, GUID hợp lệ
- `Date`: bắt buộc
- `StartMinutes`: 0-1439
- `DurationMinutes`: 15-240
- `Mode`: bắt buộc, `video` hoặc `inPerson`
- `LocationOrLink`: bắt buộc, tối đa 500 ký tự
- `InterviewerId`: nếu có thì GUID hợp lệ

### Offer Draft / Send Offer

FE schema: `offerSchema`

BE contract:

- `OfferTemplateId`: tùy chọn, nếu có thì GUID hợp lệ
- `BaseSalary`: bắt buộc, > 0
- `CurrencyCode`: bắt buộc, tối đa 10 ký tự
- `BonusDescription`: tùy chọn, tối đa 500 ký tự
- `EquityNotes`: tùy chọn, tối đa 500 ký tự
- `EmploymentType`: bắt buộc, tối đa 100 ký tự
- `ProbationPeriod`: tùy chọn, tối đa 100 ký tự
- `ReportingManagerId`: tùy chọn, nếu có thì GUID hợp lệ
- `PersonalMessage`: tùy chọn, tối đa 2000 ký tự
- `BenefitIds[]`: mỗi phần tử phải là GUID hợp lệ

### Candidate Profile Save / Update

FE schema:

- `candidateProfileSchema`
- composer con sẽ phải bám các schema draft tương ứng trong `formValidation.ts`

BE contract:

- `Name`: bắt buộc, tối đa 255 ký tự
- `Headline`: tùy chọn, tối đa 100 ký tự
- `Email`: bắt buộc, email hợp lệ
- `Phone`: tùy chọn, nếu có thì match `^0\d{9}$`
- `Location`: tùy chọn, tối đa 250 ký tự
- `Bio`: tùy chọn, tối đa 1000 ký tự
- `Github`: tùy chọn, URL tuyệt đối hợp lệ, tối đa 500 ký tự
- `Linkedin`: tùy chọn, URL tuyệt đối hợp lệ, tối đa 500 ký tự
- `Skills[].SkillId`: bắt buộc
- `Skills[].YearsOfExperience`: nếu có thì >= 0
- `ExperienceEntries[].Title`: bắt buộc, tối đa 255 ký tự
- `ExperienceEntries[].Company`: bắt buộc, tối đa 255 ký tự
- `ExperienceEntries[].Bullets`: bắt buộc, mỗi dòng tối đa 500 ký tự
- `ExperienceEntries[].Period.StartMonth`: 1-12
- `ExperienceEntries[].Period.StartYear`: 1900..`currentYear + 1`
- `ExperienceEntries[].Period.EndMonth`: 1-12 nếu không phải current
- `ExperienceEntries[].Period.EndYear`: 1900..`currentYear + 1` nếu không phải current
- `Projects[].Name`: bắt buộc, tối đa 255 ký tự
- `Projects[].Role`: tùy chọn, tối đa 255 ký tự
- `Projects[].Description`: tùy chọn, tối đa 2000 ký tự
- `Projects[].Technologies[]`: mỗi item tối đa 100 ký tự
- `Educations[].School`: bắt buộc, tối đa 255 ký tự
- `Educations[].Degree`: bắt buộc, tối đa 255 ký tự
- `Educations[].FieldOfStudy`: tùy chọn, tối đa 255 ký tự
- `Educations[].Description`: tùy chọn, tối đa 1000 ký tự
- `Certifications[].Name`: bắt buộc, tối đa 255 ký tự
- `Certifications[].Issuer`: tùy chọn, tối đa 255 ký tự
- `Certifications[].CredentialId`: tùy chọn, tối đa 255 ký tự
- `Certifications[].CredentialUrl`: tùy chọn, URL tuyệt đối hợp lệ, tối đa 500 ký tự
- `Certifications[].ExpiresOn >= IssuedOn` nếu cả hai cùng có
- `Languages[].Name`: bắt buộc, tối đa 100 ký tự
- `Languages[].Proficiency`: bắt buộc, tối đa 100 ký tự
- `Sections[].SectionKey`: tùy chọn, tối đa 100 ký tự
- `Sections[].Title`: bắt buộc, tối đa 255 ký tự
- `Sections[].SectionType`: bắt buộc, tối đa 100 ký tự
- `Sections[].Source`: bắt buộc, tối đa 100 ký tự
- `Sections[].Items[].ItemType`: bắt buộc, tối đa 100 ký tự
- `Sections[].Items[].Title`: bắt buộc, tối đa 255 ký tự
- `Sections[].Items[].Subtitle|Organization|Location`: tùy chọn, tối đa 255 ký tự
- `Sections[].Items[].Description`: tùy chọn, tối đa 2000 ký tự
- `Sections[].Items[].DateLabel`: tùy chọn, tối đa 100 ký tự
- `Sections[].Items[].Tags[]`: mỗi item tối đa 100 ký tự

## File triển khai chính

FE:

- `src/common/validation/formValidation.ts`
- `src/pages/public/CandidateRegisterScreen.tsx`
- `src/pages/hr/JobCreatingScreen.tsx`
- `src/pages/public/JobDetailScreen.tsx`
- `src/pages/candidate/ApplyJobScreen.tsx`
- `src/pages/hr/InterviewScheduleScreen.tsx`
- `src/pages/hr/SendOfferScreen.tsx`
- `src/pages/candidate/candidate-profile-screen/*`

BE:

- `RecruitPro.Application/Validators/CreateJobRequestValidator.cs`
- `RecruitPro.Application/Validators/PatchJobRequestValidator.cs`
- `RecruitPro.Application/Validators/ApplyJobRequestValidator.cs`
- `RecruitPro.Application/Validators/CreateInterviewRequestValidator.cs`
- `RecruitPro.Application/Validators/UpdateCandidateProfileRequestValidator.cs`
- `RecruitPro.Application/Validators/UpdateCandidateSkillsRequestValidator.cs`
- `RecruitPro.Application/Validators/UpsertCandidateExperienceRequestValidator.cs`
- `RecruitPro.Application/Validators/UpsertApplicationOfferRequestValidator.cs`

## Rule review checklist khi mở PR mới

- Có schema FE chưa?
- Có inline field error chưa?
- Có validator BE tương ứng chưa?
- FE message và BE rule có match không?
- Có thêm rule mới thì đã cập nhật file này chưa?
