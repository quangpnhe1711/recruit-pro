using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Common;

public static class PaginationMetaBuilder
{
    public static ApiEnvelopeMeta Build(int page, int pageSize, int totalItems)
    {
        return new ApiEnvelopeMeta
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }
}
