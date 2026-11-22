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
        services.AddScoped<IMembershipSubscriptionService, MembershipSubscriptionService>();
        services.AddScoped<ITrainerService, TrainerService>();
        services.AddScoped<ICheckinService, CheckinService>();
        services.AddScoped<IGymClassService, GymClassService>();
        services.AddScoped<IClassBookingService, ClassBookingService>();
        services.AddScoped<IPersonalTrainerService, PersonalTrainerService>();
        services.AddScoped<IBodyMeasurementService, BodyMeasurementService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
