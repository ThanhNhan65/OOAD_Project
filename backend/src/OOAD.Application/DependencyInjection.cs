using OOAD.Application.Services.Implementations;
using OOAD.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace OOAD.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMemberCardService, MemberCardService>();
        services.AddScoped<ITrainerService, TrainerService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IGymRuleService, GymRuleService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
