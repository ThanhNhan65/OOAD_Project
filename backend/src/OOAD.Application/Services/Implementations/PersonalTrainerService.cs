using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class PersonalTrainerService : IPersonalTrainerService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ITrainerRepository _trainerRepository;
    private readonly ITrainerAvailabilityRepository _availabilityRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PersonalTrainerService(
        IMemberRepository memberRepository,
        ITrainerRepository trainerRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IMembershipRepository membershipRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _memberRepository = memberRepository;
        _trainerRepository = trainerRepository;
        _availabilityRepository = availabilityRepository;
        _membershipRepository = membershipRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PtBalanceDto> GetPtBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var now = DateTime.UtcNow;
        var activeMemberships = await _membershipRepository.GetActiveByMemberIdWithPlanAsync(member.Id, now, cancellationToken);

        int freeSessions = 0;
        int packageSessions = 0;
        string? activePlanName = null;
        DateTime? expiryDate = null;

        foreach (var ms in activeMemberships)
        {
            if (activePlanName == null && ms.Plan != null)
            {
                activePlanName = ms.Plan.Name;
                expiryDate = ms.EndDate;
            }

            if (ms.Plan?.PlanType == "PT")
            {
                packageSessions += ms.RemainingSessions;
            }
            else
            {
                freeSessions += ms.RemainingSessions;
            }
        }

        return new PtBalanceDto
        {
            Total = freeSessions + packageSessions,
            FreeSessions = freeSessions,
            PackageSessions = packageSessions,
            ActivePlanName = activePlanName ?? "Chưa có gói hiệu lực",
            ExpiryDate = expiryDate
        };
    }

    public async Task<PtBookingDto> BookPtSessionAsync(Guid userId, BookPtRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var availability = await _availabilityRepository.GetByIdWithTrainerDetailsAsync(request.AvailabilityId, cancellationToken);
        if (availability == null)
            throw new NotFoundException("Khung giờ rảnh không tồn tại.");

        if (availability.IsBooked)
            throw new ValidationException("Khung giờ này đã được học viên khác đặt.");

        if (availability.StartAt <= DateTime.UtcNow)
            throw new ValidationException("Không thể đặt khung giờ đã trôi qua.");

        // Tìm gói hội viên còn buổi PT (ưu tiên buổi tặng trước, sau đó tới gói PT)
        var now = DateTime.UtcNow;
        var activeMemberships = await _membershipRepository.GetActiveByMemberIdWithPlanAsync(member.Id, now, cancellationToken);
        var membership = activeMemberships
            .Where(m => m.RemainingSessions > 0)
            .OrderBy(m => m.Plan.PlanType == "PT" ? 1 : 0)
            .FirstOrDefault();

        if (membership == null)
            throw new ValidationException("Bạn không còn buổi tập PT nào khả dụng. Vui lòng mua thêm gói PT hoặc gia hạn gói VIP.");

        // Kiểm tra hội viên có trùng lịch PT khác vào khung giờ này không
        var hasConflict = await _bookingRepository.HasPtTimeConflictAsync(
            member.Id,
            availability.StartAt,
            availability.EndAt,
            cancellationToken);

        if (hasConflict)
            throw new ValidationException("Bạn đã có một lịch hẹn PT khác trùng vào khung giờ này.");

        // Đặt trước 1 buổi
        membership.RemainingSessions -= 1;
        availability.IsBooked = true;

        var booking = new Booking
        {
            MemberId = member.Id,
            MembershipId = membership.Id,
            Type = BookingType.PersonalTrainer,
            AvailabilityId = availability.Id,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PtBookingDto
        {
            Id = booking.Id,
            MemberId = member.Id,
            MemberName = member.User.FullName,
            MemberPhone = member.User.Phone,
            TrainerId = availability.Trainer.Id,
            TrainerName = availability.Trainer.User.FullName,
            TrainerSpecialty = availability.Trainer.Specialty,
            BranchId = availability.Trainer.BranchId,
            BranchName = availability.Trainer.Branch?.Name ?? "Chi nhánh OOAD",
            AvailabilityId = availability.Id,
            StartAt = availability.StartAt,
            EndAt = availability.EndAt,
            Status = booking.Status.ToString().ToUpper(),
            Note = request.Note,
            SourceType = membership.Plan?.PlanType == "PT" ? "pt_package" : "membership_free",
            CheckedIn = false,
            CanCancel = true,
            CanReview = false,
            IsReviewed = false,
            CreatedAt = booking.CreatedAt
        };
    }

    public async Task<IEnumerable<PtBookingDto>> GetMyPtBookingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            return Enumerable.Empty<PtBookingDto>();

        var bookings = await _bookingRepository.GetPtBookingsByMemberIdAsync(member.Id, cancellationToken);
        var now = DateTime.UtcNow;

        return bookings.Select(b =>
        {
            var av = b.Availability!;
            var hoursLeft = (av.StartAt - now).TotalHours;
            var isAttended = b.Status == BookingStatus.Attended;
            return new PtBookingDto
            {
                Id = b.Id,
                MemberId = member.Id,
                MemberName = b.Member?.User?.FullName ?? "Hội viên",
                MemberPhone = b.Member?.User?.Phone,
                TrainerId = av.TrainerId,
                TrainerName = av.Trainer?.User?.FullName ?? "HLV",
                TrainerSpecialty = av.Trainer?.Specialty,
                BranchId = av.Trainer?.BranchId ?? Guid.Empty,
                BranchName = av.Trainer?.Branch?.Name ?? "Chi nhánh OOAD",
                AvailabilityId = av.Id,
                StartAt = av.StartAt,
                EndAt = av.EndAt,
                Status = b.Status.ToString().ToUpper(),
                SourceType = b.Membership?.Plan?.PlanType == "PT" ? "pt_package" : "membership_free",
                CheckedIn = isAttended,
                CanCancel = (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && hoursLeft >= 2,
                CanReview = isAttended && b.Review == null,
                IsReviewed = b.Review != null,
                CreatedAt = b.CreatedAt
            };
        });
    }

    public async Task CancelPtBookingAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var booking = await _bookingRepository.GetByIdAndMemberIdWithPtDetailsAsync(bookingId, member.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy thông tin đặt lịch PT.");

        if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Attended)
            throw new ValidationException("Không thể hủy buổi tập này.");

        var now = DateTime.UtcNow;
        if (booking.Availability != null)
        {
            var hoursLeft = (booking.Availability.StartAt - now).TotalHours;
            if (hoursLeft < 2 && booking.Status == BookingStatus.Confirmed)
            {
                throw new ValidationException("Đã quá thời hạn hủy miễn phí (trước giờ tập 2 tiếng).");
            }

            booking.Availability.IsBooked = false;
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;

        // Hoàn lại số buổi vào Membership
        if (booking.Membership != null)
        {
            booking.Membership.RemainingSessions += 1;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<TrainerPtRequestItemDto>> GetTrainerPtRequestsAsync(Guid trainerUserId, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdWithDetailsAsync(
            trainerUserId,
            includeAvailabilities: false,
            asNoTracking: true,
            cancellationToken);

        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var bookings = await _bookingRepository.GetPtBookingsByTrainerIdAsync(trainer.Id, cancellationToken: cancellationToken);

        return bookings.Select(b =>
        {
            var av = b.Availability!;
            return new TrainerPtRequestItemDto
            {
                Id = b.Id,
                MemberId = b.MemberId,
                MemberName = b.Member?.User?.FullName ?? "Hội viên",
                MemberPhone = b.Member?.User?.Phone,
                BranchId = trainer.BranchId,
                BranchName = trainer.Branch?.Name ?? "Chi nhánh OOAD",
                StartAt = av.StartAt,
                EndAt = av.EndAt,
                Status = b.Status.ToString().ToUpper(),
                SourceType = b.Membership?.Plan?.PlanType == "PT" ? "pt_package" : "membership_free",
                CheckedIn = b.Status == BookingStatus.Attended,
                CreatedAt = b.CreatedAt
            };
        });
    }

    public async Task RespondPtRequestAsync(Guid trainerUserId, Guid bookingId, bool accept, string? reason, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var booking = await _bookingRepository.GetByIdAndTrainerIdWithPtDetailsAsync(bookingId, trainer.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy yêu cầu đặt lịch PT.");

        if (booking.Status != BookingStatus.Pending)
            throw new ValidationException("Yêu cầu này không ở trạng thái chờ duyệt.");

        if (accept)
        {
            booking.Status = BookingStatus.Confirmed;
        }
        else
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            if (booking.Availability != null)
            {
                booking.Availability.IsBooked = false;
            }
            if (booking.Membership != null)
            {
                booking.Membership.RemainingSessions += 1;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CompletePtSessionAsync(Guid trainerUserId, Guid bookingId, bool attended, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var booking = await _bookingRepository.GetByIdAndTrainerIdWithPtDetailsAsync(bookingId, trainer.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy buổi tập PT.");

        booking.Status = attended ? BookingStatus.Attended : BookingStatus.NoShow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelPtByTrainerAsync(Guid trainerUserId, Guid bookingId, string reason, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var booking = await _bookingRepository.GetByIdAndTrainerIdWithPtDetailsAsync(bookingId, trainer.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy buổi tập PT.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        if (booking.Availability != null)
        {
            booking.Availability.IsBooked = false;
        }

        if (booking.Membership != null)
        {
            booking.Membership.RemainingSessions += 1;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
