using System.Collections.Generic;

namespace RecruitPro.Application.DTOs.Request.Automation;

public class WorkflowConditionInput
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class WorkflowActionInput
{
    public string Type { get; set; } = string.Empty;
    /// <summary>Action config as a JSON object string (validated to be well-formed JSON).</summary>
    public string ConfigJson { get; set; } = "{}";
}

public class CreateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerEventType { get; set; } = string.Empty;
    public string Mode { get; set; } = "Shadow";
    public List<WorkflowConditionInput> Conditions { get; set; } = new();
    public List<WorkflowActionInput> Actions { get; set; } = new();
}

/// <summary>Updates the working draft of a workflow (creates a new non-active version).</summary>
public class UpdateWorkflowRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? TriggerEventType { get; set; }
    public string? Mode { get; set; }
    public List<WorkflowConditionInput>? Conditions { get; set; }
    public List<WorkflowActionInput>? Actions { get; set; }
}

public class SetWorkflowEnabledRequest
{
    public bool IsEnabled { get; set; }
}

public class McpToolTestRequest
{
    /// <summary>Tool input arguments as a JSON object string.</summary>
    public string InputJson { get; set; } = "{}";
}
