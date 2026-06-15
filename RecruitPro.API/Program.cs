using RecruitPro.Application.Interfaces;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Configurations;
using RecruitPro.API.Extensions;
using RecruitPro.Infrastructure.Extensions;
using RecruitPro.Application.Extensions;
using RecruitPro.API.Filters;
using RecruitPro.API.Middlewares;
using System;

var builder = WebApplication.CreateBuilder(args);

//setting jwt
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationActionFilter>();
});

// Add FluentValidation
builder.Services.AddApplicationValidators();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Console.WriteLine("ENV = " + builder.Environment.EnvironmentName);
Console.WriteLine("CONN = " + builder.Configuration.GetConnectionString("Mycnn"));
Console.WriteLine("OpenAI Enabled = " + builder.Configuration.GetValue<bool>("OpenAi:Enabled"));
Console.WriteLine("OpenAI Model = " + (builder.Configuration["OpenAi:Model"] ?? "gpt-4.1-mini"));
Console.WriteLine("OpenAI ApiKey Present = " + (!string.IsNullOrWhiteSpace(builder.Configuration["OpenAi:ApiKey"])));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Mycnn"))
        .EnableSensitiveDataLogging()
        .LogTo(Console.WriteLine, LogLevel.Information));

// config jwt settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// config minio settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("MinioSettings"));

// config OpenAI settings
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAi"));

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
