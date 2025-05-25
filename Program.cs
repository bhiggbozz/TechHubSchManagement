
using Microsoft.AspNetCore.Builder;
using TechHub.Core.Profiles;
using TechHub.Service.Interface;
using TechHub.Service.Service;
using TechHub.Service.Service.DatabaseService;
using TechhubMS.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped(typeof(ICommandRespository<>), typeof(CommandRepositoryService<>));
builder.Services.AddScoped(typeof(IQueryRepository<>), typeof(QueryRepositoryService<>));

builder.Services.AddScoped<ISchoolService, SchoolService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDbTransactionScopeFactory, DbTransactionScopeFactory>();

builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
//app.MapControllerRoute();

app.MapControllers();
app.UseMiddleware<ResponseCodeMiddleware>();
//app.UseEndpoints(endpoint =>
// endpoint.MapControllers();
//endpoint

app.Run();
