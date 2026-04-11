//using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using TechHub.Background.Jobs;
using TechHub.BackgroundJobs.Interfaces;
using TechHub.BackgroundJobs.Jobs;
using TechHub.BackgroundJobs.Services;
using TechHub.Core.Utilities;
using TechHub.QuestionBank.Services;
using TechHub.Service.Interface;
using TechHub.Service.Service;
using TechHub.Service.Service.DatabaseService;
using TechHub.Service.util;
using TechhubMS.Middleware.Interface;
using TechhubMS.Middleware.Services;
using UserService = TechHub.Service.Service.UserService;

namespace TechhubMS
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMultiTenantServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register HttpContextAccessor
            services.AddHttpContextAccessor();

			// Register services
			//services.AddScoped<ITenantService, TenantService>();
			services.AddScoped<IUtilities, Utilities>();

			services.AddScoped<IEmailService, EmailService>();
			services.AddScoped(typeof(ICommandRespository<>), typeof(CommandRepositoryService<>));
			services.AddScoped(typeof(IQueryRepository<>), typeof(QueryRepositoryService<>));
			services.AddScoped<ITenantService, TenantService>();
			services.AddSingleton<Serilog.ILogger>(Log.Logger);

			services.AddScoped<ISchoolService, SchoolService>();
			services.AddScoped<IUserService, UserService>();
			services.AddScoped<IDbTransactionScopeFactory, DbTransactionScopeFactory>();
			services.AddScoped<AuthService>();
			services.AddScoped<IAdminApprovalService, AdminApprovalService>();
			services.AddScoped<ITeacherTrustScoreService, TeacherTrustScoreService>();
			//services.AddScoped<IAdminPermissionsService, AdminPermissionsService>();

			//background jobs
			services.AddScoped<ICloudinaryService, CloudinaryService>();

			services.AddScoped<MediaUploadJob>();
			services.AddScoped<MediaCleanupJob>();

			services.AddScoped<IMediaService, MediaService>();
			services.AddScoped<IClassPreparationService, ClassPreparationService>();
			services.AddScoped<IBackgroundJobService, BackgroundJobService>();

			services.AddScoped<IEmailService, EmailService>();
			//services.AddScoped<IRefreshTokenService, RefreshTokenService>();


			//services.AddScoped<IAdminPermissionsService, AdminPermissionsService>();

			services.AddSingleton<IConnectionStringResolver, ConnectionStringResolver> ();
			services.AddSingleton<JwtTokenGenerator>();

			// Register QuestionBank module
			services.AddQuestionBankServices();


			var jwtSettings = configuration.GetSection("Jwt");
			var secretKey = jwtSettings["SecretKey"];

			if (string.IsNullOrEmpty(secretKey))
			{
				throw new InvalidOperationException(
					"JWT SecretKey is not configured. Please add Jwt:SecretKey to appsettings.json");
			}
			// JWT Authentication
			services.AddAuthentication(options =>
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
					IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"])),
                    ClockSkew = TimeSpan.Zero
                };
            });

			services.AddCors(options =>
			{
				options.AddPolicy("MultiTenantCors", builder =>
				{
					builder
						.SetIsOriginAllowed(origin =>
						{
							if (string.IsNullOrWhiteSpace(origin))
								return false;

							var uri = new Uri(origin);
							var host = uri.Host;

							if (host.EndsWith(".vluethub.com"))
								return true;

							if (host.EndsWith(".localhost") || host == "localhost")
								return true;

							var allowedOrigins = configuration
								.GetSection("Cors:AllowedOrigins")
								.Get<string[]>() ?? Array.Empty<string>();

							return allowedOrigins.Contains(origin);
						})
						.AllowAnyMethod()
						.AllowAnyHeader()
						.AllowCredentials(); 
				});
			});
			return services;
        }
    }
}
