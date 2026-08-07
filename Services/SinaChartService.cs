using System.Globalization;
using System.Text.Json;
using StockTickerLite.Models;

namespace StockTickerLite.Services;

public sealed record IntradayPoint(DateTime Time,decimal Open,decimal High,decimal Low,decimal Price,long Volume,decimal Amount,decimal Average,decimal? Ma5=null,decimal? Ma10=null,decimal? Ma20=null,decimal? Ma30=null,decimal? Ma60=null,decimal? Ma120=null);

public sealed class SinaChartService : IDisposable
{
    private readonly HttpClient _client=new(){BaseAddress=new Uri("https://quotes.sina.cn/"),Timeout=TimeSpan.FromSeconds(10)};
    public SinaChartService(){_client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 StockTickerLite/1.0");}
    public async Task<IReadOnlyList<IntradayPoint>> GetChartAsync(string code,string chartType,CancellationToken cancellationToken=default)
    {
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
    private static bool TryDate(JsonElement e,string name,out DateTime value){value=default;return e.TryGetProperty(name,out var p)&&DateTime.TryParse(p.GetString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out value);}
    private static bool TryDecimal(JsonElement e,string name,out decimal value){value=0;return e.TryGetProperty(name,out var p)&&decimal.TryParse(p.GetString(),NumberStyles.Any,CultureInfo.InvariantCulture,out value);}
    private static bool TryLong(JsonElement e,string name,out long value){value=0;return e.TryGetProperty(name,out var p)&&long.TryParse(p.GetString(),NumberStyles.Any,CultureInfo.InvariantCulture,out value);}
    public void Dispose()=>_client.Dispose();
}
