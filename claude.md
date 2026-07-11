# TechHub — Assessment Start Attempt Endpoint Analysis

## Endpoint

```
POST /api/Assessment/{assessmentId}/start
```

## Response You Got

```json
{
    "responseMessage": "You have an active attempt. Resume it.",
    "responseCode": "99000",
    "status": "successful",
    "data": {
        "attemptId": "5e27cecd-c008-4f2b-a055-b80539c54413",
        "resume": true
    }
}
```

## Flow (AssessmentService.StartAttempt, line 401)

1. **Extract claims** — reads `UserId`, `SchoolId`, `Role` from JWT
2. **Check active attempt** — queries `AssessmentAttempt` WHERE `AssessmentId = {assessmentId}`, `StudentId = {studentId}`, `SchoolId = {schoolId}`, `Status = 'InProgress'`
3. **If InProgress exists** — returns immediately:
   ```
   Ok("You have an active attempt. Resume it.", new { AttemptId = inProgress.Id, Resume = true })
   ```
   This is what you are hitting — the student already has an active attempt (`5e27cecd...`).
4. **If no InProgress** — counts existing completed attempts, creates new attempt, fetches questions + options, returns full attempt data with questions.

## Response Codes & HTTP Status Mapping (AssessmentController.MapResponse)

In the controller, response codes map to HTTP status codes:

| ResponseCode | HTTP Status | Meaning |
|---|---|---|
| `"99000"` | `200 OK` | Success |
| `"99134"` | `404 Not Found` | Resource not found |
| `"AX1003"` | `403 Forbidden` | Not authorized for this action |
| `"99107"` | `401 Unauthorized` | Bad token |
| `"99161"` | `409 Conflict` | Resource conflict |
| anything else | `400 Bad Request` | Validation error |

## Why You See This

The student (identified by JWT) has already started an attempt on this assessment (`db46f707-...`) that was never submitted. The system found a row in `AssessmentAttempt` with `Status = 'InProgress'` and returned the existing `attemptId`.

## To Resume the Attempt

- **Submit answers** → `POST /api/Assessment/answer` with body:
  ```json
  {
    "attemptId": "5e27cecd-c008-4f2b-a055-b80539c54413",
    "questionId": "...",
    "selectedOptionId": "..."  // or "typedAnswer", "isSkipped"
  }
  ```
- **Check attempt detail** (questions, options) → `GET /api/Assessment/{assessmentId}/detail`
- **Submit the whole attempt** → `POST /api/Assessment/{attemptId}/submit`
- **View result** → `GET /api/Assessment/result/{attemptId}`
- **View history** → `GET /api/Assessment/{assessmentId}/history`

## To Force a New Attempt (Discard InProgress)

There is no "cancel attempt" endpoint. The `InProgress` row must be manually deleted from the `AssessmentAttempt` table, or its status changed to something else (`Abandoned`), before calling `start` again.

## Key Tables

| Table | Columns |
|---|---|
| `Assessment` | Id, Code, Title, Description, SchoolId, CreatedBy, IsActive |
| `AssessmentConfig` | Id, AssessmentId, TimeLimitMinutes, ShuffleQuestions, PassMarkPercent, ShowResultImmediately, EasyMarks, MediumMarks, HardMarks, ExamLevelMarks |
| `AssessmentQuestion` | Id, AssessmentId, QuestionId, SchoolId, DisplayOrder, IsActive |
| `AssessmentAssignment` | Id, AssessmentId, TargetType (Student\|Subject\|Classroom), TargetId, SchoolId, IsActive |
| `AssessmentAttempt` | Id, AssessmentId, StudentId, SchoolId, AttemptNumber, IsOfficial, AutoMarksObtained, ManualMarksObtained, TotalMarks, FinalScorePercent, IsPassed, Status (InProgress\|Submitted\|PartiallyGraded\|FullyGraded), StartedAt, SubmittedAt, TimeTakenSeconds |
| `AssessmentAttemptAnswer` | Id, AttemptId, QuestionId, SchoolId, SelectedOptionId, IsCorrect, AutoMarksObtained, TypedAnswer, BoardSessionId, AudioUrl, MaxMarks, IsSkipped, TeacherFeedback, ManualMarksObtained |

## Attempt Logic

- Only **first attempt** is `IsOfficial = true` (counts for grading)
- **AttemptNumber** auto-increments per student per assessment
- Objective questions (SelectedOptionId) are **auto-graded immediately** on answer submission
- Theory/essay questions require manual grading via `POST /api/Assessment/grading/{id}/grade`
- Final score = `(AutoMarksObtained + ManualMarksObtained) / TotalMarks * 100`