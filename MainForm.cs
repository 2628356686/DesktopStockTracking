using System.Runtime.InteropServices;
using StockTickerLite.Configuration;
using StockTickerLite.Models;
using StockTickerLite.Services;

namespace StockTickerLite;

internal sealed class BufferedFlowLayoutPanel:FlowLayoutPanel
{
    public BufferedFlowLayoutPanel(){DoubleBuffered=true;ResizeRedraw=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
}

public sealed class MainForm : Form
{
    private const int HotKeyId=100; private const int WmHotKey=0x0312; private const int ModAlt=1; private const int ModControl=2; private const int ModShift=4; private const int GwlExStyle=-20; private const int WsExTransparent=0x20; private const int WsExLayered=0x80000;
    private readonly SettingsStore _store=new(); private readonly SinaQuoteService _quotes=new(); private readonly SinaChartService _charts=new(); private readonly System.Windows.Forms.Timer _timer=new();
    private readonly FlowLayoutPanel _rows=new BufferedFlowLayoutPanel(); private readonly Label _status=new(); private readonly NotifyIcon _tray=new(); private readonly ContextMenuStrip _menu=new();
    private readonly HashSet<string> _alerts=new(StringComparer.OrdinalIgnoreCase); private readonly Dictionary<string,StockQuote> _latest=new(StringComparer.OrdinalIgnoreCase); private readonly Dictionary<string,List<(DateTime Time,decimal Price)>> _history=new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings; private CancellationTokenSource? _cts; private bool _refreshing; private Point _dragOrigin; private Font? _rowFont; private bool _rowsNeedRebuild=true;

    public MainForm()
    {
        _settings=_store.Load(); Text="轻量桌面盯盘"; FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual; AutoSize=true; AutoSizeMode=AutoSizeMode.GrowAndShrink; ShowInTaskbar=false; Padding=new Padding(5); MinimumSize=new Size(160,24);
        _rows.AutoSize=true; _rows.AutoSizeMode=AutoSizeMode.GrowAndShrink; _rows.FlowDirection=FlowDirection.TopDown; _rows.WrapContents=false; _rows.Margin=Padding.Empty;
        _status.AutoSize=true; _status.ForeColor=Color.DimGray; _status.Margin=new Padding(1,3,1,0);
        var root=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty};root.Controls.Add(_rows);root.Controls.Add(_status);Controls.Add(root);
        BuildMenu(); AssignMenu(root); _tray.Icon=SystemIcons.Application;_tray.Text="轻量桌面盯盘";_tray.ContextMenuStrip=_menu;_tray.DoubleClick+=(_,_)=>ToggleVisible();
        _timer.Tick+=async(_,_)=>await RefreshAsync(); MouseDown+=BeginDrag;MouseMove+=Drag;MouseUp+=EndDrag;root.MouseDown+=BeginDrag;root.MouseMove+=Drag;root.MouseUp+=EndDrag;
        ApplySettings(true); Shown+=async(_,_)=>await RefreshAsync();
    }

    private void BuildMenu()
    {
        _menu.Items.Add("设置(&S)",null,(_,_)=>OpenSettings());
        _menu.Items.Add("立即刷新(&R)",null,async(_,_)=>await RefreshAsync());
        var chart=new ToolStripMenuItem("股价图");chart.DropDownOpening+=(_,_)=>{while(chart.DropDownItems.Count>0){var oldItem=chart.DropDownItems[0];chart.DropDownItems.RemoveAt(0);oldItem.Dispose();}foreach(var s in _settings.Stocks){var item=new ToolStripMenuItem(string.IsNullOrWhiteSpace(s.DisplayName)?s.Code:s.DisplayName);item.Click+=(_,_)=>OpenDetails(s);chart.DropDownItems.Add(item);}};_menu.Items.Add(chart);
        var display=new ToolStripMenuItem("显示");display.DropDownItems.Add("隐藏/显示主界面",null,(_,_)=>ToggleVisible());display.DropDownItems.Add("总在最前",null,(_,_)=>{_settings.AlwaysOnTop=!_settings.AlwaysOnTop;TopMost=_settings.AlwaysOnTop;Save();});display.DropDownItems.Add("鼠标穿透",null,(_,_)=>{_settings.MouseThrough=!_settings.MouseThrough;ApplyMouseThrough();Save();});_menu.Items.Add(display);
        _menu.Items.Add(new ToolStripSeparator());_menu.Items.Add("退出(&Q)",null,(_,_)=>Close());
        _menu.Opening+=(_,_)=>{if(_menu.Items[3] is ToolStripMenuItem d){((ToolStripMenuItem)d.DropDownItems[1]).Checked=_settings.AlwaysOnTop;((ToolStripMenuItem)d.DropDownItems[2]).Checked=_settings.MouseThrough;}};
        ContextMenuStrip=_menu;
    }

