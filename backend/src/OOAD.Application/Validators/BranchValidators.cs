using FluentValidation;
using OOAD.Application.DTOs.Branch;

namespace OOAD.Application.Validators;

public class CreateBranchDtoValidator : AbstractValidator<CreateBranchDto>
{
    public CreateBranchDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên chi nhánh không được để trống.")
            .MaximumLength(150).WithMessage("Tên chi nhánh tối đa 150 ký tự.");

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("Quận/Huyện không được để trống.")
            .MaximumLength(50).WithMessage("Quận/Huyện tối đa 50 ký tự.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Địa chỉ chi nhánh không được để trống.")
            .MaximumLength(255).WithMessage("Địa chỉ tối đa 255 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.PhoneNumber != null);

        RuleFor(x => x.OperatingHours)
            .NotEmpty().WithMessage("Giờ hoạt động không được để trống.")
            .MaximumLength(50).WithMessage("Giờ hoạt động tối đa 50 ký tự.");
    }
}

public class UpdateBranchDtoValidator : AbstractValidator<UpdateBranchDto>
{
    public UpdateBranchDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên chi nhánh không được để trống.")
            .MaximumLength(150).WithMessage("Tên chi nhánh tối đa 150 ký tự.");

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("Quận/Huyện không được để trống.")
            .MaximumLength(50).WithMessage("Quận/Huyện tối đa 50 ký tự.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Địa chỉ chi nhánh không được để trống.")
            .MaximumLength(255).WithMessage("Địa chỉ tối đa 255 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.PhoneNumber != null);

        RuleFor(x => x.OperatingHours)
            .NotEmpty().WithMessage("Giờ hoạt động không được để trống.")
            .MaximumLength(50).WithMessage("Giờ hoạt động tối đa 50 ký tự.");
    }
}
