using System;
using System.Threading.Tasks;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.Application.Services;

/// <summary>
/// DB-backed permission check (user_roles → role_permissions → permissions.code). Matrix changes take
/// effect on the next request without re-issuing JWTs, and only ACTIVE accounts keep their grants.
/// </summary>
public class PermissionCheckService : IPermissionCheckService
{
    // ponytail: one DB query per guarded sysadmin request; add a short-TTL cache if this ever guards hot paths.
    private readonly IRbacRepository _rbacRepository;

    public PermissionCheckService(IRbacRepository rbacRepository)
    {
        _rbacRepository = rbacRepository;
    }

    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return Task.FromResult(false);
        }

        return _rbacRepository.UserHasPermissionAsync(userId, permissionCode);
    }
}
