using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users
{
	public class ConfirmPasswordChangeViewModel
	{
		[Required]
		public string Token { get; set; }
	}
}
