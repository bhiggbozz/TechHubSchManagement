using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Entities.Performance;
using TechHub.Service.Interface;

namespace TechHub.Service.Repository;

public class PerformanceRepository : IPerformanceRepository
{
    private readonly IMongoCollection<PerformanceSnapshot> _collection;
    private readonly ILogger _logger;

    public PerformanceRepository(IOptions<MongoDbSettings> mongoSettings, ILogger logger)
    {
        _logger = logger;
        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.AnalyticsDatabaseName);
        _collection = database.GetCollection<PerformanceSnapshot>("performance_snapshots");

        var indexes = new[]
        {
            new CreateIndexModel<PerformanceSnapshot>(
                Builders<PerformanceSnapshot>.IndexKeys
                    .Ascending(s => s.SchoolId)
                    .Ascending(s => s.DocType)),
            new CreateIndexModel<PerformanceSnapshot>(
                Builders<PerformanceSnapshot>.IndexKeys
                    .Ascending(s => s.DocType)
                    .Ascending(s => s.ClassroomId)
                    .Ascending(s => s.SubjectId)),
            new CreateIndexModel<PerformanceSnapshot>(
                Builders<PerformanceSnapshot>.IndexKeys
                    .Ascending(s => s.TeacherId)),
            new CreateIndexModel<PerformanceSnapshot>(
                Builders<PerformanceSnapshot>.IndexKeys
                    .Ascending(s => s.StudentId)),
            new CreateIndexModel<PerformanceSnapshot>(
                Builders<PerformanceSnapshot>.IndexKeys
                    .Ascending(s => s.DocType)
                    .Ascending(s => s.SchoolId)
                    .Ascending(s => s.SubjectId)
                    .Ascending(s => s.TopicId)),
            new CreateIndexModel<PerformanceSnapshot>(
                Builders<PerformanceSnapshot>.IndexKeys
                    .Ascending(s => s.DocType)
                    .Ascending(s => s.SchoolId)
                    .Ascending(s => s.SubjectId)
                    .Ascending(s => s.TopicId)
                    .Ascending(s => s.SubTopicName))
        };
        _collection.Indexes.CreateMany(indexes);
    }

    public async Task UpsertSnapshotAsync(PerformanceSnapshot snapshot)
    {
        var filters = new List<FilterDefinition<PerformanceSnapshot>>
        {
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.DocType, snapshot.DocType),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.SchoolId, snapshot.SchoolId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.ClassroomId, snapshot.ClassroomId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.SubjectId, snapshot.SubjectId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.TeacherId, snapshot.TeacherId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.StudentId, snapshot.StudentId)
        };

        if (snapshot.TopicId.HasValue)
            filters.Add(Builders<PerformanceSnapshot>.Filter.Eq(s => s.TopicId, snapshot.TopicId));
        if (!string.IsNullOrEmpty(snapshot.SubTopicName))
            filters.Add(Builders<PerformanceSnapshot>.Filter.Eq(s => s.SubTopicName, snapshot.SubTopicName));

        var filter = Builders<PerformanceSnapshot>.Filter.And(filters);

        var existing = await _collection.Find(filter).FirstOrDefaultAsync();
        if (existing != null)
        {
            snapshot.Id = existing.Id;
        }
        else
        {
            snapshot.Id = ObjectId.GenerateNewId();
        }

        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, snapshot, options);
    }

    public async Task<PerformanceSnapshot?> FindSnapshotAsync(
        string docType, Guid schoolId, Guid? classroomId, Guid? subjectId,
        Guid? topicId, string? subTopicName, Guid? teacherId, Guid? studentId)
    {
        var filters = new List<FilterDefinition<PerformanceSnapshot>>
        {
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.DocType, docType),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.SchoolId, schoolId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.ClassroomId, classroomId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.SubjectId, subjectId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.TeacherId, teacherId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.StudentId, studentId)
        };

        if (topicId.HasValue)
            filters.Add(Builders<PerformanceSnapshot>.Filter.Eq(s => s.TopicId, topicId));
        if (!string.IsNullOrEmpty(subTopicName))
            filters.Add(Builders<PerformanceSnapshot>.Filter.Eq(s => s.SubTopicName, subTopicName));

        return await _collection
            .Find(Builders<PerformanceSnapshot>.Filter.And(filters))
            .FirstOrDefaultAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetBySchoolAsync(Guid schoolId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId)
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetByClassroomAsync(Guid schoolId, Guid classroomId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId && s.ClassroomId == classroomId)
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetBySubjectAsync(Guid schoolId, Guid subjectId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId && s.SubjectId == subjectId && s.DocType == "classroom_subject")
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetByTeacherAsync(Guid teacherId)
    {
        return await _collection
            .Find(s => s.TeacherId == teacherId)
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetByStudentAsync(Guid studentId)
    {
        return await _collection
            .Find(s => s.StudentId == studentId && s.DocType == "student")
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetByClassroomSubjectAsync(Guid schoolId, Guid classroomId, Guid subjectId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId
                    && s.ClassroomId == classroomId
                    && s.SubjectId == subjectId
                    && s.DocType == "classroom_subject")
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetByClassroomSubjectTopicAsync(Guid schoolId, Guid classroomId, Guid subjectId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId
                    && s.ClassroomId == classroomId
                    && s.SubjectId == subjectId
                    && s.DocType == "classroom_subject_topic")
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetBySubjectTopicAsync(Guid schoolId, Guid subjectId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId
                    && s.SubjectId == subjectId
                    && s.DocType == "classroom_subject_topic")
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetBySubjectSubTopicAsync(Guid schoolId, Guid subjectId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId
                    && s.SubjectId == subjectId
                    && s.DocType == "classroom_subject_subtopic")
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetByClassroomSubjectSubTopicAsync(Guid schoolId, Guid classroomId, Guid subjectId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId
                    && s.ClassroomId == classroomId
                    && s.SubjectId == subjectId
                    && s.DocType == "classroom_subject_subtopic")
            .ToListAsync();
    }

    public async Task<PerformanceSnapshot?> GetSchoolAggregateAsync(Guid schoolId)
    {
        return await _collection
            .Find(s => s.SchoolId == schoolId && s.DocType == "school")
            .FirstOrDefaultAsync();
    }

    public async Task DeleteAllBySchoolAsync(Guid schoolId)
    {
        await _collection.DeleteManyAsync(s => s.SchoolId == schoolId);
    }

    public async Task<List<PerformanceSnapshot>> GetStudentSubjectScoresAsync(Guid studentId, Guid schoolId)
    {
        return await _collection
            .Find(s => s.StudentId == studentId
                    && s.SchoolId == schoolId
                    && s.DocType == "student_subject")
            .Sort(Builders<PerformanceSnapshot>.Sort.Ascending(s => s.SubjectName))
            .ToListAsync();
    }

    public async Task<List<PerformanceSnapshot>> GetStudentSubTopicScoresAsync(Guid studentId, Guid schoolId)
    {
        return await _collection
            .Find(s => s.StudentId == studentId
                    && s.SchoolId == schoolId
                    && s.DocType == "student_subtopic")
            .Sort(Builders<PerformanceSnapshot>.Sort.Ascending(s => s.SubjectName)
                  .Ascending(s => s.SubTopicName))
            .ToListAsync();
    }
}
