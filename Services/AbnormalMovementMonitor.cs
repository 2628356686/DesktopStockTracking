using StockTickerLite.Models;

namespace StockTickerLite.Services;

public static class AbnormalMovementMonitor
{
    public static bool IsIndex(string code)
    {
        var normalized=StockCode.Normalize(code);
        return normalized.StartsWith("sh000",StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("sz399",StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("bj899",StringComparison.OrdinalIgnoreCase);
    }

    public static string BenchmarkCode(string code)
    {
        var normalized=StockCode.Normalize(code);
        if(normalized.StartsWith("sh688",StringComparison.OrdinalIgnoreCase))return "sh000688";
        if(normalized.StartsWith("sh",StringComparison.OrdinalIgnoreCase))return "sh000002";
        if(normalized.StartsWith("sz300",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz301",StringComparison.OrdinalIgnoreCase))return "sz399102";
        if(normalized.StartsWith("sz",StringComparison.OrdinalIgnoreCase))return "sz399107";
        return "bj899050";
    }

    public static string BuildSummary(string code,string displayName,StockQuote quote,IReadOnlyList<IntradayPoint> stock,IReadOnlyList<IntradayPoint> benchmark,bool dragonTiger,bool severe)
    {
        if(IsIndex(code))return "异动监控仅适用于个股。";
        var name=string.IsNullOrWhiteSpace(displayName)?quote.Name:displayName;
        var lines=new List<string>{name,""};
        var normalized=StockCode.Normalize(code);var north=normalized.StartsWith("bj",StringComparison.OrdinalIgnoreCase);var growth=normalized.StartsWith("sh688",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz300",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz301",StringComparison.OrdinalIgnoreCase);
        if(dragonTiger)
        {
            var dailyLimit=north?20m:growth?15m:7m;var amplitudeLimit=north||growth?30m:15m;var threeDayLimit=north?40m:growth?30m:20m;
            var dailyValue=growth||north?quote.ChangePercent:DeviationWithin(stock,benchmark,1,dailyLimit,-dailyLimit);
            var amplitude=quote.Low>0?(quote.High-quote.Low)/quote.Low*100:0;
            lines.Add("龙虎榜异动：");
            lines.Add(dailyValue is{}d?$"  单日{(growth||north?"涨跌幅":"偏离值")} {Signed(d)}，距±{dailyLimit:0}%阈值 {DistanceToEither(d,dailyLimit)}":"  单日指标：日K数据不足");
            lines.Add($"  当日振幅 {amplitude:0.00}%，距{amplitudeLimit:0}%阈值 {DistanceUp(amplitude,amplitudeLimit)}");
            var three=DeviationWithin(stock,benchmark,3,threeDayLimit,-threeDayLimit);lines.Add(three is{}t?$"  3日内累计偏离值 {Signed(t)}，距±{threeDayLimit:0}%阈值 {DistanceToEither(t,threeDayLimit)}":"  3日内累计偏离值：日K数据不足");
        }
        if(severe)
        {
            if(dragonTiger)lines.Add("");
            var tenUp=north?150m:100m;var tenDown=north?-60m:-50m;var thirtyUp=north?300m:200m;var thirtyDown=north?-75m:-70m;
            lines.Add("严重异动：");
            AddPeriod(lines,10,DeviationWithin(stock,benchmark,10,tenUp,tenDown),tenUp,tenDown);
            AddPeriod(lines,30,DeviationWithin(stock,benchmark,30,thirtyUp,thirtyDown),thirtyUp,thirtyDown);
        }
        return string.Join(Environment.NewLine,lines);
    }

    public static string BuildInlineStatus(string code,IReadOnlyList<IntradayPoint> stock,IReadOnlyList<IntradayPoint> benchmark)
    {
        if(IsIndex(code))return string.Empty;
        var normalized=StockCode.Normalize(code);var north=normalized.StartsWith("bj",StringComparison.OrdinalIgnoreCase);var growth=normalized.StartsWith("sh688",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz300",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz301",StringComparison.OrdinalIgnoreCase);
        var dailyLimit=north?30m:growth?20m:10m;var tenUp=north?150m:100m;var thirtyUp=north?300m:200m;
        var badges=new List<string>();AddBadge(badges,10,DeviationWithin(stock,benchmark,10,tenUp,north?-60m:-50m),tenUp,dailyLimit);AddBadge(badges,30,DeviationWithin(stock,benchmark,30,thirtyUp,north?-75m:-70m),thirtyUp,dailyLimit);
        return string.Join(" ",badges);
    }

    public static decimal? GetSevereThresholdDistance(string code,IReadOnlyList<IntradayPoint> stock,IReadOnlyList<IntradayPoint> benchmark)
    {
        if(IsIndex(code))return null;
        var normalized=StockCode.Normalize(code);var north=normalized.StartsWith("bj",StringComparison.OrdinalIgnoreCase);
        var tenUp=north?150m:100m;var tenDown=north?-60m:-50m;var thirtyUp=north?300m:200m;var thirtyDown=north?-75m:-70m;
        var distances=new List<decimal>();
        AddThresholdDistance(distances,DeviationWithin(stock,benchmark,10,tenUp,tenDown),tenUp,tenDown);
        AddThresholdDistance(distances,DeviationWithin(stock,benchmark,30,thirtyUp,thirtyDown),thirtyUp,thirtyDown);
        return distances.Count==0?null:distances.Min();
    }

    public static string BuildDragonTigerInlineStatus(string code,StockQuote quote,IReadOnlyList<IntradayPoint> stock,IReadOnlyList<IntradayPoint> benchmark)
    {
        if(IsIndex(code))return string.Empty;
        var normalized=StockCode.Normalize(code);var north=normalized.StartsWith("bj",StringComparison.OrdinalIgnoreCase);var growth=normalized.StartsWith("sh688",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz300",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz301",StringComparison.OrdinalIgnoreCase);
        var dailyLimit=north?20m:growth?15m:7m;var amplitudeLimit=north||growth?30m:15m;var threeDayLimit=north?40m:growth?30m:20m;
        var dailyValue=growth||north?(decimal?)quote.ChangePercent:DeviationWithin(stock,benchmark,1,dailyLimit,-dailyLimit);var amplitude=quote.Low>0?(quote.High-quote.Low)/quote.Low*100:0;var threeDay=DeviationWithin(stock,benchmark,3,threeDayLimit,-threeDayLimit);
        var badges=new List<string>();
        if(dailyValue is{}daily)AddDragonTigerBadge(badges,"龙虎榜当日偏离",Math.Abs(daily),dailyLimit);
        AddDragonTigerBadge(badges,"龙虎榜当日振幅",amplitude,amplitudeLimit);
        if(threeDay is{}three)AddDragonTigerBadge(badges,"龙虎榜3日偏离",Math.Abs(three),threeDayLimit);
        return string.Join(" ",badges);
    }

    public static string BuildTurnoverInlineStatus(string code,StockQuote quote)
    {
        if(IsIndex(code)||quote.TurnoverRate is not{}turnover)return string.Empty;
        var threshold=DragonTigerTurnoverThreshold(code);var badges=new List<string>();
        AddDragonTigerBadge(badges,"龙虎榜当日换手率",turnover,threshold);
        return string.Join(" ",badges);
    }

    public static string BuildTurnoverSummaryLine(string code,StockQuote quote)
    {
        if(IsIndex(code)||quote.TurnoverRate is not{}turnover)return string.Empty;
        var threshold=DragonTigerTurnoverThreshold(code);
        return $"  当日换手率 {turnover:0.00}%，距{threshold:0}%阈值 {DistanceUp(turnover,threshold)}";
    }

    private static decimal DragonTigerTurnoverThreshold(string code)
    {
        var normalized=StockCode.Normalize(code);
        var growth=normalized.StartsWith("sh688",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz300",StringComparison.OrdinalIgnoreCase)||normalized.StartsWith("sz301",StringComparison.OrdinalIgnoreCase);
        return growth?30m:20m;
    }

    private static void AddDragonTigerBadge(List<string> badges,string label,decimal value,decimal threshold)
    {
        var remaining=threshold-value;
        if(remaining<=0)badges.Add($"{label}已达{value:0.##}%");
        else if(remaining<=threshold*0.20m)badges.Add($"{label}差{remaining:0.##}%");
    }

    private static void AddBadge(List<string> badges,int days,decimal? value,decimal threshold,decimal nearRange)
    {
        if(value is not{}v)return;
        var remaining=threshold-v;
        if(remaining<=0)badges.Add($"异动{days}日偏离已达{v:0.##}%");
        else if(remaining<=nearRange)badges.Add($"异动{days}日偏离差{remaining:0.##}%");
    }

    private static void AddThresholdDistance(List<decimal> distances,decimal? value,decimal up,decimal down)
    {
        if(value is not{}v)return;
        if(v>=up||v<=down){distances.Add(0);return;}
        distances.Add(Math.Min(up-v,v-down));
    }

    private static void AddPeriod(List<string> lines,int days,decimal? value,decimal up,decimal down)
    {
        if(value is not{}v){lines.Add($"  {days}日内累计偏离值：日K数据不足");return;}
        var direction=v>=up
            ?$"已达 {v:0.00}%"
            :v<=down
                ?$"已达 {v:0.00}%"
                :v>=0
                    ?$"距上涨阈值+{up:0}%还差 {up-v:0.00}%"
                    :$"距下跌阈值{down:0}%还差 {v-down:0.00}%";
        lines.Add($"  {days}日内累计偏离值 {Signed(v)}，{direction}");
    }

    private static decimal? DeviationWithin(IReadOnlyList<IntradayPoint> stock,IReadOnlyList<IntradayPoint> benchmark,int maxDays,decimal upLimit,decimal downLimit)
    {
        var stocks=stock.GroupBy(x=>x.Time.Date).ToDictionary(x=>x.Key,x=>x.Last().Price);
        var indices=benchmark.GroupBy(x=>x.Time.Date).ToDictionary(x=>x.Key,x=>x.Last().Price);
        var dates=stocks.Keys.Intersect(indices.Keys).OrderBy(x=>x).ToList();
        if(dates.Count<2)return null;
        var end=dates[^1];var availableDays=Math.Min(maxDays,dates.Count-1);
        decimal? selected=null;var selectedRatio=decimal.MinValue;
        for(var days=1;days<=availableDays;days++)
        {
            // 交易所口径：统计期包含最近 days 个交易日，基准为统计期首日前一交易日收盘。
            var start=dates[^(days+1)];var stockStart=stocks[start];var indexStart=indices[start];
            if(stockStart<=0||indexStart<=0)continue;
            var stockReturn=(stocks[end]/stockStart-1)*100;var indexReturn=(indices[end]/indexStart-1)*100;
            var value=stockReturn-indexReturn;
            var ratio=value>=0?value/upLimit:value/downLimit;
            if(ratio<=selectedRatio)continue;
            selected=value;selectedRatio=ratio;
        }
        return selected;
    }

    private static string Signed(decimal value)=>$"{value:+0.00;-0.00;0.00}%";
    private static string DistanceUp(decimal value,decimal limit)=>value>=limit?$"已达 {value:0.00}%":$"还差 {limit-value:0.00}%";
    private static string DistanceToEither(decimal value,decimal limit)=>Math.Abs(value)>=limit?$"已达 {value:0.00}%":$"还差 {limit-Math.Abs(value):0.00}%";
}
