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
        // Validate customer before entering the transaction (no lock needed here)
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Cliente", request.CustomerId);

        if (!customer.IsActive)
            throw new BusinessRuleException("INACTIVE_CUSTOMER", "Clientes inativos não podem criar pedidos.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        Order? order = null;
        List<Domain.Entities.Product>? lockedProducts = null;

        // Execute stock debit + order creation inside a single transaction.
        // GetByIdsForUpdateAsync issues UPDLOCK so the lock is held until Commit.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            lockedProducts = await _productRepository.GetByIdsForUpdateAsync(productIds, ct);

            // Validate all products exist
            var missing = productIds.Except(lockedProducts.Select(p => p.Id)).ToList();
            if (missing.Any())
                throw new BusinessRuleException("PRODUCT_NOT_FOUND",
                    $"Produto(s) não encontrado(s): {string.Join(", ", missing)}.");

            var productMap = lockedProducts.ToDictionary(p => p.Id);

            // Validate active + sufficient stock
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

            // Build items with price snapshot
            var orderId = Guid.NewGuid();
            var orderItems = request.Items
                .Select(item => new OrderItem(orderId, item.ProductId, item.Quantity, productMap[item.ProductId].Price))
                .ToList();

            order = new Order(request.CustomerId, orderItems);

            // Debit stock — all or nothing
            foreach (var item in request.Items)
                productMap[item.ProductId].DebitStock(item.Quantity);

            await _orderRepository.AddAsync(order, ct);
            // SaveChanges + Commit happen automatically inside ExecuteInTransactionAsync
        }, ct);

        return BuildOrderResponse(order!, customer, lockedProducts!);
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
        // Write path: load only what's needed for saving — no Customer/Product navigations.
        // This prevents EF Core from tracking unrelated entities during SaveChanges,
        // which was causing spurious DbUpdateConcurrencyException.
        var order = await _orderRepository.GetByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException("Pedido", id);

        var transitioned = order.TransitionTo(request.Status, request.Reason);

        if (transitioned && request.Status == Domain.Enums.OrderStatus.Cancelled && order.CanReturnStock())
        {
            // Return stock inside a transaction so the UPDLOCK covers the UPDATE
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var productIds = order.Items.Select(i => i.ProductId).ToList();
                var products = await _productRepository.GetByIdsForUpdateAsync(productIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                foreach (var item in order.Items)
                {
                    if (productMap.TryGetValue(item.ProductId, out var product))
                        product.ReturnStock(item.Quantity);
                }
            }, ct);
        }
        else if (transitioned)
        {
            await _unitOfWork.CommitAsync(ct);
        }
        // If !transitioned (idempotent — same status), nothing to save.

        // Reload with full navigation for the response (AsNoTracking, fresh read).
        var fullOrder = await _orderRepository.GetByIdAsync(id, ct);
        return ToResponse(fullOrder!);
    }

    private OrderResponse BuildOrderResponse(Order order, Domain.Entities.Customer customer, List<Domain.Entities.Product> products)
    {
        var productMap = products.ToDictionary(p => p.Id);
        return new OrderResponse(
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
            i.Id, i.ProductId,
            i.Product?.Name ?? string.Empty,
            i.Quantity, i.UnitPrice, i.TotalValue)).ToList(),
        order.StatusHistory.Select(ToHistoryResponse).ToList());

    private OrderStatusHistoryResponse ToHistoryResponse(Domain.Entities.OrderStatusHistory h) => new(
        h.Id,
        h.PreviousStatus?.ToString(),
        h.NewStatus.ToString(),
        _tz.ToSaoPaulo(h.ChangedAt),
        h.Reason);
}
