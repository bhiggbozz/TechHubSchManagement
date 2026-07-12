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
├── TechHub.Entity.Migration           (Data — EF Core DbContext for schema migrations)
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

- Roles: `Student`, `HeadTeacher`, `Administrator`, `SuperAdministrator`, `SubjectTeacher`, `ClassTeacher`
- Login: `POST /api/User/login`
- JWT claims: `ClaimTypes.NameIdentifier`, `ClaimTypes.Role`, `"SchoolId"`, `"TenantId"`, `"SchoolName"`
- Token expiry: 1 hour
- Belongs to a school (has SchoolId)

### 2. Platform Users (PlatformUser table — separate system)

- Roles: `PlatformAdmin`, `PlatformSuperAdmin`
- Login: `POST /api/Platform/login`
- JWT claims: `ClaimTypes.NameIdentifier`, `ClaimTypes.Role` (no SchoolId — not tied to any school)
- Token expiry: 1 hour
- Manages the platform, not individual schools

### Platform Role Hierarchy

| Role | Created By | Can Do |
|------|-----------|--------|
| `PlatformSuperAdmin` | Seeded in DB migration | Create PlatformAdmins, access all platform endpoints |
| `PlatformAdmin` | PlatformSuperAdmin via `POST /api/Platform/admin/create` | Provision schools, manage platform operations |

### AdminPermission Bit-Flag System (school-level)

`[Flags]` enum stored as int in `AdminPermissions` table:

| Permission | Value | Description |
|------------|-------|-------------|
| None | 0 | No permissions |
| ApproveClasses | 1 | Approve class preparations |
| CreateClasses | 2 | Create class preparations |
| ManageTeachers | 4 | Manage teacher accounts |
| ManageStudents | 8 | Manage student accounts |
| ViewReports | 16 | View performance reports |
| ManageClassrooms | 32 | Manage classrooms |
| ManageSubjects | 64 | Manage subjects |
| CreateUsers | 128 | Create user accounts |

Pre-defined combos: `BasicAdmin = 18` (CreateClasses\|ViewReports), `FullAdmin = 127`

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
| `School` | Id, SchoolName, Location, CountryId, StateId, Address, IsActive |
| `SchoolCode` | SchoolId, Code (used for student registration codes) |
| `TenantInfo` | Id, SchoolId, Identifier (subdomain), IsActive |
| `Users` | Id, FirstName, LastName, EmailAddress, UserName, HashPassword (SHA256), SchoolId, RoleId, IsActive |
| `Role` | Id, Name (Student/HeadTeacher/Administrator/SuperAdministrator/SubjectTeacher/ClassTeacher) |
| `Classroom` | Id, Name, SchoolId, NoOfStudents |
| `Subjects` | Id, Subject, Category (Major/Minor), ClassCategory (Primary/Secondary/Colleges), SchoolId |
| `StudentClassroom` | StudentId, ClassroomId |
| `TeacherSubject` | TeacherId, SubjectId |
| `ClassroomTeacher` | TeacherId, ClassroomId |
| `LoginHistory` | Id, UserId, RoleId, PasswordFailed, DeviceType, DeviceIp |
| `RefreshTokens` | Id, UserId, Token, ExpiresAt |
| `AdminPermissions` | Id, UserId, SchoolId, Permissions (int bitmask), CreatedBy, IsActive |

### Platform Users

| Table | Key Columns |
|-------|-------------|
| `PlatformUser` | Id, FirstName, LastName, Email, Username, PasswordHash (SHA256), Role (PlatformAdmin\|PlatformSuperAdmin), IsActive, IsDeleted, CreatedBy |

### Lesson Planning

