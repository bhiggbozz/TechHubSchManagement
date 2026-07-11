-- =====================================================
-- Student Lesson Progress (watch tracking)
-- Tracks which lessons a student has watched/viewed
-- Used by the student dashboard to show unwatched lessons
-- =====================================================

CREATE TABLE StudentLessonProgress (
    Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    StudentId    UNIQUEIDENTIFIER NOT NULL,
    LessonId     UNIQUEIDENTIFIER NOT NULL,
    SchoolId     UNIQUEIDENTIFIER NOT NULL,
    WatchedAt    NVARCHAR(30)     NOT NULL,
    CreationDate NVARCHAR(30)     NOT NULL
);

CREATE UNIQUE INDEX IX_StudentLessonProgress_Student_Lesson
    ON StudentLessonProgress(StudentId, LessonId, SchoolId);

CREATE INDEX IX_StudentLessonProgress_LessonId
    ON StudentLessonProgress(LessonId);
