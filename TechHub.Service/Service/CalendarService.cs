using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Calendar;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	/// <summary>
	/// School calendar, level 1 and 2: academic sessions and the terms inside them.
	/// The school defines its own structure — names and number of terms are free text,
	/// nothing here assumes First/Second/Third Term. Reads are open to every signed-in
	/// user of the school (parents and students included: this data is school-wide, not
	/// child-specific); writes need SuperAdministrator or an Administrator holding
	/// <see cref="AdminPermission.ManageCalendar"/>.
	/// </summary>
	public class CalendarService : ICalendarService
	{
		private const int MaxTermsPerSession = 6;
		private const int MaxSessionDays = 550;

		private readonly IQueryRepository<AcademicSession> _sessionQueryRepo;
		private readonly ICommandRespository<AcademicSession> _sessionCommandRepo;
		private readonly IQueryRepository<AcademicTerm> _termQueryRepo;
		private readonly ICommandRespository<AcademicTerm> _termCommandRepo;
		private readonly ILogger _logger;

		public CalendarService(
			IQueryRepository<AcademicSession> sessionQueryRepo,
			ICommandRespository<AcademicSession> sessionCommandRepo,
			IQueryRepository<AcademicTerm> termQueryRepo,
			ICommandRespository<AcademicTerm> termCommandRepo,
			ILogger logger)
		{
			_sessionQueryRepo = sessionQueryRepo;
			_sessionCommandRepo = sessionCommandRepo;
			_termQueryRepo = termQueryRepo;
			_termCommandRepo = termCommandRepo;
			_logger = logger;
		}

		#region Templates

		public BaseResponse GetTemplates()
		{
			var templates = new List<CalendarTemplateDto>
			{
				new() { Key = "ThreeTerm", Label = "Three terms (First / Second / Third Term)", TermNames = new() { "First Term", "Second Term", "Third Term" } },
				new() { Key = "British", Label = "British (Autumn / Spring / Summer)", TermNames = new() { "Autumn Term", "Spring Term", "Summer Term" } },
				new() { Key = "TwoSemester", Label = "Two semesters", TermNames = new() { "First Semester", "Second Semester" } },
				new() { Key = "Custom", Label = "Custom", TermNames = new() }
			};

			return Ok("Calendar templates retrieved", templates);
		}

		#endregion

		#region Reads

		public async Task<BaseResponse> GetSessionsAsync(AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					var sessions = await LoadSessionsAsync(schoolId);
					var terms = await LoadTermsForSchoolAsync(schoolId);
					var today = SchoolToday();

					var data = sessions.Select(s => ToDto(s, terms.Where(t => t.SessionId == s.Id), today)).ToList();
					return Ok(data.Count == 0 ? "No academic sessions have been set up yet" : $"{data.Count} session(s) found", data);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error listing sessions");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving academic sessions");
				}
			}
		}

		public async Task<BaseResponse> GetSessionAsync(Guid? sessionId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					var today = SchoolToday();
					AcademicSession? session;

					if (sessionId.HasValue)
					{
						session = await LoadSessionAsync(sessionId.Value, schoolId);
						if (session is null)
							return Fail(ResponseCode.NotFound, "Academic session not found");
					}
					else
					{
						var all = await LoadSessionsAsync(schoolId);
						session = PickCurrent(all, today);
						if (session is null)
							return Ok("No academic sessions have been set up yet", null);
					}

					var terms = await LoadTermsAsync(session.Id);
					return Ok("Academic session retrieved", ToDto(session, terms, today));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error getting session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the academic session");
				}
			}
		}

		#endregion

		#region Session writes

		public async Task<BaseResponse> CreateSessionAsync(CreateAcademicSessionViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var denied = await CheckCanManageAsync(claims);
					if (denied != null) return denied;
					var schoolId = ParseSchoolId(claims);
					var userId = ParseUserId(claims);

					if (model is null)
						return Fail(ResponseCode.BadRequest, "Request body is required");

					var name = (model.Name ?? string.Empty).Trim();
					var sessionError = ValidateSession(name, model.StartDate.Date, model.EndDate.Date);
					if (sessionError != null) return Fail(ResponseCode.BadRequest, sessionError);

					if (model.Terms is null || model.Terms.Count == 0)
						return Fail(ResponseCode.BadRequest, "A session needs at least one term");

					var termInputs = model.Terms
						.Select(t => (Id: Guid.NewGuid(), Name: (t.Name ?? string.Empty).Trim(), Start: t.StartDate.Date, End: t.EndDate.Date))
						.ToList();
					var termError = ValidateTerms(model.StartDate.Date, model.EndDate.Date, termInputs.Select(t => (t.Name, t.Start, t.End)).ToList());
					if (termError != null) return Fail(ResponseCode.BadRequest, termError);

					var overlapName = await FindOverlappingSessionNameAsync(schoolId, model.StartDate.Date, model.EndDate.Date, Guid.Empty);
					if (overlapName != null)
						return Fail(ResponseCode.Conflict, $"These dates overlap the existing session \"{overlapName}\"");

					var sessionId = Guid.NewGuid();
					var values = new Dictionary<string, object>
					{
						{ "SessionId", sessionId }, { "SchoolId", schoolId }, { "SessionName", name },
						{ "SessionStart", model.StartDate.Date }, { "SessionEnd", model.EndDate.Date }, { "UserId", userId }
					};

					// One batch, one transaction: either the session and every term exist, or none do.
					var sql = new StringBuilder();
					sql.AppendLine("SET XACT_ABORT ON; BEGIN TRAN;");
					sql.AppendLine("INSERT INTO AcademicSession (Id, SchoolId, Name, StartDate, EndDate, CreatedBy, IsActive) VALUES (@SessionId, @SchoolId, @SessionName, @SessionStart, @SessionEnd, @UserId, 1);");

					var ordered = termInputs.OrderBy(t => t.Start).ToList();
					for (var i = 0; i < ordered.Count; i++)
					{
						sql.AppendLine($"INSERT INTO AcademicTerm (Id, SessionId, SchoolId, Name, Sequence, StartDate, EndDate, CreatedBy, IsActive) VALUES (@T{i}Id, @SessionId, @SchoolId, @T{i}Name, {i + 1}, @T{i}Start, @T{i}End, @UserId, 1);");
						values[$"T{i}Id"] = ordered[i].Id;
						values[$"T{i}Name"] = ordered[i].Name;
						values[$"T{i}Start"] = ordered[i].Start;
						values[$"T{i}End"] = ordered[i].End;
					}
					sql.AppendLine("COMMIT;");

					await _sessionCommandRepo.UpdateAsync(sql.ToString(), values);

					_logger.Information("Calendar: session {SessionId} \"{Name}\" created with {Count} term(s) by {UserId} (template: {Template})",
						sessionId, name, ordered.Count, userId, model.Template ?? "none");

					var created = await LoadSessionAsync(sessionId, schoolId);
					var terms = await LoadTermsAsync(sessionId);
					return Ok("Academic session created", ToDto(created!, terms, SchoolToday()));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error creating session");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while creating the academic session");
				}
			}
		}

		public async Task<BaseResponse> UpdateSessionAsync(Guid sessionId, UpdateAcademicSessionViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var denied = await CheckCanManageAsync(claims);
					if (denied != null) return denied;
					var schoolId = ParseSchoolId(claims);

					if (model is null)
						return Fail(ResponseCode.BadRequest, "Request body is required");

					var session = await LoadSessionAsync(sessionId, schoolId);
					if (session is null)
						return Fail(ResponseCode.NotFound, "Academic session not found");

					var name = (model.Name ?? string.Empty).Trim();
					var sessionError = ValidateSession(name, model.StartDate.Date, model.EndDate.Date);
					if (sessionError != null) return Fail(ResponseCode.BadRequest, sessionError);

					// Existing terms must still fit inside the new session dates.
					var terms = await LoadTermsAsync(sessionId);
					var termError = ValidateTerms(model.StartDate.Date, model.EndDate.Date, terms.Select(t => (t.Name, t.StartDate.Date, t.EndDate.Date)).ToList());
					if (termError != null)
						return Fail(ResponseCode.BadRequest, "These session dates would leave a term outside the session. " + termError);

					var overlapName = await FindOverlappingSessionNameAsync(schoolId, model.StartDate.Date, model.EndDate.Date, sessionId);
					if (overlapName != null)
						return Fail(ResponseCode.Conflict, $"These dates overlap the existing session \"{overlapName}\"");

					await _sessionCommandRepo.UpdateAsync(
						"UPDATE AcademicSession SET Name = @Name, StartDate = @StartDate, EndDate = @EndDate, ModifiedAt = GETUTCDATE() WHERE Id = @Id AND SchoolId = @SchoolId",
						new Dictionary<string, object>
						{
							{ "Name", name }, { "StartDate", model.StartDate.Date }, { "EndDate", model.EndDate.Date },
							{ "Id", sessionId }, { "SchoolId", schoolId }
						});

					var updated = await LoadSessionAsync(sessionId, schoolId);
					return Ok("Academic session updated", ToDto(updated!, await LoadTermsAsync(sessionId), SchoolToday()));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error updating session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while updating the academic session");
				}
			}
		}

		public async Task<BaseResponse> DeleteSessionAsync(Guid sessionId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var denied = await CheckCanManageAsync(claims);
					if (denied != null) return denied;
					var schoolId = ParseSchoolId(claims);

					var session = await LoadSessionAsync(sessionId, schoolId);
					if (session is null)
						return Fail(ResponseCode.NotFound, "Academic session not found");

					// Soft delete, session and its terms together.
					await _sessionCommandRepo.UpdateAsync(
						@"SET XACT_ABORT ON; BEGIN TRAN;
						  UPDATE AcademicTerm SET IsActive = 0 WHERE SessionId = @Id AND SchoolId = @SchoolId;
						  UPDATE AcademicSession SET IsActive = 0, ModifiedAt = GETUTCDATE() WHERE Id = @Id AND SchoolId = @SchoolId;
						  COMMIT;",
						new Dictionary<string, object> { { "Id", sessionId }, { "SchoolId", schoolId } });

					_logger.Information("Calendar: session {SessionId} deleted by {UserId}", sessionId, ParseUserId(claims));
					return Ok("Academic session deleted", null);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error deleting session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while deleting the academic session");
				}
			}
		}

		#endregion

		#region Term writes

		public async Task<BaseResponse> AddTermAsync(Guid sessionId, TermInputViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var denied = await CheckCanManageAsync(claims);
					if (denied != null) return denied;
					var schoolId = ParseSchoolId(claims);
					var userId = ParseUserId(claims);

					if (model is null)
						return Fail(ResponseCode.BadRequest, "Request body is required");

					var session = await LoadSessionAsync(sessionId, schoolId);
					if (session is null)
						return Fail(ResponseCode.NotFound, "Academic session not found");

					var existing = await LoadTermsAsync(sessionId);
					var proposed = existing.Select(t => (t.Name, t.StartDate.Date, t.EndDate.Date)).ToList();
					proposed.Add(((model.Name ?? string.Empty).Trim(), model.StartDate.Date, model.EndDate.Date));

					var error = ValidateTerms(session.StartDate.Date, session.EndDate.Date, proposed);
					if (error != null) return Fail(ResponseCode.BadRequest, error);

					var termId = Guid.NewGuid();
					await _termCommandRepo.UpdateAsync(
						"INSERT INTO AcademicTerm (Id, SessionId, SchoolId, Name, Sequence, StartDate, EndDate, CreatedBy, IsActive) VALUES (@Id, @SessionId, @SchoolId, @Name, 0, @StartDate, @EndDate, @UserId, 1)",
						new Dictionary<string, object>
						{
							{ "Id", termId }, { "SessionId", sessionId }, { "SchoolId", schoolId },
							{ "Name", (model.Name ?? string.Empty).Trim() }, { "StartDate", model.StartDate.Date },
							{ "EndDate", model.EndDate.Date }, { "UserId", userId }
						});
					await ResequenceAsync(sessionId);

					return Ok("Term added", ToDto(session, await LoadTermsAsync(sessionId), SchoolToday()));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error adding term to session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while adding the term");
				}
			}
		}

		public async Task<BaseResponse> UpdateTermAsync(Guid termId, TermInputViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var denied = await CheckCanManageAsync(claims);
					if (denied != null) return denied;
					var schoolId = ParseSchoolId(claims);

					if (model is null)
						return Fail(ResponseCode.BadRequest, "Request body is required");

					var term = await LoadTermAsync(termId, schoolId);
					if (term is null)
						return Fail(ResponseCode.NotFound, "Term not found");

					var session = await LoadSessionAsync(term.SessionId, schoolId);
					if (session is null)
						return Fail(ResponseCode.NotFound, "Academic session not found");

					var others = (await LoadTermsAsync(session.Id)).Where(t => t.Id != termId).ToList();
					var proposed = others.Select(t => (t.Name, t.StartDate.Date, t.EndDate.Date)).ToList();
					proposed.Add(((model.Name ?? string.Empty).Trim(), model.StartDate.Date, model.EndDate.Date));

					var error = ValidateTerms(session.StartDate.Date, session.EndDate.Date, proposed);
					if (error != null) return Fail(ResponseCode.BadRequest, error);

					await _termCommandRepo.UpdateAsync(
						"UPDATE AcademicTerm SET Name = @Name, StartDate = @StartDate, EndDate = @EndDate WHERE Id = @Id AND SchoolId = @SchoolId",
						new Dictionary<string, object>
						{
							{ "Name", (model.Name ?? string.Empty).Trim() }, { "StartDate", model.StartDate.Date },
							{ "EndDate", model.EndDate.Date }, { "Id", termId }, { "SchoolId", schoolId }
						});
					await ResequenceAsync(session.Id);

					return Ok("Term updated", ToDto(session, await LoadTermsAsync(session.Id), SchoolToday()));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error updating term {TermId}", termId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while updating the term");
				}
			}
		}

		public async Task<BaseResponse> DeleteTermAsync(Guid termId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var denied = await CheckCanManageAsync(claims);
					if (denied != null) return denied;
					var schoolId = ParseSchoolId(claims);

					var term = await LoadTermAsync(termId, schoolId);
					if (term is null)
						return Fail(ResponseCode.NotFound, "Term not found");

					var remaining = (await LoadTermsAsync(term.SessionId)).Count(t => t.Id != termId);
					if (remaining == 0)
						return Fail(ResponseCode.BadRequest, "A session needs at least one term. Delete the session instead.");

					await _termCommandRepo.UpdateAsync(
						"UPDATE AcademicTerm SET IsActive = 0 WHERE Id = @Id AND SchoolId = @SchoolId",
						new Dictionary<string, object> { { "Id", termId }, { "SchoolId", schoolId } });
					await ResequenceAsync(term.SessionId);

					var session = await LoadSessionAsync(term.SessionId, schoolId);
					return Ok("Term deleted", ToDto(session!, await LoadTermsAsync(term.SessionId), SchoolToday()));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Calendar: error deleting term {TermId}", termId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while deleting the term");
				}
			}
		}

		#endregion

		#region Validation

		private static string? ValidateSession(string name, DateTime start, DateTime end)
		{
			if (string.IsNullOrWhiteSpace(name)) return "Session name is required";
			if (name.Length > 100) return "Session name must be 100 characters or fewer";
			if (start == default || end == default) return "Session start and end dates are required";
			if (end < start) return "Session end date cannot be before its start date";
			if ((end - start).TotalDays > MaxSessionDays) return $"A session cannot be longer than {MaxSessionDays} days";
			return null;
		}

		/// <summary>
		/// Rules for a complete set of terms: count, names, each inside the session,
		/// start &lt;= end, unique names (case-insensitive) and no overlaps. Dates are inclusive,
		/// so a term starting on the day the previous one ends counts as an overlap.
		/// </summary>
		private static string? ValidateTerms(DateTime sessionStart, DateTime sessionEnd, List<(string Name, DateTime Start, DateTime End)> terms)
		{
			if (terms.Count > MaxTermsPerSession)
				return $"A session can have at most {MaxTermsPerSession} terms";

			foreach (var t in terms)
			{
				if (string.IsNullOrWhiteSpace(t.Name)) return "Every term needs a name";
				if (t.Name.Length > 100) return $"Term name \"{t.Name}\" must be 100 characters or fewer";
				if (t.Start == default || t.End == default) return $"Term \"{t.Name}\" needs a start and end date";
				if (t.End < t.Start) return $"Term \"{t.Name}\" ends before it starts";
				if (t.Start < sessionStart || t.End > sessionEnd)
					return $"Term \"{t.Name}\" ({Fmt(t.Start)} to {Fmt(t.End)}) must be inside the session ({Fmt(sessionStart)} to {Fmt(sessionEnd)})";
			}

			var duplicate = terms.GroupBy(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
			if (duplicate != null) return $"Two terms are both named \"{duplicate.Key}\"";

			var ordered = terms.OrderBy(t => t.Start).ToList();
			for (var i = 1; i < ordered.Count; i++)
			{
				if (ordered[i].Start <= ordered[i - 1].End)
					return $"Terms \"{ordered[i - 1].Name}\" and \"{ordered[i].Name}\" overlap";
			}

			return null;
		}

		/// <summary>Name of another active session of this school that overlaps the dates, or null if none does.</summary>
		private async Task<string?> FindOverlappingSessionNameAsync(Guid schoolId, DateTime start, DateTime end, Guid excludeId)
			=> (await _sessionQueryRepo.QueryAsync<AcademicSession>(
				@"SELECT TOP 1 * FROM AcademicSession
				  WHERE SchoolId = @SchoolId AND IsActive = 1 AND Id <> @ExcludeId AND StartDate <= @End AND EndDate >= @Start",
				new Dictionary<string, object> { { "SchoolId", schoolId }, { "ExcludeId", excludeId }, { "Start", start }, { "End", end } }))
				.FirstOrDefault()?.Name;

		#endregion

		#region Data access

		private async Task<List<AcademicSession>> LoadSessionsAsync(Guid schoolId)
			=> (await _sessionQueryRepo.QueryAsync<AcademicSession>(
				"SELECT * FROM AcademicSession WHERE SchoolId = @SchoolId AND IsActive = 1 ORDER BY StartDate DESC",
				new Dictionary<string, object> { { "SchoolId", schoolId } })).ToList();

		private async Task<AcademicSession?> LoadSessionAsync(Guid sessionId, Guid schoolId)
			=> (await _sessionQueryRepo.QueryAsync<AcademicSession>(
				"SELECT * FROM AcademicSession WHERE Id = @Id AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "Id", sessionId }, { "SchoolId", schoolId } })).FirstOrDefault();

		private async Task<AcademicTerm?> LoadTermAsync(Guid termId, Guid schoolId)
			=> (await _termQueryRepo.QueryAsync<AcademicTerm>(
				"SELECT * FROM AcademicTerm WHERE Id = @Id AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "Id", termId }, { "SchoolId", schoolId } })).FirstOrDefault();

		private async Task<List<AcademicTerm>> LoadTermsAsync(Guid sessionId)
			=> (await _termQueryRepo.QueryAsync<AcademicTerm>(
				"SELECT * FROM AcademicTerm WHERE SessionId = @SessionId AND IsActive = 1 ORDER BY StartDate",
				new Dictionary<string, object> { { "SessionId", sessionId } })).ToList();

		private async Task<List<AcademicTerm>> LoadTermsForSchoolAsync(Guid schoolId)
			=> (await _termQueryRepo.QueryAsync<AcademicTerm>(
				"SELECT * FROM AcademicTerm WHERE SchoolId = @SchoolId AND IsActive = 1 ORDER BY StartDate",
				new Dictionary<string, object> { { "SchoolId", schoolId } })).ToList();

		/// <summary>Sequence is always start-date order (1..n), recomputed after any term change.</summary>
		private Task ResequenceAsync(Guid sessionId)
			=> _termCommandRepo.UpdateAsync(
				@";WITH ranked AS (
				      SELECT Id, ROW_NUMBER() OVER (ORDER BY StartDate) AS rn
				      FROM AcademicTerm WHERE SessionId = @SessionId AND IsActive = 1)
				  UPDATE t SET t.Sequence = ranked.rn
				  FROM AcademicTerm t JOIN ranked ON ranked.Id = t.Id",
				new Dictionary<string, object> { { "SessionId", sessionId } });

		#endregion

		#region Permissions

		/// <summary>
		/// Null when the caller may manage the calendar; otherwise the failure to return.
		/// SuperAdministrator always can; an Administrator needs the ManageCalendar flag.
		/// </summary>
		private async Task<BaseResponse?> CheckCanManageAsync(AuthenticatedUserClaims claims)
		{
			var schoolId = ParseSchoolId(claims);
			var userId = ParseUserId(claims);
			if (schoolId == Guid.Empty || userId == Guid.Empty)
				return Fail(ResponseCode.Unauthorized, "Invalid user context");

			if (string.Equals(claims?.Role, nameof(UserRole.SuperAdministrator), StringComparison.OrdinalIgnoreCase))
				return null;

			if (!string.Equals(claims?.Role, nameof(UserRole.Administrator), StringComparison.OrdinalIgnoreCase))
				return Fail(ResponseCode.Forbidden, "Only administrators can set up the school calendar");

			var mask = (await _sessionQueryRepo.QueryAsync<int>(
				"SELECT Permissions FROM AdminPermissions WHERE UserId = @UserId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "UserId", userId }, { "SchoolId", schoolId } })).FirstOrDefault();

			if (!((AdminPermission)mask).HasPermission(AdminPermission.ManageCalendar))
				return Fail(ResponseCode.Forbidden, "You don't have permission to manage the school calendar. Contact your SuperAdministrator.");

			return null;
		}

		#endregion

		#region Helpers

		/// <summary>
		/// The school's local "today". Schools are in West Africa (WAT, UTC+1, no DST); the
		/// zone is looked up by both its Windows and IANA ids since the API runs on Windows
		/// in dev and Linux in the container, with a fixed +1 fallback.
		/// </summary>
		private static DateTime SchoolToday()
		{
			foreach (var id in new[] { "Africa/Lagos", "W. Central Africa Standard Time" })
			{
				try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)).Date; }
				catch (TimeZoneNotFoundException) { }
				catch (InvalidTimeZoneException) { }
			}
			return DateTime.UtcNow.AddHours(1).Date;
		}

		private static string StatusOf(DateTime start, DateTime end, DateTime today)
			=> today < start ? "Upcoming" : today > end ? "Past" : "Current";

		/// <summary>The session containing today, else the nearest upcoming one, else the latest past one.</summary>
		private static AcademicSession? PickCurrent(List<AcademicSession> sessions, DateTime today)
			=> sessions.FirstOrDefault(s => s.StartDate.Date <= today && today <= s.EndDate.Date)
			   ?? sessions.Where(s => s.StartDate.Date > today).OrderBy(s => s.StartDate).FirstOrDefault()
			   ?? sessions.OrderByDescending(s => s.EndDate).FirstOrDefault();

		private static AcademicSessionDto ToDto(AcademicSession s, IEnumerable<AcademicTerm> terms, DateTime today)
			=> new()
			{
				Id = s.Id,
				Name = s.Name,
				StartDate = Fmt(s.StartDate),
				EndDate = Fmt(s.EndDate),
				Status = StatusOf(s.StartDate.Date, s.EndDate.Date, today),
				Terms = terms.OrderBy(t => t.StartDate).Select(t => new AcademicTermDto
				{
					Id = t.Id,
					Name = t.Name,
					Sequence = t.Sequence,
					StartDate = Fmt(t.StartDate),
					EndDate = Fmt(t.EndDate),
					Status = StatusOf(t.StartDate.Date, t.EndDate.Date, today)
				}).ToList()
			};

		private static string Fmt(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

		private static Guid ParseSchoolId(AuthenticatedUserClaims? claims)
			=> Guid.TryParse(claims?.SchoolId, out var id) ? id : Guid.Empty;

		private static Guid ParseUserId(AuthenticatedUserClaims? claims)
			=> Guid.TryParse(claims?.UserId, out var id) ? id : Guid.Empty;

		private static BaseResponse Ok(string message, object? data)
			=> new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = message, Status = "successful", Data = data! };

		private static BaseResponse Fail(string code, string message)
			=> new BaseResponse { ResponseCode = code, ResponseMessage = message, Status = "failed" };

		#endregion
	}
}
