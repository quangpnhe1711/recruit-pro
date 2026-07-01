using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Interfaces.IRepositories;

/// <summary>Persistence for workflow definitions, versions, executions, steps and dead-letters.</summary>
public interface IWorkflowRepository
{
    // --- definitions / versions ---
    Task AddDefinitionAsync(WorkflowDefinition definition);
    Task AddVersionAsync(WorkflowDefinitionVersion version);
    Task<WorkflowDefinition?> GetDefinitionAsync(Guid id);
    Task<WorkflowDefinition?> GetDefinitionTrackedAsync(Guid id);
    Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync();
    Task<WorkflowDefinitionVersion?> GetVersionAsync(Guid id);
    Task<WorkflowDefinitionVersion?> GetVersionTrackedAsync(Guid id);
    Task<int> GetMaxVersionNoAsync(Guid definitionId);
    Task<IReadOnlyList<WorkflowDefinitionVersion>> GetVersionsAsync(Guid definitionId);

    /// <summary>Active versions of enabled definitions — the dispatcher filters these by trigger.</summary>
    Task<IReadOnlyList<WorkflowDefinitionVersion>> GetActiveEnabledVersionsAsync();
    Task<bool> DefinitionExistsByNameAsync(string name);

    // --- executions / steps / dead-letters ---
    Task AddExecutionAsync(WorkflowExecution execution);
    Task AddStepAsync(WorkflowExecutionStep step);
    Task AddDeadLetterAsync(WorkflowActionDeadLetter deadLetter);
    Task<bool> ExecutionExistsAsync(Guid versionId, string dedupKey);
    Task<WorkflowExecution?> GetExecutionAsync(Guid id);
    Task<WorkflowExecution?> GetExecutionTrackedAsync(Guid id);
    Task<(IReadOnlyList<WorkflowExecution> Items, int Total)> QueryExecutionsAsync(
        Guid? workflowDefinitionId, string? status, string? eventType, string? mode,
        DateTime? from, DateTime? to, int page, int pageSize);
    Task<IReadOnlyList<WorkflowExecution>> GetRetryableExecutionsAsync(DateTime now, int max);
    Task<IReadOnlyList<WorkflowExecution>> GetRecentExecutionsAsync(int max);

    // --- dashboard aggregates ---
    Task<int> CountExecutionsSinceAsync(DateTime since);
    Task<int> CountExecutionsByStatusAsync(WorkflowExecutionStatus status);
    Task<int> CountUnresolvedDeadLettersAsync();
}
