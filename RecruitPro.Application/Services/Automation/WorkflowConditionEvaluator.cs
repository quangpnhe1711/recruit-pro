using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Interfaces.IServices.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Deterministic AND-evaluation of a workflow's structured conditions against a flat event payload.
/// Missing fields fail the condition (except the explicit <c>exists</c> operator). Pure logic — no I/O.
/// </summary>
public class WorkflowConditionEvaluator : IWorkflowConditionEvaluator
{
    public ConditionEvaluationResult Evaluate(
        IReadOnlyList<WorkflowConditionModel> conditions, JsonElement payload, DateTime now)
    {
        foreach (WorkflowConditionModel condition in conditions ?? [])
        {
            if (!EvaluateOne(condition, payload, now, out string detail))
            {
                return ConditionEvaluationResult.Fail(condition.Field, detail);
            }
        }
        return ConditionEvaluationResult.Pass();
    }

    private static bool EvaluateOne(WorkflowConditionModel condition, JsonElement payload, DateTime now, out string detail)
    {
        detail = string.Empty;
        bool present = WorkflowPayload.TryGetProperty(payload, condition.Field, out _);
        string op = condition.Operator ?? string.Empty;

        switch (op)
        {
            case WorkflowConditionOperator.Exists:
                detail = present ? "field present" : "field missing";
                return present;

            case WorkflowConditionOperator.Equals:
                return Compare(payload, condition, present, ref detail, isEqual: true);

            case WorkflowConditionOperator.NotEquals:
                return Compare(payload, condition, present, ref detail, isEqual: false);

            case WorkflowConditionOperator.GreaterThan:
            case WorkflowConditionOperator.GreaterThanOrEqual:
            {
                decimal? actual = WorkflowPayload.GetDecimal(payload, condition.Field);
                if (actual is null || !TryDecimal(condition.Value, out decimal threshold))
                {
                    detail = "non-numeric or missing value";
                    return false;
                }
                bool ok = op == WorkflowConditionOperator.GreaterThan ? actual > threshold : actual >= threshold;
                detail = $"{actual} {op} {threshold} => {ok}";
                return ok;
            }

            case WorkflowConditionOperator.InList:
            {
                string? actual = WorkflowPayload.GetString(payload, condition.Field);
                if (actual is null)
                {
                    detail = "field missing";
                    return false;
                }
                HashSet<string> list = (condition.Value ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.ToLowerInvariant())
                    .ToHashSet();
                bool ok = list.Contains(actual.ToLowerInvariant());
                detail = $"'{actual}' in [{string.Join(',', list)}] => {ok}";
                return ok;
            }

            case WorkflowConditionOperator.OlderThanDays:
            {
                DateTime? actual = WorkflowPayload.GetDateTime(payload, condition.Field);
                if (actual is null || !TryDecimal(condition.Value, out decimal days))
                {
                    detail = "non-date or missing value";
                    return false;
                }
                bool ok = (now - actual.Value).TotalDays >= (double)days;
                detail = $"age {(now - actual.Value).TotalDays:F1}d >= {days}d => {ok}";
                return ok;
            }

            default:
                detail = $"unknown operator '{op}'";
                return false;
        }
    }

    private static bool Compare(JsonElement payload, WorkflowConditionModel condition, bool present, ref string detail, bool isEqual)
    {
        if (!present)
        {
            detail = "field missing";
            return !isEqual; // missing != value is true; missing == value is false.
        }

        string actual = WorkflowPayload.GetString(payload, condition.Field) ?? string.Empty;
        string expected = condition.Value ?? string.Empty;

        bool equalValues;
        if (TryDecimal(actual, out decimal a) && TryDecimal(expected, out decimal b))
        {
            equalValues = a == b;
        }
        else
        {
            equalValues = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        detail = $"'{actual}' {(isEqual ? "==" : "!=")} '{expected}' => {(isEqual ? equalValues : !equalValues)}";
        return isEqual ? equalValues : !equalValues;
    }

    private static bool TryDecimal(string? value, out decimal result)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
}
