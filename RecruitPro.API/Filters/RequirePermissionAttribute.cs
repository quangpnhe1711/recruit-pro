using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RecruitPro.API.Extensions;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Filters;

/// <summary>
/// Enforces a fine-grained RBAC permission (permissions.code) on top of JWT authentication. The check
/// hits the live role_permissions tables via <see cref="IPermissionCheckService"/>, so grants edited in
/// the System Admin matrix apply immediately — hiding a button in the frontend is never the boundary.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permissionCode)
        : base(typeof(RequirePermissionFilter))
    {
        Arguments = [permissionCode];
    }
}

internal sealed class RequirePermissionFilter : IAsyncAuthorizationFilter
{
    private readonly string _permissionCode;
    private readonly IPermissionCheckService _permissionCheck;

    public RequirePermissionFilter(string permissionCode, IPermissionCheckService permissionCheck)
    {
        _permissionCode = permissionCode;
        _permissionCheck = permissionCheck;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        Guid? userId = context.HttpContext.User.TryGetCurrentUserId();
        if (userId == null)
        {
            ApiResponse<object> unauthorized = ApiResponse<object>.Unauthorized(ErrorCodes.Unauthenticated);
            context.Result = new ObjectResult(unauthorized) { StatusCode = unauthorized.StatusCode };
            return;
        }

        if (!await _permissionCheck.HasPermissionAsync(userId.Value, _permissionCode))
        {
            ApiResponse<object> forbidden = ApiResponse<object>.Forbidden(ErrorCodes.Forbidden);
            context.Result = new ObjectResult(forbidden) { StatusCode = forbidden.StatusCode };
        }
    }
}
