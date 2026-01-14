using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TechHub.Entity.Migration
{
	public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

			optionsBuilder.UseSqlServer(
				"Server = OLAMIDE\\SQLEXPRESS; Initial Catalog = TechHub; Persist Security Info = False; MultipleActiveResultSets = True; Trusted_Connection = True; TrustServerCertificate = true; Connection Timeout = 30",
				b => b.MigrationsAssembly("TechHub.Entity.Migration")
			);

			return new AppDbContext(optionsBuilder.Options);
		}
	}
}
