using System.Drawing.Drawing2D;
using StockTickerLite.Models;
using StockTickerLite.Services;

namespace StockTickerLite;

public sealed class StockDetailsForm : Form
{
    private readonly Label _title=new(){AutoSize=true,Font=new Font("Microsoft YaHei UI",14,FontStyle.Bold)};
    private readonly Label _details=new(){AutoSize=true,Font=new Font("Consolas",10)};
    private readonly PriceCanvas _canvas=new(){Dock=DockStyle.Fill};
    private readonly SinaChartService _charts=new();
    private readonly System.Windows.Forms.Timer _refreshTimer=new();
    private readonly string _code;
    private string _chartType;
    private readonly ComboBox _chartSelector=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=128};
    private readonly int _intradayRefreshSeconds;
    private CancellationTokenSource? _cts;
    private bool _refreshing;
    private bool _hasData;
    private readonly StockQuote _latestQuote;
    private readonly Func<string,CancellationToken,Task<IReadOnlyList<IntradayPoint>>>? _customChartLoader;

    public StockDetailsForm(StockItem stock,StockQuote quote,IReadOnlyList<(DateTime Time,decimal Price)> history,string chartType,int refreshSeconds,Func<string,CancellationToken,Task<IReadOnlyList<IntradayPoint>>>? customChartLoader=null)
    {
        _code=customChartLoader is null?stock.NormalizedCode:stock.Code;_chartType=chartType;_latestQuote=quote;_customChartLoader=customChartLoader;_intradayRefreshSeconds=Math.Clamp(refreshSeconds,1,10);
        Text="当日分时图";StartPosition=FormStartPosition.CenterParent;Size=new Size(760,540);MinimumSize=new Size(500,360);Font=new Font("Microsoft YaHei UI",9);
        var top=new Panel{Dock=DockStyle.Top,Height=125,Padding=new Padding(18,14,18,6)};var name=string.IsNullOrWhiteSpace(stock.DisplayName)?quote.Name:stock.DisplayName;
        _title.Text=$"{name}  {quote.Code}";_title.ForeColor=Color.Black;
        _details.Text=FormatLatestDetails();_details.ForeColor=ChangeColor(_latestQuote.Change);
        _canvas.HoverPointChanged+=ShowHoverDetails;
        _title.Location=new Point(18,12);_details.Location=new Point(18,48);
        _chartSelector.Items.AddRange(["分时图","日K线","周K线","月K线","5分钟","15分钟","30分钟","60分钟"]);_chartSelector.SelectedItem=_chartSelector.Items.Cast<string>().Contains(chartType)?chartType:"分时图";_chartType=_chartSelector.Text;_chartSelector.Location=new Point(top.ClientSize.Width-_chartSelector.Width-18,14);_chartSelector.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        top.Controls.Add(_title);top.Controls.Add(_details);top.Controls.Add(_chartSelector);_canvas.SetLoading(quote.PreviousClose);Controls.Add(_canvas);Controls.Add(top);
        _chartSelector.SelectedIndexChanged+=async(_,_)=>{if(string.IsNullOrWhiteSpace(_chartSelector.Text)||_chartType==_chartSelector.Text)return;_chartType=_chartSelector.Text;_refreshTimer.Interval=RefreshInterval();_canvas.SetLoading(_latestQuote.PreviousClose);await RefreshChartAsync(true);};
        _refreshTimer.Interval=RefreshInterval();
        _refreshTimer.Tick+=async (_,_)=>await RefreshChartAsync();
        Shown+=async (_,_)=>{await RefreshChartAsync();if(!IsDisposed)_refreshTimer.Start();};
    }
    public void SetChartData(IReadOnlyList<IntradayPoint> data,string chartType){Text=chartType;_canvas.SetData(data,chartType);}
    public void SetChartError(string message)=>_canvas.SetError(message);

    private string FormatLatestDetails()=>$"现价 {_latestQuote.Current,10:0.00}    涨跌 {_latestQuote.Change,9:+0.00;-0.00;0.00}    涨幅 {_latestQuote.ChangePercent,8:+0.00;-0.00;0.00}%\r\n"+$"今开 {_latestQuote.Open,10:0.00}    最高 {_latestQuote.High,9:0.00}    最低 {_latestQuote.Low,10:0.00}\r\n"+$"昨收 {_latestQuote.PreviousClose,10:0.00}    成交量 {_latestQuote.Volume/10000m,7:0.00}万    成交额 {_latestQuote.Amount/100000000m,7:0.00}亿";
    private void ShowHoverDetails(IntradayPoint? point,decimal? reference)
    {
        if(point is null){_details.Text=FormatLatestDetails();_details.ForeColor=ChangeColor(_latestQuote.Change);return;}
        var previous=reference.GetValueOrDefault(point.Open);var change=point.Price-previous;var percent=previous==0?0:change/previous*100;
        _details.ForeColor=ChangeColor(change);
        var dateLabel=_chartType=="分时图"?$"{point.Time:yyyy-MM-dd HH:mm}":$"{point.Time:yyyy-MM-dd}";
        _details.Text=$"{dateLabel}    收盘 {point.Price,8:0.00}    涨跌 {change,9:+0.00;-0.00;0.00}    涨幅 {percent,8:+0.00;-0.00;0.00}%\r\n"+$"开盘 {point.Open,10:0.00}    最高 {point.High,9:0.00}    最低 {point.Low,10:0.00}\r\n"+$"昨收 {previous,10:0.00}    成交量 {point.Volume/10000d,7:0.00}万    成交额 {point.Amount/100000000m,7:0.00}亿";
    }
    private int RefreshInterval()=>_chartType=="分时图"?_intradayRefreshSeconds*1000:60000;
    private static Color ChangeColor(decimal change)=>change>0?Color.FromArgb(210,35,35):change<0?Color.FromArgb(0,135,65):Color.FromArgb(45,45,45);

    private async Task RefreshChartAsync(bool force=false)
    {
        if((_refreshing&&!force)||IsDisposed)return;
        _cts?.Cancel();_cts?.Dispose();var cts=new CancellationTokenSource();_cts=cts;_refreshing=true;var requestedChartType=_chartType;
        try
        {
            var data=_customChartLoader is null?await _charts.GetChartAsync(_code,requestedChartType,cts.Token):await _customChartLoader(requestedChartType,cts.Token);
            if(IsDisposed||!ReferenceEquals(_cts,cts))return;SetChartData(data,requestedChartType);_hasData=data.Count>0;
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!IsDisposed&&ReferenceEquals(_cts,cts)&&!_hasData)SetChartError("行情加载失败："+ex.Message);}
        finally{if(ReferenceEquals(_cts,cts))_refreshing=false;}
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();_cts?.Cancel();_cts?.Dispose();_refreshTimer.Dispose();_charts.Dispose();base.OnFormClosed(e);
    }

    private sealed class PriceCanvas:Control
    {
        private IReadOnlyList<IntradayPoint> _data=[];private IReadOnlyList<IntradayPoint> _allData=[];private int _visibleCount;private decimal _previous;private string? _message;private string _chartType="分时图";private int _hover=-1;private Rectangle _plot;private Rectangle _volumePlot;
        public event Action<IntradayPoint?,decimal?>? HoverPointChanged;
        public PriceCanvas(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;Cursor=Cursors.Cross;TabStop=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);MouseMove+=MoveHover;MouseWheel+=ZoomXAxis;MouseEnter+=(_,_)=>Focus();MouseLeave+=(_,_)=>{_hover=-1;HoverPointChanged?.Invoke(null,null);Invalidate();};}
        public void SetLoading(decimal previous){_previous=previous;_message="正在加载当天 1 分钟行情…";Invalidate();}
        public void SetData(IReadOnlyList<IntradayPoint> data,string chartType){var preserveZoom=_chartType==chartType&&_visibleCount>0&&_visibleCount<_allData.Count;_allData=data;_chartType=chartType;if(chartType=="分时图")_visibleCount=data.Count;else if(!preserveZoom)_visibleCount=data.Count;else _visibleCount=Math.Clamp(_visibleCount,Math.Min(20,data.Count),data.Count);ApplyVisibleRange();_message=data.Count<2?"暂无足够的行情数据":null;_hover=-1;HoverPointChanged?.Invoke(null,null);Invalidate();}
        public void SetError(string message){_data=[];_allData=[];_visibleCount=0;_message=message;Invalidate();}
        private void ApplyVisibleRange(){_data=_visibleCount>0&&_visibleCount<_allData.Count?_allData.TakeLast(_visibleCount).ToArray():_allData;}
        private void ZoomXAxis(object? sender,MouseEventArgs e)
        {
            if(_chartType=="分时图"||_allData.Count<3)return;
            var minimum=Math.Min(20,_allData.Count);var current=_visibleCount<=0?_allData.Count:_visibleCount;
            var step=Math.Max(5,(int)Math.Round(current*.12));var next=e.Delta>0?current-step:current+step;
            _visibleCount=Math.Clamp(next,minimum,_allData.Count);ApplyVisibleRange();_hover=-1;Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);var intraday=_chartType=="分时图";var left=Math.Min(62,Math.Max(42,Width/7));var plotTop=intraday?34:78;var plotWidth=Math.Max(30,ClientSize.Width-left-22);var availableHeight=Math.Max(30,ClientSize.Height-plotTop-38);if(intraday){_plot=new Rectangle(left,plotTop,plotWidth,availableHeight);_volumePlot=Rectangle.Empty;}else{var gap=16;var volumeHeight=Math.Clamp((int)(availableHeight*.20f),42,100);var priceHeight=Math.Max(45,availableHeight-volumeHeight-gap);if(priceHeight+volumeHeight+gap>availableHeight)volumeHeight=Math.Max(24,availableHeight-priceHeight-gap);_plot=new Rectangle(left,plotTop,plotWidth,priceHeight);_volumePlot=new Rectangle(left,_plot.Bottom+gap,plotWidth,volumeHeight);}
            using var grid=new Pen(Color.FromArgb(228,232,238));for(var i=0;i<=4;i++){var y=_plot.Top+_plot.Height*i/4;g.DrawLine(grid,_plot.Left,y,_plot.Right,y);}for(var i=0;i<=4;i++){var x=_plot.Left+_plot.Width*i/4;g.DrawLine(grid,x,_plot.Top,x,_plot.Bottom);}g.DrawRectangle(Pens.Silver,_plot);if(!intraday&&!_volumePlot.IsEmpty){for(var i=0;i<=4;i++){var x=_volumePlot.Left+_volumePlot.Width*i/4;g.DrawLine(grid,x,_volumePlot.Top,x,_volumePlot.Bottom);}g.DrawRectangle(Pens.Silver,_volumePlot);g.DrawString("成交量",Font,Brushes.DimGray,_volumePlot.Left+4,_volumePlot.Top+3);}
            using var priceLegend=new Pen(Color.FromArgb(30,105,210),2);using var avgLegend=new Pen(Color.FromArgb(232,145,25),2);if(intraday){g.DrawLine(priceLegend,_plot.Left,_plot.Top-18,_plot.Left+22,_plot.Top-18);g.DrawString("价格",Font,Brushes.DimGray,_plot.Left+26,_plot.Top-26);g.DrawLine(avgLegend,_plot.Left+88,_plot.Top-18,_plot.Left+110,_plot.Top-18);g.DrawString("均价",Font,Brushes.DimGray,_plot.Left+114,_plot.Top-26);}else{DrawMaLegend(g);var zoomText=$"滚轮缩放X轴  当前 {_data.Count}/{_allData.Count}";var zoomSize=g.MeasureString(zoomText,Font);g.DrawString(zoomText,Font,Brushes.Gray,_plot.Right-zoomSize.Width,_plot.Top-27);}
            if(_data.Count<2){g.DrawString(_message??"暂无数据",Font,Brushes.Gray,_plot.Left+20,_plot.Top+30);return;}
            var min=intraday?new[]{_data.Min(x=>x.Price),_data.Min(x=>x.Average),_previous}.Where(x=>x>0).Min():_data.Min(x=>x.Low);var max=intraday?new[]{_data.Max(x=>x.Price),_data.Max(x=>x.Average),_previous}.Max():_data.Max(x=>x.High);var pad=(max-min)*0.08m;if(pad<=0)pad=Math.Max(0.01m,max*0.005m);min-=pad;max+=pad;
            float X(int i)=>_plot.Left+(float)i/(_data.Count-1)*_plot.Width;float Y(decimal p)=>_plot.Bottom-(float)((p-min)/(max-min))*_plot.Height;
            if(intraday)
            {
                if(_previous>0){using var dash=new Pen(Color.FromArgb(150,150,150)){DashStyle=DashStyle.Dash};g.DrawLine(dash,_plot.Left,Y(_previous),_plot.Right,Y(_previous));}
                var prices=_data.Select((x,i)=>new PointF(X(i),Y(x.Price))).ToArray();var averages=_data.Select((x,i)=>new PointF(X(i),Y(x.Average))).ToArray();g.DrawLines(priceLegend,prices);g.DrawLines(avgLegend,averages);
            }
            else
            {
                var candleWidth=Math.Clamp(_plot.Width/(float)_data.Count*.65f,2f,10f);for(var i=0;i<_data.Count;i++){var p=_data[i];var x=X(i);var color=p.Price>=p.Open?Color.FromArgb(215,40,40):Color.FromArgb(0,145,70);using var pen=new Pen(color);using var brush=new SolidBrush(color);g.DrawLine(pen,x,Y(p.High),x,Y(p.Low));var y1=Y(Math.Max(p.Open,p.Price));var y2=Y(Math.Min(p.Open,p.Price));var body=new RectangleF(x-candleWidth/2,y1,candleWidth,Math.Max(1,y2-y1));if(p.Price>=p.Open)g.DrawRectangle(pen,body.X,body.Y,body.Width,body.Height);else g.FillRectangle(brush,body);}
                DrawMovingAverages(g,X,Y);DrawVolumes(g,X,candleWidth);
            }
            DrawAxis(g,max,min);
            if(_hover>=0&&_hover<_data.Count)DrawHover(g,X(_hover),Y(_data[_hover].Price),_hover);
        }
        private static readonly (int Period,Color Color)[] MaStyles=
        [
            (5,Color.Black),(10,Color.FromArgb(225,175,0)),(20,Color.FromArgb(210,35,35)),
            (30,Color.FromArgb(0,145,70)),(60,Color.FromArgb(35,95,210)),(120,Color.FromArgb(125,65,100))
        ];
        private decimal? MovingAverage(int index,int period)
        {
            var p=_data[index];return period switch{5=>p.Ma5,10=>p.Ma10,20=>p.Ma20,30=>p.Ma30,60=>p.Ma60,120=>p.Ma120,_=>null};
        }
        private void DrawMovingAverages(Graphics g,Func<int,float> x,Func<decimal,float> y)
        {
            foreach(var style in MaStyles){var points=new List<PointF>();for(var i=style.Period-1;i<_data.Count;i++){var value=MovingAverage(i,style.Period);if(value.HasValue)points.Add(new PointF(x(i),y(value.Value)));}if(points.Count>1){using var pen=new Pen(style.Color,1.35f);g.DrawLines(pen,points.ToArray());}}
        }
        private void DrawMaLegend(Graphics g)
        {
            const int columns=3;var cellWidth=_plot.Width/(float)columns;var startY=_plot.Top-61;
            for(var i=0;i<MaStyles.Length;i++)
            {
                var style=MaStyles[i];var column=i%columns;var row=i/columns;var x=_plot.Left+column*cellWidth;var y=startY+row*25;
                using var pen=new Pen(style.Color,3.5f){StartCap=LineCap.Round,EndCap=LineCap.Round};
                g.DrawLine(pen,x,y+8,x+40,y+8);
                using var textBrush=new SolidBrush(Color.FromArgb(65,65,65));g.DrawString($"MA{style.Period}",Font,textBrush,x+49,y);
            }
        }

        private void DrawVolumes(Graphics g,Func<int,float> x,float barWidth)
        {
            if(_volumePlot.IsEmpty)return;var maxVolume=_data.Max(p=>p.Volume);if(maxVolume<=0)return;
            var width=Math.Max(1f,barWidth);for(var i=0;i<_data.Count;i++){var p=_data[i];var height=Math.Max(1f,(float)p.Volume/maxVolume*(_volumePlot.Height-5));var rect=new RectangleF(x(i)-width/2,_volumePlot.Bottom-height,width,height);using var brush=new SolidBrush(p.Price>=p.Open?Color.FromArgb(190,215,40,40):Color.FromArgb(190,0,145,70));g.FillRectangle(brush,rect);}
            var label=maxVolume>=100000000?$"{maxVolume/100000000d:0.##}亿":$"{maxVolume/10000d:0.##}万";var size=g.MeasureString(label,Font);using var bg=new SolidBrush(Color.FromArgb(220,255,255,255));g.FillRectangle(bg,4,_volumePlot.Top-2,size.Width+3,size.Height);g.DrawString(label,Font,Brushes.DimGray,4,_volumePlot.Top-2);
        }
        private void DrawAxis(Graphics g,decimal max,decimal min)
        {
            g.DrawString(max.ToString("0.00"),Font,Brushes.DimGray,4,_plot.Top-7);g.DrawString(min.ToString("0.00"),Font,Brushes.DimGray,4,_plot.Bottom-10);
            var axisBottom=_volumePlot.IsEmpty?_plot.Bottom:_volumePlot.Bottom;var times=new[]{0,_data.Count/2,_data.Count-1};foreach(var i in times.Distinct()){var text=_data[i].Time.ToString(_chartType=="分时图"?"HH:mm":_chartType is "日K线" or "周K线"?"MM-dd":"yyyy-MM");var size=g.MeasureString(text,Font);var x=Math.Clamp(_plot.Left+(float)i/(_data.Count-1)*_plot.Width-size.Width/2,_plot.Left,_plot.Right-size.Width);g.DrawString(text,Font,Brushes.DimGray,x,axisBottom+7);}
        }
        private void DrawHover(Graphics g,float x,float y,int index)
        {
            var p=_data[index];
            using var cross=new Pen(Color.FromArgb(110,90,90,90)){DashStyle=DashStyle.Dash};g.DrawLine(cross,x,_plot.Top,x,_volumePlot.IsEmpty?_plot.Bottom:_volumePlot.Bottom);g.DrawLine(cross,_plot.Left,y,_plot.Right,y);g.FillEllipse(Brushes.White,x-4,y-4,8,8);g.DrawEllipse(Pens.DodgerBlue,x-4,y-4,8,8);
            var reference=_chartType=="分时图"?_previous:index>0?_data[index-1].Price:p.Open;var change=p.Price-reference;var changePercent=reference==0?0:change/reference*100;var timeLabel=_chartType switch{"日K线" or "周K线"=>$"日期  {p.Time:yyyy-MM-dd}","月K线"=>$"月份  {p.Time:yyyy-MM}","分时图"=>$"时间  {p.Time:HH:mm}",_=>$"时间  {p.Time:MM-dd HH:mm}"};var maInfo=_chartType=="分时图"?"":$"\nMA5  {FormatMa(index,5)}    MA10  {FormatMa(index,10)}\nMA20 {FormatMa(index,20)}    MA30  {FormatMa(index,30)}\nMA60 {FormatMa(index,60)}    MA120 {FormatMa(index,120)}";var text=$"{timeLabel}\n价格  {p.Price:0.00}\n涨跌  {change:+0.00;-0.00;0.00}\n涨幅  {changePercent:+0.00;-0.00;0.00}%\n均价  {p.Average:0.00}\n开盘  {p.Open:0.00}\n最高  {p.High:0.00}\n最低  {p.Low:0.00}\n成交量  {p.Volume/10000d:0.00}万\n成交额  {p.Amount/100000000m:0.000}亿{maInfo}";var size=g.MeasureString(text,Font);var box=new RectangleF(x+12,_plot.Top+8,size.Width+18,size.Height+14);if(box.Right>_plot.Right)box.X=x-box.Width-12;if(box.Left<_plot.Left)box.X=_plot.Left+4;using var bg=new SolidBrush(Color.FromArgb(238,255,255,255));g.FillRectangle(bg,box);g.DrawRectangle(Pens.Gray,box.X,box.Y,box.Width,box.Height);g.DrawString(text,Font,Brushes.Black,box.X+9,box.Y+7);
        }
        private string FormatMa(int index,int period)=>MovingAverage(index,period)?.ToString("0.00")??"--";
        private void MoveHover(object? sender,MouseEventArgs e)
        {
            var hoverBounds=_volumePlot.IsEmpty?_plot:Rectangle.FromLTRB(_plot.Left,_plot.Top,_plot.Right,_volumePlot.Bottom);if(_data.Count<2||!hoverBounds.Contains(e.Location)){if(_hover!=-1){_hover=-1;HoverPointChanged?.Invoke(null,null);Invalidate();}return;}var index=(int)Math.Round((e.X-_plot.Left)/(double)Math.Max(1,_plot.Width)*(_data.Count-1));index=Math.Clamp(index,0,_data.Count-1);if(index!=_hover){_hover=index;var sourceIndex=_allData.Count-_data.Count+index;var reference=_chartType=="分时图"?_previous:sourceIndex>0?_allData[sourceIndex-1].Price:_data[index].Open;HoverPointChanged?.Invoke(_data[index],reference);Invalidate();}
        }
    }
}
