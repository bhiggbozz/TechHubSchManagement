namespace TechSchPlatform.Core.ViewModel.Platform;

public class SchoolStatusDto
{
    public Guid? SchoolId { get; set; }
    public Guid? RequestId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}