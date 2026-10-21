using Analytics.Application.Analytics;
using Analytics.Application.Projects;
using Analytics.Application.Tasks;
using Analytics.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Analytics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UserService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<TaskService>();
        services.AddScoped<AnalyticsService>();
        return services;
    }
}
