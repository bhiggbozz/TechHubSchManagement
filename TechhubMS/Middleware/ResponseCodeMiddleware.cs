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
		// Keys must match the literal string values of TechHub.Core.Model.ResponseCode's
		// constants — this table previously used "AX1002"/"AX1004", which no code in the
		// app actually returns (the real constants are ResponseCode.Unauthorized="99107"
		// and ResponseCode.NotFound="99134"), so Unauthorized/NotFound/Conflict responses
		// silently stayed at whatever the controller's Ok/BadRequest ternary already set
		// (400) instead of being rewritten to their documented HTTP status.
		private static readonly Dictionary<string, int> ResponseCodeMappings = new()
	{
		{ ResponseCode.BadRequest, StatusCodes.Status400BadRequest },     // 99001
        { ResponseCode.Unauthorized, StatusCodes.Status401Unauthorized }, // 99107
        { ResponseCode.Forbidden, StatusCodes.Status403Forbidden },      // AX1003
        { ResponseCode.NotFound, StatusCodes.Status404NotFound },        // 99134
        { ResponseCode.Conflict, StatusCodes.Status409Conflict },        // 99161
        { ResponseCode.ErrorOccured, StatusCodes.Status500InternalServerError }, // 99101
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
