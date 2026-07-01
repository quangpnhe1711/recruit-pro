namespace RecruitPro.Domain.Automation;

/// <summary>
/// Cutover mode for a workflow version / event. Disabled = workflow does not run. Shadow = workflow
/// runs and records what it WOULD do but sends nothing (old direct notification still fires). Live =
/// workflow sends for real and the old direct notification is bypassed for that event.
/// </summary>
public enum WorkflowMode
{
    Disabled,
    Shadow,
    Live
}

/// <summary>Durable outbox event lifecycle.</summary>
public enum WorkflowEventStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    DeadLetter
}

/// <summary>Workflow execution lifecycle.</summary>
public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Success,
    Skipped,
    Failed,
    Retrying,
    DeadLetter
}

/// <summary>Per-step outcome inside an execution.</summary>
public enum WorkflowStepStatus
{
    Running,
    Success,
    Skipped,
    Failed
}
