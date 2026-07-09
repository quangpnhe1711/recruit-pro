using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.API.Filters;

/// <summary>
/// Resolves the code-first contract for error <see cref="ApiResponse{T}"/> objects that services RETURN
/// (as opposed to throw — those go through <c>ExceptionMiddleware</c>). Fills the flat message + nested
/// <c>error</c> block from the stable code via <see cref="IErrorMessageProvider"/>, in one central place,
/// so no controller/service ever hardcodes a message.
/// </summary>
public sealed class ErrorEnvelopeResultFilter : IAsyncAlwaysRunResultFilter
{
    private readonly IErrorMessageProvider _provider;

    public ErrorEnvelopeResultFilter(IErrorMessageProvider provider)
    {
        _provider = provider;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: IErrorEnvelope env } && !env.Success && env.ErrorDetail is null)
        {
            string traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            env.ResolveError(_provider, traceId);
        }

        await next();
    }
}
