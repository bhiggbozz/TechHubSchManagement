using System.Text.Json;
using TechSchPlatform.Core.Model;

namespace TechSchPlatform.Api.Middleware;

/// <summary>
/// Maps the <c>responseCode</c> inside every BaseResponse body to the matching
/// HTTP status code (99001 → 400, 99101 → 500, AX1003 → 403, 99107 → 401,
/// 99134 → 404, 99161 → 409). Success (99000) stays 200.
/// </summary>
public class ResponseCodeMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly Dictionary<string, int> ResponseCodeMappings = new()
    {
        { ResponseCode.BadRequest, StatusCodes.Status400BadRequest },
        { ResponseCode.Unauthorized, StatusCodes.Status401Unauthorized },
        { ResponseCode.Forbidden, StatusCodes.Status403Forbidden },
        { ResponseCode.NotFound, StatusCodes.Status404NotFound },
        { ResponseCode.Conflict, StatusCodes.Status409Conflict },
        { ResponseCode.ErrorOccured, StatusCodes.Status500InternalServerError }
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

        try
        {
            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
            memoryStream.Seek(0, SeekOrigin.Begin);

            if (!string.IsNullOrEmpty(responseBody) && context.Response.ContentType?.Contains("application/json") == true)
            {
                var json = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
                if (json is not null && json.TryGetValue("responseCode", out var responseCode))
                {
                    if (ResponseCodeMappings.TryGetValue(responseCode, out var statusCode))
                    {
                        context.Response.StatusCode = statusCode;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore if the response body is not valid JSON.
        }
        finally
        {
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
}