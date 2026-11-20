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
        services.AddScoped<ICheckinService, CheckinService>();
        return services;
    }
}
