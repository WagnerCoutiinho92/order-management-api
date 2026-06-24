using FluentAssertions;
using Moq;
using OrderManagement.Application.DTOs.Customers;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Services;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Domain.Interfaces;
using OrderManagement.Domain.Interfaces.Repositories;
using Xunit;

namespace OrderManagement.Tests.Application;

public class CustomerServiceTests
{
    private readonly Mock<ICustomerRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITimezoneConverter> _tzMock = new();
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _tzMock.Setup(t => t.ToSaoPaulo(It.IsAny<DateTime>()))
            .Returns((DateTime d) => new DateTimeOffset(d, TimeSpan.FromHours(-3)));

        _sut = new CustomerService(_repoMock.Object, _uowMock.Object, _tzMock.Object);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCustomerResponse()
    {
        _repoMock.Setup(r => r.ExistsActiveWithEmailAsync(It.IsAny<string>(), null, default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExistsActiveWithDocumentAsync(It.IsAny<string>(), null, default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Customer>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);

        var request = new CreateCustomerRequest("João Silva", "joao@email.com", "529.982.247-25");
        var result = await _sut.CreateAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("João Silva");
        result.Email.Should().Be("joao@email.com");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateEmail_ThrowsBusinessRuleException()
    {
        _repoMock.Setup(r => r.ExistsActiveWithEmailAsync(It.IsAny<string>(), null, default)).ReturnsAsync(true);

        var request = new CreateCustomerRequest("João", "joao@email.com", "529.982.247-25");
        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*e-mail*");
    }

    [Fact]
    public async Task Create_DuplicateDocument_ThrowsBusinessRuleException()
    {
        _repoMock.Setup(r => r.ExistsActiveWithEmailAsync(It.IsAny<string>(), null, default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExistsActiveWithDocumentAsync(It.IsAny<string>(), null, default)).ReturnsAsync(true);

        var request = new CreateCustomerRequest("João", "joao@email.com", "529.982.247-25");
        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*documento*");
    }

    [Fact]
    public async Task GetById_ExistingCustomer_ReturnsResponse()
    {
        var customer = new Customer("Maria", "maria@email.com", "529.982.247-25");
        _repoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);

        var result = await _sut.GetByIdAsync(customer.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(customer.Id);
    }

    [Fact]
    public async Task GetById_NonExistingCustomer_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Customer?)null);
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatus_Deactivate_ShouldReturnInactiveCustomer()
    {
        var customer = new Customer("Ana", "ana@email.com", "529.982.247-25");
        _repoMock.Setup(r => r.GetByIdAsync(customer.Id, default)).ReturnsAsync(customer);
        _uowMock.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);

        var result = await _sut.UpdateStatusAsync(customer.Id, new UpdateCustomerStatusRequest(false));

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStatus_NonExistingCustomer_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Customer?)null);

        var act = async () => await _sut.UpdateStatusAsync(Guid.NewGuid(), new UpdateCustomerStatusRequest(false));

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
