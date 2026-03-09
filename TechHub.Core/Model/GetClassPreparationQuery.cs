using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.Model;

/// <summary>
/// Get class preparations with filters and pagination
/// </summary>
public class GetClassPreparationsQuery
{
	/// <summary>
	/// Filter by status (Draft, Pending, Approved, etc.)
	/// </summary>
	public ClassPreparationStatus? Status { get; set; }

	/// <summary>
	/// Filter by teacher ID
	/// </summary>
	public Guid? TeacherId { get; set; }

	/// <summary>
	/// Filter by classroom
	/// </summary>
	public Guid? ClassroomId { get; set; }

	/// <summary>
	/// Filter by subject
	/// </summary>
	public Guid? SubjectId { get; set; }

	/// <summary>
	/// Filter by date range - from
	/// </summary>
	public DateTime? FromDate { get; set; }

	/// <summary>
	/// Filter by date range - to
	/// </summary>
	public DateTime? ToDate { get; set; }

	/// <summary>
	/// Search in topic/subtopic
	/// </summary>
	public string? SearchTerm { get; set; }

	/// <summary>
	/// Page number (1-based)
	/// </summary>
	[Range(1, int.MaxValue)]
	public int PageNumber { get; set; } = 1;

	/// <summary>
	/// Page size (1-100)
	/// </summary>
	[Range(1, 100)]
	public int PageSize { get; set; } = 20;

	/// <summary>
	/// Sort by field
	/// </summary>
	public string? SortBy { get; set; } = "CreationDate";

	/// <summary>
	/// Sort direction (asc/desc)
	/// </summary>
	public string? SortDirection { get; set; } = "desc";
}

