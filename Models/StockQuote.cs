namespace StockTickerLite.Models;

public sealed record StockQuote(
    string Code,
    string Name,
    decimal Current,
    decimal PreviousClose,
    decimal Open,
    decimal High,
    decimal Low,
    long Volume,
    decimal Amount,
    long Bid1Volume,
    decimal Bid1Price,
    long Ask1Volume,
    decimal Ask1Price,
    DateTime? QuoteTime,
    bool IsPreMarketFallback = false)
{
    public decimal Change => Current - PreviousClose;
    public decimal ChangePercent =>
        PreviousClose == 0 ? 0 : Change / PreviousClose * 100;

    public long SealedVolume
    {
        get
        {
            if (PreviousClose <= 0 || !TryGetLimitRate(out var rate)) return 0;
            var upper = decimal.Round(PreviousClose * (1 + rate), 2, MidpointRounding.AwayFromZero);
            var lower = decimal.Round(PreviousClose * (1 - rate), 2, MidpointRounding.AwayFromZero);
            if (Current == upper && Ask1Price <= 0 && Bid1Price == Current) return Bid1Volume;
            if (Current == lower && Bid1Price <= 0 && Ask1Price == Current) return Ask1Volume;
            return 0;
        }
    }

    private bool TryGetLimitRate(out decimal rate)
    {
        var code = Code.ToLowerInvariant();
        if (code.StartsWith("bj")) { rate = 0.30m; return true; }
        if (code.StartsWith("sh688") || code.StartsWith("sz300") || code.StartsWith("sz301")) { rate = 0.20m; return true; }
        if (code.StartsWith("sh60") || code.StartsWith("sz00"))
        {
            rate = Name.Contains("ST", StringComparison.OrdinalIgnoreCase) ? 0.05m : 0.10m;
            return true;
        }
        rate = 0;
        return false;
    }
}





