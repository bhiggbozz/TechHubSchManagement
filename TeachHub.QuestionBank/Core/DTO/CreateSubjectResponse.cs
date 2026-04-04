using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;

namespace TechHub.QuestionBank.Core.DTO;

public class SubjectListResponse : BaseResponse
{
	public List<SubjectDto> Subjects { get; set; } = new();
}

public class TopicListResponse : BaseResponse
{
	public List<TopicDto> Topics { get; set; } = new();
}

public class SubTopicListResponse : BaseResponse
{
	public List<SubTopicDto> SubTopics { get; set; } = new();
}

public class CreateSubjectResponse : BaseResponse
{
	public Guid SubjectId { get; set; }
}

public class CreateTopicResponse : BaseResponse
{
	public Guid TopicId { get; set; }
}

public class CreateSubTopicResponse : BaseResponse
{
	public Guid SubTopicId { get; set; }
}
