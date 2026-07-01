using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Automation;

/// <summary>Deterministic, null-safe readers over a flat event payload (JsonElement object).</summary>
public static class WorkflowPayload
{
    public static bool TryGetProperty(JsonElement payload, string field, out JsonElement value)
    {
        value = default;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        return payload.TryGetProperty(field, out value) && value.ValueKind != JsonValueKind.Null;
    }

    public static string? GetString(JsonElement payload, string field)
    {
        if (!TryGetProperty(payload, field, out JsonElement value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    public static Guid? GetGuid(JsonElement payload, string field)
    {
        string? raw = GetString(payload, field);
        return Guid.TryParse(raw, out Guid id) ? id : null;
    }

    public static decimal? GetDecimal(JsonElement payload, string field)
    {
        if (!TryGetProperty(payload, field, out JsonElement value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number))
        {
            return number;
        }
        return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;
    }

    public static DateTime? GetDateTime(JsonElement payload, string field)
    {
        string? raw = GetString(payload, field);
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
            ? dt
            : null;
    }

    /// <summary>Resolves ownership-based recipient selectors to distinct user ids from the payload.</summary>
    public static IReadOnlyList<Guid> ResolveRecipients(JsonElement payload, IEnumerable<string> selectors)
    {
        HashSet<Guid> ids = [];
        foreach (string selector in selectors)
        {
            Guid? id = selector switch
            {
                WorkflowRecipientSelector.AssignedRecruiter => GetGuid(payload, "recruiterId"),
                WorkflowRecipientSelector.AssignedDepartmentHead => GetGuid(payload, "departmentHeadId"),
                WorkflowRecipientSelector.Candidate => GetGuid(payload, "candidateUserId"),
                _ => Guid.TryParse(selector, out Guid literal) ? literal : null
            };
            if (id is { } value && value != Guid.Empty)
            {
                ids.Add(value);
            }
        }
        return [.. ids];
    }

    /// <summary>Reads a string array or single value from an action config field.</summary>
    public static IReadOnlyList<string> GetStringArray(JsonElement config, string field)
    {
        if (config.ValueKind != JsonValueKind.Object || !config.TryGetProperty(field, out JsonElement value))
        {
            return [];
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            List<string> list = [];
            foreach (JsonElement item in value.EnumerateArray())
            {
                string? s = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list.Add(s);
                }
            }
            return list;
        }
        string? single = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }

    public static string GetConfigString(JsonElement config, string field, string fallback)
    {
        if (config.ValueKind == JsonValueKind.Object
            && config.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            string? s = value.GetString();
            return string.IsNullOrWhiteSpace(s) ? fallback : s;
        }
        return fallback;
    }

    public static int GetConfigInt(JsonElement config, string field, int fallback)
    {
        if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty(field, out JsonElement value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }
            if (int.TryParse(value.ToString(), out int parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }
}
