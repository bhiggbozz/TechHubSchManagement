//using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using TechHub.Background.Jobs;
using TechHub.BackgroundJobs.Interfaces;
using TechHub.BackgroundJobs.Jobs;
using TechHub.BackgroundJobs.Services;
using TechHub.Core.Configuration;
using TechHub.Core.Entities;
using TechHub.Core.Interface;
using TechHub.Core.Utilities;
using TechHub.QuestionBank.Services;
using TechHub.Service.Interface;
using TechHub.Service.Repository;
using TechHub.Service.Service;
using TechHub.Service.Service.DatabaseService;
using TechHub.Service.Service.ImageGeneration;
using TechHub.Service.util;
using TechhubMS.Middleware.Interface;
using TechhubMS.Middleware.Services;
using UserService = TechHub.Service.Service.UserService;

namespace TechhubMS
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMultiTenantServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
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
			services.AddScoped<IQuizService, QuizService>();
			//services.AddScoped<IAdminPermissionsService, AdminPermissionsService>();

			//background jobs
			services.AddScoped<ICloudinaryService, CloudinaryService>();

			services.AddScoped<MediaUploadJob>();
			services.AddScoped<MediaCleanupJob>();
			services.AddScoped<LessonImageGenerationJob>();

			services.AddScoped<IMediaService, MediaService>();
			services.AddScoped<IClassPreparationService, ClassPreparationService>();
			services.AddScoped<IAttendanceService, AttendanceService>();
			services.AddScoped<IBackgroundJobService, BackgroundJobService>();

			services.AddScoped<IEmailService, EmailService>();
			services.AddScoped<ILessonService, LessonService>();
			services.AddScoped<IGroupService, GroupService>();
			services.AddScoped<INotificationService, NotificationService>();
			services.AddScoped<IPerformanceAggregationService, PerformanceAggregationService>();
			services.AddScoped<IPerformanceDashboardService, PerformanceDashboardService>();
			services.AddScoped<IPerformanceIncrementalService, PerformanceIncrementalService>();
			services.AddSingleton<IPerformanceRepository, PerformanceRepository>();
			//services.AddScoped<IRefreshTokenService, RefreshTokenService>();


			//services.AddScoped<IAdminPermissionsService, AdminPermissionsService>();

			services.AddSingleton<IConnectionStringResolver, ConnectionStringResolver> ();
			services.AddSingleton<JwtTokenGenerator>();

			services.AddScoped<IQueryRepository<StudentClassroom>, QueryRepositoryService<StudentClassroom>>();
			services.AddScoped<IQueryRepository<LessonContent>, QueryRepositoryService<LessonContent>>();
			services.AddScoped<ICommandRespository<SchoolRegistrationRequest>, CommandRepositoryService<SchoolRegistrationRequest>>();
			services.AddScoped<IQueryRepository<SchoolRegistrationRequest>, QueryRepositoryService<SchoolRegistrationRequest>>();

			// Quiz module repositories
			services.AddScoped<IQueryRepository<QuizAttempt>, QueryRepositoryService<QuizAttempt>>();
			services.AddScoped<ICommandRespository<QuizAttempt>, CommandRepositoryService<QuizAttempt>>();
			services.AddScoped<IQueryRepository<QuizAttemptAnswer>, QueryRepositoryService<QuizAttemptAnswer>>();
			services.AddScoped<ICommandRespository<QuizAttemptAnswer>, CommandRepositoryService<QuizAttemptAnswer>>();
			services.AddScoped<IQueryRepository<QuizConfig>, QueryRepositoryService<QuizConfig>>();
			services.AddScoped<ICommandRespository<QuizConfig>, CommandRepositoryService<QuizConfig>>();

			// Assessment module repositories
			services.AddScoped<ICommandRespository<Assessments>, CommandRepositoryService<Assessments>>();
			services.AddScoped<ICommandRespository<AssessmentConfig>, CommandRepositoryService<AssessmentConfig>>();
			services.AddScoped<ICommandRespository<AssessmentQuestion>, CommandRepositoryService<AssessmentQuestion>>();
			services.AddScoped<ICommandRespository<AssessmentAssignment>, CommandRepositoryService<AssessmentAssignment>>();
			services.AddScoped<ICommandRespository<AssessmentAttempt>, CommandRepositoryService<AssessmentAttempt>>();
			services.AddScoped<ICommandRespository<AssessmentAttemptAnswer>, CommandRepositoryService<AssessmentAttemptAnswer>>();
			services.AddScoped<IQueryRepository<Assessments>, QueryRepositoryService<Assessments>>();
			services.AddScoped<IQueryRepository<AssessmentConfig>, QueryRepositoryService<AssessmentConfig>>();
			services.AddScoped<IQueryRepository<AssessmentAttempt>, QueryRepositoryService<AssessmentAttempt>>();
			services.AddScoped<IQueryRepository<AssessmentAttemptAnswer>, QueryRepositoryService<AssessmentAttemptAnswer>>();
			services.AddScoped<IAssessmentService, AssessmentService>();
			services.AddScoped<IStudentDashboardService, StudentDashboardService>();
			services.AddScoped<IAdminDashboardService, AdminDashboardService>();
			services.AddSingleton<IAdminDashboardRepository, AdminDashboardRepository>();
			services.AddScoped<IPlatformAuthService, PlatformAuthService>();
			services.AddScoped<IPlatformAdminService, PlatformAdminService>();
			services.AddScoped<IPlatformAuditService, PlatformAuditService>();

			// Register QuestionBank module
			services.AddQuestionBankServices();

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// AI IMAGE GENERATION MODULE
			// Feature-gated per school via the SchoolFeature table.
			// The active agent is selected by ImageGeneration:Provider,
			// so providers are swappable through configuration alone.
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			services.Configure<ImageGenerationSettings>(
				configuration.GetSection(ImageGenerationSettings.SectionName));
			services.AddHttpClient("ImageGeneration");

			// Claude-backed instructional prompt refinement — redefines the
			// lesson's draft image prompt (aim + objectives + teacher materials)
			// before it is sent to the image agent. Falls back to the draft if
			// the Anthropic key is missing or the call fails.
			services.Configure<AnthropicSettings>(
				configuration.GetSection(AnthropicSettings.SectionName));
			services.AddScoped<IInstructionalPromptRefiner, ClaudeInstructionalPromptRefiner>();

			services.AddScoped<IImageGenerationAgent, StabilityImageGenerationAgent>();
			services.AddScoped<IImageGenerationAgent, OpenAiImageGenerationAgent>();
			services.AddScoped<IImageGenerationAgentFactory, ImageGenerationAgentFactory>();
			services.AddScoped<IImageGenerationService, ImageGenerationService>();
			services.AddScoped<ISchoolFeatureService, SchoolFeatureService>();

			services.AddScoped<IQueryRepository<SchoolFeature>, QueryRepositoryService<SchoolFeature>>();
			services.AddScoped<ICommandRespository<SchoolFeature>, CommandRepositoryService<SchoolFeature>>();
			services.AddScoped<IQueryRepository<LessonGenerationPrompt>, QueryRepositoryService<LessonGenerationPrompt>>();
			services.AddScoped<ICommandRespository<LessonGenerationPrompt>, CommandRepositoryService<LessonGenerationPrompt>>();
			services.AddScoped<IQueryRepository<LessonMedia>, QueryRepositoryService<LessonMedia>>();
			services.AddScoped<ICommandRespository<LessonMedia>, CommandRepositoryService<LessonMedia>>();


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

							// ── Production: strict ──────────────────────────────────
							// Only bluetsch.com school subdomains, localhost, and the
							// explicit AllowedOrigins list are accepted.
							if (env.IsProduction())
							{
								if (host.EndsWith(".bluetsch.com"))
									return true;

								if (host == "localhost" || host.EndsWith(".localhost"))
									return true;

								var prodAllowedOrigins = configuration
									.GetSection("Cors:AllowedOrigins")
									.Get<string[]>() ?? Array.Empty<string>();

								return prodAllowedOrigins.Contains(origin);
							}

							// ── Non-production: keep staging/preview rules ──────────
							if (host.EndsWith(".vluethub.com"))
								return true;

							if (host.EndsWith(".onrender.com"))
								return true;

							// ── Vercel deployments ────────────────────────────────
							if (host.EndsWith(".vercel.app"))
								return true;

							// ── Netlify deployments ───────────────────────────────
							if (host.EndsWith(".netlify.app"))
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
