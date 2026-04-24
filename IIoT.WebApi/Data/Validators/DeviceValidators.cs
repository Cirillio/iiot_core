using FluentValidation;
using IIoT.WebApi.Data.DTO;

namespace IIoT.WebApi.Data.Validators;

public class CreateDeviceDtoValidator : AbstractValidator<CreateDeviceDto>
{
    public CreateDeviceDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя устройства не может быть пустым")
            .MaximumLength(100).WithMessage("Имя устройства не может превышать 100 символов");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP-адрес не может быть пустым")
            .Matches(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$")
            .WithMessage("Некорректный формат IP-адреса");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Порт должен быть в диапазоне от 1 до 65535");

        RuleFor(x => x.SlaveId)
            .InclusiveBetween(1, 247).WithMessage("Slave ID должен быть в диапазоне от 1 до 247");
    }
}

public class UpdateDeviceDtoValidator : AbstractValidator<UpdateDeviceDto>
{
    public UpdateDeviceDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя устройства не может быть пустым")
            .MaximumLength(100).WithMessage("Имя устройства не может превышать 100 символов");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP-адрес не может быть пустым")
            .Matches(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$")
            .WithMessage("Некорректный формат IP-адреса");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Порт должен быть в диапазоне от 1 до 65535");

        RuleFor(x => x.SlaveId)
            .InclusiveBetween(1, 247).WithMessage("Slave ID должен быть в диапазоне от 1 до 247");
    }
}
