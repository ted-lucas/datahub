using DataHub.Core.Entities;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data;
using DataHub.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataHub.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddDataHubInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<DataHubDbContext>(opt =>
            opt.UseSqlServer(connectionString));

        services.Configure<JwtOptions>(config.GetSection("Jwt"));

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
