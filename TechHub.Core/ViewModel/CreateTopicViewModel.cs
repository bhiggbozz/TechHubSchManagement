using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel;

public class CreateTopicViewModel
{
	public Guid SubjectId { get; set; }
	public string Name { get; set; }
}

public class CreateSubTopicViewModel
{
	public Guid TopicId { get; set; }
	public string Name { get; set; }
}

