using System.Reflection.Metadata;
using System.Text.Json;
using TechhubMS.util;

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

			await _next(context); // Execute next middleware

			memoryStream.Seek(0, SeekOrigin.Begin);
			var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
			memoryStream.Seek(0, SeekOrigin.Begin);

			if (!string.IsNullOrEmpty(responseBody) && context.Response.ContentType?.Contains("application/json") == true)
			{
				try
				{
					var json = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
					if (json != null && json.TryGetValue("responseCode", out var responseCode))
					{
						if (ResponseCodeMappings.TryGetValue(responseCode, out var statusCode))
						{
							context.Response.StatusCode = statusCode;
						}
					}
				}
				catch (JsonException)
				{
					// Ignore if the response is not a valid JSON
				}
			}

			await memoryStream.CopyToAsync(originalBodyStream);
		


		}
	}
}
