namespace TechHub.Core.ResponseModel
{
	public class ParentSearchResponseDto
	{
		public int TotalCount { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public List<ParentSearchItemDto> Parents { get; set; } = new();
	}

	public class ParentSearchItemDto
	{
		public Guid ParentId { get; set; }
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public int StudentCount { get; set; }
		public List<ChildInfo> Students { get; set; } = new();
	}
}
