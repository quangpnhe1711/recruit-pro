namespace RecruitPro.Application.DTOs.Response;

public class ManagerRecruitmentAnalyticsDto
{
    public ManagerRecruitmentOverviewDto Overview { get; set; } = new();
    public ManagerRecruitmentTrendDto Trend { get; set; } = new();
    public List<ManagerRecruitmentFunnelItemDto> Funnel { get; set; } = [];
    public ManagerRecruitmentDistributionDto Distribution { get; set; } = new();
    public List<ManagerDepartmentPipelineDto> DepartmentPerformance { get; set; } = [];
    public List<ManagerDepartmentBreakdownDto> DepartmentBreakdown { get; set; } = [];
}

public class ManagerRecruitmentOverviewDto
{
    public int AverageReviewCycleDays { get; set; }
    public int AverageReviewCycleDeltaPercent { get; set; }
    public int ActiveCandidates { get; set; }
    public int ActiveCandidatesDelta { get; set; }
    public int PendingInterviews { get; set; }
    public int PendingInterviewsDelta { get; set; }
    public int OfferAcceptanceRate { get; set; }
    public int OfferAcceptanceDeltaPercent { get; set; }
}

public class ManagerRecruitmentTrendDto
{
    public List<string> Labels { get; set; } = [];
    public List<int> Applications { get; set; } = [];
    public List<int> CompletedInterviews { get; set; } = [];
}

public class ManagerRecruitmentFunnelItemDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int PercentFromApplied { get; set; }
}

public class ManagerRecruitmentDistributionDto
{
    public int Total { get; set; }
    public List<ManagerRecruitmentDistributionItemDto> Items { get; set; } = [];
}

public class ManagerRecruitmentDistributionItemDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percent { get; set; }
    public string ColorToken { get; set; } = string.Empty;
}

public class ManagerDepartmentPipelineDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public int ActiveApplications { get; set; }
    public int OfferedCandidates { get; set; }
    public int AcceptedCandidates { get; set; }
    public int ConversionPercent { get; set; }
}

public class ManagerDepartmentBreakdownDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public int OpenRoles { get; set; }
    public int AverageReviewCycleDays { get; set; }
    public int ActivePipeline { get; set; }
    public string RecruiterName { get; set; } = string.Empty;
}
