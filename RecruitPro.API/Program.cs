using RecruitPro.Application.Interfaces;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Configurations;
using RecruitPro.Infrastructure.Extensions;
using RecruitPro.Application.Extensions;
using RecruitPro.Application.Validators;
using FluentValidation;
using RecruitPro.API.Filters;
using RecruitPro.API.Middlewares;


var builder = WebApplication.CreateBuilder(args);

//setting jwt
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationActionFilter>();
});
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Jobs.CreateJobRequest>, CreateJobRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Jobs.PatchJobRequest>, PatchJobRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Interviews.CreateInterviewRequest>, CreateInterviewRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Interviews.UpdateInterviewStatusRequest>, UpdateInterviewStatusRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Applications.UpdateApplicationDecisionRequest>, UpdateApplicationDecisionRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Candidate.UpdateCandidateProfileRequest>, UpdateCandidateProfileRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Candidate.UpdateCandidateSkillsRequest>, UpdateCandidateSkillsRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.Candidate.UpsertCandidateExperienceRequest>, UpsertCandidateExperienceRequestValidator>();
builder.Services.AddScoped<IValidator<RecruitPro.Application.DTOs.Request.SendApplicationEmailRequest>, SendApplicationEmailRequestValidator>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Mycnn"))
        .EnableSensitiveDataLogging()
        .LogTo(Console.WriteLine, LogLevel.Information));

// config jwt settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// config minio settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("MinioSettings"));

// cors config
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:5173")
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

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
