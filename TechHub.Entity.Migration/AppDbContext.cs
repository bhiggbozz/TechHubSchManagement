using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.Models;
using TechHub.QuestionBank.Core.Entities;

namespace TechHub.Entity.Migration
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

		// Core
		public DbSet<School> School { get; set; }
		public DbSet<SchoolCode> SchoolCode { get; set; }
		public DbSet<Users> Users { get; set; }
		public DbSet<Role> Roles { get; set; }
		public DbSet<TenantInfo> TenantInfo { get; set; }
		public DbSet<PlatformUser> PlatformUsers { get; set; }
		public DbSet<LoginHistory> LoginHistory { get; set; }
		public DbSet<RefreshTokens> RefreshTokens { get; set; }
		public DbSet<AdminPermissions> AdminPermissions { get; set; }
		public DbSet<State> States { get; set; }

		// Classroom & Subjects
		public DbSet<Classroom> StudentClass { get; set; }
		public DbSet<Subjects> Subjects { get; set; }
		public DbSet<ClassroomSubject> ClassroomSubject { get; set; }
		public DbSet<ClassroomTeacher> ClassroomTeacher { get; set; }
		public DbSet<TeacherSubject> TeacherSubject { get; set; }
		public DbSet<StudentClassroom> StudentClassroom { get; set; }
		public DbSet<StudentMinorSubject> StudentMinorSubject { get; set; }
		public DbSet<StudentCourses> StudentCourses { get; set; }

		// Topic & Lesson
		public DbSet<Topic> Topic { get; set; }
		public DbSet<SubTopic> SubTopic { get; set; }
		public DbSet<LessonContent> LessonContent { get; set; }
		public DbSet<LessonMedia> LessonMedia { get; set; }
		public DbSet<ClassPreparation> ClassPreparation { get; set; }
		public DbSet<ClassPreparationMedia> ClassPreparationMedia { get; set; }
		public DbSet<ApprovalRequests> ApprovalRequests { get; set; }
		public DbSet<TeacherTrustScore> TeacherTrustScore { get; set; }
		public DbSet<StudentLessonProgress> StudentLessonProgress { get; set; }

		// Quiz
		public DbSet<Quiz> Quiz { get; set; }
		public DbSet<QuizQuestion> QuizQuestion { get; set; }
		public DbSet<QuizConfig> QuizConfig { get; set; }
		public DbSet<AssessmentSet> AssessmentSet { get; set; }
		public DbSet<QuizAttempt> QuizAttempt { get; set; }
		public DbSet<QuizAttemptAnswer> QuizAttemptAnswer { get; set; }
		public DbSet<QuizAttemptAssistance> QuizAttemptAssistance { get; set; }

		// Assessment
		public DbSet<Assessments> Assessments { get; set; }
		public DbSet<AssessmentConfig> AssessmentConfig { get; set; }
		public DbSet<AssessmentQuestion> AssessmentQuestion { get; set; }
		public DbSet<AssessmentAssignment> AssessmentAssignment { get; set; }
		public DbSet<AssessmentAttempt> AssessmentAttempt { get; set; }
		public DbSet<AssessmentAttemptAnswer> AssessmentAttemptAnswer { get; set; }
		public DbSet<AssessmentAttemptAnswerBoard> AssessmentAttemptAnswerBoard { get; set; }

		// Question Bank
		public DbSet<Questions> Questions { get; set; }
		public DbSet<QuestionOptions> QuestionOptions { get; set; }
		public DbSet<QuestionImage> QuestionImages { get; set; }
		public DbSet<QuestionJob> QuestionJob { get; set; }
		public DbSet<ScanSession> ScanSessions { get; set; }
		public DbSet<ScanToken> ScanToken { get; set; }

		// Analytics
		public DbSet<PerformanceAggregationLog> PerformanceAggregationLog { get; set; }

		// Misc
		public DbSet<EmailTemplate> EmailTemplates { get; set; }

		

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
