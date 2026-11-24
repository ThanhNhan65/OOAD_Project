using FluentValidation;
using OOAD.Application.DTOs;

namespace OOAD.Application.Validators;

public class CardCheckinRequestValidator : AbstractValidator<CardCheckinRequest>
{
    private static readonly string[] AllowedMethods = ["QR", "Card"];

    public CardCheckinRequestValidator()
    {
        RuleFor(x => x.CardCode)
            .NotEmpty().WithMessage("Mã thẻ / QR không được để trống.")
            .MaximumLength(100).WithMessage("Mã thẻ / QR tối đa 100 ký tự.");

        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Chi nhánh là bắt buộc.");

        RuleFor(x => x.Method)
            .MaximumLength(30).WithMessage("Hình thức check-in tối đa 30 ký tự.")
            .Must(m => AllowedMethods.Contains(m.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hình thức check-in bằng thẻ chỉ hỗ trợ QR hoặc Card.")
            .When(x => !string.IsNullOrWhiteSpace(x.Method));
    }
}

public class ManualCheckinRequestValidator : AbstractValidator<ManualCheckinRequest>
{
    public ManualCheckinRequestValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("Vui lòng chọn hội viên.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do check-in thủ công.")
            .MaximumLength(500).WithMessage("Lý do check-in thủ công tối đa 500 ký tự.");
    }
}

public class CardCheckoutRequestValidator : AbstractValidator<CardCheckoutRequest>
{
    public CardCheckoutRequestValidator()
    {
        RuleFor(x => x.CardCode)
            .NotEmpty().WithMessage("Mã thẻ / QR không được để trống.")
            .MaximumLength(100).WithMessage("Mã thẻ / QR tối đa 100 ký tự.");
    }
}

public class ManualCheckoutRequestValidator : AbstractValidator<ManualCheckoutRequest>
{
    public ManualCheckoutRequestValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("Vui lòng chọn hội viên cần check-out.");
    }
}
