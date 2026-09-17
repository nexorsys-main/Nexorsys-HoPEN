using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Nexorsys.Identity.API.Middlewares;

public class ErrorHandlingMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<ErrorHandlingMiddleware> _logger;

	public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled request exception for {Method} {Path}", context.Request.Method, context.Request.Path);
			if (context.Response.HasStarted) throw;
			context.Response.Clear();
			context.Response.StatusCode = StatusCodes.Status500InternalServerError;
			await context.Response.WriteAsJsonAsync(new { error = "An error occurred while processing your request." });
		}
	}
}

public static class ErrorHandlingMiddlewareExtensions
{
	public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
	{
		return builder.UseMiddleware<ErrorHandlingMiddleware>();
	}
}
