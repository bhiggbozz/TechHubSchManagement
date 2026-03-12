using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.DTO;

namespace TechHub.Core.ResponseModel;


public class ClassPreparationsListData
{
	public List<ClassPreparationDto> ClassPreparations { get; set; } = new();
	public int TotalCount { get; set; }
	public int PageNumber { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public bool HasPreviousPage { get; set; }
	public bool HasNextPage { get; set; }
}
/// <summary>
/// Single class preparation response
/// </summary>
public class ClassPreparationResponse : BaseResponse
{
	public ClassPreparationDto? ClassPreparation { get; set; }
}

/// <summary>
/// List of class preparations with pagination
/// </summary>
public class ClassPreparationsListResponse : BaseResponse
{
	public ClassPreparationsListData? Data { get; set; }
}

/// <summary>
    /// Single media file response
    /// </summary>
    //public class UploadMediaResponse : BaseResponse
    //{
    //    public MediaFileDto? MediaFile { get; set; }
    //}

    /// <summary>
    /// List of media files
    /// </summary>
    //public class MediaFilesListResponse : BaseResponse
    //{
    //    public List<MediaFileDto> MediaFiles { get; set; } = new();
    //}
