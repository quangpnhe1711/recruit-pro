using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Configurations;
using RecruitPro.API.Extensions;
using RecruitPro.Infrastructure.Extensions;
using RecruitPro.Application.Extensions;
using RecruitPro.API.Filters;
using RecruitPro.API.Middlewares;
using RecruitPro.API.Realtime;
using System;

var builder = WebApplication.CreateBuilder(args);

//setting jwt
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationActionFilter>();
});

// Upload hardening (Phase 2.4): framework-level body-size ceiling, layered under the precise per-file
// validation in CandidateService (5 MB CV cap). Kestrel + the multipart form reader reject oversized
// bodies before model binding so a huge upload can never be buffered. The global ceiling is generous
// (10 MB) so it does not break bulk Excel imports; the CV endpoints additionally carry a tighter
// [RequestSizeLimit]/[RequestFormLimits] (~6 MB) kept in sync with the 5 MB service cap.
const long maxGlobalRequestBytes = 10L * 1024 * 1024;
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxGlobalRequestBytes;
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxGlobalRequestBytes;
});
// Notification realtime is delivered over SSE (GET /api/notifications/stream), not SignalR. The
// broker is a singleton (one process-wide fan-out table); the sender forwards persisted notifications
// to it and is what the notification publisher depends on via INotificationRealtimeSender.
builder.Services.AddSingleton<INotificationSseBroker, InMemoryNotificationSseBroker>();
builder.Services.AddScoped<INotificationRealtimeSender, SseNotificationSender>();

// Add FluentValidation
builder.Services.AddApplicationValidators();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Console.WriteLine("ENV = " + builder.Environment.EnvironmentName);
// Never log the connection string (contains DB credentials). Only emit non-sensitive
// startup diagnostics, and only in Development.
if (builder.Environment.IsDevelopment())
{
    Console.WriteLine("AI Provider Enabled = " + builder.Configuration.GetValue<bool>("AiProvider:Enabled"));
    Console.WriteLine("AI Provider Model = " + (builder.Configuration["AiProvider:Model"] ?? "gemini-2.0-flash"));
    Console.WriteLine("AI Provider ApiKey Present = " + (!string.IsNullOrWhiteSpace(builder.Configuration["AiProvider:ApiKey"])));
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Mycnn"));

    // EnableSensitiveDataLogging emits parameter values (including PII/credentials) into the SQL log,
    // and LogTo streams every query to the console. Both are Development-only diagnostics and must
    // never run in Production.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

// config jwt settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// config minio settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("MinioSettings"));

// config AI provider settings
builder.Services.Configure<AiProviderSettings>(builder.Configuration.GetSection("AiProvider"));

// cors config
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


// register repo
builder.Services.AddInfrastructureServices();

//register services
builder.Services.AddApplicationBusinessLogicServices();
builder.Services.AddHostedService<SemanticScoringBackgroundService>();

//register auto mapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

//Use middleware
app.UseMiddleware<ExceptionMiddleware>();

//app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();

app.UseAuthorization();
app.MapGet("/", () => Results.Ok("RecruitPro API Running"));

app.MapControllers();

app.Run();

public partial class Program
{
}
