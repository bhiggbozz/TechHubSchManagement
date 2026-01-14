using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;
public class TenantContext
{
	public string SchoolId { get; set; }      // String
	public string SchoolName { get; set; }
	public string Domain { get; set; }
}
