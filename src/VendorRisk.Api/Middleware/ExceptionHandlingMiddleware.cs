using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VendorRisk.Domain.Vendors;

namespace VendorRisk.Api.Middleware;

/// <summary>
/// Turns unhandled exceptions into RFC 7807 problem responses, so clients get a consistent error
/// shape and the stack trace stays in the logs rather than in the payload.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing to report and nothing useful to write to the response.
            _logger.LogInformation("Request {Method} {Path} was cancelled by the client",
                context.Request.Method, context.Request.Path);
        }
        catch (DuplicateVendorNameException ex)
        {
            // A rejected name is an expected outcome, not a fault: log it as information and
            // answer with 409 rather than the generic 500 below.
            _logger.LogInformation("Rejected {Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);

            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Vendor name already exists",
                Detail = ex.Message,
                Instance = context.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Too late to replace the response; let the server terminate it.
                throw;
            }

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed. Check the server logs for details.",
                Instance = context.Request.Path
            };

            await WriteProblemAsync(context, problem);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }
}
