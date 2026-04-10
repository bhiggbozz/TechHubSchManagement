using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TechHub.Core.Entities;

namespace TechHub.Core.Models
{
	/// <summary>
	/// TenantInfo model - matches your School class pattern
	/// EF Core will create the table from this model via migrations
	/// </summary>
	[Table("TenantInfo")]
	public class TenantInfo
	{
		/// <summary>
		/// Primary key - using Guid to match your School.Id pattern
		/// EF will auto-generate this
		/// </summary>
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// Foreign key to School table
		/// Must be Guid to match School.Id type
		/// </summary>
		[Required]
		public Guid SchoolId { get; set; }

		/// <summary>
		/// Tenant identifier/subdomain (e.g., "pearl", "oxford")
		/// Unique constraint will be added in configuration
		/// </summary>
		[Required]
		[MaxLength(100)]
		public string Identifier { get; set; } = string.Empty;

		/// <summary>
		/// Tenant/School name
		/// This will be populated from School.SchoolName in queries
		/// Mark as NotMapped if you want to get it via JOIN (Dapper)
		/// Or remove this and always JOIN with School table
		/// </summary>
		[NotMapped] // This field is NOT in database, populated via JOIN
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// Whether the tenant is active
		/// </summary>
		[Required]
		public bool IsActive { get; set; } = true;

		/// <summary>
		/// Optional: Connection string for database-per-tenant
		/// </summary>
		[MaxLength(500)]
		public string? ConnectionString { get; set; } = string.Empty;

		/// <summary>
		/// When the tenant was created
		/// Matches your School.CreationDate pattern
		/// </summary>
		[Required]
		public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// When the tenant was last modified
		/// Matches your School.ModifiedDate pattern
		/// </summary>
		[Required]
		public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// JSON string for settings (NOT Dictionary - EF Core can't store Dictionary directly)
		/// Store as JSON string, then deserialize when needed
		/// </summary>
		[Column(TypeName = "nvarchar(max)")]
		public string? SettingsJson { get; set; }

		// Navigation property (optional - for EF Core)
		/// <summary>
		/// Navigation to School entity
		/// Only used if you're doing EF Core queries (not needed for Dapper)
		/// </summary>
		[ForeignKey(nameof(SchoolId))]
		public virtual School? School { get; set; }

		// Helper methods for Settings

		/// <summary>
		/// Get settings as dictionary
		/// </summary>
		[NotMapped]
		public Dictionary<string, string>? Settings
		{
			get
			{
				if (string.IsNullOrEmpty(SettingsJson))
					return null;

				try
				{
					return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(SettingsJson);
				}
				catch
				{
					return null;
				}
			}
			set
			{
				SettingsJson = value != null
					? System.Text.Json.JsonSerializer.Serialize(value)
					: null;
			}
		}

		/// <summary>
		/// Get a specific setting value
		/// </summary>
		public string? GetSetting(string key)
		{
			var settings = Settings;
			return settings?.ContainsKey(key) == true ? settings[key] : null;
		}

		/// <summary>
		/// Set a specific setting value
		/// </summary>
		public void SetSetting(string key, string value)
		{
			var settings = Settings ?? new Dictionary<string, string>();
			settings[key] = value;
			Settings = settings;
		}
	}
}