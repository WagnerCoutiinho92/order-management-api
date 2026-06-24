using FluentValidation;
using OrderManagement.Application.DTOs.Orders;

namespace OrderManagement.Application.Validators.Orders;

public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("O cliente é obrigatório.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("O pedido deve possuir ao menos um item.");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemValidator());
    }
}

public class CreateOrderItemValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("O produto é obrigatório.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");
    }
}