    private void ApplySettings(bool reposition)
    {
        _timer.Interval=_settings.RefreshSeconds*1000;_timer.Start();TopMost=_settings.AlwaysOnTop;Opacity=_settings.TransparentBackground?1d:_settings.OpacityPercent/100d;
        var previousFont=_rowFont;try{_rowFont=new Font(_settings.FontFamily,_settings.FontSize,FontStyle.Regular);}catch{_rowFont=new Font("Microsoft YaHei UI",_settings.FontSize,FontStyle.Regular);}Font=_rowFont;previousFont?.Dispose();_rowsNeedRebuild=true;
        var selectedBg=Color.FromArgb(_settings.BackgroundColorArgb);var renderBg=_settings.TransparentBackground?Color.FromArgb(2,1,1):selectedBg;BackColor=renderBg;_rows.BackColor=renderBg;foreach(Control child in Controls)child.BackColor=renderBg;TransparencyKey=_settings.TransparentBackground?renderBg:Color.Empty;_tray.Visible=_settings.ShowTrayIcon;
        UnregisterHotKey(Handle,HotKeyId);if(_settings.EnableBossKey&&Enum.TryParse<Keys>(_settings.BossKey,true,out var hotKey))RegisterHotKey(Handle,HotKeyId,ParseModifiers(_settings.BossKeyModifiers),(int)hotKey);
        ApplyMouseThrough(); Render();
        if(reposition){var wa=Screen.PrimaryScreen?.WorkingArea??Screen.GetWorkingArea(this);Location=_settings.Left>=wa.Left&&_settings.Top>=wa.Top&&_settings.Left<wa.Right&&_settings.Top<wa.Bottom?new Point(_settings.Left,_settings.Top):new Point(wa.Right-Math.Max(Width,260)-10,wa.Bottom-Math.Max(Height,100)-10);}
    }

    private void Render()
    {
        _rows.SuspendLayout();
        if(_rowsNeedRebuild||_rows.Controls.Count!=_settings.Stocks.Count)
        {
            DisposeRows();
            foreach(var stock in _settings.Stocks)
            {
                _latest.TryGetValue(stock.NormalizedCode,out var quote);
                _rows.Controls.Add(CreateRow(stock,quote));
            }
            _rowsNeedRebuild=false;
        }
        else
        {
            for(var i=0;i<_settings.Stocks.Count;i++)
            {
                var stock=_settings.Stocks[i];
                _latest.TryGetValue(stock.NormalizedCode,out var quote);
                UpdateRow((Label)_rows.Controls[i],stock,quote);
            }
        }
        _rows.ResumeLayout(true);
        _rows.Invalidate();
        _status.Text=_settings.Stocks.Count==0?"右键点击这里添加股票":$"{DateTime.Now:HH:mm:ss} · {_settings.RefreshSeconds}s";
    }

    private void DisposeRows()
    {
        while(_rows.Controls.Count>0)
        {
            var control=_rows.Controls[0];
            _rows.Controls.RemoveAt(0);
            control.Dispose();
        }
    }

    private Control CreateRow(StockItem stock,StockQuote? quote)
    {
        var label=new Label{AutoSize=true,UseCompatibleTextRendering=false,BackColor=Color.Transparent,Margin=new Padding(0,_settings.RowSpacing,0,_settings.RowSpacing),Font=_rowFont??Font,Cursor=Cursors.SizeAll,ContextMenuStrip=_menu};
        label.MouseDown+=BeginDrag;label.MouseMove+=Drag;label.MouseUp+=EndDrag;
        label.Click+=(_,_)=>{if(_settings.EnableChart&&!_settings.OpenDetailsOnDoubleClick)OpenDetails(stock);};
        label.DoubleClick+=(_,_)=>{if(_settings.EnableChart&&_settings.OpenDetailsOnDoubleClick)OpenDetails(stock);};
        UpdateRow(label,stock,quote);return label;
    }

