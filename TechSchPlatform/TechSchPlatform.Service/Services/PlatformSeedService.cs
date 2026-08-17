using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TechSchPlatform.Core.Enum;

namespace TechSchPlatform.Service.Services;

/// <summary>
/// Ensures the seeded PlatformSuperAdmin account (username: platformadmin) can
/// actually log in. The DB migration seed stores a placeholder password hash,
/// so on startup we upsert the account with the correct SHA256 hash of
/// "Platform@123". Idempotent and safe to run on every boot.
/// </summary>
public class PlatformSeedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformSeedService> _logger;

    public PlatformSeedService(IConfiguration configuration, ILogger<PlatformSeedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EnsureSuperAdminExistsAsync(CancellationToken cancellationToken = default)
    {
        const string username = "platformadmin";
        const string password = "Platform@123";

        try
        {
            var connString = _configuration.GetConnectionString("DbConnectionString");
            if (string.IsNullOrEmpty(connString))
            {
                _logger.LogWarning("DbConnectionString not configured; skipping platform seed.");
                return;
            }

            var passwordHash = PlatformAuthService.HashPassword(password);
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var role = PlatformRole.PlatformSuperAdmin.ToString();

            using var conn = new SqlConnection(connString);
            await conn.OpenAsync(cancellationToken);

            var existing = await conn.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT TOP 1 Id FROM PlatformUser WHERE Username = @Username",
                new { Username = username });

            if (existing is not null)
            {
                var changed = await conn.ExecuteAsync(@"
                    UPDATE PlatformUser
                    SET PasswordHash = @PasswordHash, Role = @Role, IsActive = 1, IsDeleted = 0, ModifiedAt = @Now
                    WHERE Id = @Id",
                    new { Id = existing, PasswordHash = passwordHash, Role = role, Now = now });

                _logger.LogInformation(changed > 0
                    ? "Platform seed: updated platformadmin account ({Role})."
                    : "Platform seed: platformadmin already up to date.",
                    role);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO PlatformUser (Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive, IsDeleted, CreatedBy, CreatedAt, ModifiedAt)
                    VALUES (@Id, 'Platform', 'Admin', 'platformadmin@bluetsch.com', @Username, @PasswordHash, @Role, 1, 0, NULL, @Now, @Now)",
                    new { Id = Guid.NewGuid(), Username = username, PasswordHash = passwordHash, Role = role, Now = now });

                _logger.LogInformation("Platform seed: created platformadmin account ({Role}).", role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform seed failed. platformadmin login may not work.");
        }
    }
}