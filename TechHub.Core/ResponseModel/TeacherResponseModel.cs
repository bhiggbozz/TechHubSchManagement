using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;

public class TeacherResponseModel
{
	public Guid Id { get; set; }
	public string FirstName { get; set; }
	public string LastName { get; set; }
	public string FullName => $"{FirstName} {LastName}";
	public string UserName { get; set; }
	public string EmailAddress { get; set; }
	public string Role { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreationDate { get; set; }
}
