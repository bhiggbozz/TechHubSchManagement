using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;

public class QuestionFilterViewModelV2
{
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
	public Guid? SubjectId { get; set; }  // ← add
	public Guid? TopicId { get; set; }  // ← add
	public List<Guid> SubTopicIds { get; set; } = new(); 
	public QuestionType? QuestionType { get; set; }
	public DifficultyLevel? DifficultyLevel { get; set; }
	public QuestionStatus? Status { get; set; }
	public string? SearchText { get; set; }
	public bool IncludePendingReview { get; set; } = false;
	public Guid? ScanSessionId { get; set; }

	// Admin-only tier toggle. Ignored entirely for non-admin callers (they
	// never see IsAdminOnly=1 rows regardless of this value). For admins:
	// null/false = everything combined (shared + admin-only), true = only
	// their admin-only stash. See ClaimsHelper.BuildAdminOnlyFilter.
	public bool? AdminOnly { get; set; }
}

