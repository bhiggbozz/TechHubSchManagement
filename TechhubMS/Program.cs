
//using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;
using TechHub.Background.Services;
using TechHub.Core.Configuration;
using TechHub.Core.Profiles;
using TechHub.QuestionBank.Controllers;
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

	builder.Host.UseSerilog();

	// Add services to the container
	builder.Services.AddControllers();
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen();
	builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
	builder.Services.AddHttpContextAccessor();
	builder.Services.Configure<CloudinarySettings>(
	builder.Configuration.GetSection("Cloudinary"));
	builder.Services.AddCors(options =>
	{
		options.AddPolicy("AllowAllOrigins",
			policy => policy
				.AllowAnyOrigin()
				.AllowAnyHeader()
				.AllowAnyMethod());
	});
	builder.Services.AddHostedService<QuestionJobWorker>();

	// Multi-tenant services
	builder.Services.AddMultiTenantServices(builder.Configuration);
	builder.Services.AddControllers()
	.AddApplicationPart(typeof(QuestionJobController).Assembly);

	var app = builder.Build();

	app.UseSerilogRequestLogging();

	// Configure the HTTP request pipeline
	//if (app.Environment.IsDevelopment())
	//{
	app.UseSwagger();
	app.UseSwaggerUI();
	//}

	app.UseHttpsRedirection();
	app.UseCors("AllowAllOrigins");

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
