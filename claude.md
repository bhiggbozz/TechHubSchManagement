# TechHub — School Management & EdTech Platform

## What It Is

A multi-tenant ASP.NET Core 8 Web API for school management: administration, lesson planning, quizzes/assessments, digital whiteboard session recording, performance analytics, and AI-powered question bank creation.

---

## Solution Architecture

```
TechhubMS.sln
├── TechhubMS                          (Web API / Presentation)
├── TechHub.Core                       (Domain — Entities, Enums, DTOs, ViewModels, Interfaces)
├── TechHub.Service                    (Application — Service implementations, Dapper repos)
├── TechHub.Entity.Migration           (Data — EF Core DbContext + comprehensive `Script_Initial.sql` for full-server deployment)
├── TechHub.Background                 (Background Workers — Hangfire jobs, hosted services)
└── TeachHub.QuestionBank              (Modular feature — AI question scanning with Claude)
```

---

## Technologies

| Technology | Version | Use |
|------------|---------|-----|
| .NET 8 | 8.0 | Framework |
| Dapper | 2.1.66 | All runtime data access |
| EF Core | 8.0.25 | Schema migrations only |
| SQL Server | via Microsoft.Data.SqlClient 6.0.1 | Primary database |
| MongoDB | Driver 2.28.0 | Board sessions + performance snapshots |
| RabbitMQ | Client 6.8.1 | Board batch async processing |
| JWT Bearer | 8.0.21 | Auth |
| Hangfire | 1.8.23 | Background job scheduling |
| Cloudinary | DotNet 1.28.0 | Media/CDN storage |
| Serilog | 4.3.1 | Structured logging |
| AutoMapper | 14.0.0 | DTO mapping |
| Anthropic.SDK | 5.10.0 | AI question extraction (Claude API) |
| SignalR | 8.0.24 | WebSocket |

---

## Two Separate Auth Systems

### 1. School Users (Users table)

- Roles: `Student`(0), `HeadTeacher`(1), `Administrator`(2), `SuperAdministrator`(3), `SubjectTeacher`(4), `ClassTeacher`(5), `Parent`(6)
- Login: `POST /api/User/login`
- JWT claims: `ClaimTypes.NameIdentifier`, `ClaimTypes.Role`, `"SchoolId"`, `"TenantId"`, `"SchoolName"`
- Token expiry: 1 hour
- Belongs to a school (has SchoolId)
- **`Role` is NOT a real table** — `Users.RoleId` is a bare `INT` with no FK, matching the `UserRole` C# enum's ordinal position exactly (confirmed against the live DB: only 0–5 in use before `Parent` was added as 6, no `Role` table exists). New roles must always be appended at the end of the enum — inserting elsewhere shifts every existing user's role.
- **Single active session per account**: on every login, all of that user's other `RefreshTokens` rows are revoked (`IsRevoked=1`). The old session's *already-issued* access token still works until its own natural expiry (≤1 hour) since access tokens are stateless — this is a deliberate cost tradeoff to avoid a per-request DB check. **Exception**: a login attempt is rejected outright (403) if the account has any `QuizAttempt`/`AssessmentAttempt` with `Status='InProgress'` — protects an in-progress exam session from being invalidated by a second login.
- **Parent role**: profiled by an Admin (`CreateUsers` permission) or SuperAdmin via `POST /api/User/profileParent` — links up to 10 students to one parent account (reuses an existing parent by email within the school rather than duplicating). Temp password generated server-side + emailed, same as any other non-Student role. Parents can use `forgot-password`/`reset-password` normally; only `Student` role is blocked from self-service reset. See `StudentParent` table below.

### 2. Platform Users (PlatformUser table — separate system)

- Roles: `PlatformSuperAdmin`, `PlatformAdmin`, `PlatformUser`
- Login: `POST /api/PlatformAuth/login`
- JWT claims: `ClaimTypes.NameIdentifier`, `ClaimTypes.Role` (no SchoolId — not tied to any school)
- Token expiry: 1 hour
- Manages the platform, not individual schools
- **Every login (success + failed attempt) is recorded** in `PlatformLoginHistory` table (see `PlatformAuthService.LogLoginAsync`)

### Platform Role Hierarchy

| Role | Created By | Can Do |
|------|-----------|--------|
| `PlatformSuperAdmin` | Seeded in DB migration | Create all platform roles, access all platform endpoints |
| `PlatformAdmin` | PlatformSuperAdmin via `POST /api/PlatformAdmin/create` | Provision schools, create `PlatformUser` accounts, manage platform operations |
| `PlatformUser` | PlatformSuperAdmin / PlatformAdmin | Lower-privilege operational access (defined by future endpoint gates) |

### Platform Audit Trail

`PlatformAuditLog` table records "who did what" on platform-level actions:

- **Action / EntityType constants**: `PlatformAuditAction` (e.g. `CreateSchool`, `ApproveSchool`, `RejectSchool`, `EditSchool`, `CreatePlatformUser`)
- Written by `IPlatformAuditService` (Dapper-backed) for:
  - School **create** (`POST /api/School/createschool`) — logged in `SchoolService.CreateSchool`
  - School registration **approve** and **reject** — logged in `SchoolService`
  - School **info edit** — logged in `SchoolService.EditSchoolInfoAsync`
  - Platform user creation — logged in `PlatformAdminController`
- Retrievable via `GET /api/PlatformAdmin/audit-logs`

### AdminPermission Bit-Flag System (school-level)

`[Flags]` enum stored as int in `AdminPermissions` table:

| Permission | Value | Description |
|------------|-------|-------------|
| None | 0 | No permissions |
| ApproveClasses | 1 | Approve class preparations |
| CreateLessons | 2 | Create class preparations (also gates student-to-class registration) |
| ManageTeachers | 4 | Manage teacher accounts |
| ManageStudents | 8 | Manage student accounts |
| ViewReports | 16 | View performance reports |
| ManageLessons | 32 | Manage lessons (also gates teacher-to-classroom assignment) |
| ManageSubjects | 64 | Manage subjects |
| CreateUsers | 128 | Create user accounts |

Pre-defined combos: `BasicAdmin = 18` (CreateLessons\|ViewReports), `FullAdmin = 127`

---

## Multi-Tenancy

- Tenant resolved from `X-Tenant-ID` header or subdomain (e.g., `pearl.vluethub.com`)
- `MultiTenantMiddleware` runs before authentication
- Resolved tenant stored in `HttpContext.Items`
- Shared database, `SchoolId` column on every entity

---

## Response Format

```json
{
    "responseMessage": "...",
    "responseCode": "99000",
    "status": "successful",
    "data": { ... }
}
```

### Response Code → HTTP Status Mapping

| Code | HTTP | Meaning |
|------|------|---------|
| `99000` | 200 | Success |
| `99001` | 400 | Validation error |
| `99101` | 500 | Server error |
| `99134` | 404 | Not found |
| `99107` | 401 | Unauthorized |
| `AX1003` | 403 | Forbidden |
| `99161` | 409 | Conflict |

---

## Key Database Tables

### School Management

