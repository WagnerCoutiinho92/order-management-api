using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderManagement.Application.DTOs.Products;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Validators.Products;

namespace OrderManagement.API.Controllers;

/// <summary>Gerenciamento de produtos.</summary>
[ApiController]
[Route("api/produtos")]
[Produces("application/json")]
[Tags("Produtos")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;
    private readonly IValidator<UpdateProductPriceRequest> _priceValidator;
    private readonly IValidator<UpdateProductStockRequest> _stockValidator;

    public ProductsController(
        IProductService service,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator,
        IValidator<UpdateProductPriceRequest> priceValidator,
        IValidator<UpdateProductStockRequest> stockValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _priceValidator = priceValidator;
        _stockValidator = stockValidator;
    }

    /// <summary>Cadastra um novo produto.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Lista produtos com paginação.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        page = Math.Max(page, 1);
        return Ok(await _service.GetAllAsync(page, pageSize, ct));
    }

    /// <summary>Consulta um produto por ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Atualiza nome e descrição do produto.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return Ok(await _service.UpdateAsync(id, request, ct));
    }

    /// <summary>Atualiza o preço do produto.</summary>
    [HttpPatch("{id:guid}/preco")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdateProductPriceRequest request, CancellationToken ct)
    {
        var validation = await _priceValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return Ok(await _service.UpdatePriceAsync(id, request, ct));
    }

    /// <summary>Atualiza o estoque do produto.</summary>
    [HttpPatch("{id:guid}/estoque")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateProductStockRequest request, CancellationToken ct)
    {
        var validation = await _stockValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return Ok(await _service.UpdateStockAsync(id, request, ct));
    }

    /// <summary>Ativa ou inativa um produto. Requer perfil Admin.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateProductStatusRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateStatusAsync(id, request, ct));
}
