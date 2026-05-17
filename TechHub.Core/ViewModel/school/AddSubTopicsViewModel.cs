using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.school;

public class AddSubTopicsViewModel
{
	public Guid TopicId { get; set; }
	public List<string> SubTopics { get; set; } = new();
}
