
//using Microsoft.AspNetCore.Authentication.JwtBearer;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;
using TechHub.Background.Configuration;
using TechHub.Background.Extensions;
using TechHub.Background.Services;
using TechHub.BackgroundJobs.Interfaces;
using TechHub.Core.Configuration;
using TechHub.Core.Profiles;
using TechHub.Entity.Migration;
using TechHub.QuestionBank.Controllers;
using TechHub.Service.Extensions;
using TechHub.Service.Infrastructure.Logging;
using TechhubMS;
using TechhubMS.Middleware;


Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
	.MinimumLevel.Override("System", LogEventLevel.Information)
	.Enrich.FromLogContext()
	.WriteTo.Console(
		outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
	)
	.WriteTo.File(
		path: "logs/log-.txt",
		rollingInterval: RollingInterval.Day,
		outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
	)
	.CreateLogger();
try
{
	Log.Information("?? Starting TechHub application");

	var builder = WebApplication.CreateBuilder(args);

	builder.Services.Configure<HostOptions>(options =>
	{
		options.BackgroundServiceExceptionBehavior =
			BackgroundServiceExceptionBehavior.Ignore;
	});

	builder.Host.UseSerilog();

	// Add services to the container
	builder.Services.AddControllers();
	builder.Services.AddEndpointsApiExplorer();
	//builder.Services.AddSwaggerGen();
	builder.Services.AddSwaggerGen(c =>
	{
		c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
		{
			Title = "TechHub API",
			Version = "v1",
			Description = "TechHub School Management Platform"
		});

		// ?? JWT Bearer token ??????????????????????????????????????????????
		c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
		{
			Name = "Authorization",
			Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
			Scheme = "Bearer",
			BearerFormat = "JWT",
			In = Microsoft.OpenApi.Models.ParameterLocation.Header,
			Description = "Enter your JWT token. Example: eyJhbGci..."
		});

		c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
	{
		{
			new Microsoft.OpenApi.Models.OpenApiSecurityScheme
			{
				Reference = new Microsoft.OpenApi.Models.OpenApiReference
				{
					Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
					Id   = "Bearer"
				}
			},
			Array.Empty<string>()
		}
	});

		// ?? X-Tenant-ID header ????????????????????????????????????????????
		c.AddSecurityDefinition("TenantId", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
		{
			Name = "X-Tenant-ID",
			Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
			In = Microsoft.OpenApi.Models.ParameterLocation.Header,
			Description = "Enter your school code. Example: pearl"
		});

		c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
	{
		{
			new Microsoft.OpenApi.Models.OpenApiSecurityScheme
			{
				Reference = new Microsoft.OpenApi.Models.OpenApiReference
				{
					Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
					Id   = "TenantId"
				}
			},
			Array.Empty<string>()
		}
	});
	});
	builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
	builder.Services.AddHttpContextAccessor();
	builder.Services.Configure<CloudinarySettings>(
	builder.Configuration.GetSection("Cloudinary"));
	builder.Services.AddHostedService<QuestionJobWorker>();
	builder.Services.AddHostedService<PerformanceAggregationWorker>();
	builder.Services.AddHostedService<AdminDashboardAggregationWorker>();

	// Multi-tenant services
	builder.Services.AddMultiTenantServices(builder.Configuration);
	builder.Services.AddControllers()
	.AddApplicationPart(typeof(QuestionJobController).Assembly);

	// Fire-and-forget database error logging (channel + writer + logger)
	builder.Services.AddDatabaseLogging();

	// Board session recording services
	builder.Services.AddBoardServices(builder.Configuration);
	builder.Services.AddBoardWorkers();
	builder.Services.AddDatabaseMigration();

	// Hangfire background job processing (SQL Server storage + embedded server)
	var hangfireConnectionString = builder.Configuration.GetConnectionString("DbConnectionString");
	builder.Services.AddHangfireServices(hangfireConnectionString, builder.Configuration);

	var app = builder.Build();

	// Register recurring background jobs (Hangfire)
	using (var scope = app.Services.CreateScope())
	{
		var backgroundJobService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
		backgroundJobService.ScheduleMediaCleanup();
		backgroundJobService.ScheduleApplicationLogsCleanup();
	}

	app.UseSerilogRequestLogging();

	// Global exception handler — outermost, before auth/endpoints, so any
	// unhandled exception is logged to ApplicationLogs and answered with 500.
	app.UseMiddleware<GlobalExceptionMiddleware>();

	// Configure the HTTP request pipeline
	//if (app.Environment.IsDevelopment())
	//{
	app.UseSwagger();
	app.UseSwaggerUI();
	//}

	if (builder.Configuration.GetValue<bool>("Hangfire:EnableDashboard", false))
	{
		app.UseHangfireDashboard(
			builder.Configuration.GetValue<string>("Hangfire:DashboardPath", "/hangfire"));
	}

	app.UseHttpsRedirection();
	app.UseCors("MultiTenantCors");

	// ? Middleware order matters!
	app.UseMiddleware<MultiTenantMiddleware>();  // Must be before Authentication
	app.UseAuthentication();
	app.UseAuthorization();
	app.UseMiddleware<ResponseCodeMiddleware>();

	app.MapControllers();

	Log.Information("? Application started successfully");

	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "?? Application failed to start");
}
finally
{
	Log.CloseAndFlush();
}

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddControllers();
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();


////builder.Services.AddScoped<IUtilities, Utilities>();
////builder.Services.AddScoped(typeof(ICommandRespository<>), typeof(CommandRepositoryService<>));
////builder.Services.AddScoped(typeof(IQueryRepository<>), typeof(QueryRepositoryService<>));
////builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);
////builder.Services.AddScoped<IDbTransactionScopeFactory, DbTransactionScopeFactory>();
////builder.Services.AddScoped<IUserService, UserService>();

//builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddHttpContextAccessor();

//builder.Services.AddCors(options =>
//{
//	options.AddPolicy("AllowAllOrigins",
//		builder => builder
//			.AllowAnyOrigin()
//			.AllowAnyHeader()
//			.AllowAnyMethod());
//});

//var jwtSettings = builder.Configuration.GetSection("Jwt");

////builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
////.AddJwtBearer(options =>
////{
////	options.TokenValidationParameters = new TokenValidationParameters
////	{
////		ValidateIssuerSigningKey = true,
////		IssuerSigningKey = new SymmetricSecurityKey(
////			Encoding.UTF8.GetBytes(jwtSettings["SecretKey"])),

////		ValidateIssuer = true,
////		ValidIssuer = jwtSettings["Issuer"],

////		ValidateAudience = true,
////		ValidAudience = jwtSettings["Audience"],

////		ValidateLifetime = true, 
////		ClockSkew = TimeSpan.Zero
////	};
////});
//builder.Services.AddMultiTenantServices( builder.Configuration);



//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//	app.UseSwagger();
//	app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();
//app.UseCors("AllowAllOrigins");
//app.UseMiddleware<MultiTenantMiddleware>();  
//app.UseAuthentication();                      
//app.UseAuthorization();                       
//app.UseMiddleware<ResponseCodeMiddleware>();  
//app.MapControllers();
////app.UseEndpoints(endpoint =>
//// endpoint.MapControllers();
////endpoint

//app.Run();
