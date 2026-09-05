using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;

public static class ApprovalStatus
{
	public const string Pending = "Pending";
	public const string Approved = "Approved";
	public const string Rejected = "Rejected";
	public const string Expired = "Expired";
}

public static class OperationType
{
	public const string SubmitLesson = "SubmitLesson";
	public const string SubmitSyllabus = "SubmitSyllabus";
	public const string CreateExamination = "CreateExamination";
	public const string CreateUser = "CreateUser";
	public const string EditUser = "EditUser";
	public const string AssignPermissions = "AssignPermissions";
	public const string RegisterToClass = "RegisterToClass";
	public const string DeactivateUser = "DeactivateUser";
	public const string CreateTopic = "CreateTopic";
	public const string AddSubTopics = "AddSubTopics";
	public const string CreateGroup = "CreateGroup";
	public const string SubmitGroupContent = "SubmitGroupContent";
}

