using FluentValidation;
using IIoT.WebApi.Data.DTO;

namespace IIoT.WebApi.Data.Validators;

public class CreateConnectionDtoValidator : AbstractValidator<CreateConnectionDto>
{
    public CreateConnectionDtoValidator()
    {
        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP-адрес не может быть пустым")
            .Matches(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$|^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$")
            .WithMessage("Некорректный формат IP-адреса или hostname");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Порт должен быть в диапазоне от 1 до 65535");

        RuleFor(x => x.Description)
            .MaximumLength(150).WithMessage("Описание не может превышать 150 символов");
    }
}

public class UpdateConnectionDtoValidator : AbstractValidator<UpdateConnectionDto>
{
    public UpdateConnectionDtoValidator()
    {
        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP-адрес не может быть пустым")
            .Matches(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$|^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$")
            .WithMessage("Некорректный формат IP-адреса или hostname");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Порт должен быть в диапазоне от 1 до 65535");

        RuleFor(x => x.Description)
            .MaximumLength(150).WithMessage("Описание не может превышать 150 символов");
    }
}
