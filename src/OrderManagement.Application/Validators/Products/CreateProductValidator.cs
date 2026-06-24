using FluentValidation;
using OrderManagement.Application.DTOs.Products;

namespace OrderManagement.Application.Validators.Products;

public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(300).WithMessage("O nome deve ter no máximo 300 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("A descrição deve ter no máximo 1000 caracteres.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("O estoque não pode ser negativo.");
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(300).WithMessage("O nome deve ter no máximo 300 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("A descrição deve ter no máximo 1000 caracteres.")
            .When(x => x.Description is not null);
    }
}

public class UpdateProductPriceValidator : AbstractValidator<UpdateProductPriceRequest>
{
    public UpdateProductPriceValidator()
    {
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.");
    }
}

public class UpdateProductStockValidator : AbstractValidator<UpdateProductStockRequest>
{
    public UpdateProductStockValidator()
    {
        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("O estoque não pode ser negativo.");
    }
}
