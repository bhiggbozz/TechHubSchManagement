using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using TechSchPlatform.Core;
using TechSchPlatform.Core.Entities;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Service.Services;

public class SchoolService : ISchoolService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SchoolService> _logger;
    private readonly IPlatformAuditService _platformAuditService;
    private readonly IEmailService _emailService;
    private readonly string _connString;

    public SchoolService(
        IConfiguration configuration,
        ILogger<SchoolService> logger,
        IPlatformAuditService platformAuditService,
        IEmailService emailService)
    {
        _configuration = configuration;
        _logger = logger;
        _platformAuditService = platformAuditService;
        _emailService = emailService;
        _connString = configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
    }

    private BaseResponse Ok(string message, object? data = null) => new()
    {
        ResponseCode = ResponseCode.successful,
        ResponseMessage = message,
        Status = "successful",
        Data = data
    };

    private BaseResponse Bad(string message, string code = ResponseCode.BadRequest) => new()
    {
        ResponseCode = code,
        ResponseMessage = message,
        Status = "failed",
        Data = null
    };

    private static string HashPassword(string plainPassword)
    {
        var bytes = Encoding.UTF8.GetBytes(plainPassword);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    private static string GenerateRandomPassword(int length = 12)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var all = upper + lower + digits + symbols;

        var chars = new char[length];
        chars[0] = upper[Random.Shared.Next(upper.Length)];
        chars[1] = lower[Random.Shared.Next(lower.Length)];
        chars[2] = digits[Random.Shared.Next(digits.Length)];
        chars[3] = symbols[Random.Shared.Next(symbols.Length)];
        for (int i = 4; i < length; i++)
            chars[i] = all[Random.Shared.Next(all.Length)];

        return string.Concat(chars.OrderBy(_ => Random.Shared.Next()));
    }

    private static bool IsPlatformRole(string? role) =>
        role == "PlatformAdmin" || role == "PlatformSuperAdmin";

    public async Task<BaseResponse> CreateSchoolAsync(CreateSchoolViewModel model, AuthenticatedUserClaims claims)
    {
        try
        {
            if (claims is null || !Guid.TryParse(claims.UserId, out var platformUserId))
                return Bad("Invalid authentication. A valid platform user token is required.", ResponseCode.Unauthorized);

            if (!IsPlatformRole(claims.Role))
                return Bad("You do not have permission to create a school", ResponseCode.Forbidden);

            if (model is null)
                return Bad("Object is empty");

            if (string.IsNullOrWhiteSpace(model.SchoolName) || string.IsNullOrWhiteSpace(model.Location) ||
                string.IsNullOrWhiteSpace(model.Address) || model.CountryId <= 0 || model.StateId <= 0)
                return Bad("SchoolName, Location, Address, CountryId and StateId are required");

            var schoolId = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO School (Id, CreationDate, ModifiedDate, SchoolName, Location, CountryId, StateId, [State], Address, HasBranch, IsActive, LogoUrl, LogoPublicId, CreatedBy, ModifiedBy)
                    VALUES (@Id, @Now, @Now, @SchoolName, @Location, @CountryId, @StateId, @State, @Address, @HasBranch, @IsActive, @LogoUrl, @LogoPublicId, @CreatedBy, @ModifiedBy)",
                    new
                    {
                        Id = schoolId,
                        Now = now,
                        model.SchoolName,
                        model.Location,
                        model.CountryId,
                        model.StateId,
                        State = (object?)model.State ?? DBNull.Value,
                        model.Address,
                        model.HasBranch,
                        model.ISActive,
                        LogoUrl = string.Empty,
                        LogoPublicId = string.Empty,
                        CreatedBy = platformUserId,
                        ModifiedBy = platformUserId
                    },
                    transaction);

                await conn.ExecuteAsync(@"
                    INSERT INTO SchoolCode (SchoolId, Code)
                    VALUES (@SchoolId, @Code)",
                    new { SchoolId = schoolId, Code = schoolId.ToString() },
                    transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            await _platformAuditService.LogAsync(
                claims,
                PlatformAuditAction.SchoolCreated,
                PlatformAuditAction.EntitySchool,
                schoolId,
                $"School '{model.SchoolName}' created",
                new { model.Location, model.CountryId, model.StateId });

            return Ok("School created successfully", new { SchoolId = schoolId });
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error creating school");
            if (ex.Message.ToLower().Contains("duplicate"))
                return Bad("School information exists", ResponseCode.Conflict);
            return Bad(ex.Message, ResponseCode.ErrorOccured);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating school");
            return Bad(ex.Message, ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> ProvisionSchoolAsync(ProvisionSchoolViewModel model, AuthenticatedUserClaims claims)
    {
        try
        {
            if (!Guid.TryParse(claims.UserId, out var platformUserId))
                return Bad("Invalid authentication", ResponseCode.Unauthorized);

            if (model is null)
                return Bad("Object is empty");

            if (string.IsNullOrWhiteSpace(model.TenantIdentifier))
                return Bad("Tenant identifier is required");

            if (string.IsNullOrWhiteSpace(model.SchoolCode))
                return Bad("School code is required");

            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var schoolId = Guid.NewGuid();
            var adminUserId = Guid.NewGuid();
            var generatedPassword = GenerateRandomPassword();

            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var existingTenant = await conn.QueryFirstOrDefaultAsync<Guid?>(
                    "SELECT TOP 1 Id FROM TenantInfo WHERE Identifier = @Identifier AND IsActive = 1",
                    new { Identifier = model.TenantIdentifier }, transaction);

                if (existingTenant is not null)
                {
                    await transaction.RollbackAsync();
                    return Bad("Tenant identifier already exists", ResponseCode.Conflict);
                }

                var existingCode = await conn.QueryFirstOrDefaultAsync<Guid?>(
                    "SELECT TOP 1 SchoolId FROM SchoolCode WHERE Code = @Code",
                    new { Code = model.SchoolCode }, transaction);

                if (existingCode is not null)
                {
                    await transaction.RollbackAsync();
                    return Bad("School code already exists", ResponseCode.Conflict);
                }

                var existingUser = await conn.QueryFirstOrDefaultAsync<Guid?>(
                    "SELECT TOP 1 Id FROM Users WHERE UserName = @Username",
                    new { Username = model.AdminUsername }, transaction);

                if (existingUser is not null)
                {
                    await transaction.RollbackAsync();
                    return Bad("Admin username already exists", ResponseCode.Conflict);
                }

                // 1. Insert School
                await conn.ExecuteAsync(@"
                    INSERT INTO School (Id, CreationDate, ModifiedDate, SchoolName, Location, CountryId, StateId, [State], Address, HasBranch, LogoUrl, LogoPublicId, IsActive, CreatedBy, ModifiedBy)
                    VALUES (@Id, @Now, @Now, @SchoolName, @Location, @CountryId, @StateId, @State, @Address, @HasBranch, @LogoUrl, @LogoPublicId, 1, @CreatedBy, @ModifiedBy)",
                    new
                    {
                        Id = schoolId,
                        Now = now,
                        model.SchoolName,
                        model.Location,
                        model.CountryId,
                        model.StateId,
                        State = (object?)model.State ?? DBNull.Value,
                        model.Address,
                        model.HasBranch,
                        LogoUrl = string.Empty,
                        LogoPublicId = string.Empty,
                        CreatedBy = platformUserId,
                        ModifiedBy = platformUserId
                    },
                    transaction);

                // 2. Insert SchoolCode
                await conn.ExecuteAsync(@"
                    INSERT INTO SchoolCode (SchoolId, Code)
                    VALUES (@SchoolId, @Code)",
                    new { SchoolId = schoolId, Code = model.SchoolCode },
                    transaction);

                // 3. Insert TenantInfo
                await conn.ExecuteAsync(@"
                    INSERT INTO TenantInfo (Id, SchoolId, Identifier, IsActive, ConnectionString, CreatedDate, ModifiedDate)
                    VALUES (@Id, @SchoolId, @Identifier, 1, NULL, @Now, @Now)",
                    new { Id = Guid.NewGuid(), SchoolId = schoolId, Identifier = model.TenantIdentifier, Now = DateTime.UtcNow },
                    transaction);

                // 4. Insert Admin User (Administrator role - RoleId = 2)
                var passwordHash = HashPassword(generatedPassword);
                await conn.ExecuteAsync(@"
                    INSERT INTO Users (Id, CreationDate, ModifiedDate, FirstName, MiddleName, LastName, EmailAddress, HashPassword,
                        IsActive, HasAccess, UserName, SchoolId, RoleId, CreatedBy)
                    VALUES (@Id, @Now, @Now, @FirstName, @MiddleName, @LastName, @Email, @PasswordHash,
                        1, 1, @Username, @SchoolId, 2, @CreatedBy)",
                    new
                    {
                        Id = adminUserId,
                        Now = now,
                        FirstName = model.AdminFirstName,
                        MiddleName = (object?)model.AdminMiddleName ?? DBNull.Value,
                        LastName = model.AdminLastName,
                        Email = model.AdminEmail,
                        PasswordHash = passwordHash,
                        Username = model.AdminUsername,
                        SchoolId = schoolId,
                        CreatedBy = platformUserId
                    },
                    transaction);

                // 5. Insert AdminPermissions (FullAdmin = 127)
                await conn.ExecuteAsync(@"
                    INSERT INTO AdminPermissions (Id, UserId, SchoolId, Permissions, CreationDate, ModifiedDate, CreatedBy, IsActive)
                    VALUES (@Id, @UserId, @SchoolId, 127, @Now, @Now, @CreatedBy, 1)",
                    new
                    {
                        Id = Guid.NewGuid(),
                        UserId = adminUserId,
                        SchoolId = schoolId,
                        Now = now,
                        CreatedBy = adminUserId
                    },
                    transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var subject = $"Welcome to {model.SchoolName} - TechHub";
                    var body = BuildWelcomeEmail(
                        model.AdminFirstName,
                        model.SchoolName,
                        model.SchoolCode,
                        model.TenantIdentifier,
                        model.Location,
                        model.AdminUsername,
                        generatedPassword);
                    await _emailService.SendAsync(model.AdminEmail, $"{model.AdminFirstName} {model.AdminLastName}", subject, body);
                    _logger.LogInformation("Welcome email sent to {Email} for school {School}", model.AdminEmail, model.SchoolName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send welcome email for school {School}", model.SchoolName);
                }
            });

            _logger.LogInformation(
                "School provisioned - SchoolId: {SchoolId}, Code: {Code}, Name: {Name}, Tenant: {Tenant}, Admin: {Admin}",
                schoolId, model.SchoolCode, model.SchoolName, model.TenantIdentifier, model.AdminUsername);

            return Ok("School provisioned successfully", new
            {
                SchoolId = schoolId,
                SchoolName = model.SchoolName,
                SchoolCode = model.SchoolCode,
                TenantIdentifier = model.TenantIdentifier,
                LogoUrl = model.LogoUrl,
                AdminUserId = adminUserId,
                AdminUsername = model.AdminUsername,
                AdminEmail = model.AdminEmail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning school");
            return Bad("An error occurred while provisioning school", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> SubmitRegistrationRequestAsync(SchoolRegistrationRequestViewModel model)
    {
        try
        {
            if (model is null)
                return Bad("Object is empty");

            if (string.IsNullOrWhiteSpace(model.TenantIdentifier))
                return Bad("Tenant identifier is required");

            if (string.IsNullOrWhiteSpace(model.SchoolCode))
                return Bad("School code is required");

            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            var existingTenant = await conn.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT TOP 1 Id FROM TenantInfo WHERE Identifier = @Identifier AND IsActive = 1",
                new { Identifier = model.TenantIdentifier });

            if (existingTenant is not null)
                return Bad("Tenant identifier already exists", ResponseCode.Conflict);

            var existingCode = await conn.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT TOP 1 SchoolId FROM SchoolCode WHERE Code = @Code",
                new { Code = model.SchoolCode });

            if (existingCode is not null)
                return Bad("School code already exists", ResponseCode.Conflict);

            var existingUser = await conn.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT TOP 1 Id FROM Users WHERE UserName = @Username",
                new { Username = model.AdminUsername });

            if (existingUser is not null)
                return Bad("Username already exists", ResponseCode.Conflict);

            await conn.ExecuteAsync(@"
                INSERT INTO SchoolRegistrationRequest
                    (Id, SchoolName, Location, CountryId, StateId, [State], Address, HasBranch, TenantIdentifier, SchoolCode,
                     LogoUrl, LogoPublicId, AdminFirstName, AdminMiddleName, AdminLastName, AdminEmail, AdminUsername,
                     AdminPassword, [Status], CreatedAt)
                VALUES
                    (@Id, @SchoolName, @Location, @CountryId, @StateId, @State, @Address, @HasBranch, @TenantIdentifier, @SchoolCode,
                     @LogoUrl, @LogoPublicId, @AdminFirstName, @AdminMiddleName, @AdminLastName, @AdminEmail, @AdminUsername,
                     '', 'Pending', @CreatedAt)",
                new
                {
                    Id = Guid.NewGuid(),
                    model.SchoolName,
                    model.Location,
                    model.CountryId,
                    model.StateId,
                    State = (object?)model.State ?? DBNull.Value,
                    model.Address,
                    model.HasBranch,
                    model.TenantIdentifier,
                    model.SchoolCode,
                    LogoUrl = (object?)model.LogoUrl ?? DBNull.Value,
                    LogoPublicId = (object?)model.LogoPublicId ?? DBNull.Value,
                    model.AdminFirstName,
                    AdminMiddleName = (object?)model.AdminMiddleName ?? DBNull.Value,
                    model.AdminLastName,
                    model.AdminEmail,
                    model.AdminUsername,
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });

            _logger.LogInformation("School registration request submitted - School: {Name}, Code: {Code}", model.SchoolName, model.SchoolCode);

            return Ok("Registration request submitted successfully. Awaiting approval.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting school registration request");
            return Bad("An error occurred while submitting registration request", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetRegistrationRequestsAsync(string? statusFilter)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            IEnumerable<SchoolRegistrationRequest> results;

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                results = await conn.QueryAsync<SchoolRegistrationRequest>(
                    "SELECT * FROM SchoolRegistrationRequest WHERE Status = @Status ORDER BY CreatedAt DESC",
                    new { Status = statusFilter });
            }
            else
            {
                results = await conn.QueryAsync<SchoolRegistrationRequest>(
                    "SELECT * FROM SchoolRegistrationRequest ORDER BY CreatedAt DESC");
            }

            var mapped = results.Select(r => new RegistrationRequestResponse
            {
                Id = r.Id,
                SchoolName = r.SchoolName,
                Location = r.Location,
                Address = r.Address,
                TenantIdentifier = r.TenantIdentifier,
                SchoolCode = r.SchoolCode,
                LogoUrl = r.LogoUrl,
                LogoPublicId = r.LogoPublicId,
                AdminFirstName = r.AdminFirstName,
                AdminLastName = r.AdminLastName,
                AdminEmail = r.AdminEmail,
                AdminUsername = r.AdminUsername,
                Status = r.Status,
                RejectionReason = r.RejectionReason,
                CreatedAt = r.CreatedAt,
                RespondedAt = r.RespondedAt
            }).ToList();

            return Ok("Registration requests retrieved", mapped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving registration requests");
            return Bad("An error occurred", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> ApproveRegistrationRequestAsync(Guid requestId, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.UserId, out var platformUserId))
            return Bad("Invalid authentication", ResponseCode.Unauthorized);

        if (!IsPlatformRole(claims.Role))
            return Bad("You do not have permission to approve school registration", ResponseCode.Forbidden);

        using var conn = new SqlConnection(_connString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            var request = await conn.QueryFirstOrDefaultAsync<SchoolRegistrationRequest>(
                "SELECT * FROM SchoolRegistrationRequest WHERE Id = @Id",
                new { Id = requestId }, transaction);

            if (request is null)
            {
                await transaction.RollbackAsync();
                return Bad("Registration request not found", ResponseCode.NotFound);
            }

            if (request.Status != "Pending")
            {
                await transaction.RollbackAsync();
                return Bad($"Request already {request.Status}", ResponseCode.BadRequest);
            }

            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var schoolId = Guid.NewGuid();
            var adminUserId = Guid.NewGuid();

            // 1. Insert School
            await conn.ExecuteAsync(@"
                INSERT INTO School (Id, CreationDate, ModifiedDate, SchoolName, Location, CountryId, StateId, [State], Address, HasBranch, LogoUrl, LogoPublicId, IsActive, CreatedBy, ModifiedBy)
                VALUES (@Id, @Now, @Now, @SchoolName, @Location, @CountryId, @StateId, @State, @Address, @HasBranch, @LogoUrl, @LogoPublicId, 1, @CreatedBy, @ModifiedBy)",
                new
                {
                    Id = schoolId,
                    Now = now,
                    request.SchoolName,
                    request.Location,
                    request.CountryId,
                    request.StateId,
                    State = (object?)request.State ?? DBNull.Value,
                    request.Address,
                    request.HasBranch,
                    LogoUrl = string.Empty,
                    LogoPublicId = string.Empty,
                    CreatedBy = platformUserId,
                    ModifiedBy = platformUserId
                },
                transaction);

            // 2. Insert SchoolCode
            await conn.ExecuteAsync(@"
                INSERT INTO SchoolCode (SchoolId, Code)
                VALUES (@SchoolId, @Code)",
                new { SchoolId = schoolId, Code = request.SchoolCode },
                transaction);

            // 3. Insert TenantInfo
            await conn.ExecuteAsync(@"
                INSERT INTO TenantInfo (Id, SchoolId, Identifier, IsActive, ConnectionString, CreatedDate, ModifiedDate)
                VALUES (@Id, @SchoolId, @Identifier, 1, NULL, @Now, @Now)",
                new { Id = Guid.NewGuid(), SchoolId = schoolId, Identifier = request.TenantIdentifier, Now = DateTime.UtcNow },
                transaction);

            // 4. Insert Admin User (SuperAdministrator role - RoleId = 3)
            var generatedPassword = GenerateRandomPassword();
            var passwordHash = HashPassword(generatedPassword);
            await conn.ExecuteAsync(@"
                INSERT INTO Users (Id, CreationDate, ModifiedDate, FirstName, MiddleName, LastName, EmailAddress, HashPassword,
                    IsActive, HasAccess, UserName, SchoolId, RoleId, CreatedBy)
                VALUES (@Id, @Now, @Now, @FirstName, @MiddleName, @LastName, @Email, @PasswordHash,
                    1, 1, @Username, @SchoolId, 3, @CreatedBy)",
                new
                {
                    Id = adminUserId,
                    Now = now,
                    FirstName = request.AdminFirstName,
                    MiddleName = (object?)request.AdminMiddleName ?? DBNull.Value,
                    LastName = request.AdminLastName,
                    Email = request.AdminEmail,
                    PasswordHash = passwordHash,
                    Username = request.AdminUsername,
                    SchoolId = schoolId,
                    CreatedBy = platformUserId
                },
                transaction);

            // 5. Insert AdminPermissions (FullAdmin = 127)
            await conn.ExecuteAsync(@"
                INSERT INTO AdminPermissions (Id, UserId, SchoolId, Permissions, CreationDate, ModifiedDate, CreatedBy, IsActive)
                VALUES (@Id, @UserId, @SchoolId, 127, @Now, @Now, @CreatedBy, 1)",
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUserId,
                    SchoolId = schoolId,
                    Now = now,
                    CreatedBy = adminUserId
                },
                transaction);

            // 6. Update request status
            await conn.ExecuteAsync(@"
                UPDATE SchoolRegistrationRequest SET Status = 'Approved', ApprovedBy = @ApprovedBy, RespondedAt = @RespondedAt
                WHERE Id = @Id",
                new { Id = requestId, ApprovedBy = platformUserId, RespondedAt = DateTime.UtcNow },
                transaction);

            await transaction.CommitAsync();

            _logger.LogInformation("School registration approved - School: {Name}, Code: {Code}", request.SchoolName, request.SchoolCode);

            await _platformAuditService.LogAsync(
                claims,
                PlatformAuditAction.SchoolApproved,
                PlatformAuditAction.EntitySchool,
                schoolId,
                $"School '{request.SchoolName}' approved and provisioned (code: {request.SchoolCode})",
                new { request.SchoolCode, request.TenantIdentifier, AdminUserId = adminUserId });

            _ = Task.Run(async () =>
            {
                try
                {
                    var subject = $"Welcome to {request.SchoolName} - TechHub";
                    var body = BuildWelcomeEmail(
                        request.AdminFirstName,
                        request.SchoolName,
                        request.SchoolCode,
                        request.TenantIdentifier,
                        request.Location,
                        request.AdminUsername,
                        generatedPassword);
                    await _emailService.SendAsync(request.AdminEmail, $"{request.AdminFirstName} {request.AdminLastName}", subject, body);
                    _logger.LogInformation("Welcome email sent to {Email} for school {School}", request.AdminEmail, request.SchoolName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send welcome email for school {School}", request.SchoolName);
                }
            });

            return Ok("School registration approved and provisioned successfully", new
            {
                SchoolId = schoolId,
                SchoolName = request.SchoolName,
                SchoolCode = request.SchoolCode,
                TenantIdentifier = request.TenantIdentifier,
                AdminUserId = adminUserId,
                AdminUsername = request.AdminUsername
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error approving school registration");
            return Bad($"An error occurred while approving registration: {ex.Message}", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> RejectRegistrationRequestAsync(Guid requestId, string reason, AuthenticatedUserClaims claims)
    {
        if (!Guid.TryParse(claims.UserId, out var platformUserId))
            return Bad("Invalid authentication", ResponseCode.Unauthorized);

        if (!IsPlatformRole(claims.Role))
            return Bad("You do not have permission to reject school registration", ResponseCode.Forbidden);

        try
        {
            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            var request = await conn.QueryFirstOrDefaultAsync<SchoolRegistrationRequest>(
                "SELECT * FROM SchoolRegistrationRequest WHERE Id = @Id",
                new { Id = requestId });

            if (request is null)
                return Bad("Registration request not found", ResponseCode.NotFound);

            if (request.Status != "Pending")
                return Bad($"Request already {request.Status}", ResponseCode.BadRequest);

            await conn.ExecuteAsync(@"
                UPDATE SchoolRegistrationRequest SET Status = 'Rejected', RejectionReason = @Reason, ApprovedBy = @ApprovedBy, RespondedAt = @RespondedAt
                WHERE Id = @Id",
                new { Id = requestId, Reason = reason, ApprovedBy = platformUserId, RespondedAt = DateTime.UtcNow });

            _logger.LogInformation("School registration rejected - School: {Name}, Code: {Code}, Reason: {Reason}",
                request.SchoolName, request.SchoolCode, reason);

            await _platformAuditService.LogAsync(
                claims,
                PlatformAuditAction.SchoolRejected,
                PlatformAuditAction.EntitySchool,
                requestId,
                $"School registration '{request.SchoolName}' rejected",
                new { request.SchoolCode, reason });

            return Ok("Registration request rejected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting school registration");
            return Bad("An error occurred", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> EditSchoolInfoAsync(Guid schoolId, SchoolEditViewModel model, AuthenticatedUserClaims claims)
    {
        try
        {
            if (claims is null || !Guid.TryParse(claims.UserId, out _))
                return Bad("Invalid authentication", ResponseCode.Unauthorized);

            if (!IsPlatformRole(claims.Role))
                return Bad("You do not have permission to edit school info", ResponseCode.Forbidden);

            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            var school = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id, SchoolName, Location, CountryId, StateId, [State], Address, HasBranch, ISActive, LogoUrl, LogoPublicId FROM School WHERE Id = @Id",
                new { Id = schoolId });

            if (school is null)
                return Bad("School not found", ResponseCode.NotFound);

            var before = new
            {
                school.SchoolName,
                school.Location,
                school.CountryId,
                school.StateId,
                school.State,
                school.Address,
                school.HasBranch,
                school.ISActive,
                school.LogoUrl,
                school.LogoPublicId
            };

            var now = DateTime.UtcNow;
            var actorId = Guid.TryParse(claims.UserId, out var parsedActorId) ? (Guid?)parsedActorId : null;

            await conn.ExecuteAsync(@"
                UPDATE School SET
                    SchoolName = COALESCE(@SchoolName, SchoolName),
                    Location = COALESCE(@Location, Location),
                    CountryId = @CountryId,
                    StateId = @StateId,
                    [State] = COALESCE(@State, [State]),
                    Address = COALESCE(@Address, Address),
                    HasBranch = @HasBranch,
                    ISActive = @IsActive,
                    LogoUrl = COALESCE(@LogoUrl, LogoUrl),
                    LogoPublicId = COALESCE(@LogoPublicId, LogoPublicId),
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = @Now
                WHERE Id = @Id",
                new
                {
                    Id = schoolId,
                    SchoolName = model.SchoolName,
                    Location = model.Location,
                    CountryId = model.CountryId,
                    StateId = model.StateId,
                    State = model.State,
                    Address = model.Address,
                    HasBranch = model.HasBranch ?? before.HasBranch,
                    IsActive = model.IsActive ?? before.ISActive,
                    LogoUrl = model.LogoUrl,
                    LogoPublicId = model.LogoPublicId,
                    ModifiedBy = actorId,
                    Now = now
                });

            _logger.LogInformation("School info edited - School: {SchoolId} by {UserId}", schoolId, claims.UserId);

            await _platformAuditService.LogAsync(
                claims,
                PlatformAuditAction.SchoolEdited,
                PlatformAuditAction.EntitySchool,
                schoolId,
                $"School info updated ({school.SchoolName})",
                new { Before = before });

            return Ok("School info updated successfully", new
            {
                SchoolId = schoolId,
                SchoolName = model.SchoolName ?? (string)school.SchoolName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing school info - SchoolId: {SchoolId}", schoolId);
            return Bad("An error occurred while editing school info", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetAllSchoolsWithStatusAsync()
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            var sql = @"
                SELECT
                    NULL AS SchoolId,
                    r.Id AS RequestId,
                    r.SchoolName,
                    r.Status
                FROM SchoolRegistrationRequest r
                WHERE r.Status != 'Approved'

                UNION ALL

                SELECT
                    s.Id AS SchoolId,
                    NULL AS RequestId,
                    s.SchoolName,
                    'Approved' AS Status
                FROM School s
                WHERE s.ISActive = 1
                ORDER BY SchoolName";

            var results = await conn.QueryAsync<SchoolStatusDto>(sql);
            return Ok("Schools retrieved", results.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schools with status");
            return Bad("An error occurred", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetPendingSchoolIdsAsync()
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            var sql = @"SELECT Id AS RequestId, SchoolName FROM SchoolRegistrationRequest WHERE Status = 'Pending' ORDER BY CreatedAt DESC";

            var results = await conn.QueryAsync<dynamic>(sql);
            return Ok("Pending schools retrieved", results.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending school IDs");
            return Bad("An error occurred", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetSchoolApprovalStatusAsync(Guid schoolId)
    {
        try
        {
            using var conn = new SqlConnection(_connString);

            var school = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id, SchoolName, ISActive FROM School WHERE Id = @Id",
                new { Id = schoolId });

            if (school is null)
                return Bad("School not found", ResponseCode.NotFound);

            var request = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Status, RejectionReason FROM SchoolRegistrationRequest WHERE SchoolName = @SchoolName ORDER BY CreatedAt DESC",
                new { SchoolName = (string)school.SchoolName });

            var status = request is not null ? (string)request.Status : "Approved";
            var rejectionReason = request is not null ? (string?)request.RejectionReason : null;

            return Ok("School approval status retrieved", new
            {
                SchoolId = (Guid)school.Id,
                SchoolName = (string)school.SchoolName,
                Status = status,
                RejectionReason = rejectionReason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching school approval status for {SchoolId}", schoolId);
            return Bad("An error occurred", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetStatesAsync(int countryId)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            var states = await conn.QueryAsync<State>(
                "SELECT Id, States, CountryId FROM State WHERE CountryId = @CountryId ORDER BY States",
                new { CountryId = countryId });

            if (!states.Any())
                return Bad("No State for this country");

            return Ok("successful", states.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching states");
            return Bad(ex.Message, ResponseCode.ErrorOccured);
        }
    }

    private static string BuildWelcomeEmail(
        string firstName,
        string schoolName,
        string schoolCode,
        string tenantIdentifier,
        string location,
        string adminUsername,
        string generatedPassword)
    {
        return $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>School Created Successfully</h2>
                <p>Dear {firstName},</p>
                <p>Your school <strong>{schoolName}</strong> has been created on TechHub.</p>
                <h3>School Details</h3>
                <ul>
                    <li><strong>School:</strong> {schoolName}</li>
                    <li><strong>School Code:</strong> {schoolCode}</li>
                    <li><strong>Tenant ID:</strong> {tenantIdentifier}</li>
                    <li><strong>Location:</strong> {location}</li>
                </ul>
                <p style='margin-top: 24px;'>
                    <a href='https://{tenantIdentifier}.bluetsch.com'
                       style='background-color: #2563eb; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                        Go to Your School Portal
                    </a>
                </p>
                <p style='color: #64748b; font-size: 12px;'>Or paste this link into your browser:<br/>
                    https://{tenantIdentifier}.bluetsch.com</p>
                <h3>Admin Login Credentials</h3>
                <ul>
                    <li><strong>Username:</strong> {adminUsername}</li>
                    <li><strong>Password:</strong> {generatedPassword}</li>
                </ul>
                <p>Please log in and change your password on first login.</p>
                <p>Best regards,<br/>TechHub Platform Team</p>
            </body>
            </html>";
    }
}