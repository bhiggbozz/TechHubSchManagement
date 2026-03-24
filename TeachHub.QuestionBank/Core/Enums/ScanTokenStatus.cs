using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Enums;

public enum ScanTokenStatus
{
	Active = 0,
	// Token issued and valid
	// Not yet used

	Used = 1,
	// Token consumed by one scan call
	// Cannot be reused

	Expired = 2,
	// TTL elapsed before use
	// Teacher must request new token

	Revoked = 3
	// Admin or system revoked
	// School subscription expired etc
}

