using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;

namespace RecruitPro.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception)
        {
            context.Response.ContentType = "application/json";

            int statusCode;
            ApiResponse<object> response;

            if (exception is BaseException customException)
            {
                statusCode = customException.StatusCode;

                response = statusCode switch
                {
                    400 => ApiResponse<object>.BadRequest(customException.Message),
                    401 => ApiResponse<object>.Unauthorized(customException.Message),
                    404 => ApiResponse<object>.NotFound(customException.Message),
                    _ => ApiResponse<object>.Error(customException.Message)
                };
            }
            else
            {
                statusCode = 500;

                response = ApiResponse<object>.Error(
                    "Đã xảy ra lỗi hệ thống"
                );
            }

            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
