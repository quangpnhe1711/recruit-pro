# Phase 1 — Required Features (OData · XML · gRPC service communication)

Bổ sung 3 requirement bắt buộc còn thiếu. Giữ nguyên PostgreSQL.

## Tổng quan

| Feature | Cách hiện thực | Điểm chạm |
|---|---|---|
| XML content negotiation | `AddXmlDataContractSerializerFormatters()` + `RespectBrowserAcceptHeader` | `RecruitPro.API/Program.cs`, `Controllers/XmlDemoController.cs` |
| OData (2 endpoint) | `Microsoft.AspNetCore.OData`, route prefix `/odata` | `RecruitPro.API/OData/*`, `Controllers/OData/*` |
| Service communication | gRPC service riêng, API gọi bằng gRPC client | `RecruitPro.ScoringService/*`, `Controllers/ExternalScoreController.cs` |

---

## 1. XML content negotiation

Bật XML formatter toàn hệ thống (`DataContractSerializer`) + tôn trọng header `Accept`. Endpoint demo trả **cùng dữ liệu** dạng JSON hoặc XML tuỳ `Accept`:

```bash
# XML
curl -H "Accept: application/xml" http://localhost:5000/api/xml-demo/jobs
#   -> Content-Type: application/xml; <ArrayOfJobODataDto>...</ArrayOfJobODataDto>

# JSON (cùng endpoint)
curl -H "Accept: application/json" http://localhost:5000/api/xml-demo/jobs
```

> Đã verify: `Accept: application/xml` → `application/xml; charset=utf-8`; `Accept: application/json` → `application/json`.
>
> Lưu ý: các endpoint bọc trong `ApiResponse<T>` (đồ thị DTO phức tạp) vẫn trả JSON vì graph đó không XML-serialize sạch — nên `XmlDemoController` trả DTO phẳng (`JobODataDto`) để minh hoạ content negotiation rõ ràng.

---

## 2. OData

Route prefix `/odata` (tách khỏi `/api`). Hỗ trợ `$filter / $orderby / $select / $top / $skip / $count`.

| Endpoint | Quyền | Nguồn |
|---|---|---|
| `GET /odata/Jobs` | Public (job Approved) | `Controllers/OData/JobsController.cs` |
| `GET /odata/Applications` | HR, Manager (JWT) | `Controllers/OData/ApplicationsController.cs` |

Query mẫu:

```bash
# Lọc theo tiêu đề + sắp xếp + lấy 5
curl "http://localhost:5000/odata/Jobs?\$filter=contains(Title,'Java')&\$orderby=CreatedAt desc&\$top=5"

# Chọn cột + đếm
curl "http://localhost:5000/odata/Jobs?\$select=Title,Location,Status&\$count=true"

# Applications (cần Bearer token HR/Manager)
curl -H "Authorization: Bearer <JWT>" \
  "http://localhost:5000/odata/Applications?\$filter=Status eq 'Interview'&\$select=Id,Status&\$orderby=AppliedAt desc"
```

> `Status`/`WorkMode`… phơi ra dạng chuỗi nên filter đọc tự nhiên (`Status eq 'Approved'`). Query hiện được compose in-memory trên tập đã lọc (quy mô demo).

---

## 3. gRPC service communication

`RecruitPro.ScoringService` là service độc lập; API gọi qua gRPC để chấm điểm match ứng viên ↔ job.

- Contract: `RecruitPro.ScoringService/Protos/scoring.proto` — `rpc ScoreCandidate(ScoreRequest) returns (ScoreReply)`.
- Server: `RecruitPro.ScoringService` (HTTP/2 cleartext).
- Client: API đăng ký `AddGrpcClient<Scorer.ScorerClient>` (`Program.cs`), địa chỉ từ `Services:ScoringUrl`.
- Endpoint REST demo (gọi xuyên gRPC): `GET /api/hr/applications/{applicationId}/external-score` (HR/Manager).

```bash
curl -H "Authorization: Bearer <JWT>" \
  http://localhost:5000/api/hr/applications/<applicationId>/external-score
# -> { score, label, matchedSkills, missingSkills, source: "RecruitPro.ScoringService (gRPC)" }
```

Self-check logic chấm điểm (không cần server):

```bash
dotnet run --project RecruitPro.ScoringService -- --selftest   # -> scoring self-test OK
```

---

## Cách chạy

### Local (2 tiến trình)

```bash
# Terminal 1 — scoring service (gRPC h2c :5210)
dotnet run --project RecruitPro.ScoringService

# Terminal 2 — API (:5000; đọc Services:ScoringUrl = http://localhost:5210)
dotnet run --project RecruitPro.API
```

Cần PostgreSQL đang chạy (docker `postgres` cổng 5433, khớp `ConnectionStrings:Mycnn`).

### Docker (docker-compose)

```bash
docker compose up --build
```

`docker-compose.yml` có thêm service `scoring`; API nhận `Services__ScoringUrl=http://scoring:8080` để gọi qua network nội bộ.