| Table | Key Columns |
|-------|-------------|
| `LessonContent` | Id, SchoolId, ClassroomId, SubjectId, TopicId, Aim, Description, Status (Draft\|PendingApproval\|Approved\|Rejected\|Published), QuizCode, CreatedBy |
| `LessonMedia` | Id, LessonId, MediaUrl, MediaType |
| `ClassPreparation` | Id, SchoolId, TeacherId, Status (Draft\|Pending\|Approved\|Rejected\|InProgress\|Completed), SubmittedAt, ApprovedAt |
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
| POST | `/api/User/update-password/newUser` | Anonymous | First-time password setup |
| POST | `/api/User/AssignPermissions` | SuperAdmin | Assign admin bitmask permissions |
| GET | `/api/User/GetAdminPermissions` | JWT | Get user's permissions |
| POST | `/api/User/RevokePermissions` | SuperAdmin | Revoke permissions |
| POST | `/api/User/refresh-token` | Anonymous | Refresh JWT |
| POST | `/api/Platform/login` | Anonymous | Platform user login |
| POST | `/api/Platform/admin/create` | PlatformSuperAdmin | Create PlatformAdmin |

### School Management

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/School/createschool` | PlatformAdmin/SuperAdmin | Create school record |
| POST | `/api/School/provision` | PlatformAdmin/SuperAdmin | **Full provision**: school + tenant + admin user + email |
| POST | `/api/School/getState` | - | Get states by country |
| POST | `/api/School/createschoolclassroom` | JWT | Create classroom |
| POST | `/api/School/registersubject` | JWT | Register subject |
| GET | `/api/School/getAllSchoolSubjects` | JWT | List subjects |
| GET | `/api/School/GetAllClassrooms` | JWT | Paginated classrooms |
| GET | `/api/School/GetAllSubjects` | JWT | Filtered subjects |
| POST | `/api/School/AssignTeachers` | JWT | Assign teachers to classroom |
| PUT | `/api/School/logo` | JWT | Update school logo |
| POST | `/api/School/topics` | JWT | Create topic |
| GET | `/api/School/topics/{subjectId}` | JWT | Topics by subject |
| POST | `/api/School/subtopics` | JWT | Create subtopic |
| GET | `/api/School/subtopics/{topicId}` | JWT | Subtopics by topic |
| GET | `/api/School/classroom/{id}/curriculum` | JWT | Classroom curriculum |

### Lessons

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/lessons/submit` | JWT | Submit lesson |
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
| POST | `/api/questionjob/submit` | JWT | Submit image for AI extraction |
| GET | `/api/questionjob/{jobId}/status` | JWT | Poll job status |

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
5. `POST /api/Assessment/{attemptId}/submit` — finalizes, calculates score
6. Only first attempt is `IsOfficial = true`

### Quiz Attempt Flow
- Similar to assessment but attached to a lesson via `QuizCode`
- `StartQuizAttempt` → `SubmitQuizAttempt` → `GetQuizResult`

### Direct-to-CDN Upload
- Server generates signed Cloudinary upload token
- Frontend uploads directly to Cloudinary
- Frontend calls `confirm-upload` to update DB

### Background Jobs (Hangfire)
- `MediaUploadJob`, `MediaCleanupJob`, `AIContentAnalysisJob`, `ThumbnailGeneratorJob`, `PerformanceAggregationJob`

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
- **First PlatformSuperAdmin is seeded** via `Scriptsv11_PlatformUsers.sql` with username `platformadmin` and password `Platform@123`.
- **ProvisisonSchool flow**: Creates School → SchoolCode → TenantInfo → Users (Administrator) → AdminPermissions (FullAdmin) → sends welcome email, all in one transaction.
- **Assessment expiry**: `AssessmentConfig.ExpiresAt` is checked in `StartAttempt`. If expired, returns "Assessment has expired" error.
- **Student board sessionId format**: `{assessmentId}_{studentId}_{questionId}` for assessment answer board strokes.
- **Student subject scores** are pre-computed by `PerformanceAggregationWorker` (24h cycle) and stored as `student_subject` DocType in MongoDB — not queried live. Ranking uses `RANK() OVER (PARTITION BY SubjectId ORDER BY AvgScore DESC)`.
