using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TechhubMS.Middleware.Interface;
using TechhubMS.Middleware.Services;

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
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<AuthService>();

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
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"])),
                    ClockSkew = TimeSpan.Zero
                };
            });

            return services;
        }
    }
}
