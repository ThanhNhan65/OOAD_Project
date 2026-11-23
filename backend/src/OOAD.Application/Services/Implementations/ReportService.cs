using OOAD.Application.DTOs.Report;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class ReportService : IReportService
{
    private static readonly string[] Palette = new string[]
    {
        "#7c5cff", "#ff5c8a", "#00d1b2", "#ffb020", "#3fa9ff", "#a259ff", "#2ed47a"
    };

    private readonly IBranchRepository _branchRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly ICheckinRepository _checkinRepository;
    private readonly IGymClassRepository _gymClassRepository;
    private readonly IClassSessionRepository _classSessionRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly ITrainerRepository _trainerRepository;
    private readonly IBookingRepository _bookingRepository;

    public ReportService(
        IBranchRepository branchRepository,
        IPaymentRepository paymentRepository,
        IMembershipRepository membershipRepository,
        IMemberRepository memberRepository,
        ICheckinRepository checkinRepository,
        IGymClassRepository gymClassRepository,
        IClassSessionRepository classSessionRepository,
        IReviewRepository reviewRepository,
        ITrainerRepository trainerRepository,
        IBookingRepository bookingRepository)
    {
        _branchRepository = branchRepository;
        _paymentRepository = paymentRepository;
        _membershipRepository = membershipRepository;
        _memberRepository = memberRepository;
        _checkinRepository = checkinRepository;
        _gymClassRepository = gymClassRepository;
        _classSessionRepository = classSessionRepository;
        _reviewRepository = reviewRepository;
        _trainerRepository = trainerRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<ReportOverviewDto> GetOverviewAsync(
        Guid? branchId = null,
        int months = 6,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 24);
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Danh sách các tháng cần thống kê
        var monthStarts = Enumerable.Range(0, months)
            .Select(i => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1 - i)))
            .ToList();

        var branches = await _branchRepository.GetAllAsync(cancellationToken);

        // 1. Payments (Completed)
        var payments = await _paymentRepository.GetCompletedWithDetailsAsync(branchId, cancellationToken);

        // 2. Memberships
        var memberships = await _membershipRepository.GetAllWithPlanAsync(branchId, cancellationToken);

        var memberIds = branchId.HasValue
            ? memberships.Select(m => m.MemberId).Distinct().ToHashSet()
            : (await _memberRepository.GetAllIdsAsync(cancellationToken)).ToHashSet();

        bool ActiveOn(Guid memberId, DateTime date) =>
            memberships.Any(m => m.MemberId == memberId && m.StartDate.Date <= date && date <= m.EndDate.Date);

        var activeMembersNow = memberIds.Count(id => ActiveOn(id, today));

        // 3. Doanh thu theo tháng
        var revenue = monthStarts.Select(ms =>
        {
            var me = ms.AddMonths(1);
            var key = ms.ToString("yyyy-MM");
            var label = $"T{ms.Month}/{ms.Year % 100:D2}";
            var inMonth = payments.Where(p =>
            {
                var dt = p.PaidAt ?? p.CreatedAt;
                return dt >= ms && dt < me;
            }).ToList();

            var ptRev = inMonth.Where(p => p.Membership.Plan.Name.Contains("PT", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
            var msRev = inMonth.Sum(p => p.Amount) - ptRev;

            var byBranch = branches.ToDictionary(
                b => b.Id.ToString(),
                b => inMonth.Where(p => p.Membership.BranchId == b.Id).Sum(p => p.Amount)
            );

            return new MonthlyRevenueDto(key, label, msRev, ptRev, byBranch);
        }).ToList();

        // 4. Biến động hội viên & Churn rate
        var churn = monthStarts.Select(ms =>
        {
            var me = ms.AddMonths(1).AddDays(-1);
            var end = me > today ? today : me;
            var key = ms.ToString("yyyy-MM");
            var label = $"T{ms.Month}/{ms.Year % 100:D2}";

            var activeStart = memberIds.Where(id => ActiveOn(id, ms)).ToList();
            var lost = activeStart.Count(id =>
                !ActiveOn(id, end) &&
                !memberships.Any(m => m.MemberId == id && m.StartDate.Date > ms && m.StartDate.Date <= end));

            var newMembers = memberIds.Count(id =>
            {
                var first = memberships.Where(m => m.MemberId == id).OrderBy(m => m.StartDate).FirstOrDefault();
                return first != null && first.StartDate.Date >= ms && first.StartDate.Date <= me;
            });

            var rate = activeStart.Count > 0 ? Math.Round((double)lost / activeStart.Count * 100, 1) : 0;
            return new MonthlyChurnDto(key, label, activeStart.Count, lost, newMembers, rate);
        }).ToList();

        // 5. Cơ cấu gói đang hoạt động
        var activeMs = memberships.Where(m => m.StartDate.Date <= today && today <= m.EndDate.Date).ToList();
        var planUsage = activeMs
            .GroupBy(m => m.Plan.Name)
            .Select((g, idx) => new PlanUsageDto(g.Key, g.Count(), Palette[idx % Palette.Length]))
            .ToList();

        // 6. Check-in & Đang có mặt
        var allCheckins = await _checkinRepository.GetAllByBranchAsync(branchId: null, cancellationToken);
        var allOpenCheckins = allCheckins.Where(c => c.CheckOutAt == null).ToList();

        var branchPresences = branches.Select(b => new BranchPresenceDto(
            b.Id,
            b.Name,
            b.OperatingHours,
            allOpenCheckins.Count(c => c.BranchId == b.Id)
        )).ToList();

        var filteredCheckins = branchId.HasValue
            ? allCheckins.Where(c => c.BranchId == branchId.Value).ToList()
            : allCheckins.ToList();

        var visits = monthStarts.Select(ms =>
        {
            var me = ms.AddMonths(1);
            var label = $"T{ms.Month}/{ms.Year % 100:D2}";
            return new MonthlyVisitDto(label, filteredCheckins.Count(c => c.CheckInAt >= ms && c.CheckInAt < me));
        }).ToList();

        var hours = Enumerable.Range(5, 18).Select(h => new HourlyCheckinDto(
            $"{h}h",
            filteredCheckins.Count(c => c.CheckInAt.ToLocalTime().Hour == h)
        )).ToList();

        // 7. Lớp học
        var gymClasses = await _gymClassRepository.GetAllWithBranchAsync(branchId, cancellationToken);
        var sessions = await _classSessionRepository.GetPastNonCancelledWithDetailsAsync(now, cancellationToken);
        var reviews = await _reviewRepository.GetAllAsync(cancellationToken);

        var classReports = gymClasses.Select(c =>
        {
            var ss = sessions.Where(s => s.ClassId == c.Id).ToList();
            var bks = ss.SelectMany(s => s.Bookings).ToList();
            var booked = bks.Count(b => b.Status != BookingStatus.Cancelled);
            var attended = bks.Count(b => b.Status.ToString().Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                                          b.Status.ToString().Equals("Attended", StringComparison.OrdinalIgnoreCase));
            var cap = c.Capacity > 0 ? c.Capacity : 20;
            var fill = ss.Count > 0 ? (int)Math.Round((double)booked / (ss.Count * cap) * 100) : 0;
            var attRate = booked > 0 ? (int)Math.Round((double)attended / booked * 100) : 0;
            var waitlisted = ss.Sum(s => s.WaitlistEntries.Count);
            var rv = reviews.Where(r => r.ClassId == c.Id).ToList();
            var trainerName = ss.FirstOrDefault()?.Trainer?.User?.FullName ?? "—";

            return new ClassReportDto(
                c.Id,
                c.Name,
                c.Branch?.Name ?? "—",
                trainerName,
                ss.Count,
                Math.Min(fill, 100),
                Math.Min(attRate, 100),
                waitlisted,
                rv.Count > 0 ? Math.Round(rv.Average(r => r.Rating), 1) : 0,
                rv.Count
            );
        }).ToList();

        // 8. Huấn luyện viên
        var trainers = await _trainerRepository.GetAllWithDetailsAsync(branchId: null, cancellationToken);
        var ptBookings = await _bookingRepository.GetAllPtBookingsWithAvailabilityAsync(cancellationToken);

        var trainerReports = trainers.Select(t =>
        {
            var pt = ptBookings.Where(b => b.Availability!.TrainerId == t.Id).ToList();
            var done = pt.Count(b => b.Status.ToString().Equals("Completed", StringComparison.OrdinalIgnoreCase));
            var noShow = pt.Count(b => b.Status.ToString().Equals("NoShow", StringComparison.OrdinalIgnoreCase));
            var rejected = pt.Count(b => b.Status == BookingStatus.Cancelled);
            var clsCount = sessions.Count(s => s.TrainerId == t.Id);
            var rv = reviews.Where(r => r.TrainerId == t.Id).ToList();

            return new TrainerReportDto(
                t.Id,
                t.User?.FullName ?? "HLV",
                done,
                noShow,
                rejected,
                clsCount,
                rv.Count > 0 ? Math.Round(rv.Average(r => r.Rating), 2) : 0,
                rv.Count
            );
        }).ToList();

        // 9. Giao dịch gần đây
        var recentPayments = payments.Take(6).Select(p => new RecentPaymentDto(
            p.Id,
            p.Membership.Member?.User?.FullName ?? "Hội viên",
            p.Membership.Plan?.Name ?? "Thanh toán gói tập",
            p.Amount,
            p.PaidAt ?? p.CreatedAt
        )).ToList();

        return new ReportOverviewDto(
            activeMembersNow,
            memberIds.Count,
            branchPresences,
            revenue,
            churn,
            planUsage,
            visits,
            hours,
            classReports,
            trainerReports,
            recentPayments
        );
    }
}