| Table | Key Columns |
|-------|-------------|
| `School` | Id, SchoolName, Location, CountryId, StateId, Address, IsActive, LogoUrl, LogoPublicId, Identifier, State, CreatedBy, ModifiedBy |
| `SchoolCode` | SchoolId, Code (used for student registration codes) |
| `TenantInfo` | Id, SchoolId, Identifier (subdomain), IsActive |
| `Users` | Id, FirstName, MiddleName, LastName, EmailAddress, UserName, HashPassword (SHA256), SchoolId, RoleId (int, no FK — see UserRole enum), IsActive |
| `Classroom` | Id, Name, SchoolId, NoOfStudents |
| `Subjects` | Id, Subject, Category (Major/Minor), ClassCategory (Primary/Secondary/Colleges), SchoolId |
| `StudentClassroom` | StudentId, ClassroomId |
| `TeacherSubject` | TeacherId, SubjectId |
| `ClassroomTeacher` | TeacherId, ClassroomId |
| `StudentParent` | Id, StudentId, ParentId, SchoolId, CreatedBy, CreatedAt, IsActive. Filtered unique index on (StudentId, ParentId) WHERE IsActive=1. Max 10 active rows per ParentId, enforced in `UserService.ProfileParent` |
| `PasswordResetToken` | Id, UserId, SchoolId, Token (unique), ExpiresAt (60 min), CreatedAt, IsUsed. Single-use; token alone resolves the user/school server-side — the reset-password page needs no tenant header |
| `LoginHistory` | Id, UserId, RoleId, PasswordFailed, DeviceType, DeviceIp. Also doubles as the "first-time login" signal: a user with zero rows here is treated as first-time. **The first row is only ever written on a successful password change** (`UpdatePasswordFirstTime`), never on a mere login attempt — otherwise a failed password-change would let a retry with the temp password silently look like a normal login |
| `RefreshTokens` | Id, UserId, SchoolId, Token, ExpiresAt, CreatedAt, IsRevoked, RevokedAt, ReplacedByToken |
| `AdminPermissions` | Id, UserId, SchoolId, Permissions (int bitmask), CreatedBy, IsActive |

### Platform Users

| Table | Key Columns |
|-------|-------------|
| `PlatformUser` | Id, FirstName, LastName, Email, Username, PasswordHash (SHA256), Role (PlatformSuperAdmin\|PlatformAdmin\|PlatformUser), IsActive, IsDeleted, CreatedBy |
| `PlatformLoginHistory` | Id, PlatformUserId, Username, Email, Role, PasswordFailed, DeviceType, DeviceIp, CreatedAt |
| `PlatformAuditLog` | Id, ActorId, ActorName, ActorRole, Action, EntityType, EntityId, Description, DetailsJson, CreatedAt |

### Lesson Planning

| Table | Key Columns |
|-------|-------------|
| `LessonContent` | Id, SchoolId, ClassroomId, SubjectId, TopicId, Aim, Description, Status (Draft\|PendingApproval\|Approved\|Rejected\|Published), QuizCode, CreatedBy |
| `LessonMedia` | Id, LessonId, MediaUrl, MediaType |
| `ClassPreparation` | Id, SchoolId, TeacherId, Status (Draft\|Pending\|Approved\|Rejected\|InProgress\|Completed), SubmittedAt, ApprovedAt, ApprovalNotes, ApprovalTimeMinutes, IsUrgent, NeedsReview, AutoApprovalEligible |
| `ClassPreparationMedia` | Id, ClassPreparationId, MediaUrl, MediaType |

### Quiz System

| Table | Key Columns |
|-------|-------------|
| `Quiz` | Id, Code, LessonId, Title |
| `QuizConfig` | Id, QuizCode, TimeLimitMinutes, PassMarkPercent, ShuffleQuestions, AllowRetakes, MaxAttempts |
| `QuizQuestion` | Id, QuizCode, QuestionId, DisplayOrder |
| `QuizAttempt` | Id, QuizCode, LessonId, StudentId, SchoolId, AttemptNumber, Status (InProgress\|Submitted\|PartiallyGraded\|FullyGraded), TotalCorrect, TotalWrong, FinalScorePercent, IsPassed, StartedAt, SubmittedAt |
| `QuizAttemptAnswer` | Id, AttemptId, QuestionId, SelectedOptionId, IsCorrect, AutoMarksObtained, TypedAnswer, MaxMarks, IsSkipped |
| `QuizAttemptStatus` | Constants: InProgress, Submitted, PartiallyGraded, FullyGraded, Abandoned |

### Assessment System

| Table | Key Columns |
|-------|-------------|
| `Assessment` | Id, Code (AS-xxx), Title, Description, SchoolId, CreatedBy, IsActive |
| `AssessmentConfig` | Id, AssessmentId, TimeLimitMinutes, ShuffleQuestions, PassMarkPercent, ShowResultImmediately, EasyMarks, MediumMarks, HardMarks, ExamLevelMarks |
| `AssessmentQuestion` | Id, AssessmentId, QuestionId, DisplayOrder, IsActive |
| `AssessmentAssignment` | Id, AssessmentId, TargetType (Student\|Subject\|Classroom), TargetId, SchoolId |
| `AssessmentAttempt` | Id, AssessmentId, StudentId, SchoolId, AttemptNumber, IsOfficial, Status (InProgress\|Submitted\|PartiallyGraded\|FullyGraded), AutoMarksObtained, ManualMarksObtained, TotalMarks, FinalScorePercent, IsPassed, StartedAt, SubmittedAt, TimeTakenSeconds |
| `AssessmentAttemptAnswer` | Id, AttemptId, QuestionId, SelectedOptionId, IsCorrect, AutoMarksObtained, TypedAnswer, BoardSessionId, AudioUrl, MaxMarks, IsSkipped |

### Question Bank

| Table | Key Columns |
|-------|-------------|
| `Questions` | Id, Title, TextContent, QuestionType (Objective\|Theory\|TrueFalse), DifficultyLevel, MarksAllocation, SchoolId, SubjectId, TopicId, Status |
| `QuestionOptions` | Id, QuestionId, OptionLabel, OptionText, IsCorrect |
| `QuestionImage` | Id, QuestionId, ImageUrl |
| `ScanSession` | Id, TeacherId, SchoolId, Status |
| `ScanToken` | Id, TeacherId, Remaining, ExpiresAt |
| `QuestionJob` | Id, Status (Pending\|Processing\|Completed\|Failed), AIConfidenceScore |

### Board Session (MongoDB)

| Collection | Key Fields |
|-----------|------------|
| `BoardSession` | Id, LessonId, TeacherId, SchoolId, Status, StartedAt, EndedAt, TotalStrokes, TotalBatches |
| `BoardStroke` | Id, SessionId, BatchIndex, Data (compressed stroke JSON) |
| `BoardBatch` | Id, SessionId, BatchIndex, Strokes[], CreatedAt |
| `student_board_batches` | Id, SessionId (assessmentId_studentId_questionId), BoardIndex (1..N), Strokes[], CreatedAt |
| `PerformanceSnapshot` | DocType (school\|classroom_subject\|student\|teacher\|student_subject), AggregatedData. `student_subject` includes SubjectRank, TotalStudentsInSubject |

### Lesson Progress (Watched Lessons)

| Table | Key Columns |
|-------|-------------|
| `StudentLessonProgress` | Id, StudentId, LessonId, SchoolId, WatchedAt |

### Attendance

