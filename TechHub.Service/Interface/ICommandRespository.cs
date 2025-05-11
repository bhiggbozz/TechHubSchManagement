using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace TechHub.Service.Interface
{
	public interface ICommandRespository<T> where T : class
	{
		 Task Create(T entity);
	}
}
