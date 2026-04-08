using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Entities;

public class QuestionOptions
{
	public Guid Id { get; set; }
	public Guid QuestionId { get; set; }

	// ─────────────────────────────────────────────────────
	// LABEL + ORDER
	// OptionLabel : A | B | C | D | E | F | G | H
	// OrderIndex  : controls display order
	// Dynamic — no fixed count, supports 4, 6, 7, 8 etc
	// ─────────────────────────────────────────────────────
	public string OptionLabel { get; set; }
	public int OrderIndex { get; set; }

	// ─────────────────────────────────────────────────────
	// CONTENT — PLAIN TEXT (existing scan pipeline)
	// ─────────────────────────────────────────────────────
	public string OptionText { get; set; }

	// ─────────────────────────────────────────────────────
	// CONTENT — AI GENERATED (new upload pipeline)
	// OptionHtml   : Claude-generated HTML for this option
	// ContentParts : JSON — type/value pairs (text/latex/image)
	// HasLatex     : true if option contains a LaTeX equation
	// HasImages    : true if option contains an image
	// ─────────────────────────────────────────────────────
	public string? OptionHtml { get; set; }
	public string? ContentParts { get; set; }
	public bool HasLatex { get; set; }
	public bool HasImages { get; set; }

	// ─────────────────────────────────────────────────────
	// ANSWER
	// ─────────────────────────────────────────────────────
	public bool IsCorrect { get; set; }

	// ─────────────────────────────────────────────────────
	// AUDIT
	// ─────────────────────────────────────────────────────
	public bool IsActive { get; set; } = true;
	public bool IsDeleted { get; set; } = false;
	public string DeletedDate { get; set; }
	public string CreationDate { get; set; }
	public string ModifiedDate { get; set; }
}