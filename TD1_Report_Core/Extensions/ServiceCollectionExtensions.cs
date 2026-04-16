using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TD1_Report_Core.Extensions;

/// <summary>
/// ServiceCollection 擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 自動註冊 TD1_Report_Core 下所有以 Core 結尾的類別
    /// </summary>
    public static IServiceCollection AddTd1ReportCore(this IServiceCollection services)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        List<Type> coreTypes = assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.IsPublic &&
                type.Name.EndsWith("Core", StringComparison.Ordinal))
            .ToList();

        foreach (Type coreType in coreTypes)
        {
            services.AddScoped(coreType);
        }

        return services;
    }
}