    private void UpdateRow(Label label,StockItem stock,StockQuote? q)
    {
        label.Tag=stock;
        if(q is null){label.Text=$"{stock.NormalizedCode}  正在获取";label.ForeColor=Color.DimGray;return;}
        var code=FormatCode(q.Code,_settings.CodeDisplayMode);
        var sourceName=string.IsNullOrWhiteSpace(stock.DisplayName)?q.Name:stock.DisplayName;
        var name=FormatName(sourceName,_settings.NameDisplayMode);
        if(_settings.NoteDisplayMode==2&&!string.IsNullOrWhiteSpace(stock.Note))name=stock.Note;
        else if(_settings.NoteDisplayMode==1&&!string.IsNullOrWhiteSpace(stock.Note))name+=stock.Note;
        var parts=new List<string>();
        if(code.Length>0)parts.Add(code);if(name.Length>0)parts.Add(name);
        if(_settings.PriceDisplayMode!=1){var price=q.Current.ToString("0.00");if(_settings.PriceDisplayMode==2)price+=FormatSigned(q.Change,_settings.RiseSymbol,_settings.FallSymbol);parts.Add(price);}
        if(_settings.ChangeDisplayMode!=2)parts.Add(FormatSigned(q.ChangePercent,_settings.RiseSymbol,_settings.FallSymbol)+_settings.PercentSymbol);
        if(_settings.ShowSealVolume&&q.SealedVolume>0)parts.Add("封"+FormatSealVolume(q.SealedVolume));
        if(_settings.ShowVolume)parts.Add((q.Volume/10000m).ToString("0.00")+"万");
        if(_settings.ShowProfit&&stock.CostPrice is{}cost&&stock.Position is{}pos){var profit=(q.Current-cost)*pos;parts.Add("盈亏"+FormatSigned(profit,_settings.RiseSymbol,_settings.FallSymbol));}
        label.Text=string.Join(_settings.AlignText?"  ":" ",parts);
        label.ForeColor=_settings.ChangeDisplayMode==1?Color.FromArgb(_settings.FlatColorArgb):q.Change>0?Color.FromArgb(_settings.RiseColorArgb):q.Change<0?Color.FromArgb(_settings.FallColorArgb):Color.FromArgb(_settings.FlatColorArgb);
    }

    private static string FormatCode(string code,int mode)=>mode switch{1=>code.Length>3?code[^3..]:code,2=>code.Length>2?code[^2..]:code,3=>string.Empty,_=>code};
    private static string FormatName(string name,int mode)=>mode switch{1=>name[..Math.Min(2,name.Length)],2=>name[..Math.Min(1,name.Length)],3=>name.Length<=2?name:name[^2..],4=>name.Length<=1?name:name[^1..],5=>string.Empty,6=>name.PadRight(4).Substring(0,4),_=>name};
    private static string FormatSigned(decimal value,string rise,string fall)=>value>0?$"{rise}{value:0.00}":value<0?$"{fall}{Math.Abs(value):0.00}":value.ToString("0.00");
    private static string FormatSealVolume(long shares){var hands=shares/100m;return hands>=10000?$"{hands/10000m:0.##}万手":$"{hands:0.##}手";}

    private async Task RefreshAsync()
    {
        if(_refreshing||_settings.Stocks.Count==0)return;_refreshing=true;_cts?.Cancel();_cts?.Dispose();_cts=new CancellationTokenSource();
        try
        {
            var data=await _quotes.GetQuotesAsync(_settings.Stocks.Select(x=>x.Code),_cts.Token);foreach(var pair in data){_latest[pair.Key]=pair.Value;if(pair.Value.IsPreMarketFallback)continue;if(!_history.TryGetValue(pair.Key,out var h))_history[pair.Key]=h=[];if(h.Count==0||h[^1].Price!=pair.Value.Current){h.Add((DateTime.Now,pair.Value.Current));if(h.Count>600)h.RemoveAt(0);}}
            foreach(var stock in _settings.Stocks)if(_latest.TryGetValue(stock.NormalizedCode,out var q)&&!q.IsPreMarketFallback)CheckAlert(stock,q);Render();var latest=data.Values.Where(x=>x.QuoteTime.HasValue).Select(x=>x.QuoteTime!.Value).DefaultIfEmpty(DateTime.Now).Max();_status.Text=$"{latest:HH:mm:ss} · {_settings.RefreshSeconds}s";
        }
        catch(OperationCanceledException){}catch(Exception ex){_status.Text=ex is HttpRequestException?"网络异常，等待重试":"刷新失败："+ex.Message;}finally{_refreshing=false;}
    }

