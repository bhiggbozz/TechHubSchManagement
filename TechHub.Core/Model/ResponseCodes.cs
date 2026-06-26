using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model
{
	public static class ResponseCode
	{
		public const string BadRequest = "99001";
		public const string successful = "99000";
		public const string ErrorOccured = "99101";
		public const string Forbidden = "AX1003";
		public const string Unauthorized = "99107";
		public const string Conflict = "99161";
		public const string NotFound = "99134";
		public const string COnflict = "409";

	}
}
