using FluentValidation;
using OOAD.Application.DTOs;

namespace OOAD.Application.Validators;

public class IssueCardRequestValidator : AbstractValidator<IssueCardRequest>
{
    private static readonly string[] AllowedPreviousStatuses = ["LOCKED", "LOST"];

    public IssueCardRequestValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("MemberId là bắt buộc khi cấp thẻ.");

        RuleFor(x => x.PreviousCardStatus)
            .MaximumLength(30).WithMessage("Trạng thái thẻ cũ tối đa 30 ký tự.")
            .Must(s => AllowedPreviousStatuses.Contains(s!.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái thẻ cũ chỉ chấp nhận LOCKED hoặc LOST.")
            .When(x => !string.IsNullOrWhiteSpace(x.PreviousCardStatus));

        RuleFor(x => x.Reason)
            .MaximumLength(255).WithMessage("Lý do tối đa 255 ký tự.")
            .When(x => x.Reason != null);
    }
}

public class UpdateCardStatusRequestValidator : AbstractValidator<UpdateCardStatusRequest>
{
    private static readonly string[] AllowedStatuses = ["ACTIVE", "LOCKED", "LOST"];

    public UpdateCardStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái thẻ (Status) là bắt buộc (ACTIVE, LOCKED hoặc LOST).")
            .MaximumLength(30).WithMessage("Trạng thái thẻ tối đa 30 ký tự.")
            .Must(s => AllowedStatuses.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái thẻ chỉ chấp nhận ACTIVE, LOCKED hoặc LOST.");

        RuleFor(x => x.Reason)
            .MaximumLength(255).WithMessage("Lý do tối đa 255 ký tự.")
            .When(x => x.Reason != null);
    }
}
