-- ═══════════════════════════════════════════════════════════════════════════
-- Scriptsv8_PerformanceIndexes.sql
-- 
-- PERFORMANCE QUERY INDEXES
-- 
-- These indexes support the real-time dashboard queries for:
--   GET /api/Performance/subject/{subjectId}/classrooms
--   GET /api/Performance/subject/{subjectId}/topics
--
-- Query pattern:
--   SELECT ... FROM QuizAttempt qa
--   JOIN LessonContent lc ON lc.Id = qa.LessonId
--   WHERE qa.SchoolId = @SchoolId AND lc.SubjectId = @SubjectId
--   GROUP BY lc.ClassroomId  (or lc.TopicId)
--
-- ═══════════════════════════════════════════════════════════════════════════

-- 1. Covering index for LessonContent filtering by SubjectId + SchoolId
--    Includes columns needed for GROUP BY and SELECT (ClassroomId, TopicId, SubTopic)
--    Avoids key lookups into the clustered index.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LessonContent_SubjectId_SchoolId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_LessonContent_SubjectId_SchoolId
        ON LessonContent (SubjectId, SchoolId)
        INCLUDE (ClassroomId, TopicId, SubTopic)
        WHERE IsActive = 1;
END

-- 2. Covering index for QuizAttempt performance aggregation
--    Covers the JOIN column (LessonId), filter columns (SchoolId, Status),
--    and aggregation columns (FinalScorePercent, IsPassed, StudentId, ModifiedDate).
--    This makes the aggregation queries index-only (no clustered index lookups).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuizAttempt_PerformanceLookup')
BEGIN
    CREATE NONCLUSTERED INDEX IX_QuizAttempt_PerformanceLookup
        ON QuizAttempt (LessonId, SchoolId, Status)
        INCLUDE (FinalScorePercent, IsPassed, StudentId, ModifiedDate);
END

PRINT 'Performance indexes created successfully.';
