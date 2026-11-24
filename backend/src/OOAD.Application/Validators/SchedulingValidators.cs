using FluentValidation;
using OOAD.Application.DTOs;

namespace OOAD.Application.Validators;

public class CreateGymClassRequestValidator : AbstractValidator<CreateGymClassRequest>
{
    public CreateGymClassRequestValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Vui lòng chọn chi nhánh cho lớp học.");

        RuleFor(x => x.TrainerId)
            .NotEmpty().WithMessage("Vui lòng chọn huấn luyện viên phụ trách.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên lớp học không được để trống.")
            .MaximumLength(150).WithMessage("Tên lớp học tối đa 150 ký tự.");

        RuleFor(x => x.Room)
            .NotEmpty().WithMessage("Phòng tập không được để trống.")
            .MaximumLength(100).WithMessage("Tên phòng tối đa 100 ký tự.");

        RuleFor(x => x.Capacity)
            .InclusiveBetween(1, 200).WithMessage("Sĩ số lớp phải từ 1 đến 200.");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Giờ bắt đầu lớp không được để trống.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600).WithMessage("Thời lượng buổi học phải từ 1 đến 600 phút.")
            .When(x => x.DurationMinutes.HasValue);

        RuleFor(x => x.GenerateWeeks)
            .InclusiveBetween(1, 52).WithMessage("Số tuần sinh lịch phải từ 1 đến 52.");
    }
}

public class UpdateGymClassRequestValidator : AbstractValidator<UpdateGymClassRequest>
{
    public UpdateGymClassRequestValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Vui lòng chọn chi nhánh cho lớp học.");

        RuleFor(x => x.TrainerId)
            .NotEmpty().WithMessage("Vui lòng chọn huấn luyện viên phụ trách.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên lớp học không được để trống.")
            .MaximumLength(150).WithMessage("Tên lớp học tối đa 150 ký tự.");

        RuleFor(x => x.Room)
            .NotEmpty().WithMessage("Phòng tập không được để trống.")
            .MaximumLength(100).WithMessage("Tên phòng tối đa 100 ký tự.");

        RuleFor(x => x.Capacity)
            .InclusiveBetween(1, 200).WithMessage("Sĩ số lớp phải từ 1 đến 200.");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Giờ bắt đầu lớp không được để trống.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600).WithMessage("Thời lượng buổi học phải từ 1 đến 600 phút.")
            .When(x => x.DurationMinutes.HasValue);

        RuleFor(x => x.GenerateWeeks)
            .InclusiveBetween(1, 52).WithMessage("Số tuần sinh lịch phải từ 1 đến 52.");
    }
}

public class GenerateSessionsRequestValidator : AbstractValidator<GenerateSessionsRequest>
{
    public GenerateSessionsRequestValidator()
    {
        RuleFor(x => x.Weeks)
            .InclusiveBetween(1, 52).WithMessage("Số tuần sinh buổi học phải từ 1 đến 52.");
    }
}

public class RescheduleSessionRequestValidator : AbstractValidator<RescheduleSessionRequest>
{
    public RescheduleSessionRequestValidator()
    {
        RuleFor(x => x.SessionDate)
            .NotEmpty().WithMessage("Ngày học mới không được để trống.");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Giờ bắt đầu mới không được để trống.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600).WithMessage("Thời lượng buổi học phải từ 1 đến 600 phút.")
            .When(x => x.DurationMinutes.HasValue);

        RuleFor(x => x.Room)
            .MaximumLength(100).WithMessage("Tên phòng tối đa 100 ký tự.")
            .When(x => x.Room != null);
    }
}

public class CancelSessionRequestValidator : AbstractValidator<CancelSessionRequest>
{
    public CancelSessionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Lý do hủy buổi tối đa 500 ký tự.")
            .When(x => x.Reason != null);
    }
}

public class BookClassRequestValidator : AbstractValidator<BookClassRequest>
{
    public BookClassRequestValidator()
    {
        RuleFor(x => x.ClassSessionId)
            .NotEmpty().WithMessage("Vui lòng chọn buổi học cần đăng ký.");
    }
}

public class CreateBookingReviewRequestValidator : AbstractValidator<CreateBookingReviewRequest>
{
    public CreateBookingReviewRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Số sao đánh giá phải từ 1 đến 5.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("Nội dung đánh giá tối đa 1000 ký tự.")
            .When(x => x.Comment != null);
    }
}

public class UpdateTrainerProfileRequestValidator : AbstractValidator<UpdateTrainerProfileRequest>
{
    public UpdateTrainerProfileRequestValidator()
    {
        RuleFor(x => x.Specialty)
            .MaximumLength(255).WithMessage("Chuyên môn tối đa 255 ký tự.")
            .When(x => x.Specialty != null);

        RuleFor(x => x.ExperienceYears)
            .InclusiveBetween(0, 60).WithMessage("Số năm kinh nghiệm phải từ 0 đến 60.")
            .When(x => x.ExperienceYears >= 0);

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Tiểu sử tối đa 1000 ký tự.")
            .When(x => x.Bio != null);

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.Phone != null);
    }
}

public class AvailabilityRequestValidator : AbstractValidator<AvailabilityRequest>
{
    public AvailabilityRequestValidator()
    {
        RuleFor(x => x.StartAt)
            .NotEmpty().WithMessage("Thời gian bắt đầu không được để trống.");

        RuleFor(x => x.EndAt)
            .NotEmpty().WithMessage("Thời gian kết thúc không được để trống.")
            .GreaterThan(x => x.StartAt).WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
    }
}

public class BookPtRequestValidator : AbstractValidator<BookPtRequest>
{
    public BookPtRequestValidator()
    {
        RuleFor(x => x.AvailabilityId)
            .NotEmpty().WithMessage("Vui lòng chọn khung giờ rảnh của huấn luyện viên.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Ghi chú đặt lịch tối đa 500 ký tự.")
            .When(x => x.Note != null);
    }
}

public class RespondPtRequestValidator : AbstractValidator<RespondPtRequest>
{
    public RespondPtRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.")
            .When(x => x.Reason != null);
    }
}
