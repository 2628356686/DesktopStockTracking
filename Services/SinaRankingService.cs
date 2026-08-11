using System.Globalization;
using System.Text.Json;

namespace StockTickerLite.Services;

public sealed record StockRankingItem(string Code, string Name, string Industry, decimal ChangePercent, decimal Metric);

public sealed class SinaRankingService : IDisposable
{
    private readonly HttpClient _client;
    private readonly SinaQuoteService _quoteService = new();

    public SinaRankingService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _client.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn/");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 DesktopStockTracking/1.0");
    }

    public async Task<IReadOnlyList<StockRankingItem>> GetHotStocksAsync(CancellationToken cancellationToken)
    {
        const string url = "https://quotes.sina.cn/cn/api/openapi.php/StockSelectionService.getHotStocks?num=20&callback=rankingCallback";
        var text = await GetTextAsync(url, cancellationToken);
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new FormatException("新浪热股排行返回格式异常");

        using var document = JsonDocument.Parse(text[start..(end + 1)]);
        if (!document.RootElement.TryGetProperty("result", out var result)
            || !result.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
            throw new FormatException("新浪热股排行缺少数据");

        var items = new List<StockRankingItem>();
        foreach (var item in data.EnumerateArray())
        {
            var code = Text(item, "symbol");
            if (!IsAStock(code)) continue;
            items.Add(new StockRankingItem(code, Text(item, "name"), "", 0, Number(item, "pv")));
            if (items.Count == 20) break;
        }
        var quotes = await _quoteService.GetQuotesAsync(items.Select(x => x.Code), cancellationToken, includeExtendedInfo: true);
        return items.Select(item => quotes.TryGetValue(item.Code, out var quote)
            ? item with { Industry = quote.Industry, ChangePercent = quote.ChangePercent }
            : item).ToArray();
    }

    public Task<IReadOnlyList<StockRankingItem>> GetCapitalInflowAsync(CancellationToken cancellationToken) =>
        GetCapitalFlowAsync(ascending: false, cancellationToken);

    public Task<IReadOnlyList<StockRankingItem>> GetCapitalOutflowAsync(CancellationToken cancellationToken) =>
        GetCapitalFlowAsync(ascending: true, cancellationToken);

    private async Task<IReadOnlyList<StockRankingItem>> GetCapitalFlowAsync(bool ascending, CancellationToken cancellationToken)
    {
        var direction = ascending ? "1" : "0";
        var url = "https://vip.stock.finance.sina.com.cn/quotes_service/api/jsonp.php/var%20rankingData=/MoneyFlow.ssl_bkzj_ssggzj?page=1&num=80&sort=netamount&asc=" + direction;
        var text = await GetTextAsync(url, cancellationToken);
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
            throw new FormatException("新浪资金排行返回格式异常");

        using var document = JsonDocument.Parse(text[start..(end + 1)]);
        var items = new List<StockRankingItem>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var code = Text(item, "symbol");
            if (!IsAStock(code)) continue;
            items.Add(new StockRankingItem(code, Text(item, "name"), "", Number(item, "changeratio") * 100, Number(item, "netamount")));
            if (items.Count == 20) break;
        }
        var quotes = await _quoteService.GetQuotesAsync(items.Select(x => x.Code), cancellationToken, includeExtendedInfo: true);
        return items.Select(item => quotes.TryGetValue(item.Code, out var quote)
            ? item with { Industry = quote.Industry }
            : item).ToArray();
    }

    private async Task<string> GetTextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

    private static decimal Number(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value)
        && decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;

    private static bool IsAStock(string symbol) =>
        symbol.Length == 8
        && (symbol.StartsWith("sh6", StringComparison.OrdinalIgnoreCase)
            || symbol.StartsWith("sz0", StringComparison.OrdinalIgnoreCase)
            || symbol.StartsWith("sz3", StringComparison.OrdinalIgnoreCase)
            || symbol.StartsWith("bj", StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _client.Dispose();
        _quoteService.Dispose();
    }
}