| Table | Key Columns |
|-------|-------------|
| `AttendanceSession` | Id, SchoolId, TeacherId, AttendanceType (0=Class\|1=Subject\|2=SubTopic), ClassroomId, SubjectId, SubTopicId, ClassPreparationId, Status (0=Open\|1=Closed\|2=Cancelled), StartedAt, EndedAt, CreatedBy. **Stats only count `Status=1` (Closed) sessions** — an open session that's never been ended contributes zero to `/stats`, even if it has scan records |
| `AttendanceRecord` | Id, SessionId, StudentId, SchoolId, IsPresent, IsManual, AttendedAt, CreatedBy |
| `ClassroomSubject` | ClassroomId, SubjectId, SchoolId, CreatedBy, IsActive. **Known bug**: `SchoolService.GetExistingClassroomSubjects` (the duplicate-assignment check used by `RegisterClassroomSubject`) queries a table called `ClassroomSubjects` (plural) — doesn't exist, throws every time, silently swallowed by a bare `catch`, so the duplicate check has never actually worked. Not yet fixed. |

### AI Image Generation (per-school feature flag)

| Table | Key Columns |
|-------|-------------|
| `SchoolFeature` | Id, SchoolId, FeatureKey (`ai_image_generation`), IsEnabled, ConfigurationJson, CreatedAt, UpdatedAt, CreatedBy, IsActive. One row per (SchoolId, FeatureKey) — capability is granted only when `IsEnabled = 1` AND `IsActive = 1` |
| `LessonGenerationPrompt` | Id, SchoolId, LessonId, CreatedBy, PromptText (final prompt sent to agent), TeacherPrompt (raw teacher override, null if auto-built), AgentType (Stability/OpenAI), Style, Status (Pending\|Completed\|Failed), MediaId (LessonMedia row, null on failure), ImageUrl, ImagePublicId, ErrorMessage, CreatedAt, IsActive. One row per generation attempt; "last prompt" = `ORDER BY CreatedAt DESC, Id DESC` |

---

## API Endpoints

### Authentication & Users

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/User/login` | Anonymous | School user login (tenant-aware) |
| POST | `/api/User/createUser` | JWT (Admin/SuperAdmin) | Create a school user |
| POST | `/api/User/EditUser` | JWT | Edit user |
| GET | `/api/User/GetStudents` | JWT | Paginated students |
| POST | `/api/User/updatePassword` | JWT | Update password |
| POST | `/api/User/update-password/newUser` | Anonymous | First-time password setup. Guard: rejects unless the account has **zero** `LoginHistory` rows (changed from "exactly one" when the premature history-write on login was removed) |
| POST | `/api/User/forgot-password` | Anonymous (tenant-aware) | Request a reset link. Always returns the same generic success message regardless of whether the username exists (no enumeration). `Student` role gets a distinct 403 ("contact your administrator") instead of an email. Generates a `PasswordResetToken`, emails `https://{tenant}.bluetsch.com/reset-password?token=...` |
| POST | `/api/User/reset-password` | Anonymous, **no tenant header** | Completes a reset: `{ token, newHashPassword, confirmHashPassword }`. Excluded from `MultiTenantMiddleware` — the token alone resolves the user/school |
| POST | `/api/User/profileParent` | JWT (Admin w/ CreateUsers, or SuperAdmin) | `{ parentFirstName, parentLastName, parentEmail, studentIds[≤10] }` — creates or reuses a Parent account, links students, emails temp password only when newly created |
| POST | `/api/User/removeStudentParent` | JWT (Admin w/ CreateUsers, or SuperAdmin) | `{ studentId, parentId }` — soft-deletes one link; parent account untouched |
| POST | `/api/User/deactivateParent/{parentId}` | JWT (Admin w/ CreateUsers, or SuperAdmin) | Sets the parent's `IsActive=0`; student links left as-is |
| GET | `/api/User/my-children` | Parent JWT | Lists the caller's own linked students (Id, name, classroom) — the only way a parent frontend can discover its children's IDs |
| POST | `/api/User/AssignPermissions` | SuperAdmin | Assign admin bitmask permissions |
| GET | `/api/User/GetAdminPermissions` | JWT | Get user's permissions |
| POST | `/api/User/RevokePermissions` | SuperAdmin | Revoke permissions |
| POST | `/api/User/refresh-token` | Anonymous | Refresh JWT |
| POST | `/api/PlatformAuth/login` | Anonymous | Platform user login |
| POST | `/api/PlatformAdmin/create` | PlatformSuperAdmin/PlatformAdmin | Create platform user (Role field: PlatformSuperAdmin/PlatformAdmin/PlatformUser; hierarchy enforced in service) |
| GET | `/api/PlatformAdmin/users` | PlatformSuperAdmin/PlatformAdmin | List platform users |
| GET | `/api/PlatformAdmin/login-history` | PlatformSuperAdmin/PlatformAdmin | Platform login history (filter by userId) |
| GET | `/api/PlatformAdmin/audit-logs` | PlatformSuperAdmin/PlatformAdmin | Platform audit trail (filter by action/entityType) |

### School Management

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/School/createschool` | PlatformAdmin/PlatformSuperAdmin | Create school record (platform JWT; token UserId stored as `CreatedBy`/`ModifiedBy`; audit-logged as `CreateSchool`) |
| POST | `/api/School/provision` | PlatformAdmin/PlatformSuperAdmin | **Full provision**: school + tenant + admin user + email |
| POST | `/api/School/register` | Anonymous | Submit a school registration request (school self-signup) |
| GET | `/api/School/registration-requests` | Anonymous | List registration requests (`?status=Pending`) |
| POST | `/api/School/approve/{requestId}` | PlatformAdmin/PlatformSuperAdmin | Approve + provision a registration request (`ApprovedBy` stored; audit-logged) |
| POST | `/api/School/reject/{requestId}` | PlatformAdmin/PlatformSuperAdmin | Reject a registration request (`reason` in body) |
| POST | `/api/School/getState` | - | Get states by country |
| POST | `/api/School/createschoolclassroom` | JWT | Create classroom |
| POST | `/api/School/registersubject` | JWT | Register subject |
| GET | `/api/School/getAllSchoolSubjects` | JWT | List subjects |
| GET | `/api/School/GetAllClassrooms` | JWT | Paginated classrooms |
| GET | `/api/School/GetAllSubjects` | JWT | Filtered subjects |
| POST | `/api/School/AssignTeachers` | JWT | Assign teachers to classroom |
| DELETE | `/api/School/RemoveClassroomSubject` | Admin/SuperAdmin | `{ classroomId, subjectIds[] }` — soft-deletes (IsActive=0) one or more classroom↔subject links |
| PUT | `/api/School/logo` | **SuperAdmin only** (was any JWT) | Update school logo — **the only endpoint allowed to ever write `School.LogoUrl`/`LogoPublicId`**. Every school-creation path (`createschool`, `provision`, `approve`) always inserts empty strings for these regardless of any value submitted at registration time |
| POST | `/api/School/topics` | JWT | Create topic |
| GET | `/api/School/topics/{subjectId}` | JWT | Topics by subject |
| POST | `/api/School/subtopics` | JWT | Create subtopic |
| GET | `/api/School/subtopics/{topicId}` | JWT | Subtopics by topic |
| GET | `/api/School/classroom/{id}/curriculum` | JWT | Classroom curriculum |
| PUT | `/api/School/edit/{schoolId}` | PlatformAdmin/SuperAdmin | Edit school info (audit-logged; stores `ModifiedBy`) |

### Lessons

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/lessons/submit` | JWT | Submit lesson |
| POST | `/api/lessons/admin/submit` | Admin (CreateLessons\|ManageLessons) / SuperAdmin | **Admin lesson submission** — same SubmitLesson service; SuperAdmin bypasses, Administrator must hold CreateLessons or ManageLessons |
| POST | `/api/lessons/draft` | JWT | Save draft |
| GET | `/api/lessons/{id}` | JWT | Get lesson |
| GET | `/api/lessons/classroom/{id}` | JWT | Lessons by classroom |
| GET | `/api/lessons/student/classroom/{id}` | Student | Student's lessons (Published only) |
| GET | `/api/lessons/my-lessons` | Teacher | Teacher's lessons |
| POST | `/api/lessons/{id}/respond` | JWT | Approve/reject lesson |
| GET | `/api/lessons/pending-approvals` | JWT | Pending approvals |

