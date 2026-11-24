using FluentValidation;
using OOAD.Application.DTOs;

namespace OOAD.Application.Validators;

public class CreateBodyMeasurementRequestValidator : AbstractValidator<CreateBodyMeasurementRequest>
{
    public CreateBodyMeasurementRequestValidator()
    {
        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Cân nặng phải lớn hơn 0 kg.")
            .LessThanOrEqualTo(500).WithMessage("Cân nặng không hợp lệ (tối đa 500 kg).");

        RuleFor(x => x.Height)
            .GreaterThan(0).WithMessage("Chiều cao phải lớn hơn 0 cm.")
            .LessThanOrEqualTo(300).WithMessage("Chiều cao tối đa 300 cm.")
            .When(x => x.Height.HasValue);

        RuleFor(x => x.Bmi)
            .GreaterThan(0).WithMessage("Chỉ số BMI phải lớn hơn 0.")
            .LessThanOrEqualTo(100).WithMessage("Chỉ số BMI tối đa 100.")
            .When(x => x.Bmi.HasValue);

        RuleFor(x => x.BodyFat)
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ mỡ cơ thể phải từ 0% đến 100%.")
            .When(x => x.BodyFat.HasValue);

        RuleFor(x => x.MuscleMass)
            .InclusiveBetween(0, 300).WithMessage("Khối lượng cơ phải từ 0 đến 300 kg.")
            .When(x => x.MuscleMass.HasValue);

        RuleFor(x => x.Waist)
            .GreaterThan(0).WithMessage("Vòng eo phải lớn hơn 0 cm.")
            .LessThanOrEqualTo(300).WithMessage("Vòng eo tối đa 300 cm.")
            .When(x => x.Waist.HasValue);

        RuleFor(x => x.Chest)
            .GreaterThan(0).WithMessage("Vòng ngực phải lớn hơn 0 cm.")
            .LessThanOrEqualTo(300).WithMessage("Vòng ngực tối đa 300 cm.")
            .When(x => x.Chest.HasValue);

        RuleFor(x => x.Hips)
            .GreaterThan(0).WithMessage("Vòng mông phải lớn hơn 0 cm.")
            .LessThanOrEqualTo(300).WithMessage("Vòng mông tối đa 300 cm.")
            .When(x => x.Hips.HasValue);

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.")
            .When(x => x.Note != null);
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Số sao đánh giá phải từ 1 đến 5 sao.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("Nội dung đánh giá tối đa 1000 ký tự.")
            .When(x => x.Comment != null);
    }
}
