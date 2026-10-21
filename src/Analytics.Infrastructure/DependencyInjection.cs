using Analytics.Application.Analytics;
using Analytics.Application.Logs;
using Analytics.Application.Projects;
using Analytics.Application.Tasks;
using Analytics.Application.Users;
using Analytics.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // full_name -> FullName without aliasing every column.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        // One pooled data source for the whole app.
        services.AddNpgsqlDataSource(connectionString);

        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<ISystemLogRepository, SystemLogRepository>();
        services.AddSingleton<IAnalyticsQueries, AnalyticsQueries>();
        return services;
    }
}
