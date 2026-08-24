using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace StockTickerLite.Services;

public sealed class SinaIndustryService : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);
    private readonly HttpClient _client;
    private IReadOnlyDictionary<string, decimal> _cached =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    private DateTime _updatedAt = DateTime.MinValue;

    public SinaIndustryService()
    {
        _client = DirectHttpClient.Create(
            TimeSpan.FromSeconds(8),
            "https://vip.stock.finance.sina.com.cn/");
        _client.DefaultRequestHeaders.Referrer =
            new Uri("https://vip.stock.finance.sina.com.cn/mkt/frames/sl_bk.html");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) StockTickerLite/1.0");
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetChangePercentsAsync(
        CancellationToken cancellationToken)
    {
        if (_cached.Count > 0 && DateTime.UtcNow - _updatedAt < CacheDuration)
            return _cached;

        var bytes = await _client.GetByteArrayAsync("q/view/SwHy.php", cancellationToken);
        var script = Encoding.GetEncoding("GB18030").GetString(bytes);
        var start = script.IndexOf('{');
        var end = script.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidDataException("新浪行业板块数据格式不正确。");

        using var document = JsonDocument.Parse(script[(start)..(end + 1)]);
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var fields = property.Value.GetString()?.Split(',');
            if (fields is null || fields.Length <= 5 || string.IsNullOrWhiteSpace(fields[1]))
                continue;
            if (decimal.TryParse(fields[5], NumberStyles.Any, CultureInfo.InvariantCulture,
                    out var changePercent))
                result[fields[1].Trim()] = changePercent;
        }

        if (result.Count == 0)
            throw new InvalidDataException("新浪行业板块数据为空。");

        _cached = result;
        _updatedAt = DateTime.UtcNow;
        return _cached;
    }

    public void Dispose() => _client.Dispose();
}
