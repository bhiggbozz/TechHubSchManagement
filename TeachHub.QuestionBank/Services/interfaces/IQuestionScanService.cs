using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;

namespace TechHub.QuestionBank.Services.interfaces;

public interface IQuestionScanService
{
	/// <summary>
	/// Teacher requests permission for one scan
	/// Validates quota and subscription
	/// Returns short-lived single-use token
	/// </summary>
	Task<RequestScanTokenResponse> RequestScanToken(RequestScanTokenViewModel model, AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Thin proxy to Claude API
	/// Token validated and consumed here
	/// Image piped to Claude via streaming
	/// Response streamed back to frontend
	/// Backend does zero processing
	/// </summary>
	Task ProcessScan(Guid tokenId,IFormFile image,HttpContext httpContext);
	// HttpContext needed for SSE streaming
	// Response written directly to context
	// Not returned as normal response object

	/// <summary>
	/// Save confirmed questions after review
	/// Creates ScanSession record
	/// Saves each question as PendingReview
	/// Idempotent via LocalSessionId
	/// </summary>
	Task<SaveScanResultsResponse> SaveScanResults(SaveScanResultsViewModel model, AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Check teacher scan quota
	/// Frontend calls before showing scan UI
	/// Prevents failed attempts
	/// </summary>
	Task<ScanQuotaResponse> GetScanQuota(AuthenticatedUserClaims userClaims);
}
