namespace RecruitPro.Application.Automation;

/// <summary>Deterministic, AI-free next-step suggestion after an interview. Pure + unit-testable.</summary>
public static class NextStepSuggestion
{
    public const string BandStrong = "strong";
    public const string BandMedium = "medium";
    public const string BandLow = "low";
    public const string BandUnknown = "unknown";

    public static (string Band, string Suggestion) Compute(decimal? score)
    {
        if (score is null)
        {
            return (BandUnknown, "Cần HR/Manager xem lại feedback phỏng vấn để quyết định bước tiếp theo.");
        }

        if (score >= 80m)
        {
            return (BandStrong, "Kết quả phỏng vấn tốt — đề xuất cân nhắc gửi Offer cho ứng viên.");
        }

        if (score >= 50m)
        {
            return (BandMedium, "Kết quả ở mức trung bình — đề xuất xem xét thêm hoặc lên lịch phỏng vấn vòng 2.");
        }

        return (BandLow, "Kết quả phỏng vấn thấp — đề xuất xem xét giữ lại (Hold) hoặc từ chối (Reject).");
    }
}
