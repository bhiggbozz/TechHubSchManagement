using Microsoft.AspNetCore.Http;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Text.Json;
using TechHub.Core.Model;

namespace TechhubMS.Middleware
{
	public class ResponseCodeMiddleware
	{
		private readonly RequestDelegate _next;
		private static readonly Dictionary<string, int> ResponseCodeMappings = new()
	{
		{ ResponseCode.BadRequest, StatusCodes.Status400BadRequest },   // Bad Request
        { "AX1002", StatusCodes.Status401Unauthorized }, // Unauthorized
        { "AX1003", StatusCodes.Status403Forbidden },    // Forbidden
        { "AX1004", StatusCodes.Status404NotFound },     // Not Found
        { ResponseCode.ErrorOccured, StatusCodes.Status500InternalServerError }, // Internal Server Error
		//{ "99000", StatusCodes.Status200OK } // Internal Server Error

    };
		public ResponseCodeMiddleware(RequestDelegate next)
		{
			_next = next;
		}
		public async Task Invoke(HttpContext context)
		{

			var originalBodyStream = context.Response.Body;
			using var memoryStream = new MemoryStream();
			context.Response.Body = memoryStream;

			// Stash the matched endpoint (Controller.Action) BEFORE downstream runs,
			// so the global exception handler can read it from HttpContext.Items even
			// when a controller throws and unwinds past this middleware.
			context.Items["Logging_Endpoint"] = ResolveEndpoint(context);

			try
			{
				await _next(context); // Execute next middleware

				memoryStream.Seek(0, SeekOrigin.Begin);
				var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
				memoryStream.Seek(0, SeekOrigin.Begin);

				if (!string.IsNullOrEmpty(responseBody) && context.Response.ContentType?.Contains("application/json") == true)
				{
					var json = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
					if (json != null && json.TryGetValue("responseCode", out var responseCode))
					{
						if (ResponseCodeMappings.TryGetValue(responseCode, out var statusCode))
						{
							context.Response.StatusCode = statusCode;

							// Note: swallowed exceptions (ResponseCode 99101) are
							// persisted to ApplicationLogs centrally by the
							// ApplicationLogsSink — every service logs the real
							// exception via _logger.Error(ex, ...) before returning
							// the generic BaseResponse, so the row carries the
							// actual message + stack trace. No logging here.
						}
					}
				}
			}
			catch (JsonException)
			{
				// Ignore if the response is not a valid JSON
			}
			finally
			{
				// Always restore the real body stream — including when a
				// downstream exception unwinds past this middleware — so a
				// global handler writing a 500 to context.Response actually
				// reaches the client instead of a discarded MemoryStream.
				try
				{
					await memoryStream.CopyToAsync(originalBodyStream);
				}
				finally
				{
					context.Response.Body = originalBodyStream;
				}
			}
		}

		/// <summary>
		/// Matched controller.action of the failing request (e.g.
		/// "AssessmentController.StartAttempt") via the endpoint metadata,
		/// falling back to the display name. Exception-safe — never throws.
		/// </summary>
		private static string? ResolveEndpoint(HttpContext context)
		{
			try
			{
				var endpoint = context.GetEndpoint();
				if (endpoint is null)
					return null;

				var methodInfo = endpoint.Metadata.GetMetadata<System.Reflection.MethodInfo>();
				if (methodInfo is not null)
					return $"{methodInfo.DeclaringType?.Name}.{methodInfo.Name}";

				return endpoint.DisplayName;
			}
			catch
			{
				return null;
			}
		}
	}
}
