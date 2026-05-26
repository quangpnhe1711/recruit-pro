using RecruitPro.Application.Interfaces;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.API.Extensions;
using RecruitPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using RecruitPro.Application.Configurations;
using RecruitPro.Infrastructure.Extensions;
using RecruitPro.Application.Extensions;


var builder = WebApplication.CreateBuilder(args);

//setting jwt
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// add db context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// config jwt settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));


// register repo
builder.Services.AddInfrastructureServices();

//register services
builder.Services.AddApplicationBusinessLogicServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
