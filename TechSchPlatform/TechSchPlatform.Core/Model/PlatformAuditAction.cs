namespace TechSchPlatform.Core.Model;

public static class PlatformAuditAction
{
    public const string SchoolCreated = "CreateSchool";
    public const string SchoolApproved = "ApproveSchool";
    public const string SchoolRejected = "RejectSchool";
    public const string SchoolEdited = "EditSchool";
    public const string PlatformUserCreated = "CreatePlatformUser";
    public const string PlatformUserEdited = "EditPlatformUser";

    // Entity types
    public const string EntitySchool = "School";
    public const string EntityRegistrationRequest = "SchoolRegistrationRequest";
    public const string EntityPlatformUser = "PlatformUser";
}