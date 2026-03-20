using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Enums;

public enum ResolutionChoice
{
	KeepLocal = 1,
	// Teacher's device version wins
	// Server gets overwritten with local

	KeepServer = 2,
	// Server version wins
	// Local gets overwritten with server
	// Frontend cleans up local record

	KeepMerged = 3,
	// Teacher manually merged both
	// MergedData contains the result

	DiscardLocal = 4
	// Teacher abandons local changes entirely
	// Local record cleaned up
	// No server update needed
}


/// <summary>
/// Why a conflict occurred
/// Helps frontend display appropriate message
/// </summary>
public enum ConflictReason
{
	BothVersionsEdited = 1,
	// Local and server both changed
	// since last sync

	ServerDeletedLocally = 2,
	// Server version was deleted
	// but local still has edits

	LocalDeletedOnServer = 3,
	// Teacher deleted on another device
	// but this device still has it

	SchemaVersionMismatch = 4
	// Question structure changed
	// requires migration before sync
}

