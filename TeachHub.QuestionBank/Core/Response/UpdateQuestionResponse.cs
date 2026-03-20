using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Response;

	public class UpdateQuestionResponse : BaseResponse
	{
		public Guid QuestionId { get; set; }
		public string ClientId { get; set; }
		public string NewModifiedDate { get; set; }
		// Frontend stores this immediately
		// Used as LastKnownModifiedDate on next edit

		public bool IsConflict { get; set; }
		public ConflictDetail ConflictDetail { get; set; }
		// Populated only when IsConflict is true
		// Frontend routes teacher to conflict resolution screen
	}

