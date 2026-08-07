namespace StockTickerLite.Models;

public sealed class StockItem
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Note { get; set; } = "";
    public decimal? CostPrice { get; set; }
    public int? Position { get; set; }
    public decimal? UpperAlert { get; set; }
    public decimal? LowerAlert { get; set; }
    public decimal? ChangePercentAlert { get; set; }

    public string NormalizedCode => StockCode.Normalize(Code);
}

public static class StockCode
{
    public static string Normalize(string input)
    {
        var code = input.Trim().ToLowerInvariant();
        if (code.StartsWith("sh") || code.StartsWith("sz") || code.StartsWith("bj"))
            return code;

        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length != 6)
            return code;

        if (digits.StartsWith("60") || digits.StartsWith("68") ||
            digits.StartsWith("51") || digits.StartsWith("56") ||
            digits.StartsWith("58") || digits.StartsWith("11"))
            return "sh" + digits;

        if (digits.StartsWith("82") || digits.StartsWith("83") ||
            digits.StartsWith("87") || digits.StartsWith("88") ||
            digits.StartsWith("92"))
            return "bj" + digits;

        return "sz" + digits;
    }

    public static bool IsSupported(string input)
    {
        var normalized = Normalize(input);
        return normalized.Length == 8 &&
               (normalized.StartsWith("sh") ||
                normalized.StartsWith("sz") ||
                normalized.StartsWith("bj")) &&
               normalized[2..].All(char.IsDigit);
    }
}
