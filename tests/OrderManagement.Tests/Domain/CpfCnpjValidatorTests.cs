using FluentAssertions;
using OrderManagement.Domain.Helpers;
using Xunit;

namespace OrderManagement.Tests.Domain;

public class CpfCnpjValidatorTests
{
    // ── CPF válidos ──────────────────────────────────────────────────────────
    [Theory]
    [InlineData("529.982.247-25")]   // formatado
    [InlineData("52998224725")]      // apenas dígitos
    [InlineData("111.444.777-35")]
    public void IsValid_ValidCpf_ReturnsTrue(string cpf)
        => CpfCnpjValidator.IsValid(cpf).Should().BeTrue();

    // ── CPF inválidos ─────────────────────────────────────────────────────────
    [Theory]
    [InlineData("000.000.000-00")]   // sequência repetida
    [InlineData("111.111.111-11")]
    [InlineData("529.982.247-26")]   // dígito verificador errado
    [InlineData("12345678900")]      // dígitos errados
    [InlineData("1234567")]          // tamanho errado
    public void IsValid_InvalidCpf_ReturnsFalse(string cpf)
        => CpfCnpjValidator.IsValid(cpf).Should().BeFalse();

    // ── CNPJ válidos ─────────────────────────────────────────────────────────
    [Theory]
    [InlineData("11.222.333/0001-81")]   // formatado
    [InlineData("11222333000181")]        // apenas dígitos
    [InlineData("45.997.418/0001-53")]
    public void IsValid_ValidCnpj_ReturnsTrue(string cnpj)
        => CpfCnpjValidator.IsValid(cnpj).Should().BeTrue();

    // ── CNPJ inválidos ─────────────────────────────────────────────────────
    [Theory]
    [InlineData("00.000.000/0000-00")]   // sequência repetida
    [InlineData("11.111.111/1111-11")]
    [InlineData("11.222.333/0001-82")]   // dígito verificador errado
    [InlineData("1234567890123")]        // tamanho errado
    public void IsValid_InvalidCnpj_ReturnsFalse(string cnpj)
        => CpfCnpjValidator.IsValid(cnpj).Should().BeFalse();

    // ── Entradas vazias / nulas ───────────────────────────────────────────────
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    public void IsValid_InvalidInput_ReturnsFalse(string input)
        => CpfCnpjValidator.IsValid(input).Should().BeFalse();
}
