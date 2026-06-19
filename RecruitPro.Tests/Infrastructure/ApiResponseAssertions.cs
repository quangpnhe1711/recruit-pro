using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RecruitPro.Tests.Infrastructure;

public static class ApiResponseAssertions
{
    public static async Task<JsonDocument> AssertNo500AndEnvelopeAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();

        JsonDocument json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("statusCode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("message", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
        return json;
    }
}
