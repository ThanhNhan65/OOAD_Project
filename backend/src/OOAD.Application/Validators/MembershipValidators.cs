using FluentValidation;
using OOAD.Application.DTOs;

namespace OOAD.Application.Validators;

public class CreateUpdateMembershipPlanRequestValidator : AbstractValidator<CreateUpdateMembershipPlanRequest>
{
    private static readonly string[] AllowedPlanTypes = ["MEMBERSHIP", "PT"];

    public CreateUpdateMembershipPlanRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên gói tập không được để trống.")
            .MaximumLength(150).WithMessage("Tên gói tập tối đa 150 ký tự.");

        RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, 3650).WithMessage("Thời hạn gói phải từ 1 đến 3650 ngày.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Giá gói tập không được âm.");

        RuleFor(x => x.PriorityLevel)
            .InclusiveBetween(0, 10).WithMessage("Mức độ ưu tiên phải từ 0 đến 10.");

        RuleFor(x => x.MaxBookingsPerMonth)
            .InclusiveBetween(0, 500).WithMessage("Số lượt đặt mỗi tháng phải từ 0 đến 500.");

        RuleFor(x => x.MaxFreezeTimes)
            .InclusiveBetween(0, 100).WithMessage("Số lần tạm ngưng tối đa phải từ 0 đến 100.");

        RuleFor(x => x.MaxFreezeDays)
            .InclusiveBetween(0, 365).WithMessage("Số ngày tạm ngưng tối đa phải từ 0 đến 365.");

        RuleFor(x => x.PlanType)
            .NotEmpty().WithMessage("Loại gói tập không được để trống.")
            .MaximumLength(50).WithMessage("Loại gói tập tối đa 50 ký tự.")
            .Must(t => AllowedPlanTypes.Contains(t.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Loại gói tập phải là MEMBERSHIP hoặc PT.");

        RuleFor(x => x.PtSessions)
            .InclusiveBetween(0, 500).WithMessage("Số buổi PT phải từ 0 đến 500.");

        RuleFor(x => x.BranchIds)
            .Must(ids => ids != null && ids.Any(id => id != Guid.Empty))
            .WithMessage("Vui lòng chọn ít nhất một chi nhánh áp dụng khi không bật toàn hệ thống.")
            .When(x => !x.IsAllBranches);
    }
}

public class PurchaseMembershipRequestValidator : AbstractValidator<PurchaseMembershipRequest>
{
    public PurchaseMembershipRequestValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("Vui lòng chọn gói tập cần đăng ký.");

        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50).WithMessage("Phương thức thanh toán tối đa 50 ký tự.");
    }
}

public class RetryPaymentRequestValidator : AbstractValidator<RetryPaymentRequest>
{
    public RetryPaymentRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50).WithMessage("Phương thức thanh toán tối đa 50 ký tự.");
    }
}

public class CreateFreezeRequestValidator : AbstractValidator<CreateFreezeRequest>
{
    public CreateFreezeRequestValidator()
    {
        RuleFor(x => x.MembershipId)
            .NotEmpty().WithMessage("Vui lòng chọn gói hội viên cần tạm ngưng.");

        RuleFor(x => x.FromDate)
            .NotEmpty().WithMessage("Vui lòng chọn ngày bắt đầu tạm ngưng.");

        RuleFor(x => x.Days)
            .InclusiveBetween(1, 365).WithMessage("Số ngày tạm ngưng phải từ 1 đến 365 ngày.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do tạm ngưng.")
            .MaximumLength(500).WithMessage("Lý do tạm ngưng tối đa 500 ký tự.");
    }
}

public class HandleFreezeRequestValidator : AbstractValidator<HandleFreezeRequest>
{
    public HandleFreezeRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.")
            .When(x => x.Note != null);
    }
}
