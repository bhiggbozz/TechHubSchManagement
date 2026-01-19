
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TechHub.Core.Profiles;
using TechHub.Core.Utilities;
using TechHub.Service.Interface;
using TechHub.Service.Service;
using TechHub.Service.Service.DatabaseService;
using TechhubMS;
using TechhubMS.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped(typeof(ICommandRespository<>), typeof(CommandRepositoryService<>));
builder.Services.AddScoped(typeof(IQueryRepository<>), typeof(QueryRepositoryService<>));
builder.Services.AddScoped<ITenantService, TenantService>();

builder.Services.AddScoped<ISchoolService, SchoolService>();
builder.Services.AddScoped<IUtilities, Utilities>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDbTransactionScopeFactory, DbTransactionScopeFactory>();

builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAllOrigins",
		builder => builder
			.AllowAnyOrigin()
			.AllowAnyHeader()
			.AllowAnyMethod());
});

var jwtSettings = builder.Configuration.GetSection("Jwt");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(
			Encoding.UTF8.GetBytes(jwtSettings["SecretKey"])),

		ValidateIssuer = true,
		ValidIssuer = jwtSettings["Issuer"],

		ValidateAudience = true,
		ValidAudience = jwtSettings["Audience"],

		ValidateLifetime = true, 
		ClockSkew = TimeSpan.Zero
	};
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseCors("AllowAllOrigins");
//app.MapControllerRoute();

app.MapControllers();
app.UseMiddleware<ResponseCodeMiddleware>();
app.UseMiddleware<MultiTenantMiddleware>();

app.UseAuthentication();

//app.UseEndpoints(endpoint =>
// endpoint.MapControllers();
//endpoint

app.Run();
