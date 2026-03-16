using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;

public class ClassroomDetails : BaseResponse
{
	public ClassroomsListData? Data { get; set; }
}

public class ClassroomsListData
{
	public List<ClassroomDto> Classrooms { get; set; } = new();
	public int TotalCount { get; set; }
	public int PageNumber { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public bool HasPreviousPage { get; set; }
	public bool HasNextPage { get; set; }
}

public class ClassroomDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int NoOfStudents { get; set; }
	public bool IsActive { get; set; }
	public string CreationDate { get; set; } = string.Empty;
	public string ModifiedDate { get; set; } = string.Empty;
}

public class ClassroomDetailResponse : BaseResponse
{
	public ClassroomDto? Classroom { get; set; }
}

