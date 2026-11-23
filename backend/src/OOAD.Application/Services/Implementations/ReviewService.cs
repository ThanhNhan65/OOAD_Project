using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly ITrainerRepository _trainerRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(
        IReviewRepository reviewRepository,
        ITrainerRepository trainerRepository,
        IMemberRepository memberRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _trainerRepository = trainerRepository;
        _memberRepository = memberRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TrainerReviewSummaryDto> GetTrainerReviewsAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetWithDetailsAsync(trainerId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy Huấn luyện viên.");

        var reviews = await _reviewRepository.GetByTrainerIdWithDetailsAsync(trainer.Id, cancellationToken);
        return BuildSummary(trainer, reviews);
    }

    public async Task<TrainerReviewSummaryDto> GetMyTrainerReviewsAsync(Guid trainerUserId, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var reviews = await _reviewRepository.GetByTrainerIdWithDetailsAsync(trainer.Id, cancellationToken);
        return BuildSummary(trainer, reviews);
    }

    public async Task<ReviewDto> SubmitReviewAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        if (request.Rating < 1 || request.Rating > 5)
            throw new ValidationException("Số sao đánh giá phải từ 1 đến 5 sao.");

        Guid? trainerId = request.TrainerId;
        Guid? classId = request.ClassId;

        if (request.BookingId.HasValue)
        {
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(request.BookingId.Value, cancellationToken);
            if (booking != null && booking.MemberId == member.Id)
            {
                if (booking.Availability != null)
                {
                    trainerId = booking.Availability.TrainerId;
                }
                else if (booking.ClassSession != null)
                {
                    trainerId = booking.ClassSession.TrainerId;
                    classId = booking.ClassSession.ClassId;
                }
            }
        }

        var review = new Review
        {
            MemberId = member.Id,
            TrainerId = trainerId,
            ClassId = classId,
            BookingId = request.BookingId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        };

        await _reviewRepository.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        string? trainerName = null;
        if (trainerId.HasValue)
        {
            var tr = await _trainerRepository.GetWithDetailsAsync(trainerId.Value, cancellationToken);
            trainerName = tr?.User?.FullName;
        }

        return new ReviewDto
        {
            Id = review.Id,
            MemberId = member.Id,
            MemberName = member.User.FullName,
            TrainerId = trainerId,
            TrainerName = trainerName,
            BookingId = request.BookingId,
            ClassId = classId,
            Rating = review.Rating,
            Comment = review.Comment,
            TargetType = classId.HasValue ? "class" : "trainer",
            CreatedAt = review.CreatedAt
        };
    }

    private static TrainerReviewSummaryDto BuildSummary(Trainer trainer, IReadOnlyList<Review> reviews)
    {
        var dist = new Dictionary<int, int>
        {
            { 5, 0 }, { 4, 0 }, { 3, 0 }, { 2, 0 }, { 1, 0 }
        };

        foreach (var r in reviews)
        {
            if (dist.ContainsKey(r.Rating))
                dist[r.Rating]++;
            else
                dist[r.Rating] = 1;
        }

        var avg = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 5.0;

        return new TrainerReviewSummaryDto
        {
            TrainerId = trainer.Id,
            TrainerName = trainer.User?.FullName ?? "HLV",
            AverageRating = Math.Round(avg, 2),
            TotalReviews = reviews.Count,
            RatingDistribution = dist,
            Reviews = reviews.Select(r => new ReviewDto
            {
                Id = r.Id,
                MemberId = r.MemberId,
                MemberName = r.Member?.User?.FullName ?? "Hội viên",
                TrainerId = r.TrainerId,
                TrainerName = trainer.User?.FullName,
                BookingId = r.BookingId,
                ClassId = r.ClassId,
                ClassName = r.GymClass?.Name,
                Rating = r.Rating,
                Comment = r.Comment,
                TargetType = r.ClassId.HasValue ? "class" : "trainer",
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
