using OOAD.Application.Common.Interfaces;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;
using OOAD.Infrastructure.Persistence.Seed;
using OOAD.Infrastructure.Repositories;
using OOAD.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OOAD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=OOADDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<AppDbContextInitialiser>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 1. Identity & Account
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IMemberCardRepository, MemberCardRepository>();
        services.AddScoped<ITrainerRepository, TrainerRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();

        // 2. Membership & Payment
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IFreezeRequestRepository, FreezeRequestRepository>();
        services.AddScoped<IPaymentStrategy, CardPaymentStrategy>();
        services.AddScoped<IPaymentStrategy, WalletPaymentStrategy>();
        services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();

        // 3. Branch & Equipment
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IEquipmentIssueRepository, EquipmentIssueRepository>();

        // 4. Scheduling (Class & PT)
        services.AddScoped<IGymClassRepository, GymClassRepository>();
        services.AddScoped<IClassSessionRepository, ClassSessionRepository>();
        services.AddScoped<ITrainerAvailabilityRepository, TrainerAvailabilityRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IWaitlistEntryRepository, WaitlistEntryRepository>();

        // 5. Access Control
        services.AddScoped<ICheckinRepository, CheckinRepository>();

        // 6. Progress & Review
        services.AddScoped<IBodyMeasurementRepository, BodyMeasurementRepository>();
        services.AddScoped<IMetricTypeRepository, MetricTypeRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();

        // 7. Notification
        services.AddScoped<INotificationRepository, NotificationRepository>();

        return services;
    }
}
