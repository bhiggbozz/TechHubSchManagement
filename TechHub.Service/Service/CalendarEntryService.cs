using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
	/// School calendar, level 3: dated entries (assessment week, quiz period, exam, holiday …).
	/// Authority is a three-level ladder — 3 admin (SuperAdministrator, or Administrator with
	/// ManageCalendar), 2 head teacher, 1 class/subject teacher. Each level creates entries in
	/// its own scope and edits its own; a higher level can edit the levels below it, and every
	/// such edit is logged with the before/after values so viewers see what changed and by
	/// whom. Once someone higher has changed an entry, the creator can no longer edit it.
	/// </summary>
	public class CalendarEntryService : ICalendarEntryService
	{
		private const int LevelTeacher = 1;
		private const int LevelHead = 2;
		private const int LevelAdmin = 3;

		private readonly IQueryRepository<CalendarEntry> _query;
		private readonly ICommandRespository<CalendarEntry> _command;
		private readonly ILogger _logger;

		public CalendarEntryService(
			IQueryRepository<CalendarEntry> query,
			ICommandRespository<CalendarEntry> command,
			ILogger logger)
		{
			_query = query;
			_command = command;
			_logger = logger;
		}

		#region Types

		public BaseResponse GetEntryTypes()
			=> Ok("Calendar entry types retrieved", new List<CalendarEntryTypeDto>
			{
				new() { Key = CalendarEntryTypes.Assessment, Label = "Assessment period" },
				new() { Key = CalendarEntryTypes.Quiz, Label = "Quiz period" },
				new() { Key = CalendarEntryTypes.MidTerm, Label = "Mid-term" },
				new() { Key = CalendarEntryTypes.Exam, Label = "Exam" },
				new() { Key = CalendarEntryTypes.Holiday, Label = "Holiday" },
				new() { Key = CalendarEntryTypes.Other, Label = "Other" }
			});

		#endregion

		#region Reads

		public async Task<BaseResponse> ListAsync(CalendarEntryQueryViewModel query, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var actor = await ResolveActorAsync(claims);
					if (actor is null) return Fail(ResponseCode.Unauthorized, "Invalid user context");
					query ??= new CalendarEntryQueryViewModel();

					var visibility = await ResolveVisibilityAsync(actor, query.StudentId);
					if (visibility.Error != null) return visibility.Error;

					var rows = await LoadRowsAsync(actor.SchoolId, null, query.From?.Date, query.To?.Date);
					var visible = rows.Where(r => IsVisible(r, actor, visibility)).ToList();

					if (query.ClassroomId.HasValue)
						visible = visible.Where(r => r.ScopeType == CalendarEntryScopes.School || r.ClassroomId == query.ClassroomId).ToList();
					if (query.SubjectId.HasValue)
						visible = visible.Where(r => r.ScopeType == CalendarEntryScopes.School || r.SubjectId == query.SubjectId).ToList();

					var changes = await LoadChangesAsync(actor.SchoolId, null, query.From?.Date, query.To?.Date, onlyOthers: true);
					var today = SchoolToday();
					var data = visible.Select(r => ToDto(r, actor, changes.Where(c => c.EntryId == r.Id).ToList(), today, includeHistory: false)).ToList();

					return Ok(data.Count == 0 ? "No calendar entries found" : $"{data.Count} calendar entr{(data.Count == 1 ? "y" : "ies")} found", data);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "CalendarEntries: error listing entries");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving calendar entries");
				}
			}
		}

		public async Task<BaseResponse> GetAsync(Guid entryId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var actor = await ResolveActorAsync(claims);
					if (actor is null) return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var row = (await LoadRowsAsync(actor.SchoolId, entryId, null, null)).FirstOrDefault();
					if (row is null) return Fail(ResponseCode.NotFound, "Calendar entry not found");

					var visibility = await ResolveVisibilityAsync(actor, null);
					if (visibility.Error != null) return visibility.Error;
					if (!IsVisible(row, actor, visibility)) return Fail(ResponseCode.NotFound, "Calendar entry not found");

					var changes = await LoadChangesAsync(actor.SchoolId, entryId, null, null, onlyOthers: false);
					return Ok("Calendar entry retrieved", ToDto(row, actor, changes, SchoolToday(), includeHistory: true));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "CalendarEntries: error getting entry {EntryId}", entryId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the calendar entry");
				}
			}
		}

		#endregion

		#region Writes

		public async Task<BaseResponse> CreateAsync(CreateCalendarEntryViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var actor = await ResolveActorAsync(claims);
					if (actor is null) return Fail(ResponseCode.Unauthorized, "Invalid user context");
					if (actor.Level == 0)
						return Fail(ResponseCode.Forbidden, actor.Role == nameof(UserRole.Administrator)
							? "You don't have permission to manage the school calendar. Contact your SuperAdministrator."
							: "You don't have permission to add calendar entries");
					if (model is null) return Fail(ResponseCode.BadRequest, "Request body is required");

					var title = (model.Title ?? string.Empty).Trim();
					var description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
					var type = NormaliseType(model.EntryType);
					var scope = NormaliseScope(model.ScopeType);
					var start = model.StartDate.Date;
					var end = model.EndDate.Date;

					var fieldError = ValidateFields(title, description, type, start, end);
					if (fieldError != null) return Fail(ResponseCode.BadRequest, fieldError);
					if (scope is null)
						return Fail(ResponseCode.BadRequest, "Scope must be School, Classroom or Subject");

					var scopeError = await ValidateScopeAsync(actor, scope, model.ClassroomId, model.SubjectId);
					if (scopeError != null) return scopeError;

					var placement = await FindPlacementAsync(actor, start, end, type);
					if (placement.Error != null) return placement.Error;

					var id = Guid.NewGuid();
					await _command.UpdateAsync(
						@"INSERT INTO CalendarEntry (Id, SchoolId, SessionId, TermId, Title, EntryType, Description, StartDate, EndDate,
						      ScopeType, ClassroomId, SubjectId, CreatedBy, CreatedByRole, SupersededLevel, IsActive)
						  VALUES (@Id, @SchoolId, @SessionId, @TermId, @Title, @EntryType, @Description, @Start, @End,
						      @Scope, @ClassroomId, @SubjectId, @UserId, @Role, 0, 1)",
						new Dictionary<string, object>
						{
							{ "Id", id }, { "SchoolId", actor.SchoolId }, { "SessionId", placement.SessionId },
							{ "TermId", SqlNull(placement.TermId) }, { "Title", title }, { "EntryType", type! },
							{ "Description", SqlNull(description) }, { "Start", start }, { "End", end },
							{ "Scope", scope }, { "ClassroomId", SqlNull(model.ClassroomId) },
							{ "SubjectId", SqlNull(model.SubjectId) }, { "UserId", actor.UserId }, { "Role", actor.Role }
						});

					_logger.Information("CalendarEntries: entry {EntryId} \"{Title}\" ({Type}, {Scope}) created by {UserId} ({Role})",
						id, title, type, scope, actor.UserId, actor.Role);

					return await ReturnEntryAsync("Calendar entry created", id, actor);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "CalendarEntries: error creating entry");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while creating the calendar entry");
				}
			}
		}

		public async Task<BaseResponse> UpdateAsync(Guid entryId, UpdateCalendarEntryViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var actor = await ResolveActorAsync(claims);
					if (actor is null) return Fail(ResponseCode.Unauthorized, "Invalid user context");
					if (model is null) return Fail(ResponseCode.BadRequest, "Request body is required");

					var entry = (await LoadRowsAsync(actor.SchoolId, entryId, null, null)).FirstOrDefault();
					if (entry is null) return Fail(ResponseCode.NotFound, "Calendar entry not found");

					var denied = CheckCanModify(actor, entry);
					if (denied != null) return denied;

					var title = (model.Title ?? string.Empty).Trim();
					var description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
					var type = NormaliseType(model.EntryType);
					var start = model.StartDate.Date;
					var end = model.EndDate.Date;

					var fieldError = ValidateFields(title, description, type, start, end);
					if (fieldError != null) return Fail(ResponseCode.BadRequest, fieldError);

					// The editor's own authority decides where the entry may sit (a teacher stays inside a term).
					var placement = await FindPlacementAsync(actor, start, end, type);
					if (placement.Error != null) return placement.Error;

					var othersEntry = entry.CreatedBy != actor.UserId;
					var unchanged = entry.Title == title && entry.EntryType == type && entry.Description == description
						&& entry.StartDate.Date == start && entry.EndDate.Date == end;
					if (unchanged) return await ReturnEntryAsync("No changes to save", entryId, actor);

					// Entry update and its history row go in together or not at all.
					await _command.UpdateAsync(
						@"SET XACT_ABORT ON; BEGIN TRAN;
						  UPDATE CalendarEntry SET Title = @Title, EntryType = @EntryType, Description = @Description,
						      StartDate = @Start, EndDate = @End, SessionId = @SessionId, TermId = @TermId, ModifiedAt = GETUTCDATE(),
						      SupersededLevel = CASE WHEN @OthersEntry = 1 AND SupersededLevel < @Level THEN @Level ELSE SupersededLevel END
						  WHERE Id = @Id AND SchoolId = @SchoolId AND IsActive = 1;
						  INSERT INTO CalendarEntryChange (Id, EntryId, SchoolId, ChangedBy, ChangedByRole, Action,
						      OldTitle, NewTitle, OldEntryType, NewEntryType, OldStartDate, OldEndDate, NewStartDate, NewEndDate, OnOthersEntry)
						  VALUES (NEWID(), @Id, @SchoolId, @UserId, @Role, 'Updated',
						      @OldTitle, @Title, @OldType, @EntryType, @OldStart, @OldEnd, @Start, @End, @OthersEntry);
						  COMMIT;",
						new Dictionary<string, object>
						{
							{ "Id", entryId }, { "SchoolId", actor.SchoolId }, { "UserId", actor.UserId }, { "Role", actor.Role },
							{ "Level", actor.Level }, { "OthersEntry", othersEntry ? 1 : 0 },
							{ "Title", title }, { "EntryType", type! }, { "Description", SqlNull(description) },
							{ "Start", start }, { "End", end }, { "SessionId", placement.SessionId }, { "TermId", SqlNull(placement.TermId) },
							{ "OldTitle", entry.Title }, { "OldType", entry.EntryType }, { "OldStart", entry.StartDate.Date }, { "OldEnd", entry.EndDate.Date }
						});

					_logger.Information("CalendarEntries: entry {EntryId} updated by {UserId} ({Role}); othersEntry={OthersEntry}",
						entryId, actor.UserId, actor.Role, othersEntry);

					return await ReturnEntryAsync("Calendar entry updated", entryId, actor);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "CalendarEntries: error updating entry {EntryId}", entryId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while updating the calendar entry");
				}
			}
		}

		public async Task<BaseResponse> DeleteAsync(Guid entryId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var actor = await ResolveActorAsync(claims);
					if (actor is null) return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var entry = (await LoadRowsAsync(actor.SchoolId, entryId, null, null)).FirstOrDefault();
					if (entry is null) return Fail(ResponseCode.NotFound, "Calendar entry not found");

					var denied = CheckCanModify(actor, entry);
					if (denied != null) return denied;

					var othersEntry = entry.CreatedBy != actor.UserId;
					await _command.UpdateAsync(
						@"SET XACT_ABORT ON; BEGIN TRAN;
						  UPDATE CalendarEntry SET IsActive = 0, ModifiedAt = GETUTCDATE() WHERE Id = @Id AND SchoolId = @SchoolId AND IsActive = 1;
						  INSERT INTO CalendarEntryChange (Id, EntryId, SchoolId, ChangedBy, ChangedByRole, Action,
						      OldTitle, OldEntryType, OldStartDate, OldEndDate, OnOthersEntry)
						  VALUES (NEWID(), @Id, @SchoolId, @UserId, @Role, 'Deleted', @OldTitle, @OldType, @OldStart, @OldEnd, @OthersEntry);
						  COMMIT;",
						new Dictionary<string, object>
						{
							{ "Id", entryId }, { "SchoolId", actor.SchoolId }, { "UserId", actor.UserId }, { "Role", actor.Role },
							{ "OthersEntry", othersEntry ? 1 : 0 }, { "OldTitle", entry.Title }, { "OldType", entry.EntryType },
							{ "OldStart", entry.StartDate.Date }, { "OldEnd", entry.EndDate.Date }
						});

					_logger.Information("CalendarEntries: entry {EntryId} deleted by {UserId} ({Role}); othersEntry={OthersEntry}",
						entryId, actor.UserId, actor.Role, othersEntry);

					return Ok("Calendar entry deleted", null);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "CalendarEntries: error deleting entry {EntryId}", entryId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while deleting the calendar entry");
				}
			}
		}

		#endregion

		#region Rules

		private sealed class Actor
		{
			public Guid UserId { get; init; }
			public Guid SchoolId { get; init; }
			public string Role { get; init; } = string.Empty;
			/// <summary>0 = read-only (student, parent, an Administrator without ManageCalendar), 1 teacher, 2 head teacher, 3 admin.</summary>
			public int Level { get; init; }
			/// <summary>Classrooms the caller is assigned to (ClassTeacher / HeadTeacher).</summary>
			public HashSet<Guid> Classrooms { get; init; } = new();
			/// <summary>For a head teacher: subjects taught in their classrooms (covers subject entries that have no classroom).</summary>
			public HashSet<Guid> SubjectsInClassrooms { get; init; } = new();
			/// <summary>For a SubjectTeacher: (SubjectId, ClassroomId?) assignments; a null classroom means every class.</summary>
			public List<(Guid SubjectId, Guid? ClassroomId)> TeachingAssignments { get; init; } = new();
		}

		private async Task<Actor?> ResolveActorAsync(AuthenticatedUserClaims? claims)
		{
			var schoolId = ParseId(claims?.SchoolId);
			var userId = ParseId(claims?.UserId);
			if (schoolId == Guid.Empty || userId == Guid.Empty) return null;

			var role = claims?.Role ?? string.Empty;
			var level = 0;

			if (Is(role, UserRole.SuperAdministrator))
				level = LevelAdmin;
			else if (Is(role, UserRole.Administrator))
			{
				var mask = (await _query.QueryAsync<int>(
					"SELECT Permissions FROM AdminPermissions WHERE UserId = @UserId AND SchoolId = @SchoolId AND IsActive = 1",
					new Dictionary<string, object> { { "UserId", userId }, { "SchoolId", schoolId } })).FirstOrDefault();
				if (((AdminPermission)mask).HasPermission(AdminPermission.ManageCalendar))
					level = LevelAdmin;
			}
			else if (Is(role, UserRole.HeadTeacher))
				level = LevelHead;
			else if (Is(role, UserRole.ClassTeacher) || Is(role, UserRole.SubjectTeacher))
				level = LevelTeacher;

			var actor = new Actor { UserId = userId, SchoolId = schoolId, Role = NormaliseRole(role), Level = level };

			if (Is(role, UserRole.HeadTeacher) || Is(role, UserRole.ClassTeacher))
			{
				foreach (var id in await QueryIdsAsync(
					"SELECT ClassroomId FROM ClassroomTeacher WHERE TeacherId = @UserId AND SchoolId = @SchoolId AND ISNULL(IsActive, 1) = 1", userId, schoolId))
					actor.Classrooms.Add(id);

				if (Is(role, UserRole.HeadTeacher) && actor.Classrooms.Count > 0)
				{
					foreach (var id in await QueryIdsAsync(
						@"SELECT DISTINCT cs.SubjectId FROM ClassroomSubject cs
						  JOIN ClassroomTeacher ct ON ct.ClassroomId = cs.ClassroomId AND ISNULL(ct.IsActive, 1) = 1
						  WHERE ct.TeacherId = @UserId AND cs.SchoolId = @SchoolId AND cs.IsActive = 1", userId, schoolId))
						actor.SubjectsInClassrooms.Add(id);
				}
			}

			if (Is(role, UserRole.SubjectTeacher))
			{
				var rows = await _query.QueryAsync<TeachingRow>(
					"SELECT SubjectId, ClassroomId FROM TeacherSubject WHERE TeacherId = @UserId AND SchoolId = @SchoolId AND IsActive = 1",
					new Dictionary<string, object> { { "UserId", userId }, { "SchoolId", schoolId } });
				actor.TeachingAssignments.AddRange(rows.Select(r => (r.SubjectId, r.ClassroomId)));
			}

			return actor;
		}

		private sealed class TeachingRow
		{
			public Guid SubjectId { get; set; }
			public Guid? ClassroomId { get; set; }
		}

		private async Task<List<Guid>> QueryIdsAsync(string sql, Guid userId, Guid schoolId)
			=> (await _query.QueryAsync<Guid>(sql,
				new Dictionary<string, object> { { "UserId", userId }, { "SchoolId", schoolId } })).ToList();

		/// <summary>Null when the actor may create an entry of this scope in these classrooms/subjects; otherwise the failure.</summary>
		private async Task<BaseResponse?> ValidateScopeAsync(Actor actor, string scope, Guid? classroomId, Guid? subjectId)
		{
			if (scope == CalendarEntryScopes.School)
			{
				if (classroomId.HasValue || subjectId.HasValue)
					return Fail(ResponseCode.BadRequest, "A school-wide entry can't have a classroom or subject");
				return actor.Level == LevelAdmin ? null : Fail(ResponseCode.Forbidden, "Only an administrator can add school-wide entries");
			}

			if (scope == CalendarEntryScopes.Classroom)
			{
				if (!classroomId.HasValue || subjectId.HasValue)
					return Fail(ResponseCode.BadRequest, "A classroom entry needs a classroom and no subject");
			}
			else
			{
				if (!subjectId.HasValue)
					return Fail(ResponseCode.BadRequest, "A subject entry needs a subject");
			}

			if (classroomId.HasValue && !await ExistsAsync("Classroom", classroomId.Value, actor.SchoolId))
				return Fail(ResponseCode.NotFound, "Classroom not found");
			if (subjectId.HasValue && !await ExistsAsync("Subjects", subjectId.Value, actor.SchoolId))
				return Fail(ResponseCode.NotFound, "Subject not found");

			switch (actor.Level)
			{
				case LevelAdmin:
					return null;

				case LevelHead:
					// Classes they are assigned to; a subject entry must say which of their classes it is for.
					if (!classroomId.HasValue)
						return Fail(ResponseCode.BadRequest, "Choose which of your classes this entry is for");
					return actor.Classrooms.Contains(classroomId.Value)
						? null : Fail(ResponseCode.Forbidden, "You can only add entries for classes you are assigned to");

				default:
					if (Is(actor.Role, UserRole.ClassTeacher))
					{
						if (scope != CalendarEntryScopes.Classroom)
							return Fail(ResponseCode.Forbidden, "Class teachers can add entries for their class");
						return actor.Classrooms.Contains(classroomId!.Value)
							? null : Fail(ResponseCode.Forbidden, "You can only add entries for classes you are assigned to");
					}

					// SubjectTeacher
					if (scope != CalendarEntryScopes.Subject)
						return Fail(ResponseCode.Forbidden, "Subject teachers can add entries for their subjects");
					var teaches = actor.TeachingAssignments.Any(a =>
						a.SubjectId == subjectId!.Value && (!classroomId.HasValue || a.ClassroomId == null || a.ClassroomId == classroomId));
					return teaches ? null : Fail(ResponseCode.Forbidden, "You can only add entries for subjects you teach");
			}
		}

		/// <summary>
		/// Edit/delete rights. Admin: any entry. Head teacher: their own, plus teachers' entries in
		/// classes they are assigned to. Teacher: only their own. Nobody below the level that last
		/// changed an entry can edit it again.
		/// </summary>
		private static BaseResponse? CheckCanModify(Actor actor, CalendarEntryRow entry)
		{
			if (actor.Level == LevelAdmin) return null;
			if (actor.Level == 0)
				return Fail(ResponseCode.Forbidden, "You don't have permission to change calendar entries");

			if (entry.CreatedBy == actor.UserId)
				return actor.Level >= entry.SupersededLevel
					? null
					: Fail(ResponseCode.Forbidden, $"This entry was changed by {LevelLabel(entry.SupersededLevel)} and can no longer be edited by you");

			var creatorLevel = LevelOfRole(entry.CreatedByRole);
			if (actor.Level == LevelHead && creatorLevel == LevelTeacher && entry.SupersededLevel <= LevelHead && HeadCovers(actor, entry))
				return null;

			return Fail(ResponseCode.Forbidden, creatorLevel >= actor.Level
				? "You can't change an entry made by someone at your level or above"
				: "You can only change entries for classes you are assigned to");
		}

		private static bool HeadCovers(Actor head, CalendarEntryRow entry)
			=> entry.ClassroomId.HasValue
				? head.Classrooms.Contains(entry.ClassroomId.Value)
				: entry.SubjectId.HasValue && head.SubjectsInClassrooms.Contains(entry.SubjectId.Value);

		private static bool CanEdit(Actor actor, CalendarEntryRow entry) => CheckCanModify(actor, entry) == null;

		#endregion

		#region Visibility

		private sealed class Visibility
		{
			public bool SeeAll { get; init; }
			public HashSet<Guid> Classrooms { get; init; } = new();
			public HashSet<Guid> Subjects { get; init; } = new();
			public BaseResponse? Error { get; init; }
		}

		private async Task<Visibility> ResolveVisibilityAsync(Actor actor, Guid? studentId)
		{
			var role = actor.Role;
			if (Is(role, UserRole.SuperAdministrator) || Is(role, UserRole.Administrator) || Is(role, UserRole.HeadTeacher))
				return new Visibility { SeeAll = true };

			if (Is(role, UserRole.ClassTeacher))
				return new Visibility { Classrooms = actor.Classrooms };

			if (Is(role, UserRole.SubjectTeacher))
				return new Visibility { Subjects = actor.TeachingAssignments.Select(a => a.SubjectId).ToHashSet() };

			// Student, or Parent (their own children only)
			List<Guid> students;
			if (Is(role, UserRole.Parent))
			{
				var children = await QueryIdsAsync(
					"SELECT StudentId FROM StudentParent WHERE ParentId = @UserId AND SchoolId = @SchoolId AND IsActive = 1", actor.UserId, actor.SchoolId);
				if (studentId.HasValue)
				{
					if (!children.Contains(studentId.Value))
						return new Visibility { Error = Fail(ResponseCode.Forbidden, "You can only view the calendar of your own children") };
					children = new List<Guid> { studentId.Value };
				}
				students = children;
			}
			else
				students = new List<Guid> { actor.UserId };

			var visibility = new Visibility();
			foreach (var student in students)
			{
				var args = new Dictionary<string, object> { { "StudentId", student }, { "SchoolId", actor.SchoolId } };
				foreach (var c in await _query.QueryAsync<Guid>(
					"SELECT ClassroomId FROM StudentClassroom WHERE StudentId = @StudentId AND SchoolId = @SchoolId AND IsActive = 1", args))
					visibility.Classrooms.Add(c);
				foreach (var s in await _query.QueryAsync<Guid>(
					@"SELECT cs.SubjectId FROM ClassroomSubject cs JOIN StudentClassroom sc ON sc.ClassroomId = cs.ClassroomId AND sc.IsActive = 1
					  WHERE sc.StudentId = @StudentId AND cs.SchoolId = @SchoolId AND cs.IsActive = 1
					  UNION SELECT SubjectId FROM StudentMinorSubject WHERE StudentId = @StudentId AND SchoolId = @SchoolId AND IsActive = 1", args))
					visibility.Subjects.Add(s);
			}
			return visibility;
		}

		private static bool IsVisible(CalendarEntryRow row, Actor actor, Visibility v)
		{
			if (v.SeeAll || row.CreatedBy == actor.UserId || row.ScopeType == CalendarEntryScopes.School) return true;
			if (row.ScopeType == CalendarEntryScopes.Classroom)
				return row.ClassroomId.HasValue && v.Classrooms.Contains(row.ClassroomId.Value);

			// Subject entry: the viewer must have the subject, and if it is tied to a class, be in that class
			// (subject teachers have no class list, so the subject alone decides for them).
			if (!row.SubjectId.HasValue || !v.Subjects.Contains(row.SubjectId.Value)) return false;
			if (Is(actor.Role, UserRole.SubjectTeacher)) return true;
			return !row.ClassroomId.HasValue || v.Classrooms.Contains(row.ClassroomId.Value);
		}

		#endregion

		#region Validation & placement

		private static string? NormaliseType(string? type)
			=> CalendarEntryTypes.All.FirstOrDefault(t => string.Equals(t, type?.Trim(), StringComparison.OrdinalIgnoreCase));

		private static string? NormaliseScope(string? scope)
			=> new[] { CalendarEntryScopes.School, CalendarEntryScopes.Classroom, CalendarEntryScopes.Subject }
				.FirstOrDefault(s => string.Equals(s, scope?.Trim(), StringComparison.OrdinalIgnoreCase));

		private static string? ValidateFields(string title, string? description, string? type, DateTime start, DateTime end)
		{
			if (title.Length == 0) return "Title is required";
			if (title.Length > 100) return "Title can't be longer than 100 characters";
			if (description is { Length: > 500 }) return "Description can't be longer than 500 characters";
			if (type is null) return $"Type must be one of: {string.Join(", ", CalendarEntryTypes.All)}";
			if (end < start) return "The end date can't be before the start date";
			if ((end - start).TotalDays > 366) return "An entry can't be longer than a year";
			return null;
		}

		private sealed class Placement
		{
			public Guid SessionId { get; init; }
			public Guid? TermId { get; init; }
			public BaseResponse? Error { get; init; }
		}

		/// <summary>
		/// Entries must sit inside one academic session. Admin entries may fall in a break between terms
		/// (a holiday does); everyone else's must sit inside a single term, and only an admin adds holidays.
		/// </summary>
		private async Task<Placement> FindPlacementAsync(Actor actor, DateTime start, DateTime end, string? type)
		{
			var args = new Dictionary<string, object> { { "SchoolId", actor.SchoolId }, { "Start", start }, { "End", end } };
			var session = (await _query.QueryAsync<IdRow>(
				"SELECT TOP 1 Id FROM AcademicSession WHERE SchoolId = @SchoolId AND IsActive = 1 AND StartDate <= @Start AND EndDate >= @End", args)).FirstOrDefault();
			if (session is null)
				return new Placement { Error = Fail(ResponseCode.BadRequest, "These dates must fall inside an academic session. Ask your administrator to set up the school calendar first.") };

			var term = (await _query.QueryAsync<IdRow>(
				"SELECT TOP 1 Id FROM AcademicTerm WHERE SessionId = @SessionId AND IsActive = 1 AND StartDate <= @Start AND EndDate >= @End",
				new Dictionary<string, object> { { "SessionId", session.Id }, { "Start", start }, { "End", end } })).FirstOrDefault();

			if (actor.Level != LevelAdmin)
			{
				if (type == CalendarEntryTypes.Holiday)
					return new Placement { Error = Fail(ResponseCode.Forbidden, "Only an administrator can add holidays") };
				if (term is null)
					return new Placement { Error = Fail(ResponseCode.BadRequest, "These dates must fall inside a single term") };
			}

			return new Placement { SessionId = session.Id, TermId = term?.Id };
		}

		private sealed class IdRow
		{
			public Guid Id { get; set; }
		}

		private async Task<bool> ExistsAsync(string table, Guid id, Guid schoolId)
			=> (await _query.QueryAsync<int>(
				$"SELECT COUNT(1) FROM {table} WHERE Id = @Id AND SchoolId = @SchoolId AND ISNULL(IsActive, 1) = 1",
				new Dictionary<string, object> { { "Id", id }, { "SchoolId", schoolId } })).FirstOrDefault() > 0;

		#endregion

		#region Data access & mapping

		private sealed class CalendarEntryRow : CalendarEntry
		{
			public string? ClassroomName { get; set; }
			public string? SubjectName { get; set; }
			public string? CreatedByName { get; set; }
		}

		private sealed class ChangeRow : CalendarEntryChange
		{
			public string? ChangedByName { get; set; }
		}

		private async Task<List<CalendarEntryRow>> LoadRowsAsync(Guid schoolId, Guid? entryId, DateTime? from, DateTime? to)
		{
			var args = new Dictionary<string, object> { { "SchoolId", schoolId } };
			var where = "e.SchoolId = @SchoolId AND e.IsActive = 1";
			if (entryId.HasValue) { where += " AND e.Id = @EntryId"; args["EntryId"] = entryId.Value; }
			if (to.HasValue) { where += " AND e.StartDate <= @To"; args["To"] = to.Value; }
			if (from.HasValue) { where += " AND e.EndDate >= @From"; args["From"] = from.Value; }

			return (await _query.QueryAsync<CalendarEntryRow>(
				$@"SELECT e.*, c.Name AS ClassroomName, s.Subject AS SubjectName,
				          LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))) AS CreatedByName
				   FROM CalendarEntry e
				   LEFT JOIN Classroom c ON c.Id = e.ClassroomId
				   LEFT JOIN Subjects s ON s.Id = e.SubjectId
				   LEFT JOIN Users u ON u.Id = e.CreatedBy
				   WHERE {where} ORDER BY e.StartDate, e.EndDate, e.Title", args)).ToList();
		}

		private async Task<List<ChangeRow>> LoadChangesAsync(Guid schoolId, Guid? entryId, DateTime? from, DateTime? to, bool onlyOthers)
		{
			var args = new Dictionary<string, object> { { "SchoolId", schoolId } };
			var where = "ch.SchoolId = @SchoolId AND e.IsActive = 1 AND ch.Action = 'Updated'";
			if (onlyOthers) where += " AND ch.OnOthersEntry = 1";
			if (entryId.HasValue) { where += " AND ch.EntryId = @EntryId"; args["EntryId"] = entryId.Value; }
			if (to.HasValue) { where += " AND e.StartDate <= @To"; args["To"] = to.Value; }
			if (from.HasValue) { where += " AND e.EndDate >= @From"; args["From"] = from.Value; }

			return (await _query.QueryAsync<ChangeRow>(
				$@"SELECT ch.*, LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))) AS ChangedByName
				   FROM CalendarEntryChange ch
				   JOIN CalendarEntry e ON e.Id = ch.EntryId
				   LEFT JOIN Users u ON u.Id = ch.ChangedBy
				   WHERE {where} ORDER BY ch.ChangedAt DESC", args)).ToList();
		}

		private async Task<BaseResponse> ReturnEntryAsync(string message, Guid entryId, Actor actor)
		{
			var row = (await LoadRowsAsync(actor.SchoolId, entryId, null, null)).First();
			var changes = await LoadChangesAsync(actor.SchoolId, entryId, null, null, onlyOthers: false);
			return Ok(message, ToDto(row, actor, changes, SchoolToday(), includeHistory: true));
		}

		private static CalendarEntryDto ToDto(CalendarEntryRow r, Actor actor, List<ChangeRow> changes, DateTime today, bool includeHistory)
		{
			var ordered = changes.OrderByDescending(c => c.ChangedAt).ToList();
			var lastOther = ordered.FirstOrDefault(c => c.OnOthersEntry);

			return new CalendarEntryDto
			{
				Id = r.Id,
				SessionId = r.SessionId,
				TermId = r.TermId,
				Title = r.Title,
				EntryType = r.EntryType,
				Description = r.Description,
				StartDate = Fmt(r.StartDate),
				EndDate = Fmt(r.EndDate),
				Status = today < r.StartDate.Date ? "Upcoming" : today > r.EndDate.Date ? "Past" : "Current",
				ScopeType = r.ScopeType,
				ClassroomId = r.ClassroomId,
				ClassroomName = r.ClassroomName,
				SubjectId = r.SubjectId,
				SubjectName = r.SubjectName,
				CreatedById = r.CreatedBy,
				CreatedByName = r.CreatedByName ?? string.Empty,
				CreatedByRole = r.CreatedByRole,
				IsAdminEntry = LevelOfRole(r.CreatedByRole) == LevelAdmin,
				CanEdit = CanEdit(actor, r),
				LastChange = lastOther is null ? null : ToChangeDto(lastOther),
				Changes = includeHistory ? ordered.Select(ToChangeDto).ToList() : null
			};
		}

		private static CalendarEntryChangeDto ToChangeDto(ChangeRow c)
		{
			var name = string.IsNullOrWhiteSpace(c.ChangedByName) ? "a user" : c.ChangedByName!;
			var parts = new List<string>();

			var datesChanged = c.OldStartDate?.Date != c.NewStartDate?.Date || c.OldEndDate?.Date != c.NewEndDate?.Date;
			if (datesChanged && c.OldStartDate.HasValue && c.NewStartDate.HasValue)
				parts.Add($"dates changed from {Range(c.OldStartDate.Value, c.OldEndDate!.Value)} to {Range(c.NewStartDate.Value, c.NewEndDate!.Value)}");
			if (c.OldTitle != c.NewTitle && c.OldTitle != null && c.NewTitle != null)
				parts.Add($"title changed from \"{c.OldTitle}\" to \"{c.NewTitle}\"");
			if (c.OldEntryType != c.NewEntryType && c.OldEntryType != null && c.NewEntryType != null)
				parts.Add($"type changed from {c.OldEntryType} to {c.NewEntryType}");

			var detail = parts.Count == 0 ? "details were edited" : string.Join("; ", parts);
			var message = $"Updated by {RoleLabel(c.ChangedByRole)} {name} on {c.ChangedAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}: {detail}.";

			return new CalendarEntryChangeDto
			{
				ChangedById = c.ChangedBy,
				ChangedByName = name,
				ChangedByRole = c.ChangedByRole,
				ChangedAt = DateTime.SpecifyKind(c.ChangedAt, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture),
				Action = c.Action,
				OnOthersEntry = c.OnOthersEntry,
				PreviousTitle = c.OldTitle,
				NewTitle = c.NewTitle,
				PreviousEntryType = c.OldEntryType,
				NewEntryType = c.NewEntryType,
				PreviousStartDate = c.OldStartDate.HasValue ? Fmt(c.OldStartDate.Value) : null,
				PreviousEndDate = c.OldEndDate.HasValue ? Fmt(c.OldEndDate.Value) : null,
				NewStartDate = c.NewStartDate.HasValue ? Fmt(c.NewStartDate.Value) : null,
				NewEndDate = c.NewEndDate.HasValue ? Fmt(c.NewEndDate.Value) : null,
				Message = message
			};
		}

		/// <summary>"3 Mar 2027" for one day, "3 Mar – 7 Mar 2027" for a range.</summary>
		private static string Range(DateTime start, DateTime end)
		{
			var ci = CultureInfo.InvariantCulture;
			return start.Date == end.Date
				? start.ToString("d MMM yyyy", ci)
				: $"{start.ToString("d MMM", ci)} – {end.ToString("d MMM yyyy", ci)}";
		}

		#endregion

		#region Helpers

		private static bool Is(string? role, UserRole expected)
			=> string.Equals(role, expected.ToString(), StringComparison.OrdinalIgnoreCase);

		private static string NormaliseRole(string role)
			=> System.Enum.GetNames<UserRole>().FirstOrDefault(n => string.Equals(n, role, StringComparison.OrdinalIgnoreCase)) ?? role;

		private static int LevelOfRole(string role)
			=> Is(role, UserRole.SuperAdministrator) || Is(role, UserRole.Administrator) ? LevelAdmin
				: Is(role, UserRole.HeadTeacher) ? LevelHead
				: Is(role, UserRole.ClassTeacher) || Is(role, UserRole.SubjectTeacher) ? LevelTeacher : 0;

		private static string LevelLabel(int level)
			=> level >= LevelAdmin ? "an administrator" : level == LevelHead ? "the head teacher" : "a teacher";

		private static string RoleLabel(string role)
			=> Is(role, UserRole.SuperAdministrator) || Is(role, UserRole.Administrator) ? "admin"
				: Is(role, UserRole.HeadTeacher) ? "head teacher"
				: Is(role, UserRole.ClassTeacher) ? "class teacher"
				: Is(role, UserRole.SubjectTeacher) ? "subject teacher" : "staff member";

		/// <summary>The school's local "today" (WAT, UTC+1, no DST) — same logic as <see cref="CalendarService"/>.</summary>
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

		private static string Fmt(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

		/// <summary>Dapper parameters take a plain null for SQL NULL (DBNull.Value is rejected).</summary>
		private static object SqlNull(object? value) => value!;

		private static Guid ParseId(string? value) => Guid.TryParse(value, out var id) ? id : Guid.Empty;

		private static BaseResponse Ok(string message, object? data)
			=> new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = message, Status = "successful", Data = data! };

		private static BaseResponse Fail(string code, string message)
			=> new BaseResponse { ResponseCode = code, ResponseMessage = message, Status = "failed" };

		#endregion
	}
}
