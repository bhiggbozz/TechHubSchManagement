using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// TOKEN REQUEST
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Teacher requests permission for one scan operation
/// Must be called before any image is sent
/// </summary>
public class RequestScanTokenViewModel
{
	public ScanType ScanType { get; set; }
	// Teacher declares what they are scanning
	// Determines prompt used in proxy

	public string FileType { get; set; }
	// "image" or "pdf"

	public string LocalSessionId { get; set; }
	// Device generated UUID
	// Links token to IndexedDB entry
	// Used for tab closure recovery

	public string DeviceId { get; set; }
}