### Quiz

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/quiz/create` | JWT | Create quiz |
| POST | `/api/quiz/configure` | JWT | Configure quiz |
| PATCH | `/api/quiz/lesson/{id}/attach` | JWT | Attach quiz to lesson |
| GET | `/api/quiz/lesson/{id}` | JWT | Quiz by lesson |
| GET | `/api/quiz/student/lesson/{id}/display` | Student | Quiz preview |
| GET | `/api/quiz/code/{code}/display` | Student | Quiz by code |
| POST | `/api/quiz/attempt/start` | Student | Start quiz attempt |
| POST | `/api/quiz/attempt/{id}/submit` | Student | Submit quiz attempt |
| GET | `/api/quiz/attempt/{id}/result` | Student | Get result |
| GET | `/api/quiz/grading/pending` | Teacher | Pending manual grades |
| POST | `/api/quiz/grading/{id}/grade` | Teacher | Grade answer |

### Assessment

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/Assessment/create` | JWT | Create assessment |
| POST | `/api/Assessment/assign` | JWT | Assign (Student/Subject/Classroom) |
| GET | `/api/Assessment/student/list` | Student | List student's assessments |
| GET | `/api/Assessment/{id}/detail` | JWT | Assessment with questions |
| POST | `/api/Assessment/{id}/start` | Student | **Start or resume** attempt |
| POST | `/api/Assessment/answer` | Student | Submit single answer |
| POST | `/api/Assessment/submit-all` | Student | **Submit all answers + attempt in one call** |
| POST | `/api/Assessment/{attemptId}/submit` | Student | Submit entire attempt |
| GET | `/api/Assessment/result/{attemptId}` | Student | Get result |
| GET | `/api/Assessment/{id}/history` | Student | Attempt history |

### Performance & Dashboard

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/performance/navbar` | JWT | Role-specific navbar stats |
| GET | `/api/performance/dashboard` | JWT | Role-specific dashboard |
| GET | `/api/performance/classroom/{id}` | JWT | Classroom breakdown |
| GET | `/api/performance/subject/{id}` | JWT | Subject breakdown |
| GET | `/api/performance/student-summary` | Student | **New/unattempted assessments, quizzes, unwatched lessons** |
| GET | `/api/performance/student/subject-scores` | Student | Per-subject averages + ranking (from MongoDB aggregation) |
| POST | `/api/performance/lesson/{lessonId}/watch` | Student | Mark lesson as watched |
| POST | `/api/performance/refresh` | Admin | Trigger aggregation |

### Board Session

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/board/session/{sessionId}/batch` | Teacher | Submit 1-min stroke batch |
| POST | `/api/board/session/{sessionId}/manifest` | Teacher | Submit session manifest |
| GET | `/api/board/session/{sessionId}` | JWT | Get session |
| GET | `/api/board/session/{sessionId}/manifest` | Student | Get manifest for download |
| GET | `/api/board/session/{sessionId}/batch/{indexKey}` | Student | Get stroke batch |
| POST | `/api/board/student/session/{sessionId}/batch` | Student | Submit assessment answer board stroke batch (upsert by boardIndex) |
| GET | `/api/board/student/session/{sessionId}/board/{boardIndex}` | Student | Get assessment answer board strokes |

### Attendance

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/Attendance/session/start` | Teacher roles (`ClassTeacher`/`SubjectTeacher`/`HeadTeacher`/Admin/SuperAdmin) | Start session; **Class → ClassTeacher only**, **Subject/SubTopic → SubjectTeacher only**, admins/HeadTeacher bypass |
| POST | `/api/Attendance/session/{sessionId}/end` | Teacher roles | Close session (owner or admin) |
| POST | `/api/Attendance/session/{sessionId}/scan` | Teacher roles | Mark student present via QR (`{ QrToken }`); rejects non-enrolled / non-eligible students |
| GET | `/api/Attendance/session/{sessionId}` | JWT | Get session + records |
| GET | `/api/Attendance/session/{sessionId}/summary` | Teacher roles | Present/absent breakdown vs roster |
| GET | `/api/Attendance/sessions` | Teacher roles | Caller's sessions (`?attendanceType&pageNumber&pageSize`) |
| GET | `/api/Attendance/student/{studentId}/qrcode` | JWT (staff or self) | Student QR PNG |
| GET | `/api/Attendance/student/{studentId}/qr-token` | JWT (staff or self) | Student QR token JSON |
| GET | `/api/Attendance/student/me/qrcode` / `student/me/qr-token` | Student | Own QR |
| GET | `/api/Attendance/student/{studentId}/stats` | Teacher roles + **Parent** | Filterable (Daily/Weekly/Monthly/MonthlyRange × Class/Subject) attendance stats. Parent must own the student via an active `StudentParent` link, checked via `IsParentOfStudentAsync` — else 403 |
| GET | `/api/Attendance/student/{studentId}/attendance` | JWT (staff, self, or **Parent** for own children) | Student attendance history — same Parent-ownership check as `/stats` |

### Question Bank

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/questions/createquestions` | JWT | Create question |
| POST | `/api/questions/batch` | JWT | Batch create |
| GET | `/api/questions/{id}` | JWT | Get question |
| GET | `/api/questions/subjects/{id}` | JWT | Subject questions |
| POST | `/api/questions/{id}/publish` | JWT | Publish question |
| POST | `/api/questions/scan/token/request` | JWT | Request scan token |
| POST | `/api/questions/scan/process/{tokenId}` | JWT | AI scan (SSE streaming) |
| POST | `/api/questions/sync` | JWT | Sync offline questions |
| POST | `/api/questionjob/submit` | JWT | Submit image for AI extraction — multipart/form-data; `image` file **or** pre-uploaded reference (`FileUrl` + `FilePublicId` + `FileType`), plus `SubTopicId` + `QuestionType` (Objective/Theory/TrueFalse) + optional `ClassroomId`/`SubjectId`/`HasImages`/`MarksAllocation` |
| GET | `/api/questionjob/{jobId}/status` | JWT | Poll job status |

