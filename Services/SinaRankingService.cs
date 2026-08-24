using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using StockTickerLite.Models;

namespace StockTickerLite.Services;

public sealed record StockRankingItem(string Code, string Name, string Industry, decimal ChangePercent, decimal Metric);
public sealed record IndustryRankingItem(string Code,string Name,decimal ChangePercent,string LeadingStock,decimal LeadingStockChangePercent);
public sealed record SectorChangeItem(string Name,string Type,decimal ChangePercent);
public sealed record LimitUpLadderItem(string Code,string Name,string Industry,decimal ChangePercent,int ConsecutiveBoards,int BreakCount,DateTime? SealTime,bool IsPreviousLimitUpFailure=false,int PreviousConsecutiveBoards=0,string IndustryBoard="",string PrimaryIndustry="",string LimitUpReason="",string ThemeType="");
internal sealed record ThsStockClassification(string Industry,IReadOnlyList<string> Concepts);

public sealed class SinaRankingService : IDisposable
{
    private static readonly HashSet<string> NonThemeConcepts=new(StringComparer.OrdinalIgnoreCase){"融资融券","沪股通","深股通","标普道琼斯A股","MSCI","富时罗素","转融券标的","机构重仓","基金重仓","参股新三板","预盈预增","预亏预减"};
    private readonly HttpClient _client;
    private readonly SinaQuoteService _quoteService = new();
    private readonly SinaChartService _chartService = new();
    private IReadOnlyList<LimitUpLadderItem> _lastYesterdayLimitUpPool=[];
    private readonly Dictionary<string,string> _primaryIndustryByStock=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,ThsStockClassification> _thsClassificationCache=new(StringComparer.OrdinalIgnoreCase);

    public SinaRankingService()
    {
        _client = DirectHttpClient.Create(TimeSpan.FromSeconds(12));
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

    public async Task<IReadOnlyList<IndustryRankingItem>> GetIndustryRankingAsync(CancellationToken cancellationToken)
    {
        const string url="https://money.finance.sina.com.cn/q/view/newFLJK.php?param=industry";
        using var response=await _client.GetAsync(url,cancellationToken);response.EnsureSuccessStatusCode();
        var bytes=await response.Content.ReadAsByteArrayAsync(cancellationToken);var script=System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        var start=script.IndexOf('{');var end=script.LastIndexOf('}');if(start<0||end<=start)return [];
        using var document=JsonDocument.Parse(script[start..(end+1)]);var result=new List<IndustryRankingItem>();
        foreach(var property in document.RootElement.EnumerateObject())
        {
            if(!property.Name.StartsWith("hangye_",StringComparison.OrdinalIgnoreCase))continue;
            var fields=property.Value.GetString()?.Split(',');if(fields is null||fields.Length<13)continue;
            result.Add(new IndustryRankingItem(fields[0],fields[1],ParseDecimal(fields[5]),fields[12],ParseDecimal(fields[9])));
        }
        return result.OrderByDescending(x=>x.ChangePercent).ToArray();
    }

    private static decimal ParseDecimal(string value)=>decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)?number:0;

    public Task<IReadOnlyList<LimitUpLadderItem>> GetLimitUpLadderAsync(CancellationToken cancellationToken)=>GetLimitUpLadderAsync(DateTime.Today,cancellationToken);

