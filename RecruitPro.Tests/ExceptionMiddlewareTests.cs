using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecruitPro.API.Middlewares;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>
/// Phase 2.3: the global exception handler must not leak the raw exception message (which can contain
/// connection details, SQL, file paths) in Production, but should still surface detail in Development.
/// </summary>
public sealed class ExceptionMiddlewareTests
{
    private const string GenericProductionMessage = "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau.";

    [Fact]
    public async Task Invoke_InProduction_ReturnsGenericMessage_AndDoesNotLeakExceptionDetail()
    {
        const string secret = "Host=db;Password=SuperSecret123;Database=postgres";
        RequestDelegate next = _ => throw new InvalidOperationException(secret);
        var environment = Mock.Of<IHostEnvironment>(env => env.EnvironmentName == Environments.Production);
        var middleware = new ExceptionMiddleware(next, Mock.Of<ILogger<ExceptionMiddleware>>(), environment);

        (string json, int statusCode) = await InvokeAndReadBodyAsync(middleware);

        statusCode.Should().Be(500);
        json.Should().NotContain(secret);
        json.Should().NotContain("SuperSecret123");

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("message").GetString().Should().Be(GenericProductionMessage);
    }

    [Fact]
    public async Task Invoke_InDevelopment_IncludesExceptionDetailForDebugging()
    {
        const string detail = "boom-development-detail";
        RequestDelegate next = _ => throw new InvalidOperationException(detail);
        var environment = Mock.Of<IHostEnvironment>(env => env.EnvironmentName == Environments.Development);
        var middleware = new ExceptionMiddleware(next, Mock.Of<ILogger<ExceptionMiddleware>>(), environment);

        (string json, int statusCode) = await InvokeAndReadBodyAsync(middleware);

        statusCode.Should().Be(500);
        json.Should().Contain(detail);
    }

    private static async Task<(string Body, int StatusCode)> InvokeAndReadBodyAsync(ExceptionMiddleware middleware)
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.Invoke(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        string content = await reader.ReadToEndAsync();
        return (content, context.Response.StatusCode);
    }
}