### AI Image Generation

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/image-generation/lesson/{lessonId}/generate` | JWT | Generate an image for a lesson (body: `{ prompt?, style?, negativePrompt?, width?, height? }`). Builds prompt from lesson aim + objectives unless `prompt` supplied; uploads to Cloudinary; attaches as `LessonMedia`; writes a `LessonGenerationPrompt` row. Feature-gated (`ai_image_generation` must be enabled for the school) |
| GET | `/api/image-generation/lesson/{lessonId}/prompt` | JWT | Last prompt used for the lesson (for review/edit + regenerate) |
| GET | `/api/image-generation/lesson/{lessonId}/history` | JWT | Full generation/prompt history, newest first |

### School Features

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/school-features` | JWT | Create/update a feature flag for a school (body: `{ schoolId, featureKey, isEnabled, configurationJson? }`). Idempotent per (SchoolId, FeatureKey) |
| GET | `/api/school-features` | JWT | List features. Platform admins may pass `?schoolId`; school users always read their own school |
| GET | `/api/school-features/check` | JWT | `?schoolId&featureKey` — whether a feature is enabled for a school |

---

## Frontend Endpoint Reference: Student Subject Scores

### `GET /api/performance/student/subject-scores`

**Auth:** Student JWT

**Request:**
- Headers: `Authorization: Bearer <token>`, `X-Tenant-ID: <subdomain>`
- Body: none

**Response (200):**
```json
{
    "responseMessage": "Student subject scores retrieved",
    "responseCode": "99000",
    "status": "successful",
    "data": [
        {
            "subjectId": "guid",
            "subjectName": "Mathematics",
            "averageScore": 78.5,
            "quizCount": 6,
            "position": 5,
            "totalStudents": 28
        }
    ]
}
```

**Frontend usage:** Display per-subject average scores with ranking position. `position` is 1-based rank within the subject (higher average = lower number). `totalStudents` is the total number of students ranked in that subject.

---

## Frontend Endpoint Reference: Quiz Performance

Four quiz-performance endpoints exist. All require a **JWT** (school user). SchoolId is always resolved from the token, so each is scoped to the caller's school.

### `GET /api/quiz/classroom/{classroomId}/performance`

**Auth:** JWT (roles Managerial)

**Request:** Header `Authorization: Bearer <token>`, `X-Tenant-ID: <subdomain>`. No body. `classroomId` in route.

**Description:** Per-lesson quiz stats for a classroom (only lessons that have a quiz attached). 404 if the classroom isn't in the caller's school.

**Response (200):**
```json
{
    "responseMessage": "3 quiz(zes) found",
    "responseCode": "99000",
    "status": "successful",
    "data": [
        {
            "lessonId": "guid",
            "quizCode": "QUIZ-1",
            "lessonTitle": "Algebra Intro",
            "subjectName": "",
            "classroomName": "SS1 A",
            "totalStudents": 30,
            "totalAttempts": 18,
            "completedAttempts": 16,
            "inProgressAttempts": 2,
            "averageScorePercent": 71.4,
            "passedCount": 12,
            "failedCount": 4,
            "passRate": 75.0
        }
    ]
}
```

### `GET /api/quiz/subject/{subjectId}/performance`

**Auth:** JWT. 404 if the subject isn't in the caller's school.

**Request:** Header only; `subjectId` in route.

**Response:** Same per-lesson shape as classroom but with `subjectName` populated and grouped across the subject's lessons (includes each lesson's `classroomName`).

```json
{
    "responseMessage": "Subject quiz performance retrieved",
    "responseCode": "99000",
    "status": "successful",
    "data": [
        {
            "lessonId": "uuid",
            "quizCode": "Q-101",
            "lessonTitle": "Fractions",
            "subjectName": "Mathematics",
            "classroomName": "SS1 B",
            "totalStudents": 25,
            "totalAttempts": 12,
            "completedAttempts": 11,
            "inProgressAttempts": 1,
            "averageScorePercent": 64.0,
            "passCount": 8,
            "failedCount": 3,
            "passRate": 72.7
        }
    ]
}
```

### `GET /api/quiz/student/{studentId}/performance`

**Auth:** JWT. Admins/head-teacher/teachers can query any student in their school. A `Student` role can only query their own `studentId`, otherwise 403 "You can only view your own quiz performance".

**Response (200):**
```json
{
    "responseMessage": "Student quiz performance retrieved",
    "responseCode": "99000",
    "status": "successful",
    "data": {
        "studentId": "uuid",
        "studentName": "John Doe",
        "totalQuizzes": 4,
        "totalAttempts": 6,
        "completedAttempts": 5,
        "inProgressAttempts": 1,
        "averageScorePercent": 68.3,
        "passRate": 66.7,
        "bestScorePercent": 92.0,
        "quizzes": [
            {
                "attemptId": "uuid",
                "lessonId": "uuid",
                "quizCode": "Q-101",
                "lessonTitle": "Fractions",
                "classroomName": "SS1 B",
                "subjectName": "Mathematics",
                "attemptCount": 2,
                "bestScorePercent": 80.0,
                "bestAttemptId": "uuid",
                "isPassed": true,
                "latestStatus": "FullyGraded",
                "latestSubmittedAt": "2026-01-01T10:00:00"
            }
        ]
    }
}
```

**Frontend usage:** Dashboard list of quiz results per lesson with best score, attempt count, latest status; `totalQuizzes`/`completedAttempts`/`inProgressAttempts` for summary cards; `passRate`/`averageScorePercent`/`bestScorePercent` highlight stats.

### `GET /api/performance/student/{studentId}/quiz-performance`

Alternative route hitting `IPerformanceDashboardService.GetStudentQuizPerformanceAsync` (returns a `QuizBreakdown` list). Same auth semantics as above.

---

## Key Design Patterns

### Generic Repository (Dapper)
- `ICommandRepository<T>` / `IQueryRepository<T>` → `CommandRepositoryService<T>` / `QueryRepositoryService<T>`
- Table name derived from `typeof(T).Name`
- Methods: `Create(entity)`, `Create(dict)`, `Create(transaction, connection, dict)`, `Get(sql)`, `GetAll(sql)`

### Assessment Attempt Flow
1. `POST /api/Assessment/{id}/start` — checks for existing `InProgress` attempt
2. If exists → returns `{ resume: true, attemptId }` (no new attempt created)
3. If none → creates new attempt, fetches shuffled questions + options
4. `POST /api/Assessment/answer` — submit one answer at a time (auto-grades objective)
5. `POST /api/Assessment/submit-all` — **submit all answers + finalize attempt in one call** (saves answers in a transaction, calculates score, marks as Submitted)
6. `POST /api/Assessment/{attemptId}/submit` — finalizes, calculates score
7. Only first attempt is `IsOfficial = true`

### Quiz Attempt Flow
- Similar to assessment but attached to a lesson via `QuizCode`
- `StartQuizAttempt` → `SubmitQuizAttempt` → `GetQuizResult`

### Direct-to-CDN Upload
- Server generates signed Cloudinary upload token
- Frontend uploads directly to Cloudinary
- Frontend calls `confirm-upload` to update DB

