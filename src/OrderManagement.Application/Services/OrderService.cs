using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs.Orders;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;

namespace OrderManagement.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITimezoneConverter _tz;

    public OrderService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ITimezoneConverter tz)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _tz = tz;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // 1. Validate customer
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Cliente", request.CustomerId);

        if (!customer.IsActive)
            throw new BusinessRuleException("INACTIVE_CUSTOMER", "Clientes inativos não podem criar pedidos.");

        // 2. Lock and load products inside a transaction (pessimistic concurrency)
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _productRepository.GetByIdsForUpdateAsync(productIds, ct);

        // 3. Validate all products exist
        var missing = productIds.Except(products.Select(p => p.Id)).ToList();
        if (missing.Any())
            throw new BusinessRuleException("PRODUCT_NOT_FOUND",
                $"Produto(s) não encontrado(s): {string.Join(", ", missing)}.");

        // 4. Validate all products are active and have sufficient stock
        var productMap = products.ToDictionary(p => p.Id);
        foreach (var item in request.Items)
        {
            var product = productMap[item.ProductId];
            if (!product.IsActive)
                throw new BusinessRuleException("INACTIVE_PRODUCT",
                    $"O produto '{product.Name}' está inativo e não pode ser adicionado ao pedido.");

            if (product.StockQuantity < item.Quantity)
                throw new BusinessRuleException("INSUFFICIENT_STOCK",
                    $"Estoque insuficiente para '{product.Name}'. Disponível: {product.StockQuantity}, Solicitado: {item.Quantity}.");
        }

        // 5. Build order items with price snapshot
        var orderId = Guid.NewGuid();
        var orderItems = request.Items.Select(item =>
        {
            var product = productMap[item.ProductId];
            return new OrderItem(orderId, item.ProductId, item.Quantity, product.Price);
        }).ToList();

        // 6. Create order aggregate
        var order = new Order(request.CustomerId, orderItems);

        // 7. Debit stock — all or nothing (if any throws, transaction rolls back)
        foreach (var item in request.Items)
            productMap[item.ProductId].DebitStock(item.Quantity);

        await _orderRepository.AddAsync(order, ct);
        await _unitOfWork.CommitAsync(ct);

        return await BuildOrderResponseAsync(order, customer, productMap.Values.ToList(), ct);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, ct);
        return order is null ? null : ToResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var orders = await _orderRepository.GetAllAsync(page, pageSize, ct);
        var total = await _orderRepository.CountAsync(ct);
        return PagedResult<OrderResponse>.Create(orders.Select(ToResponse), total, page, pageSize);
    }

    public async Task<OrderResponse> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Pedido", id);

        var transitioned = order.TransitionTo(request.Status, request.Reason);

        // Return stock on cancellation only if order hasn't been shipped
        if (transitioned && request.Status == Domain.Enums.OrderStatus.Cancelled && order.CanReturnStock())
        {
            var productIds = order.Items.Select(i => i.ProductId).ToList();
            var products = await _productRepository.GetByIdsForUpdateAsync(productIds, ct);
            var productMap = products.ToDictionary(p => p.Id);

            foreach (var item in order.Items)
            {
                if (productMap.TryGetValue(item.ProductId, out var product))
                    product.ReturnStock(item.Quantity);
            }
        }

        await _unitOfWork.CommitAsync(ct);
        return ToResponse(order);
    }

    private Task<OrderResponse> BuildOrderResponseAsync(Order order, Customer customer, List<Product> products, CancellationToken ct)
    {
        var productMap = products.ToDictionary(p => p.Id);
        var response = new OrderResponse(
            order.Id,
            order.CustomerId,
            customer.Name,
            order.Status,
            order.Status.ToString(),
            order.TotalValue,
            _tz.ToSaoPaulo(order.CreatedAt),
            order.Items.Select(i => new OrderItemResponse(
                i.Id,
                i.ProductId,
                productMap.TryGetValue(i.ProductId, out var p) ? p.Name : string.Empty,
                i.Quantity,
                i.UnitPrice,
                i.TotalValue)).ToList(),
            order.StatusHistory.Select(ToHistoryResponse).ToList());
        return Task.FromResult(response);
    }

    private OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Customer?.Name ?? string.Empty,
        order.Status,
        order.Status.ToString(),
        order.TotalValue,
        _tz.ToSaoPaulo(order.CreatedAt),
        order.Items.Select(i => new OrderItemResponse(
            i.Id,
            i.ProductId,
            i.Product?.Name ?? string.Empty,
            i.Quantity,
            i.UnitPrice,
            i.TotalValue)).ToList(),
        order.StatusHistory.Select(ToHistoryResponse).ToList());

    private OrderStatusHistoryResponse ToHistoryResponse(Domain.Entities.OrderStatusHistory h) => new(
        h.Id,
        h.PreviousStatus?.ToString(),
        h.NewStatus.ToString(),
        _tz.ToSaoPaulo(h.ChangedAt),
        h.Reason);
}
