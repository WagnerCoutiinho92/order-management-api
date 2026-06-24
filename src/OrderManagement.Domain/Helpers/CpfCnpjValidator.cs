namespace OrderManagement.Domain.Helpers;

/// <summary>
/// Validates CPF and CNPJ using the official checksum algorithms.
/// </summary>
public static class CpfCnpjValidator
{
    public static bool IsValid(string document)
    {
        var digits = new string(document.Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            11 => IsValidCpf(digits),
            14 => IsValidCnpj(digits),
            _ => false
        };
    }

    private static bool IsValidCpf(string cpf)
    {
        // Rejects sequences of same digit
        if (cpf.Distinct().Count() == 1) return false;

        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (cpf[i] - '0') * (10 - i);

        var remainder = sum % 11;
        var firstDigit = remainder < 2 ? 0 : 11 - remainder;
        if (firstDigit != cpf[9] - '0') return false;

        sum = 0;
        for (var i = 0; i < 10; i++)
            sum += (cpf[i] - '0') * (11 - i);

        remainder = sum % 11;
        var secondDigit = remainder < 2 ? 0 : 11 - remainder;
        return secondDigit == cpf[10] - '0';
    }

    private static bool IsValidCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1) return false;

        int[] weights1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] weights2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var sum = weights1.Select((w, i) => w * (cnpj[i] - '0')).Sum();
        var remainder = sum % 11;
        var firstDigit = remainder < 2 ? 0 : 11 - remainder;
        if (firstDigit != cnpj[12] - '0') return false;

        sum = weights2.Select((w, i) => w * (cnpj[i] - '0')).Sum();
        remainder = sum % 11;
        var secondDigit = remainder < 2 ? 0 : 11 - remainder;
        return secondDigit == cnpj[13] - '0';
    }
}
