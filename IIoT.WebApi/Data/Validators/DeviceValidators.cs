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

        RuleFor(x => x.ConnectionId)
            .GreaterThan(0).WithMessage("ConnectionId должен быть больше 0");

        RuleFor(x => x.SlaveId)
            .InclusiveBetween(1, 247).WithMessage("Slave ID должен быть в диапазоне от 1 до 247");

        RuleFor(x => x.MaxRegisterSpan)
            .InclusiveBetween(1, 2000).WithMessage("MaxRegisterSpan должен быть в диапазоне от 1 до 2000 (для регистров коллектор урежет до 125)");

        RuleFor(x => x.MaxBitSpan)
            .InclusiveBetween(1, 2000).WithMessage("MaxBitSpan должен быть в диапазоне от 1 до 2000");
    }
}

public class UpdateDeviceDtoValidator : AbstractValidator<UpdateDeviceDto>
{
    public UpdateDeviceDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя устройства не может быть пустым")
            .MaximumLength(100).WithMessage("Имя устройства не может превышать 100 символов");

        RuleFor(x => x.ConnectionId)
            .GreaterThan(0).WithMessage("ConnectionId должен быть больше 0");

        RuleFor(x => x.SlaveId)
            .InclusiveBetween(1, 247).WithMessage("Slave ID должен быть в диапазоне от 1 до 247");

        RuleFor(x => x.MaxRegisterSpan)
            .InclusiveBetween(1, 2000).WithMessage("MaxRegisterSpan должен быть в диапазоне от 1 до 2000 (для регистров коллектор урежет до 125)");

        RuleFor(x => x.MaxBitSpan)
            .InclusiveBetween(1, 2000).WithMessage("MaxBitSpan должен быть в диапазоне от 1 до 2000");
    }
}
