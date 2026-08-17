using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using TechSchPlatform.Api.Middleware;
using TechSchPlatform.Service.Interfaces;
using TechSchPlatform.Service.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console());

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── Services ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<IPlatformAuthService, PlatformAuthService>();
    builder.Services.AddScoped<IPlatformAdminService, PlatformAdminService>();
    builder.Services.AddScoped<IPlatformAuditService, PlatformAuditService>();
    builder.Services.AddScoped<ISchoolService, SchoolService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddSingleton<PlatformSeedService>();

    // ── JWT Authentication ────────────────────────────────────────────────
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"];

    if (string.IsNullOrEmpty(secretKey))
    {
        throw new InvalidOperationException(
            "JWT SecretKey is not configured. Please add Jwt:SecretKey to appsettings.json");
    }

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

    // ── CORS ──────────────────────────────────────────────────────────────
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("PlatformCors", policy =>
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                    return false;

                var host = new Uri(origin).Host;

                if (host.EndsWith(".bluetsch.com"))
                    return true;

                if (host == "localhost" || host.EndsWith(".localhost"))
                    return true;

                return allowedOrigins.Contains(origin);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    // ── Startup seed: ensure platformadmin login works ─────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var seed = scope.ServiceProvider.GetRequiredService<PlatformSeedService>();
        await seed.EnsureSuperAdminExistsAsync();
    }

    // ── Pipeline ───────────────────────────────────────────────────────────
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ResponseCodeMiddleware>();
    app.UseHttpsRedirection();
    app.UseCors("PlatformCors");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TechSchPlatform failed to start");
}
finally
{
    Log.CloseAndFlush();
}