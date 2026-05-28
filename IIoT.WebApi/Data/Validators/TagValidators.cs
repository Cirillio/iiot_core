using FluentValidation;
using IIoT.Shared.Models;
using IIoT.WebApi.Data.DTO;

namespace IIoT.WebApi.Data.Validators;

public class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
{
    public CreateTagDtoValidator()
    {
        RuleFor(x => x.DeviceId).GreaterThan(0).WithMessage("DeviceId должен быть больше 0");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Имя датчика не может быть пустым")
            .MaximumLength(100)
            .WithMessage("Имя датчика не может превышать 100 символов");

        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Slug не может быть пустым")
            .MaximumLength(50)
            .WithMessage("Slug не может превышать 50 символов")
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Slug может содержать только строчные буквы, цифры и подчеркивания");

        RuleFor(x => x.DataType)
            .Must(x => Enum.TryParse<TagDataType>(x?.Replace("_", ""), true, out _))
            .WithMessage("Недопустимый тип данных. Допустимые: ANALOG_RAW, ANALOG_PHYSICAL, DIGITAL, VIRTUAL");

        RuleFor(x => x.RawDataType)
            .Must(x => string.IsNullOrEmpty(x) || Enum.TryParse<RawDataType>(x.Replace("_", ""), true, out _))
            .WithMessage("Недопустимый тип данных регистра. Допустимые: INT16, UINT16, INT32, UINT32, FLOAT32, FLOAT64");

        RuleFor(x => x.PortNumber)
            .InclusiveBetween(0, 65535)
            .WithMessage("Номер порта должен быть в диапазоне от 0 до 65535");
    }
}

public class UpdateTagDtoValidator : AbstractValidator<UpdateTagDto>
{
    public UpdateTagDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Имя датчика не может быть пустым")
            .MaximumLength(100)
            .WithMessage("Имя датчика не может превышать 100 символов");

        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Slug не может быть пустым")
            .MaximumLength(50)
            .WithMessage("Slug не может превышать 50 символов")
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Slug может содержать только строчные буквы, цифры и подчеркивания");

        RuleFor(x => x.DataType)
            .Must(x => Enum.TryParse<TagDataType>(x?.Replace("_", ""), true, out _))
            .WithMessage("Недопустимый тип данных. Допустимые: ANALOG_RAW, ANALOG_PHYSICAL, DIGITAL, VIRTUAL");

        RuleFor(x => x.RawDataType)
            .Must(x => string.IsNullOrEmpty(x) || Enum.TryParse<RawDataType>(x.Replace("_", ""), true, out _))
            .WithMessage("Недопустимый тип данных регистра. Допустимые: INT16, UINT16, INT32, UINT32, FLOAT32, FLOAT64");

        RuleFor(x => x.PortNumber)
            .InclusiveBetween(0, 65535)
            .WithMessage("Номер порта должен быть в диапазоне от 0 до 65535");
    }
}
