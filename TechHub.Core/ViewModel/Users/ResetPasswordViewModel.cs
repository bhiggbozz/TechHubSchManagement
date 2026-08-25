using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users
{
	public class ResetPasswordViewModel
	{
		[Required]
		public string Token { get; set; }
		[Required]
		public string NewHashPassword { get; set; }
		[Required]
		public string ConfirmHashPassword { get; set; }
	}
}
