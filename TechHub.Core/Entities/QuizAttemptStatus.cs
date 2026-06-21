using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public static class QuizAttemptStatus
{
	public const string InProgress = "InProgress";
	public const string Submitted = "Submitted";
	public const string PartiallyGraded = "PartiallyGraded";
	public const string FullyGraded = "FullyGraded";
	public const string Abandoned = "Abandoned";
}
