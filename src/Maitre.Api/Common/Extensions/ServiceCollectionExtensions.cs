using Maitre.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maitre.Api.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddHealthChecks();
        return services;
    }
}

