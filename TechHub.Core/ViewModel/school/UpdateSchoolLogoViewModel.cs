using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.school
{
	//public class UpdateSchoolLogoViewModel
	//{
	//	public IFormFile Logo { get; set; }

	//}

	public class UpdateSchoolLogoResponse
	{
		public string LogoUrl { get; set; } = string.Empty;
		public string PublicId { get; set; } = string.Empty;
	}

	public class SchoolLogoSetupStatus
	{
		public Guid SchoolId { get; set; }
		public string SchoolName { get; set; } = string.Empty;
		public string? LogoUrl { get; set; }
		public bool HasLogo { get; set; }
	}
}
