using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;

	public class BoardSwitchEvent
{
	public int FromBoard { get; set; }
	public int ToBoard { get; set; }
	public long TimestampMs { get; set; }  // global session time
	public int BatchIndex { get; set; }  // which batch this happened in
}
