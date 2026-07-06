using System;
using System.Collections.Generic;
using System.Linq;

namespace RecruitPro.Application.Common;

/// <summary>
/// Single source of truth for the RBAC permission matrix: every grantable permission code, the module
/// it belongs to, its normalized action, and the domain group used to lay out the System Admin matrix.
/// Codes are the EXACT strings stored in <c>permissions.code</c> (init.sql) — casing included — so this
/// catalog, the DB seed, and the <c>[RequirePermission]</c> guards can never drift apart silently.
/// </summary>
public static class RbacCatalog
{
    /// <summary>Normalized matrix actions. Read/Create/Update/Delete map to CRUD columns; the rest render as extra switches.</summary>
    public static class Actions
    {
        public const string Read = "Read";
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public const string Approve = "Approve";
        public const string Apply = "Apply";
        public const string Review = "Review";
        public const string Manage = "Manage";
    }

    /// <summary>Domain groups used to cluster modules in the matrix UI.</summary>
    public static class Groups
    {
        public const string Recruitment = "recruitment";
        public const string Candidate = "candidate";
        public const string Notifications = "notifications";
        public const string System = "system";
    }

    public sealed record RbacActionDef(string Code, string Action, bool IsCritical = false);

    public sealed record RbacModuleDef(string Key, string Group, IReadOnlyList<RbacActionDef> Actions);

    /// <summary>
    /// The permission that lets an actor edit the RBAC matrix itself. Removing the last active holder
    /// of this permission would lock everyone out of administration — guarded with 409.
    /// </summary>
    public const string ManagePermissionsCode = "PERMISSION_MANAGE";

    public static readonly IReadOnlyList<RbacModuleDef> Modules =
    [
        new("jobs", Groups.Recruitment,
        [
            new("Job_VIEW", Actions.Read),
            new("Job_CREATE", Actions.Create),
            new("Job_UPDATE", Actions.Update),
            new("Job_DELETE", Actions.Delete, IsCritical: true),
            new("Job_APPROVE", Actions.Approve),
        ]),
        new("applications", Groups.Recruitment,
        [
            new("Application_VIEW", Actions.Read),
            new("Application_APPLY", Actions.Apply),
            new("Application_REVIEW", Actions.Review),
        ]),
        new("interviews", Groups.Recruitment,
        [
            new("Interview_VIEW", Actions.Read),
            new("Interview_CREATE", Actions.Create),
            new("Interview_UPDATE", Actions.Update),
        ]),
        new("departments", Groups.Recruitment,
        [
            new("DEPARTMENT_VIEW", Actions.Read),
            new("DEPARTMENT_MANAGE", Actions.Manage),
        ]),
        new("skills", Groups.Recruitment,
        [
            new("SKILL_VIEW", Actions.Read),
            new("SKILL_MANAGE", Actions.Manage),
        ]),
        new("candidate_profiles", Groups.Candidate,
        [
            new("CANDIDATE_PROFILE_VIEW", Actions.Read),
            new("CANDIDATE_PROFILE_UPDATE", Actions.Update),
        ]),
        new("notifications", Groups.Notifications,
        [
            new("NOTIFICATION_VIEW", Actions.Read),
        ]),
        new("users", Groups.System,
        [
            new("USER_VIEW", Actions.Read),
            new("USER_CREATE", Actions.Create),
            new("USER_UPDATE", Actions.Update),
            new("USER_DELETE", Actions.Delete, IsCritical: true),
        ]),
        new("roles", Groups.System,
        [
            new("ROLE_VIEW", Actions.Read),
            new("ROLE_MANAGE", Actions.Manage, IsCritical: true),
        ]),
        new("rbac", Groups.System,
        [
            new("PERMISSION_VIEW", Actions.Read),
            new(ManagePermissionsCode, Actions.Manage, IsCritical: true),
        ]),
        new("system_logs", Groups.System,
        [
            new("System_LOG_VIEW", Actions.Read),
        ]),
    ];

    private static readonly Dictionary<string, RbacActionDef> CodeIndex = Modules
        .SelectMany(module => module.Actions)
        .ToDictionary(action => action.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>All grantable codes in canonical (DB) casing.</summary>
    public static IReadOnlyCollection<string> AllCodes { get; } = CodeIndex.Values.Select(a => a.Code).ToArray();

    public static bool TryNormalizeCode(string? code, out string canonicalCode)
    {
        if (!string.IsNullOrWhiteSpace(code) && CodeIndex.TryGetValue(code.Trim(), out RbacActionDef? def))
        {
            canonicalCode = def.Code;
            return true;
        }

        canonicalCode = string.Empty;
        return false;
    }
}
