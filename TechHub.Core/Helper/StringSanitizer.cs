using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;

namespace TechHub.Core.Helper;

public static class StringSanitizer
{

	// ═══════════════════════════════════════════════════════════
	// PRIVATE HELPERS
	// ═══════════════════════════════════════════════════════════

	/// <summary>
	/// Sanitize string to prevent SQL injection
	/// Escapes single quotes
	/// </summary>
	public static string Sanitize(string input)
		=> input?.Replace("'", "''").Trim() ?? string.Empty;

	/// <summary>
	/// Returns a bad request response with a message
	/// Keeps method bodies clean
	/// </summary>
	public static T Fail<T>(string message) where T : BaseResponse, new()
		=> new T
		{
			ResponseCode = ResponseCode.BadRequest,
			ResponseMessage = message,
			Status = "failed"
		};

	/// <summary>
	/// Returns a generic server error response
	/// </summary>
	public static T Error<T>() where T : BaseResponse, new()
		=> new T
		{
			ResponseCode = ResponseCode.ErrorOccured,
			ResponseMessage = "An error occurred. Please try again",
			Status = "failed"
		};
}

