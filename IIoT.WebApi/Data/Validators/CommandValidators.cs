using FluentValidation;
using IIoT.WebApi.Data.DTO;

namespace IIoT.WebApi.Data.Validators;

public class WriteCommandDtoValidator : AbstractValidator<WriteCommandDto>
{
    public WriteCommandDtoValidator()
    {
        RuleFor(x => x.TagId)
            .GreaterThan(0).WithMessage("TagId должен быть положительным");

        RuleFor(x => x.Value)
            .Must(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .WithMessage("Значение должно быть конечным числом");

        RuleFor(x => x.OperatorId)
            .NotEmpty().WithMessage("OperatorId не может быть пустым")
            .MaximumLength(100).WithMessage("OperatorId не может превышать 100 символов");
    }
}
