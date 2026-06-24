using FluentValidation;
using OrderManagement.Application.DTOs.Customers;
using OrderManagement.Domain.Helpers;

namespace OrderManagement.Application.Validators.Customers;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(200).WithMessage("O nome deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("O e-mail informado não é válido.")
            .MaximumLength(254).WithMessage("O e-mail deve ter no máximo 254 caracteres.");

        RuleFor(x => x.Document)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .Must(doc => CpfCnpjValidator.IsValid(doc))
            .WithMessage("O documento informado não é um CPF ou CNPJ válido.");
    }
}
