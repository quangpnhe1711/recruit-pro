using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecruitPro.API.Json;

/// <summary>
/// Normalizes every inbound request <see cref="DateTime"/> to <see cref="DateTimeKind.Unspecified"/>.
///
/// Rationale: 71 of the 72 datetime columns in the schema are <c>timestamp without time zone</c>. Npgsql
/// (non-legacy mode) REFUSES to write a <c>Kind=Utc</c> value to such a column and throws
/// <c>ArgumentException</c> -> the request 500s. A client sending an RFC 3339 UTC datetime
/// (e.g. <c>"2026-12-31T00:00:00Z"</c>) for <c>Job.Deadline</c> or <c>Offer.ProposedStartDate</c> would
/// therefore crash the endpoint. STJ parses <c>...Z</c> as <c>Kind=Utc</c>; stripping the Kind to
/// Unspecified (keeping the wall-clock digits) makes any Z-or-offset-or-naked input safe to persist,
/// matching the behavior of a naked <c>"...T00:00:00"</c> input.
///
/// This is the boundary fix: it covers every current and future [FromBody] DateTime with one converter.
/// The single <c>timestamp with time zone</c> column (<c>role_permissions.assigned_at</c>) is set
/// server-side, never from a request body, so it is unaffected. Write is delegated to STJ's native
/// DateTime writer so API responses are byte-identical to the default.
/// </summary>
public sealed class UnspecifiedDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Unspecified);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
