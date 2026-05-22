using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Model;

public class DifficultyCountRow
{
	public int DifficultyLevel { get; set; }
	public int QuestionCount { get; set; }
}

public class QuestionTypeCountRow
{
	public int QuestionType { get; set; }
	public int QuestionCount { get; set; }
}
