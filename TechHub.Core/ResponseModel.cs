using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core
{
	public class BaseResponse
	{
		public string? ResponseMessage {  get; set; }
		public string? ResponseCode { get; set; }
		public string? Status {  get; set; }
	}
}
