using Microsoft.Extensions.Options;
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
                    .Ascending(s => s.StudentId))
        };
        _collection.Indexes.CreateMany(indexes);
    }

    public async Task UpsertSnapshotAsync(PerformanceSnapshot snapshot)
    {
        var filter = Builders<PerformanceSnapshot>.Filter.And(
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.DocType, snapshot.DocType),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.SchoolId, snapshot.SchoolId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.ClassroomId, snapshot.ClassroomId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.SubjectId, snapshot.SubjectId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.TeacherId, snapshot.TeacherId),
            Builders<PerformanceSnapshot>.Filter.Eq(s => s.StudentId, snapshot.StudentId)
        );

        var existing = await _collection.Find(filter).FirstOrDefaultAsync();
        if (existing != null)
        {
            snapshot.Id = existing.Id;
        }

        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, snapshot, options);
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
}
