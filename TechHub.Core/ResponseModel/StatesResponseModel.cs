using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel
{
	public class StatesResponseModel : BaseResponse
	{
		public List<StateResponse> states { get; set; } = new List<StateResponse>();
	}
	public class StateResponse
	{

	}
}
