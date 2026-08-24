using System.Net;
using System.Text;
using StockTickerLite.Models;

namespace StockTickerLite.Services;

public sealed record StockSearchResult(string Code, string Name, string Market)
{
    public override string ToString() => $"{Code}    {Name}    {Market}";
}

public sealed class SinaStockSearchService : IDisposable
{
    private readonly HttpClient _client;

    public SinaStockSearchService()
    {
        _client = DirectHttpClient.Create(
            TimeSpan.FromSeconds(6),
            "https://suggest3.sinajs.cn/suggest/");
        _client.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn/");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 StockTickerLite/1.0");
    }

    public async Task<IReadOnlyList<StockSearchResult>> SearchAsync(string keyword, CancellationToken cancellationToken)
    {
        keyword = keyword.Trim();
        if (keyword.Length == 0) return [];
        var bytes = await _client.GetByteArrayAsync("?key=" + Uri.EscapeDataString(keyword), cancellationToken);
        var text = Encoding.GetEncoding("GB18030").GetString(bytes);
        var firstQuote = text.IndexOf('"');
        var lastQuote = text.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote + 1) return [];
        var body = text[(firstQuote + 1)..lastQuote];
        var results = new List<StockSearchResult>();
        foreach (var entry in body.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = entry.Split(',');
            if (fields.Length < 5) continue;
            var type = fields[1].Trim();
            var code = StockCode.Normalize(fields[3]);
            var name = fields[4].Trim();
            if (!StockCode.IsSupported(code) || name.Length == 0) continue;
            var market = code.StartsWith("sh") ? "沪市" : code.StartsWith("bj") ? "北交所" : "深市";
            if (type is not ("11" or "12" or "19" or "21" or "22" or "23" or "81" or "120" or "203")) continue;
            if (results.Any(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) continue;
            results.Add(new StockSearchResult(code, name, market));
            if (results.Count >= 8) break;
        }
        return results;
    }

    public void Dispose() => _client.Dispose();
}
