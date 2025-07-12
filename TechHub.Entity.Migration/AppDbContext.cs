using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Model;

namespace TechHub.Entity.Migration
{
	public class AppDbContext :DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
		
		public DbSet<Classroom> StudentClass {  get; set; }
		public  DbSet<Users> Users { get; set; }
		public  DbSet<Role> Roles { get; set; }
		public  DbSet<School> School { get; set; }
		public DbSet<SchoolCode> SchoolCode { get; set;}
		public DbSet<Subjects> Subjects { get; set; }

		public DbSet<LoginHistory> LoginHistory { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<LoginHistory>()
			.HasIndex(lh => new { lh.UserId, lh.CreationDate })
			.HasDatabaseName("IX_LoginHistory_UserId_CreationDate")
			.IsDescending(false, true)
			.IsClustered();

			modelBuilder.Entity<Users>(entity =>
			{
				// Composite Primary Key
				entity.HasKey(u => new { u.Id, u.CreationDate });

				// Optional: Column length configurations
				entity.Property(u => u.FirstName).HasMaxLength(100);
				entity.Property(u => u.LastName).HasMaxLength(100);
				entity.Property(u => u.EmailAddress).HasMaxLength(255);
				entity.Property(u => u.HashPassword).HasMaxLength(255);
				entity.Property(u => u.UserName).HasMaxLength(100);
				entity.Property(u => u.SchoolCode).HasMaxLength(50);

				// Required fields are enforced by default on non-nullable types (e.g., Guid, DateTime, int, bool)
			});

			modelBuilder.Entity<SchoolCode>(entity =>
			{
				entity.HasKey(sc => sc.SchoolId); // Primary key

				entity.Property(sc => sc.Code)
					  .HasMaxLength(80); 

				entity.Property(sc => sc.SchoolId)
					  .IsRequired();
			});

			modelBuilder.Entity<School>(entity =>
			{
				entity.HasKey(s => s.Id);

				entity.Property(s => s.CreationDate)
					  .HasMaxLength(19)
					  .IsRequired();

				entity.Property(s => s.ModifiedDate)
					  .HasMaxLength(19)
					  .IsRequired();

				entity.Property(s => s.SchoolName)
					  .HasMaxLength(500);

				entity.Property(s => s.Location)
					  .HasMaxLength(100);

				entity.Property(s => s.Address)
					  .HasMaxLength(1000);

				entity.Property(s => s.HasBranch)
					  .IsRequired();

				entity.Property(s => s.ISActive)
					  .IsRequired(false); // Nullable bit

				// Unique Constraint
				entity.HasIndex(s => new { s.SchoolName, s.Address })
					  .IsUnique()
					  .HasDatabaseName("UQ_School_information");
			});

			modelBuilder.Entity<Classroom>(entity =>
			{
				// Composite Primary Key
				entity.HasKey(sc => new { sc.Id, sc.CreationDate });

				// Column Configurations
				entity.Property(sc => sc.CreationDate)
					  //.HasDefaultValueSql("GETDATE()")
					  .IsRequired();

				entity.Property(sc => sc.ModifiedDate)
					 // .HasDefaultValueSql("GETDATE()")
					  .IsRequired();

				entity.Property(sc => sc.Name)
					  .HasMaxLength(255)
					  .IsRequired();

				entity.Property(sc => sc.TeacherName)
					  .HasMaxLength(255);

				entity.Property(sc => sc.NoOfStudents)
					  .IsRequired();

				entity.Property(sc => sc.CreatedBy)
					  .IsRequired();

				entity.Property(sc => sc.SchoolId)
					  .IsRequired();

				entity.HasIndex(sc => new { sc.SchoolId, sc.Name })
			  .IsUnique()
			  .HasDatabaseName("UQ_StudentClass_SchoolId_Name");
			});

			modelBuilder.Entity<Subjects>(entity =>
			{
				entity.ToTable("Subjects");

				entity.HasKey(e => new { e.Id, e.SchoolId })
					  .HasName("PK_Subjects");

				entity.Property(e => e.Id)
					  .IsRequired();

				entity.Property(e => e.CreationDate)
					  .HasDefaultValueSql("GETDATE()")
					  .IsRequired();

				entity.Property(e => e.ModifiedDate)
					  .HasDefaultValueSql("GETDATE()")
					  .IsRequired();

				entity.Property(e => e.Subject)
					  .HasMaxLength(255)
					  .IsRequired();
				entity.Property(e => e.Category)
					  .IsRequired();
				entity.Property(e => e.IsActive)
					  .IsRequired();

				entity.Property(e => e.SchoolId)
					  .IsRequired();

				entity.Property(e => e.CreatedBy)
					  .IsRequired();
			});
		}
	
	}
}
