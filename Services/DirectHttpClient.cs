using System.Net;

namespace StockTickerLite.Services;

internal static class DirectHttpClient
{
    public static HttpClient Create(TimeSpan timeout, string? baseAddress = null)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null,
            AutomaticDecompression = DecompressionMethods.All
        };

        return new HttpClient(handler)
        {
            BaseAddress = baseAddress is null ? null : new Uri(baseAddress),
            Timeout = timeout
        };
    }
}
