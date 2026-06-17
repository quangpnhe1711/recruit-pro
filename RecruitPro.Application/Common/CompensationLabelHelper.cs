namespace RecruitPro.Application.Common;

public static class CompensationLabelHelper
{
    public static string BuildSalaryLabel(decimal? salaryMin, decimal? salaryMax)
    {
        if (!salaryMin.HasValue && !salaryMax.HasValue)
        {
            return "Thương lượng";
        }

        if (salaryMin.HasValue && salaryMax.HasValue)
        {
            return $"{salaryMin.Value:N0} - {salaryMax.Value:N0} VNĐ";
        }

        if (salaryMin.HasValue)
        {
            return $"{salaryMin.Value:N0}+ VNĐ";
        }

        return $"Up to {salaryMax!.Value:N0} VNĐ";
    }
}
