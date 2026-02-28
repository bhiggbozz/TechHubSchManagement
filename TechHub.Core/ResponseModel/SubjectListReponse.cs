using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;

	// ViewModels/SubjectResponse.cs

	public class SubjectsListResponse : BaseResponse
	{
		public SubjectsListData? Data { get; set; }
	}

	public class SubjectsListData
	{
		public List<SubjectDto> Subjects { get; set; } = new();
		public int TotalCount { get; set; }
		public int PageNumber { get; set; }
		public int PageSize { get; set; }
		public int TotalPages { get; set; }
		public bool HasPreviousPage { get; set; }
		public bool HasNextPage { get; set; }
		public string FilteredBy { get; set; } = string.Empty;
	}

	public class SubjectDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Description { get; set; }
		public int ClassCategory { get; set; }
		public string ClassCategoryName { get; set; } = string.Empty;
		public int SubjectCategory { get; set; }
		public string SubjectCategoryName { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public string CreationDate { get; set; } = string.Empty;
		public string ModifiedDate { get; set; } = string.Empty;
	}

	public class SubjectDetailResponse : BaseResponse
	{
		public SubjectDto? Subject { get; set; }
	}

