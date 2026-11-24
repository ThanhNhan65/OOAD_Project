using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using DomainValidationException = OOAD.Domain.Exceptions.ValidationException;

namespace OOAD.API.Filters;

/// <summary>
/// Filter kiểm tra dữ liệu đầu vào tự động bằng FluentValidation trước khi request đi vào Controller Action.
/// Nếu không hợp lệ sẽ ném Domain ValidationException để ExceptionHandlingMiddleware trả về 400 Bad Request đồng nhất.
/// </summary>
public class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(arg),
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                var message = string.Join(" ", result.Errors.Select(e => e.ErrorMessage).Distinct());
                throw new DomainValidationException(message);
            }
        }

        await next();
    }
}
