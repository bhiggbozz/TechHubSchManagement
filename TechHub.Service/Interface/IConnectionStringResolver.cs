using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Service.Interface;

public interface IConnectionStringResolver
{
	string Resolve(DatabaseTarget target);
}

