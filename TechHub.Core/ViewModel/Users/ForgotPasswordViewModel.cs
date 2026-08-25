using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users
{
	public class ForgotPasswordViewModel
	{
		[Required]
		public string Username { get; set; }
	}
}
