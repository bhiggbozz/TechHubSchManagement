-- =====================================================
-- Link table: multiple board sessions per assessment answer
-- =====================================================

IF OBJECT_ID('AssessmentAttemptAnswerBoard', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentAttemptAnswerBoard (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AnswerId     UNIQUEIDENTIFIER NOT NULL,
        BoardSessionId NVARCHAR(100)  NOT NULL,
        BoardIndex   INT              NULL,
        BoardLabel   NVARCHAR(50)     NULL,
        CreatedAt    NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_AnswerBoard_AnswerId
        ON AssessmentAttemptAnswerBoard(AnswerId);

    CREATE INDEX IX_AnswerBoard_Session
        ON AssessmentAttemptAnswerBoard(BoardSessionId);
END;

PRINT 'AssessmentAttemptAnswerBoard table created.';
