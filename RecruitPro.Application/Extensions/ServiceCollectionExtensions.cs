using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Application.Services;
using RecruitPro.Application.Services.Automation;
using RecruitPro.Application.Services.Automation.Handlers;
using RecruitPro.Application.Services.Automation.Mcp;

namespace RecruitPro.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationBusinessLogicServices(this IServiceCollection services)
    {
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICandidateService, CandidateService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IManagerAnalyticsService, ManagerAnalyticsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEventService, NotificationEventService>();
        services.AddScoped<ICopilotService, CopilotService>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IApplicationSemanticScoringService, ApplicationSemanticScoringService>();
        services.AddScoped<ISemanticDiscoveryService, SemanticDiscoveryService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IApplicationOwnershipResolver, ApplicationOwnershipResolver>();

        // System Admin console (RBAC matrix + user directory + audit logs)
        services.AddScoped<IPermissionCheckService, PermissionCheckService>();
        services.AddScoped<ISysAdminRbacService, SysAdminRbacService>();
        services.AddScoped<ISysAdminDirectoryService, SysAdminDirectoryService>();

        AddWorkflowAutomationServices(services);
        AddAiOpsServices(services);

        return services;
    }

    /// <summary>v4 Workflow Automation + MCP services (deterministic-first; AI optional).</summary>
    private static void AddWorkflowAutomationServices(IServiceCollection services)
    {
        // Event bus + engine
        services.AddScoped<IRecruitProEventBus, RecruitProEventBus>();
        services.AddScoped<IWorkflowConditionEvaluator, WorkflowConditionEvaluator>();
        services.AddScoped<IWorkflowActionRegistry, WorkflowActionRegistry>();
        services.AddScoped<IWorkflowNotificationDispatcher, WorkflowNotificationDispatcher>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IWorkflowRetryService, WorkflowRetryService>();
        services.AddScoped<IWorkflowTemplateSeeder, WorkflowTemplateSeeder>();

        // Action handlers (deterministic, safe)
        services.AddScoped<IWorkflowActionHandler, NotifyUserActionHandler>();
        services.AddScoped<IWorkflowActionHandler, NotifyRoleActionHandler>();
        services.AddScoped<IWorkflowActionHandler, SendReminderActionHandler>();
        services.AddScoped<IWorkflowActionHandler, RuleBasedNextStepSuggestionActionHandler>();
        services.AddScoped<IWorkflowActionHandler, ShadowLogActionHandler>();

        // SystemAdmin-facing services
        services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddScoped<IAutomationDiagnosticsService, AutomationDiagnosticsService>();

        // MCP tools + registry + services
        services.AddScoped<IMcpTool, JobsSearchTool>();
        services.AddScoped<IMcpTool, JobsGetTool>();
        services.AddScoped<IMcpTool, ApplicationsGetTool>();
        services.AddScoped<IMcpTool, ApplicationsGetFitAnalysisTool>();
        services.AddScoped<IMcpTool, InterviewsGetScheduleTool>();
        services.AddScoped<IMcpTool, AnalyticsGetFunnelSummaryTool>();
        services.AddScoped<IMcpToolRegistry, McpToolRegistry>();
        services.AddScoped<IMcpToolService, McpToolService>();
        services.AddScoped<IMcpToolAuditService, McpToolAuditService>();
    }

    /// <summary>v5 AI Ops services: read-side telemetry metrics for the SysAdmin AI Ops dashboards.</summary>
    // ponytail: only the metrics read-side is built. Prompt-registry / provider-routing / evaluation /
    // talent-intelligence services were registered but never implemented (no class, no consumer, no FE
    // screen) — dropped to unbreak the build. Re-add a registration when its service class + a caller exist.
    private static void AddAiOpsServices(IServiceCollection services)
    {
        services.AddScoped<IAiOperationsMetricsService, AiOperationsMetricsService>();
    }
}
