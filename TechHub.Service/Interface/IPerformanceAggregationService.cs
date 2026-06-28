namespace TechHub.Service.Interface;

public interface IPerformanceAggregationService
{
    Task AggregateAllSchoolsAsync();
    Task AggregateSchoolAsync(Guid schoolId);
}
