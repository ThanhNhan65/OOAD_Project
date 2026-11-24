using FluentValidation;
using OOAD.Application.DTOs;

namespace OOAD.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Vui lòng nhập Email hoặc Tên đăng nhập.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly string[] AllowedRoles = ["MEMBER", "TRAINER"];

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .MaximumLength(150).WithMessage("Email tối đa 150 ký tự.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên tối đa 150 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Vai trò đăng ký là bắt buộc.")
            .Must(r => AllowedRoles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Chỉ cho phép đăng ký vai trò MEMBER hoặc TRAINER.");

        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Huấn luyện viên bắt buộc phải chọn chi nhánh làm việc.")
            .When(x => x.Role.Trim().Equals("TRAINER", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.ExperienceYears)
            .GreaterThanOrEqualTo(0).WithMessage("Số năm kinh nghiệm không được âm.");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.Phone != null);

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("Địa chỉ tối đa 255 ký tự.")
            .When(x => x.Address != null);

        RuleFor(x => x.Specialty)
            .MaximumLength(255).WithMessage("Chuyên môn tối đa 255 ký tự.")
            .When(x => x.Specialty != null);

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Giới thiệu bản thân tối đa 1000 ký tự.")
            .When(x => x.Bio != null);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới.")
            .MinimumLength(6).WithMessage("Mật khẩu mới phải có ít nhất 6 ký tự.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu mới không khớp.")
            .When(x => !string.IsNullOrEmpty(x.ConfirmPassword));
    }
}

public class UpdateMemberProfileRequestValidator : AbstractValidator<UpdateMemberProfileRequest>
{
    public UpdateMemberProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên tối đa 150 ký tự.");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.Phone != null);

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("Địa chỉ tối đa 255 ký tự.")
            .When(x => x.Address != null);

        RuleFor(x => x.Specialty)
            .MaximumLength(255).WithMessage("Chuyên môn tối đa 255 ký tự.")
            .When(x => x.Specialty != null);

        RuleFor(x => x.ExperienceYears)
            .GreaterThanOrEqualTo(0).WithMessage("Số năm kinh nghiệm không được âm.")
            .When(x => x.ExperienceYears.HasValue);

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Giới thiệu bản thân tối đa 1000 ký tự.")
            .When(x => x.Bio != null);
    }
}

public class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    private static readonly string[] AllowedStaffRoles = ["STAFF", "RECEPTIONIST", "TRAINER", "ADMIN"];

    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .MaximumLength(150).WithMessage("Email tối đa 150 ký tự.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên tối đa 150 ký tự.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Vai trò là bắt buộc.")
            .Must(r => AllowedStaffRoles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Vai trò phải là STAFF, RECEPTIONIST, TRAINER hoặc ADMIN.");

        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Vui lòng chọn chi nhánh trực thuộc.")
            .When(x => !x.Role.Trim().Equals("ADMIN", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.Password)
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Password));

        RuleFor(x => x.ExperienceYears)
            .GreaterThanOrEqualTo(0).WithMessage("Số năm kinh nghiệm không được âm.");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.Phone != null);

        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Chức vụ tối đa 100 ký tự.");

        RuleFor(x => x.Specialty)
            .MaximumLength(255).WithMessage("Chuyên môn tối đa 255 ký tự.")
            .When(x => x.Specialty != null);

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Giới thiệu tối đa 1000 ký tự.")
            .When(x => x.Bio != null);
    }
}

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    private static readonly string[] AllowedStatuses = ["ACTIVE", "LOCKED"];

    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => AllowedStatuses.Contains(s!.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái tài khoản phải là ACTIVE hoặc LOCKED.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

public class UpdateUserAdminRequestValidator : AbstractValidator<UpdateUserAdminRequest>
{
    public UpdateUserAdminRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên tối đa 150 ký tự.");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.")
            .When(x => x.Phone != null);

        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Chức vụ tối đa 100 ký tự.")
            .When(x => x.Position != null);

        RuleFor(x => x.Specialty)
            .MaximumLength(255).WithMessage("Chuyên môn tối đa 255 ký tự.")
            .When(x => x.Specialty != null);

        RuleFor(x => x.ExperienceYears)
            .GreaterThanOrEqualTo(0).WithMessage("Số năm kinh nghiệm không được âm.")
            .When(x => x.ExperienceYears.HasValue);

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Giới thiệu tối đa 1000 ký tự.")
            .When(x => x.Bio != null);
    }
}

public class ResetUserPasswordRequestValidator : AbstractValidator<ResetUserPasswordRequest>
{
    public ResetUserPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .MinimumLength(6).WithMessage("Mật khẩu mới phải có ít nhất 6 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.NewPassword));
    }
}
