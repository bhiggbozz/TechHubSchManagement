using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TechHub.Core.Configuration;
using TechHub.Core.Entities.Performance;
using TechHub.Service.Interface;

namespace TechHub.Service.Repository;

public class AdminDashboardRepository : IAdminDashboardRepository
{
    private readonly IMongoCollection<AdminDashboardData> _collection;

    public AdminDashboardRepository(IOptions<MongoDbSettings> mongoSettings)
    {
        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.AnalyticsDatabaseName);
        _collection = database.GetCollection<AdminDashboardData>("admin_dashboard");

        var index = new CreateIndexModel<AdminDashboardData>(
            Builders<AdminDashboardData>.IndexKeys.Ascending(d => d.SchoolId),
            new CreateIndexOptions { Unique = true });
        _collection.Indexes.CreateMany(new[] { index });
    }

    public async Task UpsertDashboardAsync(AdminDashboardData data)
    {
        var filter = Builders<AdminDashboardData>.Filter.Eq(d => d.SchoolId, data.SchoolId);

        var existing = await _collection.Find(filter).FirstOrDefaultAsync();
        if (existing != null)
        {
            data.Id = existing.Id;
        }
        else
        {
            data.Id = ObjectId.GenerateNewId();
        }

        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, data, options);
    }

    public async Task<AdminDashboardData?> GetBySchoolAsync(Guid schoolId)
    {
        return await _collection
            .Find(d => d.SchoolId == schoolId)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteBySchoolAsync(Guid schoolId)
    {
        await _collection.DeleteManyAsync(d => d.SchoolId == schoolId);
    }
}
