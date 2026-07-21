using TechHub.Core.Entities.Performance;

namespace TechHub.Service.Interface;

public interface IAdminDashboardRepository
{
    Task UpsertDashboardAsync(AdminDashboardData data);
    Task<AdminDashboardData?> GetBySchoolAsync(Guid schoolId);
    Task DeleteBySchoolAsync(Guid schoolId);
}

public interface IPerformanceRepository
{
    Task UpsertSnapshotAsync(PerformanceSnapshot snapshot);
    Task<PerformanceSnapshot?> FindSnapshotAsync(
        string docType, Guid schoolId, Guid? classroomId, Guid? subjectId,
        Guid? topicId, string? subTopicName, Guid? teacherId, Guid? studentId);
    Task<List<PerformanceSnapshot>> GetBySchoolAsync(Guid schoolId);
    Task<List<PerformanceSnapshot>> GetByClassroomAsync(Guid schoolId, Guid classroomId);
    Task<List<PerformanceSnapshot>> GetBySubjectAsync(Guid schoolId, Guid subjectId);
    Task<List<PerformanceSnapshot>> GetByTeacherAsync(Guid teacherId);
    Task<List<PerformanceSnapshot>> GetByStudentAsync(Guid studentId);
    Task<List<PerformanceSnapshot>> GetByClassroomSubjectAsync(Guid schoolId, Guid classroomId, Guid subjectId);
    Task<List<PerformanceSnapshot>> GetByClassroomSubjectTopicAsync(Guid schoolId, Guid classroomId, Guid subjectId);
    Task<List<PerformanceSnapshot>> GetBySubjectTopicAsync(Guid schoolId, Guid subjectId);
    Task<List<PerformanceSnapshot>> GetBySubjectSubTopicAsync(Guid schoolId, Guid subjectId);
    Task<List<PerformanceSnapshot>> GetByClassroomSubjectSubTopicAsync(Guid schoolId, Guid classroomId, Guid subjectId);
    Task<PerformanceSnapshot?> GetSchoolAggregateAsync(Guid schoolId);
    Task DeleteAllBySchoolAsync(Guid schoolId);
    Task<List<PerformanceSnapshot>> GetStudentSubjectScoresAsync(Guid studentId, Guid schoolId);
    Task<List<PerformanceSnapshot>> GetStudentSubTopicScoresAsync(Guid studentId, Guid schoolId);
}
