using System.Collections.Generic;
using System.Linq;
using RecruitPro.Application.Interfaces.IServices.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>Resolves an action handler by its type key. Handlers are supplied by DI.</summary>
public class WorkflowActionRegistry : IWorkflowActionRegistry
{
    private readonly Dictionary<string, IWorkflowActionHandler> _handlers;

    public WorkflowActionRegistry(IEnumerable<IWorkflowActionHandler> handlers)
        => _handlers = handlers.ToDictionary(h => h.ActionType, h => h);

    public IWorkflowActionHandler? Resolve(string actionType)
        => _handlers.TryGetValue(actionType, out IWorkflowActionHandler? handler) ? handler : null;

    public IReadOnlyCollection<string> RegisteredTypes => _handlers.Keys.ToArray();
}