    private void CheckAlert(StockItem s,StockQuote q)
    {
        var key="";var message="";if(s.UpperAlert is{}u&&q.Current>=u){key=s.NormalizedCode+":U";message=$"{q.Name} 当前 {q.Current:0.00}，达到上限 {u:0.00}";}else if(s.LowerAlert is{}l&&q.Current<=l){key=s.NormalizedCode+":L";message=$"{q.Name} 当前 {q.Current:0.00}，达到下限 {l:0.00}";}else if(s.ChangePercentAlert is{}p&&Math.Abs(q.ChangePercent)>=p){key=s.NormalizedCode+":P";message=$"{q.Name} 当前涨跌幅 {q.ChangePercent:+0.00;-0.00;0.00}%";}
        if(key.Length>0&&_alerts.Add(key)){if(_settings.EnableBalloonAlert&&_settings.ShowTrayIcon)_tray.ShowBalloonTip(5000,"股价预警",message,ToolTipIcon.Warning);if(_settings.EnableSoundAlert)System.Media.SystemSounds.Exclamation.Play();}
        if(key.Length==0){_alerts.Remove(s.NormalizedCode+":U");_alerts.Remove(s.NormalizedCode+":L");_alerts.Remove(s.NormalizedCode+":P");}
    }

    private async void OpenDetails(StockItem stock)
    {
        if(!_latest.TryGetValue(stock.NormalizedCode,out var q)){MessageBox.Show("尚未获取到该股票行情。","股价图",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        _history.TryGetValue(stock.NormalizedCode,out var h);var form=new StockDetailsForm(stock,q,h??[]);form.Show(this);
        try{var data=await _charts.GetChartAsync(stock.NormalizedCode,_settings.ChartType);if(!form.IsDisposed)form.SetChartData(data,_settings.ChartType);}
        catch(Exception ex){if(!form.IsDisposed)form.SetChartError("分时行情加载失败："+ex.Message);}
    }
    private void OpenSettings(){SetMouseThrough(false);using var d=new SettingsForm(_settings);if(d.ShowDialog(this)==DialogResult.OK){d.Result.Left=Left;d.Result.Top=Top;_settings=d.Result;Save();ApplySettings(false);_ = RefreshAsync();}else ApplyMouseThrough();}
    private void ToggleVisible(){if(Visible){Hide();}else{Show();Activate();}}
    private void ApplyMouseThrough()=>SetMouseThrough(_settings.MouseThrough);
    private void SetMouseThrough(bool enabled){var style=GetWindowLong(Handle,GwlExStyle);SetWindowLong(Handle,GwlExStyle,enabled?style|WsExLayered|WsExTransparent:style&~WsExTransparent);}
    private static int ParseModifiers(string value){var result=0;if(value.Contains("Ctrl",StringComparison.OrdinalIgnoreCase))result|=ModControl;if(value.Contains("Alt",StringComparison.OrdinalIgnoreCase))result|=ModAlt;if(value.Contains("Shift",StringComparison.OrdinalIgnoreCase))result|=ModShift;return result;}
    private void Save()=>_store.Save(_settings);
    private void BeginDrag(object? s,MouseEventArgs e){if(e.Button==MouseButtons.Left)_dragOrigin=e.Location;}
    private void Drag(object? s,MouseEventArgs e){if(e.Button==MouseButtons.Left){var p=Control.MousePosition;Location=new Point(p.X-_dragOrigin.X,p.Y-_dragOrigin.Y);}}
    private void EndDrag(object? s,MouseEventArgs e){if(e.Button==MouseButtons.Left){_settings.Left=Left;_settings.Top=Top;Save();}}
    private void AssignMenu(Control c){c.ContextMenuStrip=_menu;foreach(Control x in c.Controls)AssignMenu(x);}
    protected override void WndProc(ref Message m){if(m.Msg==WmHotKey&&m.WParam.ToInt32()==HotKeyId){if(_settings.BossKeyExits){Close();return;}ToggleVisible();}base.WndProc(ref m);}
    protected override void OnFormClosed(FormClosedEventArgs e){_timer.Stop();_cts?.Cancel();UnregisterHotKey(Handle,HotKeyId);_tray.Visible=false;_tray.Dispose();DisposeRows();_rowFont?.Dispose();_rowFont=null;_quotes.Dispose();_charts.Dispose();base.OnFormClosed(e);}
    [DllImport("user32.dll")]private static extern bool RegisterHotKey(IntPtr hWnd,int id,int fsModifiers,int vk);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(IntPtr hWnd,int id);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetWindowLong(IntPtr hWnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]private static extern int SetWindowLong(IntPtr hWnd,int index,int value);
}

