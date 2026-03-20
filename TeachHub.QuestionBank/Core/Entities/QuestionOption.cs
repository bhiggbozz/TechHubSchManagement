using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Entities;

public class QuestionOption
{
	public Guid Id { get; set; }
	public Guid QuestionId { get; set; }
	public string OptionLabel { get; set; }
	public string OptionText { get; set; }
	public bool IsCorrect { get; set; }
	public int OrderIndex { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsDeleted { get; set; } = false;
	public string DeletedDate { get; set; }
	public string CreationDate { get; set; }
	public string ModifiedDate { get; set; }
}