    public async Task<IReadOnlyList<LimitUpLadderItem>> GetLimitUpLadderAsync(DateTime tradingDate,CancellationToken cancellationToken)
    {
        tradingDate=tradingDate.Date;var date=tradingDate.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
        var todayTask=GetEastmoneyLimitUpPoolAsync("getTopicZTPool","fbt:asc",date,false,cancellationToken);
        var yesterdayTask=GetEastmoneyLimitUpPoolAsync("getYesterdayZTPool","zs:desc",date,true,cancellationToken);
        var reasonsTask=GetLimitUpReasonsAsync(date,cancellationToken);
        var previousReasonsTask=GetPreviousTradingDayLimitUpReasonsAsync(tradingDate,cancellationToken);
        await Task.WhenAll(todayTask,yesterdayTask,reasonsTask,previousReasonsTask);
        var today=await todayTask;
        var yesterday=await yesterdayTask;
        var reasons=await reasonsTask;
        var previousReasons=await previousReasonsTask;
        if(tradingDate==DateTime.Today)
        {
            try{today=today.Concat(await GetCurrentStLimitUpsAsync(cancellationToken)).GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToArray();}catch{ /* 普通涨停池仍可使用 */ }
            try{yesterday=yesterday.Concat(await GetPreviousStLimitUpsAsync(cancellationToken)).GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToArray();}catch{ /* 普通昨日池仍可使用 */ }
        }
        today=today.Select(item=>reasons.TryGetValue(StockCode.Normalize(item.Code)[2..],out var reason)?item with{LimitUpReason=reason}:item).ToArray();
        yesterday=yesterday.Select(item=>previousReasons.TryGetValue(StockCode.Normalize(item.Code)[2..],out var reason)?item with{LimitUpReason=reason}:item).ToArray();
        if(tradingDate==DateTime.Today){if(yesterday.Count>0)_lastYesterdayLimitUpPool=yesterday;else if(_lastYesterdayLimitUpPool.Count>0)yesterday=_lastYesterdayLimitUpPool;}
        var yesterdayByCode=yesterday.ToDictionary(x=>x.Code,StringComparer.OrdinalIgnoreCase);
        today=today.Select(x=>yesterdayByCode.TryGetValue(x.Code,out var previous)?x with{PreviousConsecutiveBoards=previous.ConsecutiveBoards}:x).ToArray();
        var todayCodes=today.Select(x=>x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var failures=yesterday.Where(x=>!todayCodes.Contains(x.Code)).Select(x=>x with{IsPreviousLimitUpFailure=true,PreviousConsecutiveBoards=x.ConsecutiveBoards});
        var combined=today.Concat(failures).ToArray();
        try{combined=await EnrichPoolClassificationsAsync(combined,cancellationToken);}catch{ /* 东财行业仍可作为降级分类 */ }
        try{combined=await EnrichPrimaryIndustriesAsync(combined,cancellationToken);}catch{ /* 二级行业仍可用于降级统计 */ }
        return combined.OrderByDescending(x=>x.ConsecutiveBoards).ThenBy(x=>x.IsPreviousLimitUpFailure).ThenBy(x=>x.SealTime??DateTime.MaxValue).ToArray();
    }

    public async Task<IReadOnlyList<SectorChangeItem>> GetSectorChangesAsync(CancellationToken cancellationToken)
    {
        const string industryUrl="https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=500&po=1&np=1&fltt=2&invt=2&fid=f3&fs=m%3A90%2Bt%3A2%2Bf%3A!50&fields=f3%2Cf12%2Cf14";
        const string conceptUrl="https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=1000&po=1&np=1&fltt=2&invt=2&fid=f3&fs=m%3A90%2Bt%3A3%2Bf%3A!50&fields=f3%2Cf12%2Cf14";
        var industryTask=GetEastmoneyListAsync(industryUrl,cancellationToken);var conceptTask=GetEastmoneyListAsync(conceptUrl,cancellationToken);await Task.WhenAll(industryTask,conceptTask);
        return (await industryTask).Select(x=>new SectorChangeItem(Text(x,"f14"),"行业",Number(x,"f3")))
            .Concat((await conceptTask).Select(x=>new SectorChangeItem(Text(x,"f14"),"概念",Number(x,"f3"))))
            .Where(x=>x.Name.Length>0).GroupBy(x=>(x.Name,x.Type)).Select(x=>x.First()).ToArray();
    }

    public async Task<DateTime?> FindLimitUpTradingDayAsync(DateTime start,int direction,CancellationToken cancellationToken)
    {
        direction=direction<0?-1:1;var candidate=start.Date;
        for(var i=0;i<20;i++)
        {
            candidate=candidate.AddDays(direction);
            if(candidate>DateTime.Today)return null;
            if(candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)continue;
            var date=candidate.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
            if((await GetEastmoneyLimitUpPoolAsync("getTopicZTPool","fbt:asc",date,false,cancellationToken)).Count>0)return candidate;
        }
        return null;
    }

    private async Task<IReadOnlyDictionary<string,string>> GetLimitUpReasonsAsync(string date,CancellationToken cancellationToken)
    {
        const string fields="199112,10,9001,330323,330324,330325,9002,330329,133971,133970,1968584,3475914,9003,9004";
        var url="https://data.10jqka.com.cn/dataapi/limit_up/limit_up_pool?page=1&limit=200"
            +$"&field={Uri.EscapeDataString(fields)}&filter=HS%2CGEM2STAR&order_field=330324&order_type=0&date={date}";
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.Referrer=new Uri("https://data.10jqka.com.cn/limit_up/continuous_limit_up/");
            using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();
            using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if(!document.RootElement.TryGetProperty("data",out var data)||data.ValueKind==JsonValueKind.Null||!data.TryGetProperty("info",out var info)||info.ValueKind!=JsonValueKind.Array)return new Dictionary<string,string>();
            return info.EnumerateArray().Select(item=>(Code:Text(item,"code"),Reason:Text(item,"reason_type").Trim()))
                .Where(x=>x.Code.Length==6&&!string.IsNullOrWhiteSpace(x.Reason)).GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group=>group.Key,group=>group.First().Reason,StringComparer.OrdinalIgnoreCase);
        }
        catch(OperationCanceledException){throw;}
        catch{return new Dictionary<string,string>();}
    }

    private async Task<IReadOnlyDictionary<string,string>> GetPreviousTradingDayLimitUpReasonsAsync(DateTime currentDate,CancellationToken cancellationToken)
    {
        for(var daysBack=1;daysBack<=7;daysBack++)
        {
            var candidate=currentDate.AddDays(-daysBack);
            if(candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)continue;
            var reasons=await GetLimitUpReasonsAsync(candidate.ToString("yyyyMMdd",CultureInfo.InvariantCulture),cancellationToken);
            if(reasons.Count>0)return reasons;
        }
        return new Dictionary<string,string>();
    }

    private async Task<LimitUpLadderItem[]> EnrichPrimaryIndustriesAsync(LimitUpLadderItem[] items,CancellationToken cancellationToken)
    {
        var missing=items.Where(x=>!x.Name.Contains("ST",StringComparison.OrdinalIgnoreCase)&&!_primaryIndustryByStock.ContainsKey(StockCode.Normalize(x.Code)[2..])).Select(x=>StockCode.Normalize(x.Code)[2..]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(missing.Count>0)
        {
            const string boardsUrl="https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=100&po=1&np=1&fltt=2&invt=2&fid=f3&fs=m%3A90%2Bs%3A2%2Bf%3A!50&fields=f12%2Cf14";
            var boards=await GetEastmoneyListAsync(boardsUrl,cancellationToken);using var gate=new SemaphoreSlim(6);
            await Task.WhenAll(boards.Select(async board=>
            {
                await gate.WaitAsync(cancellationToken);try
                {
                    var boardCode=Text(board,"f12");var boardName=Text(board,"f14");if(boardCode.Length==0||boardName.Length==0)return;
                    var url=$"https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=1000&po=1&np=1&fltt=2&invt=2&fid=f3&fs=b%3A{Uri.EscapeDataString(boardCode)}&fields=f12";
                    foreach(var stock in await GetEastmoneyListAsync(url,cancellationToken)){var code=Text(stock,"f12");if(code.Length==6)lock(_primaryIndustryByStock)_primaryIndustryByStock[code]=boardName;}
                }
                finally{gate.Release();}
            }));
        }
        return items.Select(item=>item.Name.Contains("ST",StringComparison.OrdinalIgnoreCase)?item with{PrimaryIndustry="ST"}:_primaryIndustryByStock.TryGetValue(StockCode.Normalize(item.Code)[2..],out var parent)?item with{PrimaryIndustry=parent}:item with{PrimaryIndustry=item.IndustryBoard}).ToArray();
    }

    private async Task<IReadOnlyList<LimitUpLadderItem>> GetCurrentStLimitUpsAsync(CancellationToken cancellationToken)
    {
        const string url="https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=500&po=1&np=1&fltt=2&invt=2&fid=f3&fs=m%3A0%2Bf%3A4%2Cm%3A1%2Bf%3A4&fields=f12%2Cf14%2Cf2%2Cf3%2Cf18";
        var entries=await GetEastmoneyListAsync(url,cancellationToken);
        var candidates=entries.Select(item=>(Item:item,Code:EastmoneyCode(Text(item,"f12")),Name:Text(item,"f14")))
            .Where(x=>x.Code.Length>0&&IsLimitPrice(x.Code,x.Name,Number(x.Item,"f2"),Number(x.Item,"f18"))).ToArray();
        var quotes=await _quoteService.GetQuotesAsync(candidates.Select(x=>x.Code),cancellationToken,includeExtendedInfo:true);
        var results=new List<LimitUpLadderItem>();
        foreach(var candidate in candidates)
        {
            if(!quotes.TryGetValue(candidate.Code,out var quote))continue;
            var dailyTask=_chartService.GetChartAsync(quote.Code,"日K线",cancellationToken);var minuteTask=_chartService.GetChartAsync(quote.Code,"分时图",cancellationToken);await Task.WhenAll(dailyTask,minuteTask);
            var (breaks,sealTime)=AnalyzeLimitUpIntraday(quote,await minuteTask);
            results.Add(new LimitUpLadderItem(quote.Code,quote.Name,quote.Industry,quote.ChangePercent,CountConsecutiveBoards(quote,await dailyTask),breaks,sealTime,IndustryBoard:quote.Industry));
        }
        return results;
    }

    private async Task<IReadOnlyList<LimitUpLadderItem>> GetPreviousStLimitUpsAsync(CancellationToken cancellationToken)
    {
        const string url="https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=200&po=1&np=1&fltt=2&invt=2&fid=f3&fs=b%3ABK1050&fields=f12%2Cf14%2Cf2%2Cf3%2Cf18";
        var entries=await GetEastmoneyListAsync(url,cancellationToken);
        var candidates=entries.Select(item=>(Item:item,Code:EastmoneyCode(Text(item,"f12")),Name:Text(item,"f14"))).Where(x=>x.Code.Length>0&&x.Name.Contains("ST",StringComparison.OrdinalIgnoreCase)).ToArray();
        var quotes=await _quoteService.GetQuotesAsync(candidates.Select(x=>x.Code),cancellationToken,includeExtendedInfo:true);
        var results=new List<LimitUpLadderItem>();
        foreach(var candidate in candidates)
        {
            if(!quotes.TryGetValue(candidate.Code,out var quote))continue;var daily=await _chartService.GetChartAsync(quote.Code,"日K线",cancellationToken);
            results.Add(new LimitUpLadderItem(quote.Code,quote.Name,quote.Industry,quote.ChangePercent,CountConsecutiveBoardsBeforeToday(quote,daily),0,null,IndustryBoard:quote.Industry));
        }
        return results;
    }

    private async Task<JsonElement[]> GetEastmoneyListAsync(string url,CancellationToken cancellationToken)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Referrer=new Uri("https://quote.eastmoney.com/center/");using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("data",out var data)&&data.ValueKind!=JsonValueKind.Null&&data.TryGetProperty("diff",out var diff)&&diff.ValueKind==JsonValueKind.Array?diff.EnumerateArray().Select(x=>x.Clone()).ToArray():[];
    }

    private static string EastmoneyCode(string rawCode)=>rawCode.Length!=6?string.Empty:rawCode.StartsWith('6')?"sh"+rawCode:rawCode.StartsWith('8')||rawCode.StartsWith('9')?"bj"+rawCode:"sz"+rawCode;
    private static bool IsLimitPrice(string code,string name,decimal current,decimal previousClose){var rate=LimitRate(code,name);return rate>0&&previousClose>0&&current==decimal.Round(previousClose*(1+rate),2,MidpointRounding.AwayFromZero);}

    private async Task<LimitUpLadderItem[]> EnrichPoolClassificationsAsync(LimitUpLadderItem[] items,CancellationToken cancellationToken)
    {
        var classifications=new Dictionary<string,ThsStockClassification>(StringComparer.OrdinalIgnoreCase);
        using var gate=new SemaphoreSlim(6);
        await Task.WhenAll(items.Select(x=>x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Select(async code=>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                ThsStockClassification classification;
                try{classification=await GetThsClassificationAsync(code,cancellationToken);}
                catch(OperationCanceledException){throw;}
                catch{classification=new ThsStockClassification(string.Empty,[]);}
                lock(classifications)classifications[code]=classification;
            }
            finally{gate.Release();}
        }));
        return items.Select(item=>
        {
            if(!classifications.TryGetValue(item.Code,out var classification))return item;
            var industry=string.IsNullOrWhiteSpace(classification.Industry)?item.IndustryBoard:classification.Industry;
            var reasonTheme=SelectReasonTheme(classification,industry,item.LimitUpReason);
            if(reasonTheme is not null)return item with{Industry=reasonTheme.Value.Name,ThemeType=reasonTheme.Value.Type,IndustryBoard=industry};
            return item with{Industry=industry,ThemeType="行业",IndustryBoard=industry};
        }).ToArray();
    }

    private async Task<ThsStockClassification> GetThsClassificationAsync(string code,CancellationToken cancellationToken)
    {
        var rawCode=StockCode.Normalize(code)[2..];
        lock(_thsClassificationCache)if(_thsClassificationCache.TryGetValue(rawCode,out var cached))return cached;
        using var request=new HttpRequestMessage(HttpMethod.Get,$"https://basic.10jqka.com.cn/{rawCode}/");
        request.Headers.Referrer=new Uri("https://basic.10jqka.com.cn/");
        using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();
        var bytes=await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var html=System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
        var text=HtmlToText(html);
        var industry=Regex.Match(text,@"所属(?:申万)?行业\s*[：:]\s*([^\s|]+)",RegexOptions.IgnoreCase).Groups[1].Value.Trim();
        var conceptText=Regex.Match(text,@"概念行情贴合度\s*[：:]\s*(.*?)\s*详情",RegexOptions.IgnoreCase|RegexOptions.Singleline).Groups[1].Value;
        var concepts=conceptText.Split(new[]{'，',',','、'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Select(NormalizeConcept).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var result=new ThsStockClassification(industry,concepts);
        lock(_thsClassificationCache)_thsClassificationCache[rawCode]=result;
        return result;
    }

    private static string HtmlToText(string html)
    {
        var withBreaks=Regex.Replace(html,@"</?(?:div|p|li|tr|td|th|br|h[1-6])\b[^>]*>"," ",RegexOptions.IgnoreCase);
        var withoutTags=Regex.Replace(withBreaks,@"<[^>]+>",string.Empty);
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags),@"\s+"," ").Trim();
    }

    private static (string Name,string Type)? SelectReasonTheme(ThsStockClassification classification,string industryBoard,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason))return null;
        var concepts=classification.Concepts.Where(x=>!NonThemeConcepts.Contains(x)).ToArray();
        var industries=new[]{classification.Industry,industryBoard}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach(var raw in reason.Split(new[]{'+','＋','、','/'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            if(concepts.Any(concept=>ThemeEquals(concept,raw)))return (raw,"概念");
            if(industries.Any(industry=>ThemeEquals(industry,raw)))return (raw,"行业");
        }
        return null;
    }

    private static bool ThemeEquals(string known,string candidate)
    {
        var left=NormalizeThemeName(known);var right=NormalizeThemeName(candidate);
        if(left.Length<2||right.Length<2)return false;
        return left.Equals(right,StringComparison.OrdinalIgnoreCase)
            || left.Length>=4&&right.Contains(left,StringComparison.OrdinalIgnoreCase)
            || right.Length>=4&&left.Contains(right,StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeThemeName(string value)
    {
        var name=NormalizeConcept(value).Replace("Ⅱ","",StringComparison.Ordinal).Replace("Ⅰ","",StringComparison.Ordinal).Trim();
        return name.EndsWith("行业",StringComparison.Ordinal)?name[..^2]:name;
    }

    private async Task<IReadOnlyList<LimitUpLadderItem>> GetEastmoneyLimitUpPoolAsync(string endpoint,string sort,string date,bool yesterday,CancellationToken cancellationToken)
    {
        const string token="7eea3edcaed734bea9cbfc24409ed989";
        var url=$"https://push2ex.eastmoney.com/{endpoint}?ut={token}&dpt=wz.ztzt&Pageindex=0&pagesize=1000&sort={Uri.EscapeDataString(sort)}&date={date}";
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Referrer=new Uri("https://quote.eastmoney.com/ztb/");
        using var response=await _client.SendAsync(request,cancellationToken);response.EnsureSuccessStatusCode();
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if(!document.RootElement.TryGetProperty("data",out var data)||data.ValueKind==JsonValueKind.Null||!data.TryGetProperty("pool",out var pool)||pool.ValueKind!=JsonValueKind.Array)return [];
        var result=new List<LimitUpLadderItem>();
        foreach(var item in pool.EnumerateArray())
        {
            var rawCode=Text(item,"c");if(rawCode.Length!=6)continue;
            var market=(int)Number(item,"m");var code=market==1?"sh"+rawCode:rawCode.StartsWith('8')||rawCode.StartsWith('9')?"bj"+rawCode:"sz"+rawCode;
            var boards=(int)Number(item,yesterday?"ylbc":"lbc");if(boards<1)boards=1;
            var time=(int)Number(item,yesterday?"yfbt":"lbt");
            var industry=Text(item,"hybk");
            result.Add(new LimitUpLadderItem(code,Text(item,"n"),industry,Number(item,"zdp"),boards,yesterday?0:(int)Number(item,"zbc"),ParsePoolTime(time,date),IndustryBoard:industry));
        }
        return result;
    }

    private static DateTime? ParsePoolTime(int value,string date)
    {
        if(value<=0)return null;var text=value.ToString("D6",CultureInfo.InvariantCulture);
        return DateTime.TryParseExact(date+text,"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)?time:null;
    }

    private static string NormalizeConcept(string value)
    {
        var name=value.Trim();
        return name.EndsWith("概念",StringComparison.Ordinal)?name[..^2]:name;
    }

    private async Task<IReadOnlyList<string>> GetRisingCandidatesAsync(CancellationToken cancellationToken)
    {
        var codes=new List<string>();
        for(var page=1;page<=5;page++)
        {
            var url=$"https://vip.stock.finance.sina.com.cn/quotes_service/api/json_v2.php/Market_Center.getHQNodeData?page={page}&num=100&sort=changepercent&asc=0&node=hs_a&symbol=&_s_r_a=page";
            var text=await GetTextAsync(url,cancellationToken);
            using var document=JsonDocument.Parse(text);
            if(document.RootElement.ValueKind!=JsonValueKind.Array)break;
            var reachedBelowLimit=false;
            foreach(var item in document.RootElement.EnumerateArray())
            {
                var change=Number(item,"changepercent");
                if(change<9m){reachedBelowLimit=true;break;}
                var code=Text(item,"symbol");if(IsAStock(code))codes.Add(code);
            }
            if(reachedBelowLimit||document.RootElement.GetArrayLength()<100)break;
        }
        return codes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsAtUpperLimit(StockQuote quote)
    {
        var rate=LimitRate(quote.Code,quote.Name);if(rate<=0||quote.PreviousClose<=0)return false;
        var limit=decimal.Round(quote.PreviousClose*(1+rate),2,MidpointRounding.AwayFromZero);
        return quote.Current==limit;
    }

    private static int CountConsecutiveBoards(StockQuote quote,IReadOnlyList<IntradayPoint> source)
    {
        var points=source.GroupBy(x=>x.Time.Date).Select(x=>x.Last()).OrderBy(x=>x.Time).ToList();
        var today=(quote.QuoteTime??DateTime.Now).Date;
        points.RemoveAll(x=>x.Time.Date==today);
        points.Add(new IntradayPoint(today,quote.Open,quote.High,quote.Low,quote.Current,quote.Volume,quote.Amount,quote.Current));
        points=points.OrderBy(x=>x.Time).ToList();
        var count=0;var rate=LimitRate(quote.Code,quote.Name);
        for(var i=points.Count-1;i>0;i--)
        {
            var limit=decimal.Round(points[i-1].Price*(1+rate),2,MidpointRounding.AwayFromZero);
            if(points[i].Price<limit)break;
            count++;
        }
        return Math.Max(1,count);
    }

    private static int CountConsecutiveBoardsBeforeToday(StockQuote quote,IReadOnlyList<IntradayPoint> source)
    {
        var today=(quote.QuoteTime??DateTime.Now).Date;
        var points=source.GroupBy(x=>x.Time.Date).Select(x=>x.Last()).Where(x=>x.Time.Date<today).OrderBy(x=>x.Time).ToList();
        var count=0;var rate=LimitRate(quote.Code,quote.Name);
        for(var i=points.Count-1;i>0;i--)
        {
            var limit=decimal.Round(points[i-1].Price*(1+rate),2,MidpointRounding.AwayFromZero);
            if(points[i].Price<limit)break;count++;
        }
        return Math.Max(1,count);
    }

    private static (int BreakCount,DateTime? SealTime) AnalyzeLimitUpIntraday(StockQuote quote,IReadOnlyList<IntradayPoint> points)
    {
        var rate=LimitRate(quote.Code,quote.Name);
        var limit=decimal.Round(quote.PreviousClose*(1+rate),2,MidpointRounding.AwayFromZero);
        var breaks=0;var sealedState=false;DateTime? lastSeal=null;
        foreach(var point in points.OrderBy(x=>x.Time))
        {
            var touched=point.High>=limit;var closed=point.Price>=limit;
            if(closed&&!sealedState)lastSeal=point.Time;
            if(!closed&&(sealedState||touched))breaks++;
            sealedState=closed;
        }
        return (breaks,lastSeal);
    }

    private static decimal LimitRate(string code,string name)
    {
        var normalized=StockCode.Normalize(code);
        if(normalized.StartsWith("bj",StringComparison.OrdinalIgnoreCase))return .30m;
        if(normalized.StartsWith("sh688",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz300",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz301",StringComparison.OrdinalIgnoreCase))return .20m;
        if(normalized.StartsWith("sh60",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz00",StringComparison.OrdinalIgnoreCase))return .10m;
        return 0;
    }

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

    private static string Text(JsonElement item,string name)
    {
        if(!item.TryGetProperty(name,out var value))return "";
        return value.ValueKind==JsonValueKind.String?value.GetString()??"":value.ToString();
    }

    private static decimal Number(JsonElement item, string name)
    {
        if(!item.TryGetProperty(name,out var value))return 0;
        if(value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var numeric))return numeric;
        return value.ValueKind==JsonValueKind.String&&decimal.TryParse(value.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)?parsed:0;
    }

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
        _chartService.Dispose();
    }
}
