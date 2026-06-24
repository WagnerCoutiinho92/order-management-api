using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs.Customers;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;

namespace OrderManagement.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITimezoneConverter _tz;

    public CustomerService(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork,
        ITimezoneConverter tz)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _tz = tz;
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedDoc = new string(request.Document.Where(char.IsDigit).ToArray());

        if (await _customerRepository.ExistsActiveWithEmailAsync(normalizedEmail, ct: ct))
            throw new BusinessRuleException("DUPLICATE_EMAIL", "Já existe um cliente ativo com este e-mail.");

        if (await _customerRepository.ExistsActiveWithDocumentAsync(normalizedDoc, ct: ct))
            throw new BusinessRuleException("DUPLICATE_DOCUMENT", "Já existe um cliente ativo com este documento.");

        var customer = new Customer(request.Name, request.Email, request.Document);
        await _customerRepository.AddAsync(customer, ct);
        await _unitOfWork.CommitAsync(ct);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, ct);
        return customer is null ? null : ToResponse(customer);
    }

    public async Task<PagedResult<CustomerResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var customers = await _customerRepository.GetAllAsync(page, pageSize, ct);
        var total = await _customerRepository.CountAsync(ct);
        return PagedResult<CustomerResponse>.Create(customers.Select(ToResponse), total, page, pageSize);
    }

    public async Task<CustomerResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Cliente", id);

        if (request.IsActive)
            customer.Activate();
        else
            customer.Deactivate();

        await _unitOfWork.CommitAsync(ct);
        return ToResponse(customer);
    }

    private CustomerResponse ToResponse(Customer c) => new(
        c.Id, c.Name, c.Email, c.Document, c.IsActive,
        _tz.ToSaoPaulo(c.CreatedAt),
        _tz.ToSaoPaulo(c.UpdatedAt));
}
