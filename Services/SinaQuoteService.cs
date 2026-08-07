using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using StockTickerLite.Models;

namespace StockTickerLite.Services;

public sealed partial class SinaQuoteService : IDisposable
{
    private readonly HttpClient _client;

    public SinaQuoteService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hq.sinajs.cn/"),
            Timeout = TimeSpan.FromSeconds(8)
        };
        _client.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn/");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) StockTickerLite/1.0");
    }

    public async Task<IReadOnlyDictionary<string, StockQuote>> GetQuotesAsync(
        IEnumerable<string> codes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = codes
            .Select(StockCode.Normalize)
            .Where(StockCode.IsSupported)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
            return new Dictionary<string, StockQuote>();

        var path = "list=" + string.Join(',', normalizedCodes);
        var bytes = await _client.GetByteArrayAsync(path, cancellationToken);
        var response = Encoding.GetEncoding("GB18030").GetString(bytes);

        var result = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in QuoteLineRegex().Matches(response))
        {
            var code = match.Groups["code"].Value.ToLowerInvariant();
            var fields = match.Groups["data"].Value.Split(',');
            var quote = Parse(code, fields);
            if (quote is not null)
                result[code] = quote;
        }

        return result;
    }

    private static StockQuote? Parse(string code, string[] fields)
    {
        if (fields.Length < 10 || string.IsNullOrWhiteSpace(fields[0]))
            return null;

        var current = Decimal(fields, 3);
        var previousClose = Decimal(fields, 2);
        if (current == 0 && previousClose == 0)
            return null;

        // 集合竞价前，新浪可能返回“现价 0、昨收正常”。这不是实际跌到 0，
        // 否则会被误算成 -100%。盘前暂用昨收作为平盘占位，开盘后自动被实时报价替换。
        var isPreMarketFallback = current <= 0 && previousClose > 0;
        if (isPreMarketFallback)
            current = previousClose;

        var open = Decimal(fields, 1);
        var high = Decimal(fields, 4);
        var low = Decimal(fields, 5);
        if (isPreMarketFallback)
            open = high = low = previousClose;

        DateTime? quoteTime = null;
        if (fields.Length > 31 &&
            DateTime.TryParseExact(
                $"{fields[30]} {fields[31]}",
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedTime))
        {
            quoteTime = parsedTime;
        }

        return new StockQuote(
            code,
            fields[0].Trim(),
            current,
            previousClose,
            open,
            high,
            low,
            Long(fields, 8),
            Decimal(fields, 9),
            Long(fields, 10),
            Decimal(fields, 11),
            Long(fields, 20),
            Decimal(fields, 21),
            quoteTime,
            isPreMarketFallback);
    }

    private static decimal Decimal(string[] fields, int index) =>
        index < fields.Length &&
        decimal.TryParse(fields[index], NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static long Long(string[] fields, int index) =>
        index < fields.Length &&
        long.TryParse(fields[index], NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    public void Dispose() => _client.Dispose();

    [GeneratedRegex(
        @"var\s+hq_str_(?<code>[a-z]{2}\d{6})=""(?<data>[^""]*)"";",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex QuoteLineRegex();
}