### Background Jobs (Hangfire)
- `MediaUploadJob`, `MediaCleanupJob`, `AIContentAnalysisJob`, `ThumbnailGeneratorJob`, `PerformanceAggregationJob`, `LessonImageGenerationJob` (auto-generates a lesson's AI image on approval; skips if a `Completed` prompt row already exists)
- **IMPORTANT:** Use DI-based Hangfire APIs (`IRecurringJobManager`, `IBackgroundJobClient`) — never the static `RecurringJob`/`BackgroundJob` helpers. `JobStorage.Current` is only set after the `BackgroundJobServer` hosted service starts (async, after `app.Run()`). Calling a static Hangfire API synchronously at startup (e.g. in `Program.cs`) throws "Current JobStorage instance has not been initialized". `IBackgroundJobService` (`TechHub.Background/Services/BackgroundJobService.cs`) injects `IRecurringJobManager` in its constructor and uses it for `ScheduleMediaCleanup()`; it also injects `IBackgroundJobClient` for one-off enqueues like `EnqueueLessonImageGeneration`. The app's startup Hangfire recurring registration is safe because it resolves the concrete service through DI.
- **Startup registration:** In `Program.cs` recurring jobs are registered in a scope right after `app.Build()` via the DI-injected `IBackgroundJobService` — do NOT switch that back to the static API.

### AI Image Generation Pipeline
- Flow: `ImageGenerationService.GenerateImageAsync` → feature-gate check (`SchoolFeature` must have `ai_image_generation` IsEnabled + IsActive) → build prompt (`TeachingPromptBuilder`) or use teacher `prompt` override → resolve agent via `IImageGenerationAgentFactory` keyed by agent `Name` (active provider = `ImageGeneration:Provider` in appsettings, default `Stability`) → call agent → upload PNG to Cloudinary → insert `LessonMedia` (MediaType image) → write `LessonGenerationPrompt` row (`Completed` with MediaId/ImageUrl, or `Failed` with ErrorMessage).
- **Agent swap:** add a new `IImageGenerationAgent` impl + register it in DI; the factory selects by the configured provider name. No service code changes needed.
- **Claude refinement only runs when the teacher submits no `prompt` override** — a caller-supplied `prompt` bypasses `ClaudeInstructionalPromptRefiner` entirely and goes straight to the image agent verbatim. There's no DB flag distinguishing a Claude-refined `PromptText` from a raw draft/override — only the server log line ("Lesson image prompt(s) refined by Claude..." vs "...using draft prompt") tells you which happened for a given row.
- **Claude's system prompt is analogy-first** (`ClaudeInstructionalPromptRefiner`): it must anchor the image in something the student has personally lived through (a campfire, an ice lolly, sweat evaporating) rather than defaulting to generic textbook/lab-diagram compositions (beakers, thermometers, arrows-and-boxes) — those are explicitly banned unless the concept truly has no everyday equivalent. May also include 1–3 short, correctly-spelled key-term labels (quoted verbatim in the prompt so the image model renders them exactly) — full running text is still banned.
- **Retry policy** (`ImageGenerationRetryPolicy`, shared by both `OpenAiImageGenerationAgent` and `StabilityImageGenerationAgent`): max 2 retries (3 attempts total), backoff `[2s, 5s]`, only for transient conditions (timeout, 429, 5xx). Permanent failures (400/401/403 — including an OpenAI billing-hard-limit block, which is a 400) fail immediately, by design — no retry can fix an account-level block.
- **Auto-generation on approval:** `EnqueueLessonImageGeneration(lessonId, schoolId, userId)` is fired via `IBackgroundJobClient` when a lesson becomes `Approved`. The job runs `LessonImageGenerationJob`, which uses `Role="Administrator"` claims and skips if a `Completed` prompt row already exists.
- **Trigger points (all 3 paths that set `Status = Approved`):**
  1. `LessonService.RespondToLesson` (manual approve via `POST /api/lessons/{id}/respond`)
  2. `LessonService.SubmitLesson` (auto-publish when teacher has no LineManager / admin submits)
  3. `UserService.RespondToApproval` (OperationType.SubmitLesson)

### Background Workers (Hosted Services)
- `QuestionJobWorker` (30s cycle) — processes AI extraction jobs
- `PerformanceAggregationWorker` (24h cycle) — aggregates quiz/assessment data to MongoDB
- `BoardSyncWorker` — consumes RabbitMQ board batches, stores in MongoDB

### Board Session Recording
- Teacher's whiteboard strokes captured in 1-minute batches
- Batches published to RabbitMQ for async processing
- `BoardSyncWorker` consumes and stores in MongoDB
- Students can download session manifests + stroke batches

---

## Important Notes

- **No cancellation endpoint exists** for in-progress assessment attempts. To force a fresh attempt, manually update `AssessmentAttempt.Status` to `Abandoned` or delete the row.
- **Lesson "watched" tracking** uses the `StudentLessonProgress` table. The `POST /api/performance/lesson/{lessonId}/watch` endpoint creates a row there.
- **School actor tracking**: `School.CreatedBy` / `School.ModifiedBy` (platform user Id) are populated in `CreateSchool`, `ProvisionSchool`, `ApproveRegistrationRequest`, and `EditSchoolInfoAsync`. `createschool` requires a `PlatformAdmin`/`PlatformSuperAdmin` JWT (previously anonymous).
- **Attendance role gating**: `StartSessionAsync` enforces **Class → `ClassTeacher` only** and **Subject/SubTopic → `SubjectTeacher` only** (via `CanManageClassroomAsync`/`CanTeachSubjectAsync`). `HeadTeacher`/`Administrator`/`SuperAdministrator` bypass both.
- **Attendance eligibility**: `ScanStudentAsync` rejects any student not enrolled before marking present — **Class** requires `StudentClassroom` membership; **Subject/SubTopic** requires enrollment in the subject (via `ClassroomSubject` or `StudentMinorSubject`) and, when the session has a `ClassroomId`, membership in that classroom too.
- **First PlatformSuperAdmin is seeded** via `Script_Initial.sql` (consolidated from all migration scripts) with username `platformadmin` and password `Platform@123`.
- **AI image generation keys are placeholders** in `appsettings.json` (`ImageGeneration:Providers:Stability:ApiKey`, `ImageGeneration:Providers:OpenAI:ApiKey`) — generation fails (writes a `Failed` `LessonGenerationPrompt` row) until real Stability/OpenAI API keys are configured. Provider is switched via `ImageGeneration:Provider`.
- **Image-generation feature gate**: a school can only generate lesson images when its `SchoolFeature` row for `ai_image_generation` has `IsEnabled = 1` AND `IsActive = 1` (managed via `POST /api/school-features`).
- **ProvisisonSchool flow**: Creates School → SchoolCode → TenantInfo → Users (Administrator) → AdminPermissions (FullAdmin) → sends welcome email, all in one transaction.
- **Assessment expiry**: `AssessmentConfig.ExpiresAt` is checked in `StartAttempt`. If expired, returns "Assessment has expired" error.
- **Student board sessionId format**: `{assessmentId}_{studentId}_{questionId}` for assessment answer board strokes.
- **Student subject scores** are pre-computed by `PerformanceAggregationWorker` (24h cycle) and stored as `student_subject` DocType in MongoDB — not queried live. Ranking uses `RANK() OVER (PARTITION BY SubjectId ORDER BY AvgScore DESC)`.
- **`createUser` permission gap (fixed)**: `UserService.CreateUser` used to only check `AdminPermission.CreateUsers` when the caller's own role was `Administrator` — any *other* role (Teacher, Student, etc.) fell through with no check at all and could create users. Now explicitly rejects any caller who isn't `SuperAdministrator` or an `Administrator` holding `CreateUsers`.
- **Attendance scan idempotency is now visible to the frontend**: `POST /api/Attendance/session/{sessionId}/scan` already no-ops (200, not an error) when the student is already marked present in that session, but previously gave the frontend no way to distinguish that from a fresh scan. The response DTO now carries `AlreadyMarked: true/false` for exactly this.
- **School.LogoUrl is never set at creation, by design**: `createschool`/`provision`/`approve` (both the monolith and TechSchPlatform) always insert `LogoUrl = LogoPublicId = string.Empty` regardless of any value present on the registration request — `SchoolRegistrationRequest.LogoUrl` still captures whatever a registrant submits, but it's inert; nothing reads it back into `School`. The **only** path that may write `School.LogoUrl` is `PUT /api/School/logo` (SuperAdmin only).
- **School approval emails link to the plain tenant subdomain** (`https://{tenantIdentifier}.bluetsch.com`), not an API path — this was previously `.../api/School/logo-setup/{schoolId}` in the monolith and had no link at all in TechSchPlatform.
- **Two stray dead-code items found, not yet cleaned up**: a default, unused `WeatherForecastController` (route `/WeatherForecast`, no `api/` prefix) in `TechhubMS`; and a second, uncompiled `Controllers/` folder sitting at the **repo root** (outside `TechhubMS/`, not referenced by any `.csproj`) with stray copies of `User`/`School`/`Board`/`WeatherForecast` controllers.

---

## Deployment & Infrastructure

### Production VPS
- **Host**: `191.215.35.9` (root SSH)
- **SSH key (local)**: `C:\Users\hp\Desktop\TechHub\github_actions` (`-i` flag; `.pub` alongside). Used for all scp/ssh deploys. `.gitignore`d, so it never gets committed — if it ever goes missing from disk, check `git stash list` first (it was recovered once from a stash, not lost) before regenerating. A backup copy also lives outside the repo at `C:\Users\hp\.ssh\techhub_vps` (+ `.pub`), safe from any repo-relative accident.
- **Monolith API**: container `techhub-api` → host `8080:80`, image `techhub-api:latest`, domain `api.bluetsch.com`. Deploy dir `/var/www/schooly` (docker-compose + `techhub-api.tar.gz` artifact; source staged at `/var/www/schooly/src` for manual `docker build`). Env overrides live in `/var/www/schooly/.env` (DB, Mongo, RabbitMQ, JWT, Cloudinary, Email, etc.). `ASPNETCORE_ENVIRONMENT=Production` is forced in compose.
- **Platform microservice (TechSchPlatform)**: container `techschplatform-api` → host `8082:80`, image `techschplatform-api:latest`, served at `https://platform.bluetsch.com/api/*` (nginx `location /api/` → `127.0.0.1:8082`). Source at `/docker/techschplatform(-src)`.
- **Frontend containers** (`/docker/bluethub-or/docker-compose.yml`): `bluethub-web` (3010→80, image `bluethub-or-web:latest`, domain bluetsch.com/www), `bluethub-landing` (3001→80), `scholarlyhub` (3011→80, domain platform.bluetsch.com `/`).
- **nginx**: `api.bluetsch.com`→8080, `www`+apex `bluetsch.com`→3010, `platform.bluetsch.com`→3011 (+ `/api/`→8082); Let's Encrypt via certbot (reload: `nginx -t && systemctl reload nginx`).
- **Container port gotcha**: modern `aspnet:8.0` images default `ASPNETCORE_HTTP_PORTS=8080` — pin `ENV ASPNETCORE_HTTP_PORTS=80` in Dockerfiles or map `-p 8082:8080`. A bare `-p 8082:80` gives 502/`000` if not pinned.

### Email (Mailtrap)
- **Production**: `EmailSettings:Provider=MailtrapApi` → `POST https://send.api.mailtrap.io/api/send` with `Authorization: Bearer <EmailSettings:MailtrapApiToken>` (token in appsettings / `.env`), JSON `{from, to[], subject, html, category}`, and a non-empty `User-Agent` (edge protection may block bare requests). Response `200 {success:true, message_ids:[...]}`.
- **Dev**: `Provider=Smtp` (sandbox `sandbox.smtp.mailtrap.io:2525`) via `appsettings.Development.json` override (`EmailSettings:Provider=Smtp`). The monolith has this override too (`TechhubMS/appsettings.Development.json`).
- **Verified sending domain: `bluetsch.com` (apex — not `www.`)**. Confirmed live via `GET /api/accounts/{id}/sending_domains` on 2026-08-22 (`dns_verified: true`, `compliance_status: compliant`); `www.bluetsch.com` is no longer even listed on the account — the domain changed since this doc was first written, always re-verify via the API rather than trusting this note if email starts failing. From addresses must end in `@bluetsch.com`. Monolith `EMAIL_FROM=noreply@bluetsch.com` (`/var/www/schooly/.env`); TechSchPlatform `FromEmail=support@bluetsch.com` (appsettings, both the repo and the VPS's `techschplatform-src` build source — the latter had drifted out of sync from git and needed a manual re-sync + rebuild).
- **Mailtrap account**: id `2493070` ("gbenga omoyele"). Verified domains: `bluetsch.com` (DNS pass), `demomailtrap.co` (demo_exhausted). Verify domains via `GET https://mailtrap.io/api/accounts/{id}/sending_domains` (Bearer token) — don't trust a stale doc note over this live check.

### CORS (monolith)
- `MultiTenantCors` policy (`ServiceCollectionExtension.cs`) branches on `env.IsProduction()`: **Production** only allows `*.bluetsch.com`, `localhost`, and the explicit `Cors:AllowedOrigins` array in `appsettings.json`. **Non-production** additionally allows `*.onrender.com`, `*.vercel.app`, `*.netlify.app` for preview/staging deploys.
- The Render deployment `techhubschmanagement.onrender.com` runs as `Production`, so it does **not** get the friendly preview-origin allowances — any staging frontend calling it needs its exact origin added to `Cors:AllowedOrigins`.
- **Env var override gotcha**: setting `Cors:AllowedOrigins` via an env var on Render requires the .NET double-underscore convention **with an array index**, e.g. `Cors__AllowedOrigins__5=https://your-site.netlify.app` — a name like `CORS_ORIGIN_0` (which only means something inside the *VPS's* `docker-compose.yml`, which explicitly remaps it to `Cors__AllowedOrigins__0`) does nothing on Render, since Render passes env vars straight through with no renaming layer. Pick an index that isn't already used by the 5 entries baked into `appsettings.json`.
- Current `Cors:AllowedOrigins`: `https://www.bluetsch.com`, `https://bluetsch.com`, `https://new-bluethub-app.netlify.app` (added for staging), plus three `localhost` dev ports.
- **EmailService design** (both monolith `TechHub.Service/Service/EmailService.cs` and `TechSchPlatform.Service/Services/EmailService.cs`): `Provider` switch — `MailtrapApi` → HTTP API; anything else → SMTP. Errors are logged, never thrown (email must not block user creation).

### TechSchPlatform microservice (`TechSchPlatform/`)
- Solution `TechSchPlatform.sln` (Core / Service / Api, net8.0), Dapper + Microsoft.Data.SqlClient; shares the monolith DB (`SQL8010.site4now.net` / `db_ac4720_techhub`).
- Separate platform-only JWT (same `Jwt:SecretKey/Issuer/Audience`). No MultiTenantMiddleware. Real SHA256-hex-lowercase password check. `PlatformSeedService` upserts `platformadmin` / `Platform@123` at startup. Parameterized Dapper. Response codes copied from monolith (`99000`/`99001`/`99101`/`99134`/`99107`/`AX1003`/`99161`).
- Endpoints: `POST /api/PlatformAuth/login`, `POST /api/PlatformAdmin/create`, `GET /api/PlatformAdmin/users`, `GET /api/PlatformAdmin/login-history`, `GET /api/PlatformAdmin/audit-logs`, `POST /api/School/createschool`, `POST /api/School/provision`, `POST /api/School/register`, `POST /api/School/approve/{requestId}`, `POST /api/School/reject/{requestId}`, `POST /api/School/getState`.
- Deployed: VPS `:8082` behind `platform.bluetsch.com/api/*`, plus **Render** (Root Directory = `TechSchPlatform`, Dockerfile = `TechSchPlatform/Dockerfile`; it lives inside the monolith repo, so Render must NOT use the repo-root Dockerfile, which builds `TechhubMS`).
  - **Confirmed this misconfiguration actually happened**: `techhub-platform.onrender.com` was found (2026-08-22) serving the monolith — proven by hitting it and getting `MultiTenantMiddleware`'s `TENANT_NOT_FOUND` JSON (a monolith-only behavior; TechSchPlatform has no tenant middleware at all). Root Directory/Dockerfile Path on that Render service need correcting to `TechSchPlatform` / `TechSchPlatform/Dockerfile` — not yet fixed as of this note.
  - There's also `techhubschmanagement.onrender.com` (a *separate*, undocumented Render service, auto-named from the GitHub repo, correctly building the monolith) — see the CORS section above for its gotchas.
- **VPS Swagger**: `TechSchPlatform.Api/Program.cs` calls `UseSwagger()`/`UseSwaggerUI()` unconditionally (no `IsDevelopment()` gate), but nginx's `platform.bluetsch.com` config only proxied `/api/` to `:8082` — `/swagger` fell through to the ScholarlyHub frontend's catch-all. Fixed by adding a `location /swagger/ { proxy_pass http://127.0.0.1:8082; ... }` block to `/etc/nginx/sites-available/platform.bluetsch.com` (before the `/api/` block). Live at `https://platform.bluetsch.com/swagger/index.html`.
- **`/docker/techschplatform-src` on the VPS is a plain file drop, not a git repo** — it can silently drift behind what's committed (found it missing both the Swagger fix and an email-domain fix that were already in git). No `git pull` available there; re-sync via `tar -czf` from a clean local checkout + `scp` + `docker build`, same as the original deploy method.
- Build/run caveats: `dotnet build` to a temp `-o` dir (VS file locks on `bin/`); run the Api dll with `-WorkingDirectory` = build output (content-root for appsettings), `--urls http://127.0.0.1:5299`. Source deploys via `tar -czf` (exclude `bin/obj/logs/.git/github_actions`), then `docker build` on the VPS.

### Frontend CI/CD (BLUETHUB-OR)
- Repo `C:\Users\hp\Desktop\TechHubFE-V2\BLUETHUB-OR`; Turborepo+pnpm; `apps/web` (main), `apps/landing`; Dockerfile target `web-nginx`.
- `.github/workflows/deploy.yml` on push to `Main`: build `web-nginx` (VITE args; defaults `https://api.bluetsch.com`, `https://www.bluetsch.com`, tenant `green`, RC `rc_live_732628123a4b4c21931bf0c8196408ec`) → save `bluethub-or-web.tar.gz` → scp to `/docker/bluethub-or` → `docker load` → `docker compose up -d --no-build web` → `docker image prune -f`.
- Branch `Main` created from `feature/lesson`, commit `51a9320`; origin `bhiggbozz/BLUETHUB-OR`, upstream `Paulolutosoye45/BLUETHUB-OR` (private). Workflow needs secrets `VPS_HOST=191.215.35.9`, `VPS_USER=root`, `VPS_SSH_KEY` (contents of `github_actions`) in the **upstream** repo — the scp step fails with "can't connect without a private SSH key or password" until they are added.

---

## Session Progress (2026-08-19)

- **TechSchPlatform built + deployed**: platform microservice (school register → approve → provision) extracted to `TechSchPlatform/`, shared DB, deployed to VPS `:8082` behind `platform.bluetsch.com/api/*` and to Render. Login/`PlatformAdmin/users` verified live.
- **Production email fixed (monolith + platform)**: monolith `EmailService.cs`, `TechhubMS/appsettings.json`, and `/var/www/schooly/.env` switched to Mailtrap API with a `MailtrapApi`/`Smtp` provider switch; verified live send (`HTTP 200`, message_id) to `plutonish007@yahoo.com`. Root cause of prior failure: production was using ElasticEmail SMTP (bad creds) → "Authentication required".
- **Containers rebuilt & re-deployed on VPS**: `techhub-api` (email fix) and `techschplatform-api` (FromEmail → verified domain).
- **Outstanding**: frontend deploy secrets still need adding to the upstream BLUETHUB-OR repo; local monolith + platform changes are not yet committed/pushed to GitHub.

## Session Progress (2026-08-25)

- **Parent role shipped end-to-end**: new `UserRole.Parent` (6), `StudentParent` link table, `ProfileParent`/`RemoveStudentParent`/`DeactivateParent`/`GetMyChildren` endpoints, Parent-aware `Login` (returns `Children` when applicable), and Parent access to the attendance stats/history endpoints scoped to their own children only. Full frontend integration spec written and handed off (endpoints, payloads, screen flow).
- **Self-service password reset shipped**: `PasswordResetToken` table, `forgot-password`/`reset-password` endpoints. Students explicitly excluded (told to contact staff instead).
- **Real bugs found and fixed** (all pre-existing, unrelated to any single feature ask): `createUser`'s missing role/permission gate for non-Administrator callers; `LoginHistory`'s first-time-login flag being consumed on a mere login *attempt* rather than a successful password change (silently let a failed password-reset attempt look like a normal subsequent login); no single-session enforcement at all (now fixed, with an explicit carve-out for in-progress quiz/assessment attempts); attendance scan's `AlreadyMarked` ambiguity; `School.LogoUrl` being writable from multiple creation flows instead of only the dedicated logo endpoint; stale Mailtrap domain (`www.bluetsch.com` → `bluetsch.com`) in both the monolith and a git-drifted TechSchPlatform VPS build source; `techhub-platform.onrender.com` confirmed actually misconfigured (serving the monolith instead of TechSchPlatform); TechSchPlatform's Swagger unreachable behind nginx (`/swagger` never proxied).
- **Known, not-yet-fixed bugs documented**: `GetExistingClassroomSubjects`'s wrong table name (silently disables duplicate-subject prevention); two dead-code leftovers (unused `WeatherForecastController`, an orphaned uncompiled root-level `Controllers/` folder).
- **Image generation prompt quality**: Claude's refiner system prompt rewritten to be analogy-first (anchor in something the student has lived through, ban generic textbook/lab compositions) instead of defaulting to clinical diagrams; added a shared, capped retry policy (2 retries, transient-only) to both image agents.
- **New endpoint**: `DELETE /api/School/RemoveClassroomSubject` (soft-delete, Admin/SuperAdmin only); `PUT /api/School/logo` narrowed to SuperAdmin only.
