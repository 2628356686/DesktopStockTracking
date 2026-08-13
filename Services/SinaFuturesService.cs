using System.Globalization;
using System.Text.Json;

namespace StockTickerLite.Services;

public sealed record FuturesQuote(string Symbol,string Name,decimal Current,decimal Change,decimal ChangePercent,decimal Open,decimal High,decimal Low,decimal PreviousSettlement,long Volume,long Position,DateTime? QuoteTime);

public sealed class SinaFuturesService:IDisposable
{
    private static readonly string[] ContinuousContracts=
    [
        "RB0","HC0","CU0","AL0","ZN0","PB0","NI0","SN0","AU0","AG0","SS0","BU0","RU0","SP0","FU0",
        "M0","Y0","P0","A0","B0","C0","CS0","I0","J0","JM0","JD0","L0","PP0","V0","EG0","EB0","PG0","LH0",
        "SR0","CF0","TA0","MA0","RM0","OI0","FG0","SA0","UR0","AP0","CJ0","PK0","PF0","SH0","SM0","SF0",
        "SI0","LC0","IF0","IH0","IC0","IM0","T0","TF0","TS0","TL0"
    ];
    private readonly HttpClient _client=new(){Timeout=TimeSpan.FromSeconds(15)};

    public SinaFuturesService()
    {
        _client.DefaultRequestHeaders.Referrer=new Uri("https://vip.stock.finance.sina.com.cn/");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 DesktopStockTracking/1.0");
    }

    public async Task<IReadOnlyList<FuturesQuote>> GetQuotesAsync(CancellationToken cancellationToken=default)
    {
        var list=string.Join(',',ContinuousContracts.Select(x=>"nf_"+x));
        using var response=await _client.GetAsync("https://hq.sinajs.cn/list="+list,cancellationToken);response.EnsureSuccessStatusCode();
        var bytes=await response.Content.ReadAsByteArrayAsync(cancellationToken);var script=System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        var result=new List<FuturesQuote>();
        foreach(var line in script.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var equals=line.IndexOf('=');if(equals<0)continue;var key=line[..equals];var symbol=key[(key.LastIndexOf("nf_",StringComparison.OrdinalIgnoreCase)+3)..].Trim();
            var fields=line[(equals+1)..].Trim().Trim('"').Split(',');if(fields.Length<15)continue;
            var current=Decimal(fields[8]);var previousSettlement=Decimal(fields[10]);if(current<=0)continue;
            var change=current-previousSettlement;var percent=previousSettlement==0?0:change/previousSettlement*100;
            DateTime? quoteTime=null;if(fields[1].Length>=6&&DateTime.TryParseExact(DateTime.Today.ToString("yyyyMMdd")+fields[1][..6],"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))quoteTime=parsed;
            result.Add(new FuturesQuote(symbol,fields[0].Trim(),current,change,percent,Decimal(fields[2]),Decimal(fields[3]),Decimal(fields[4]),previousSettlement,Long(fields[14]),Long(fields[13]),quoteTime));
        }
        return result.OrderByDescending(x=>x.ChangePercent).ToArray();
    }

    public async Task<IReadOnlyList<IntradayPoint>> GetChartAsync(string symbol,string chartType,CancellationToken cancellationToken=default)
    {
        var period=chartType switch{"5分钟"=>"5","15分钟"=>"15","30分钟"=>"30","60分钟"=>"60",_=>"1"};
        var today=DateTime.Today.ToString("yyyy_MM_dd",CultureInfo.InvariantCulture);var callback=$"var%20_{symbol}_{period}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}=";
        var url=chartType=="日K线"
            ?$"https://stock2.finance.sina.com.cn/futures/api/jsonp.php/{callback}/InnerFuturesNewService.getDailyKLine?symbol={Uri.EscapeDataString(symbol)}&type={today}"
            :$"https://stock2.finance.sina.com.cn/futures/api/jsonp.php/{callback}/InnerFuturesNewService.getFewMinLine?symbol={Uri.EscapeDataString(symbol)}&type={period}";
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Referrer=new Uri($"https://finance.sina.com.cn/futures/quotes/{symbol}.shtml");
        using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();var text=await response.Content.ReadAsStringAsync(cancellationToken);
        var start=text.IndexOf('[');var end=text.LastIndexOf(']');if(start<0||end<=start)throw new FormatException("新浪期货图表接口未返回行情数组");
        using var document=JsonDocument.Parse(text[start..(end+1)]);if(document.RootElement.ValueKind!=JsonValueKind.Array)return [];
        var raw=new List<(DateTime Time,decimal Open,decimal High,decimal Low,decimal Close,long Volume)>();
        foreach(var row in document.RootElement.EnumerateArray())
        {
            string Value(int index,string name,string shortName)
            {
                if(row.ValueKind==JsonValueKind.Array){var values=row.EnumerateArray().ToArray();return index<values.Length?values[index].ToString():string.Empty;}
                if(row.ValueKind!=JsonValueKind.Object)return string.Empty;
                if(row.TryGetProperty(name,out var value))return value.ToString();
                return row.TryGetProperty(shortName,out value)?value.ToString():string.Empty;
            }
            if(!DateTime.TryParse(Value(0,chartType=="日K线"?"date":"datetime","d"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))continue;
            var close=Decimal(Value(4,"close","c"));if(close<=0)continue;raw.Add((time,Decimal(Value(1,"open","o")),Decimal(Value(2,"high","h")),Decimal(Value(3,"low","l")),close,Long(Value(5,"volume","v"))));
        }
        if(raw.Count==0)return [];var ordered=raw.OrderBy(x=>x.Time).ToList();if(chartType=="分时图")ordered=ordered.Where(x=>x.Time.Date==ordered[^1].Time.Date).ToList();else if(ordered.Count>450)ordered=ordered.TakeLast(450).ToList();
        var result=new List<IntradayPoint>(ordered.Count);long totalVolume=0;decimal weighted=0;
        for(var i=0;i<ordered.Count;i++){var x=ordered[i];totalVolume+=x.Volume;weighted+=x.Close*x.Volume;var average=totalVolume>0?weighted/totalVolume:x.Close;result.Add(new IntradayPoint(x.Time,x.Open,x.High,x.Low,x.Close,x.Volume,0,average,Ma(i,5),Ma(i,10),Ma(i,20),Ma(i,30),Ma(i,60),Ma(i,120)));}
        return result;
        decimal? Ma(int index,int length){if(index+1<length)return null;decimal sum=0;for(var j=index-length+1;j<=index;j++)sum+=ordered[j].Close;return sum/length;}
    }

    private static decimal Decimal(string value)=>decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)?number:0;
    private static long Long(string value)=>long.TryParse(value,NumberStyles.Any,CultureInfo.InvariantCulture,out var number)?number:0;
    public void Dispose()=>_client.Dispose();
}
