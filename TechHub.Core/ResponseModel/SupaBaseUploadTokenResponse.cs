using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;

public class SupabaseUploadTokenResponse
{
	public string UploadUrl { get; set; } 
	public string PublicUrl { get; set; }  
	public string Token { get; set; }  
	public string BucketPath { get; set; }  
	public string Bucket { get; set; }
}

