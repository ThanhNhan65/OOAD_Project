using FluentValidation;
using OOAD.Application.DTOs.Equipment;

namespace OOAD.Application.Validators;

public class CreateEquipmentDtoValidator : AbstractValidator<CreateEquipmentDto>
{
    public CreateEquipmentDtoValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Vui lòng chọn chi nhánh cho thiết bị.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thiết bị không được để trống.")
            .MaximumLength(150).WithMessage("Tên thiết bị tối đa 150 ký tự.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Loại thiết bị không được để trống.")
            .MaximumLength(100).WithMessage("Loại thiết bị tối đa 100 ký tự.");
    }
}

public class UpdateEquipmentStatusDtoValidator : AbstractValidator<UpdateEquipmentStatusDto>
{
    private static readonly string[] AllowedStatuses =
    [
        "active", "operational", "repairing", "undermaintenance", "broken", "retired", "decommissioned"
    ];

    public UpdateEquipmentStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái thiết bị không được để trống.")
            .MaximumLength(50).WithMessage("Trạng thái thiết bị tối đa 50 ký tự.")
            .Must(s => AllowedStatuses.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái thiết bị không hợp lệ.");
    }
}

public class CreateEquipmentReportDtoValidator : AbstractValidator<CreateEquipmentReportDto>
{
    public CreateEquipmentReportDtoValidator()
    {
        RuleFor(x => x.EquipmentId)
            .NotEmpty().WithMessage("Vui lòng chọn thiết bị gặp sự cố.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả sự cố không được để trống.")
            .MaximumLength(1000).WithMessage("Mô tả sự cố tối đa 1000 ký tự.");
    }
}

public class UpdateReportStatusDtoValidator : AbstractValidator<UpdateReportStatusDto>
{
    private static readonly string[] AllowedStatuses =
    [
        "pending", "open", "in_progress", "investigating", "resolved", "fixed", "rejected", "closed"
    ];

    public UpdateReportStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái xử lý sự cố không được để trống.")
            .MaximumLength(50).WithMessage("Trạng thái sự cố tối đa 50 ký tự.")
            .Must(s => AllowedStatuses.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái xử lý sự cố không hợp lệ.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Ghi chú xử lý tối đa 1000 ký tự.")
            .When(x => x.Note != null);
    }
}
