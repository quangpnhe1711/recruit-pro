namespace RecruitPro.Application.DTOs.Response;

public class ApiEnvelopeMeta
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
