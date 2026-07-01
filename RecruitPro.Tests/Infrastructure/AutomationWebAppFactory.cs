using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Tests.Infrastructure;

/// <summary>
/// Web factory for v4 automation integration tests. Runs under the "Testing" environment (so the
/// workflow background loops are OFF and tests drive the engine deterministically) and injects a
/// WorkflowAutomation cutover config with every event set to the chosen mode.
/// </summary>
public sealed class AutomationWebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgresTestFixture _fixture;
    private readonly WorkflowMode _mode;

    public AutomationWebAppFactory(PostgresTestFixture fixture, WorkflowMode mode = WorkflowMode.Shadow)
    {
        _fixture = fixture;
        _mode = mode;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            string m = _mode.ToString();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mycnn"] = _fixture.ConnectionString,
                ["AiProvider:Enabled"] = "false",
                ["WorkflowAutomation:Enabled"] = "true",
                ["WorkflowAutomation:DefaultMode"] = m,
                ["WorkflowAutomation:EventModes:CandidateApplied"] = m,
                ["WorkflowAutomation:EventModes:PassedToHeadReview"] = m,
                ["WorkflowAutomation:EventModes:InterviewCompleted"] = m,
                ["WorkflowAutomation:EventModes:JobApproved"] = m,
                ["WorkflowAutomation:EventModes:HeadReviewOverdue"] = m,
                ["WorkflowAutomation:HeadReviewOverdueDays"] = "3",
                ["WorkflowAutomation:MaxAttempts"] = "3",
            });
        });
    }

    /// <summary>Drops + recreates the schema (from the EF model) and seeds base test data.</summary>
    public Task ResetAsync() => _fixture.ResetDatabaseAsync(Services);

    public IServiceScope CreateServiceScope() => Services.CreateScope();
}
