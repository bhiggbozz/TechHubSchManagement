using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TechSchPlatform API",
            Version = "v1",
            Description = "Platform administration API for TechHub — school registration, provisioning, platform user management, login history and audit trail."
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT from POST /api/PlatformAuth/login. Example: eyJhbGci..."
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

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

    app.UseSwagger();
    app.UseSwaggerUI();

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