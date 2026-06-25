using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderManagement.Application.DTOs.Orders;
using OrderManagement.Application.Interfaces;

namespace OrderManagement.API.Controllers;

/// <summary>Gerenciamento de pedidos.</summary>
[ApiController]
[Route("api/pedidos")]
[Produces("application/json")]
[Tags("Pedidos")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;
    private readonly IValidator<CreateOrderRequest> _createValidator;

    public OrdersController(IOrderService service, IValidator<CreateOrderRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    /// <summary>
    /// Cria um novo pedido.
    /// O preço unitário e o valor total são calculados pela API — valores enviados pelo cliente serão ignorados.
    /// O estoque é debitado automaticamente ao criar o pedido.
    /// </summary>
    /// <response code="201">Pedido criado com sucesso.</response>
    /// <response code="400">Dados de entrada inválidos.</response>
    /// <response code="422">Regra de negócio violada (cliente inativo, produto sem estoque, etc.).</response>
    [HttpPost]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Lista pedidos com paginação.</summary>
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

    /// <summary>Consulta um pedido por ID (inclui itens e histórico de status).</summary>
    /// <response code="404">Pedido não encontrado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Altera o status do pedido.
    /// Transições permitidas: Criado→Pago, Pago→Enviado, Criado→Cancelado.
    /// Cancelamento antes do envio devolve o estoque.
    /// Status igual ao atual é tratado como idempotente (retorna 200 sem alteração).
    /// </summary>
    /// <response code="200">Status atualizado (ou já estava no status solicitado).</response>
    /// <response code="404">Pedido não encontrado.</response>
    /// <response code="422">Transição de status inválida.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, request, ct);
        return Ok(result);
    }
}
