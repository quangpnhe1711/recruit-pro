using RecruitPro.Application.Interfaces;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Configurations;
using RecruitPro.Infrastructure.Extensions;
using RecruitPro.Application.Extensions;
using RecruitPro.API.Middlewares;


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
    options.UseNpgsql(builder.Configuration.GetConnectionString("Mycnn")));

// config jwt settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));


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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
