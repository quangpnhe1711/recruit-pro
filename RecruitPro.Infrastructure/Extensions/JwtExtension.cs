using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Infrastructure.Service;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RecruitPro.Infrastructure.Extensions
{
    public static class JwtExtension
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
            IConfiguration config)
        {
            // Get JWT settings from configuration
            var jwt = config.GetSection("Jwt");

            // Configure JWT authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwt["Issuer"],
                        ValidAudience = jwt["Audience"],

                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)
                        )
                    };
                    options.Events = new JwtBearerEvents
                    {
                        // Deactivation enforcement: a signed, unexpired access token is still rejected once the
                        // account is no longer Active or its TokenVersion has moved on (bumped on deactivate).
                        // ponytail: one cheap projected DB read per authenticated request; cache by (userId,version) if throughput ever matters.
                        OnTokenValidated = async context =>
                        {
                            ClaimsPrincipal? principal = context.Principal;
                            string? idValue = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                            if (!Guid.TryParse(idValue, out Guid userId))
                            {
                                context.Fail("Invalid token subject.");
                                return;
                            }

                            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            (string? Status, int TokenVersion)? snapshot = await userRepository.GetAuthSnapshotAsync(userId);
                            if (snapshot is null)
                            {
                                context.Fail("Account no longer exists.");
                                return;
                            }

                            if (snapshot.Value.Status is not null
                                && !snapshot.Value.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Fail("Account is not active.");
                                return;
                            }

                            _ = int.TryParse(principal?.FindFirst(JwtService.TokenVersionClaim)?.Value, out int tokenVersion);
                            if (tokenVersion != snapshot.Value.TokenVersion)
                            {
                                context.Fail("Token has been revoked.");
                            }
                        },
                        // Notification realtime moved from SignalR to SSE (GET /api/notifications/stream).
                        // The SSE client is fetch-based and sends a standard Authorization: Bearer header,
                        // so no query-string token extraction is needed here anymore.
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();
                            if (!context.Response.HasStarted)
                            {
                                context.Response.StatusCode = 401;
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsJsonAsync(
                                    ResolveEnvelope(context.HttpContext, ApiResponse<object>.Unauthorized(ErrorCodes.Unauthenticated)));
                            }
                        },
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(
                                ResolveEnvelope(context.HttpContext, ApiResponse<object>.Forbidden(ErrorCodes.Forbidden)));
                        }
                    };
                });

            return services;
        }

        // These JWT-event responses are written straight to the socket and bypass the MVC result filter,
        // so resolve the code → message + nested error block here from request-scoped services.
        private static ApiResponse<object> ResolveEnvelope(HttpContext httpContext, ApiResponse<object> response)
        {
            var provider = httpContext.RequestServices.GetService<IErrorMessageProvider>();
            if (provider is not null)
            {
                response.ResolveError(provider, httpContext.TraceIdentifier);
            }
            return response;
        }
    }
}
