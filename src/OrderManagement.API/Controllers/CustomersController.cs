using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.DTOs.Customers;
using OrderManagement.Application.Interfaces;

namespace OrderManagement.API.Controllers;

/// <summary>Gerenciamento de clientes.</summary>
[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
[Tags("Clientes")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    private readonly IValidator<CreateCustomerRequest> _createValidator;

    public CustomersController(ICustomerService service, IValidator<CreateCustomerRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    /// <summary>Cadastra um novo cliente.</summary>
    /// <response code="201">Cliente criado com sucesso.</response>
    /// <response code="400">Dados de entrada inválidos.</response>
    /// <response code="422">Regra de negócio violada (e-mail ou documento duplicado).</response>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Lista clientes com paginação.</summary>
    /// <param name="page">Número da página (padrão: 1).</param>
    /// <param name="pageSize">Itens por página (padrão: 20, máx: 100).</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);
        page = Math.Max(page, 1);
        var result = await _service.GetAllAsync(page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Consulta um cliente por ID.</summary>
    /// <response code="404">Cliente não encontrado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Ativa ou desativa um cliente.</summary>
    /// <response code="404">Cliente não encontrado.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateCustomerStatusRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, request, ct);
        return Ok(result);
    }
}
