using System.Globalization;
using System.Text.Json;
using StockTickerLite.Models;

namespace StockTickerLite.Services;

public sealed record IntradayPoint(DateTime Time,decimal Open,decimal High,decimal Low,decimal Price,long Volume,decimal Amount,decimal Average,decimal? Ma5=null,decimal? Ma10=null,decimal? Ma20=null,decimal? Ma30=null,decimal? Ma60=null,decimal? Ma120=null);

public sealed class SinaChartService : IDisposable
{
    private readonly HttpClient _client=DirectHttpClient.Create(TimeSpan.FromSeconds(10),"https://quotes.sina.cn/");
    public SinaChartService(){_client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 StockTickerLite/1.0");}
    public async Task<IReadOnlyList<IntradayPoint>> GetChartAsync(string code,string chartType,CancellationToken cancellationToken=default)
    {
        if(chartType=="分时图")
        {
            try
            {
                var exchange=await GetExchangeIntradayAsync(code,cancellationToken);
                if(exchange.Count>1)return exchange;
            }
            catch(OperationCanceledException){throw;}
            catch{ /* 交易所网页行情不可用时继续使用新浪分时 */ }
        }
        var scale=chartType switch{"5分钟"=>5,"15分钟"=>15,"30分钟"=>30,"60分钟"=>60,"日K线"=>240,"周K线"=>1200,"月K线"=>7200,_=>1};
        var length=scale==1?300:chartType=="日K线"?450:260;
        var symbol=StockCode.Normalize(code);var url=$"cn/api/json_v2.php/CN_MarketDataService.getKLineData?symbol={Uri.EscapeDataString(symbol)}&scale={scale}&ma=no&datalen={length}";
        await using var stream=await _client.GetStreamAsync(url,cancellationToken);using var json=await JsonDocument.ParseAsync(stream,cancellationToken:cancellationToken);var raw=new List<(DateTime Time,decimal Open,decimal High,decimal Low,decimal Close,long Volume,decimal Amount)>();
        if(json.RootElement.ValueKind!=JsonValueKind.Array)return [];
        foreach(var item in json.RootElement.EnumerateArray())
        {
            if(!TryDate(item,"day",out var time)||!TryDecimal(item,"close",out var close)||close<=0)continue;
            TryDecimal(item,"open",out var open);TryDecimal(item,"high",out var high);TryDecimal(item,"low",out var low);TryLong(item,"volume",out var volume);TryDecimal(item,"amount",out var amount);raw.Add((time,open,high,low,close,volume,amount));
        }
        if(raw.Count==0)return [];
        var ordered=raw.OrderBy(x=>x.Time).ToList();var latestDate=ordered[^1].Time.Date;var calculationSource=scale==1?ordered.Where(x=>x.Time.Date==latestDate).ToList():ordered;
        var calculated=new List<IntradayPoint>(calculationSource.Count);long totalVolume=0;decimal totalAmount=0;
        for(var i=0;i<calculationSource.Count;i++)
        {
            var x=calculationSource[i];totalVolume+=x.Volume;totalAmount+=x.Amount;var average=totalVolume>0?totalAmount/totalVolume:x.Close;
            calculated.Add(new IntradayPoint(x.Time,x.Open,x.High,x.Low,x.Close,x.Volume,x.Amount,average,Ma(i,5),Ma(i,10),Ma(i,20),Ma(i,30),Ma(i,60),Ma(i,120)));
        }
        if(chartType=="日K线")return calculated.Where(x=>x.Time.Date>=latestDate.AddYears(-1)).ToList();
        if(scale!=1&&calculated.Count>120)return calculated.TakeLast(120).ToList();
        return calculated;
        decimal? Ma(int index,int period){if(index+1<period)return null;decimal sum=0;for(var j=index-period+1;j<=index;j++)sum+=calculationSource[j].Close;return sum/period;}
    }

    private async Task<IReadOnlyList<IntradayPoint>> GetExchangeIntradayAsync(string code,CancellationToken cancellationToken)
    {
        var symbol=StockCode.Normalize(code);if(symbol.Length<8)return [];
        return symbol.StartsWith("sh",StringComparison.OrdinalIgnoreCase)
            ?await GetSseIntradayAsync(symbol[2..],cancellationToken)
            :symbol.StartsWith("sz",StringComparison.OrdinalIgnoreCase)?await GetSzseIntradayAsync(symbol[2..],cancellationToken):[];
    }

    private async Task<IReadOnlyList<IntradayPoint>> GetSseIntradayAsync(string code,CancellationToken cancellationToken)
    {
        var url=$"https://yunhq.sse.com.cn:32042/v1/sh1/line/{Uri.EscapeDataString(code)}?begin=0&end=-1&select=time%2Cprice%2Cvolume";
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Referrer=new Uri("https://www.sse.com.cn/");
        using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if(!json.RootElement.TryGetProperty("line",out var lines)||lines.ValueKind!=JsonValueKind.Array)return [];
        var dateText=json.RootElement.TryGetProperty("date",out var dateValue)?dateValue.ToString():DateTime.Today.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
        if(!DateTime.TryParseExact(dateText,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))date=DateTime.Today;
        var result=new List<IntradayPoint>();long totalVolume=0;decimal weighted=0;
        foreach(var line in lines.EnumerateArray())
        {
            var values=line.EnumerateArray().ToArray();if(values.Length<3)continue;var time=values[0].ToString().PadLeft(6,'0');
            if(!DateTime.TryParseExact(date.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+time,"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var timestamp))continue;
            var price=ElementDecimal(values[1]);var volume=ElementLong(values[2]);if(price<=0)continue;totalVolume+=volume;weighted+=price*volume;var average=totalVolume>0?weighted/totalVolume:price;
            result.Add(new IntradayPoint(timestamp,price,price,price,price,volume,price*volume,average));
        }
        return result;
    }

    private async Task<IReadOnlyList<IntradayPoint>> GetSzseIntradayAsync(string code,CancellationToken cancellationToken)
    {
        var url=$"https://www.szse.cn/api/market/ssjjhq/getTimeData?marketId=1&code={Uri.EscapeDataString(code)}&random={Random.Shared.NextDouble().ToString(CultureInfo.InvariantCulture)}";
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Referrer=new Uri($"https://www.szse.cn/market/trend/index.html?code={code}");
        using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if(!json.RootElement.TryGetProperty("data",out var data)||!data.TryGetProperty("picupdata",out var lines)||lines.ValueKind!=JsonValueKind.Array)return [];
        var date=DateTime.Today;if(data.TryGetProperty("marketTime",out var marketTime)&&DateTime.TryParse(marketTime.GetString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))date=parsed.Date;
        var result=new List<IntradayPoint>();long previousVolume=0;decimal previousAmount=0;
        foreach(var line in lines.EnumerateArray())
        {
            if(line.ValueKind!=JsonValueKind.Array)continue;var values=line.EnumerateArray().ToArray();if(values.Length<7||!TimeSpan.TryParse(values[0].GetString(),CultureInfo.InvariantCulture,out var time))continue;
            var price=ElementDecimal(values[1]);var average=ElementDecimal(values[2]);var cumulativeVolume=ElementLong(values[5])*100;var cumulativeAmount=ElementDecimal(values[6]);if(price<=0)continue;
            var volume=Math.Max(0,cumulativeVolume-previousVolume);var amount=Math.Max(0,cumulativeAmount-previousAmount);previousVolume=cumulativeVolume;previousAmount=cumulativeAmount;
            result.Add(new IntradayPoint(date+time,price,price,price,price,volume,amount,average>0?average:price));
        }
        return result;
    }

    private static decimal ElementDecimal(JsonElement value)=>value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number)?number:decimal.TryParse(value.ToString(),NumberStyles.Any,CultureInfo.InvariantCulture,out number)?number:0;
    private static long ElementLong(JsonElement value)=>value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out var number)?number:long.TryParse(value.ToString(),NumberStyles.Any,CultureInfo.InvariantCulture,out number)?number:0;
    private static bool TryDate(JsonElement e,string name,out DateTime value){value=default;return e.TryGetProperty(name,out var p)&&DateTime.TryParse(p.GetString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out value);}
    private static bool TryDecimal(JsonElement e,string name,out decimal value){value=0;return e.TryGetProperty(name,out var p)&&decimal.TryParse(p.GetString(),NumberStyles.Any,CultureInfo.InvariantCulture,out value);}
    private static bool TryLong(JsonElement e,string name,out long value){value=0;return e.TryGetProperty(name,out var p)&&long.TryParse(p.GetString(),NumberStyles.Any,CultureInfo.InvariantCulture,out value);}
    public void Dispose()=>_client.Dispose();
}
