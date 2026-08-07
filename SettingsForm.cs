using System.ComponentModel;
using System.Text.Json;
using StockTickerLite.Configuration;
using StockTickerLite.Models;
using StockTickerLite.Services;

namespace StockTickerLite;

public sealed class SettingsForm : Form
{
    private readonly DataGridView _stocks = new();
    private readonly TextBox _search = new();
    private readonly ListBox _suggestions = new();
    private readonly SinaStockSearchService _searchService = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 180 };
    private CancellationTokenSource? _searchCts;
    private readonly ComboBox _codeMode = Combo("完整代码", "最后3位代码", "最后2位代码", "不显示");
    private readonly ComboBox _nameMode = Combo("完整名称", "前2个字", "第1个字", "最后2个字", "最后1个字", "不显示", "强制4字符");
    private readonly ComboBox _priceMode = Combo("显示现价", "不显示", "显示现价+涨跌额");
    private readonly ComboBox _changeMode = Combo("红绿显示", "黑色显示", "不显示", "自定义颜色");
    private readonly CheckBox _sealVolume = new() { Text = "涨/跌停时显示封单量", AutoSize = true };
    private readonly ComboBox _chartType = Combo("分时图", "日K线", "周K线", "月K线", "5分钟", "15分钟", "30分钟", "60分钟");
    private readonly ComboBox _chartBackground = Combo("同主界面", "白色", "黑色", "透明");
    private readonly ComboBox _chartOpacity = Combo("同主界面", "30%", "50%", "70%", "100%");
    private readonly CheckBox _quickChart = new() { Text = "显示快速切换列表", AutoSize = true };
    private readonly CheckBox _advancedChart = new() { Text = "高级筛选", AutoSize = true };
    private readonly ComboBox _noteMode = Combo("不显示", "附加", "替换");
    private readonly TextBox _riseSymbol = new() { Text = "+", MaxLength = 1, TextAlign = HorizontalAlignment.Center };
    private readonly TextBox _fallSymbol = new() { Text = "-", MaxLength = 1, TextAlign = HorizontalAlignment.Center };
    private readonly TextBox _percentSymbol = new() { Text = "%", MaxLength = 1, TextAlign = HorizontalAlignment.Center };
    private readonly Label _sample = new() { Text = "sh600000  浦发银行  10.00  +1.20%", TextAlign = ContentAlignment.MiddleCenter };
    private readonly ComboBox _fontSize = Combo("9", "10", "11", "12", "13", "14", "16", "18", "20");
    private readonly ComboBox _spacing = Combo("无", "极窄", "窄", "中等", "较宽", "宽");
    private readonly ComboBox _opacity = Combo("100%", "90%", "80%", "70%", "60%", "50%", "40%", "30%", "20%");
    private readonly ComboBox _refresh = Combo("1s", "2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "10s");
    private readonly Button _background = new() { Size = new Size(24, 24) };
    private readonly CheckBox _boss = new() { Text = "启用", AutoSize = true };
    private readonly TextBox _bossShortcut = new() { ReadOnly = true, TextAlign = HorizontalAlignment.Center, ShortcutsEnabled = false, Cursor = Cursors.Hand };
    private readonly RadioButton _bossHide = new() { Text = "隐藏", Checked = true, AutoSize = true, Enabled = false };
    private readonly RadioButton _bossExit = new() { Text = "退出", AutoSize = true, Enabled = false };
    private readonly CheckBox _topMost = new() { Text = "总在最前", AutoSize = true };
    private readonly CheckBox _tray = new() { Text = "显示通知区图标", AutoSize = true };
    private readonly CheckBox _chart = new() { Text = "开启股价图显示", AutoSize = true };
    private readonly CheckBox _details = new() { Text = "开启详情显示", AutoSize = true };
    private readonly RadioButton _singleClick = new() { Text = "单击相应股票时", AutoSize = true };
    private readonly RadioButton _doubleClick = new() { Text = "双击相应股票时", Checked = true, AutoSize = true };
    private readonly CheckBox _mouseThrough = new() { Text = "鼠标穿透", AutoSize = true };
    private readonly CheckBox _balloon = new() { Text = "股价超限时气泡提醒", AutoSize = true };
    private readonly CheckBox _sound = new() { Text = "股价超限时声音提醒", AutoSize = true };
    private readonly CheckBox _align = new() { Text = "文字对齐", AutoSize = true };
    private readonly CheckBox _profit = new() { Text = "显示持仓盈亏", AutoSize = true };
    private readonly ToolTip _tips = new();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings source)
    {
        Result = Clone(source);
        Text = "设置"; StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true; MinimizeBox = false; ClientSize = new Size(355, 349); MinimumSize = new Size(355, 349);
        Font = new Font("宋体", 9);
        var tabs = new TabControl { Location = new Point(12, 12), Size = new Size(331, 289), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        tabs.TabPages.Add(BuildStocksPage()); tabs.TabPages.Add(BuildDisplayPage()); tabs.TabPages.Add(BuildAdvancedPage()); tabs.TabPages.Add(BuildChartPage()); tabs.TabPages.Add(BuildOtherPage());
        var ok = new Button { Text = "确定", Location = new Point(175, 310), Size = new Size(80, 29), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, FlatStyle = FlatStyle.System, UseVisualStyleBackColor = true };
        var cancel = new Button { Text = "取消", Location = new Point(261, 310), Size = new Size(80, 29), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, FlatStyle = FlatStyle.System, UseVisualStyleBackColor = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (ReadControls()) { DialogResult = DialogResult.OK; Close(); } };
        Controls.Add(tabs); Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        LoadControls(source);
    }

    private TabPage BuildStocksPage()
    {
        var page = Page("关注的股票");
        var market = Combo("A股"); market.Location = new Point(6, 7); market.Size = new Size(71, 20); market.SelectedIndex = 0;
        _search.Location = new Point(83, 7); _search.Size = new Size(233, 21); _search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; _search.ForeColor = Color.Gray; _search.Text = "请在这里输入要添加的股票";
        _suggestions.Location = new Point(83, 30); _suggestions.Size = new Size(233, 88); _suggestions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; _suggestions.Visible = false; _suggestions.IntegralHeight = false;
        _stocks.Location = new Point(6, 34); _stocks.Size = new Size(311, 223); _stocks.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; _stocks.AllowUserToAddRows = false; _stocks.AllowUserToDeleteRows = false;
        _stocks.AllowUserToResizeRows = false; _stocks.MultiSelect = false; _stocks.ReadOnly = true; _stocks.RowHeadersVisible = false;
        _stocks.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _stocks.AutoGenerateColumns = false;
        _stocks.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "序号", Width = 48 });
        _stocks.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "股票代码", Width = 115 });
        _stocks.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "股票名称", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        var menu = new ContextMenuStrip();
        menu.Items.Add("手动添加(&A)", null, (_, _) => AddStock()); menu.Items.Add("高级编辑", null, (_, _) => EditSelected());
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("上移", null, (_, _) => MoveSelected(-1)); menu.Items.Add("下移", null, (_, _) => MoveSelected(1)); menu.Items.Add("删除", null, (_, _) => DeleteSelected());
        _stocks.ContextMenuStrip = menu; _stocks.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _search.Enter += (_, _) => { if (_search.ForeColor == Color.Gray) { _search.Text = ""; _search.ForeColor = Color.Black; } };
        _search.Leave += (_, _) => { if (_search.Text.Length == 0) { _search.Text = "请在这里输入要添加的股票"; _search.ForeColor = Color.Gray; } };
        _search.TextChanged += SearchChanged; _search.KeyDown += SearchKeyDown;
        _suggestions.DoubleClick += (_, _) => AddSearchSelection(); _suggestions.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddSearchSelection(); };
        _searchTimer.Tick += async (_, _) => { _searchTimer.Stop(); await RunSearchAsync(); };
        page.Controls.Add(_stocks); page.Controls.Add(_suggestions); page.Controls.Add(_search); page.Controls.Add(market); return page;
    }

    private TabPage BuildDisplayPage()
    {
        var page=Page("显示");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10,8,10,4),ColumnCount=2,RowCount=3};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,50));layout.RowStyles.Add(new RowStyle(SizeType.Percent,50));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        var code=NewGroup("股票代码");var codeHost=Host();SetupWide(_codeMode);codeHost.Controls.Add(_codeMode);code.Controls.Add(codeHost);
        var name=NewGroup("股票名称");var nameTable=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8,8,8,5),ColumnCount=2,RowCount=2};nameTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));nameTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));nameTable.RowStyles.Add(new RowStyle(SizeType.Percent,50));nameTable.RowStyles.Add(new RowStyle(SizeType.Percent,50));_nameMode.Dock=DockStyle.Fill;_nameMode.Margin=new Padding(0,2,0,3);nameTable.Controls.Add(_nameMode,0,0);nameTable.SetColumnSpan(_nameMode,2);nameTable.Controls.Add(new Label{Text="显示备注：",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,1);_noteMode.Dock=DockStyle.Fill;_noteMode.Margin=new Padding(0,2,0,2);nameTable.Controls.Add(_noteMode,1,1);name.Controls.Add(nameTable);
        var price=NewGroup("现价及涨跌额");var priceHost=Host();SetupWide(_priceMode);priceHost.Controls.Add(_priceMode);price.Controls.Add(priceHost);
        var change=NewGroup("涨跌幅");var changeTable=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8,6,8,4),ColumnCount=4,RowCount=3};changeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));changeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));changeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));changeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));changeTable.RowStyles.Add(new RowStyle(SizeType.Percent,34));changeTable.RowStyles.Add(new RowStyle(SizeType.Percent,33));changeTable.RowStyles.Add(new RowStyle(SizeType.Percent,33));_changeMode.Dock=DockStyle.Fill;_changeMode.Margin=new Padding(0,0,0,2);changeTable.Controls.Add(_changeMode,0,0);changeTable.SetColumnSpan(_changeMode,4);_sealVolume.Dock=DockStyle.Fill;_sealVolume.Margin=Padding.Empty;_sealVolume.Font=new Font("宋体",7.5f);changeTable.Controls.Add(_sealVolume,0,1);changeTable.SetColumnSpan(_sealVolume,4);changeTable.Controls.Add(new Label{Text="涨跌符号：",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,2);SetupSymbol(_riseSymbol);SetupSymbol(_fallSymbol);SetupSymbol(_percentSymbol);changeTable.Controls.Add(_riseSymbol,1,2);changeTable.Controls.Add(_fallSymbol,2,2);changeTable.Controls.Add(_percentSymbol,3,2);change.Controls.Add(changeTable);
        layout.Controls.Add(code,0,0);layout.Controls.Add(name,1,0);layout.Controls.Add(price,0,1);layout.Controls.Add(change,1,1);
        var samplePanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(8,0,8,0)};samplePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));samplePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));samplePanel.Controls.Add(new Label{Text="示例：",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,0);_sample.Dock=DockStyle.Fill;samplePanel.Controls.Add(_sample,1,0);layout.Controls.Add(samplePanel,0,2);layout.SetColumnSpan(samplePanel,2);
        foreach(var box in new[]{_codeMode,_nameMode,_priceMode,_changeMode,_noteMode})box.SelectedIndexChanged+=(_,_)=>UpdateSample(); foreach(var text in new[]{_riseSymbol,_fallSymbol,_percentSymbol})text.TextChanged+=(_,_)=>UpdateSample(); _sealVolume.CheckedChanged+=(_,_)=>UpdateSample();
        _changeMode.SelectedIndexChanged+=(_,_)=>{var enabled=_changeMode.SelectedIndex!=2;_sealVolume.Enabled=enabled;_riseSymbol.Enabled=enabled;_fallSymbol.Enabled=enabled;_percentSymbol.Enabled=enabled;if(_changeMode.SelectedIndex==3)ChooseCustomColors();};
        page.Controls.Add(layout);return page;
        static GroupBox NewGroup(string text)=>new(){Text=text,Dock=DockStyle.Fill,Margin=new Padding(7,5,7,5)};
        static Panel Host()=>new(){Dock=DockStyle.Fill,Padding=new Padding(10,20,10,10)};
        static void SetupWide(ComboBox box){box.Dock=DockStyle.Top;box.Margin=Padding.Empty;}
        static void SetupSymbol(TextBox box){box.Dock=DockStyle.Fill;box.Margin=new Padding(2,1,2,1);}
    }

    private TabPage BuildAdvancedPage()
    {
        var page=Page("高级");
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18,8,18,0),ColumnCount=3,RowCount=9};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(var i=0;i<8;i++)table.RowStyles.Add(new RowStyle(SizeType.Percent,11.5f));
        table.RowStyles.Add(new RowStyle(SizeType.Percent,8f));
        AddRow(0,"文字大小：",_fontSize,"宋体；常规");
        AddRow(1,"行距：",_spacing,"");
        AddLabel("背景颜色：",2);var colorHost=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};_background.Location=new Point(0,2);_background.Size=new Size(24,24);colorHost.Controls.Add(_background);table.Controls.Add(colorHost,1,2);table.Controls.Add(new Label{Text="白色为透明背景",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},2,2);
        AddRow(3,"透明度：",_opacity,"数值越小越透明");
        AddRow(4,"刷新间隔：",_refresh,"可设置 1–10 秒");
        var shortcut=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};shortcut.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));shortcut.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));_boss.Dock=DockStyle.Fill;shortcut.Controls.Add(_boss,0,0);_bossShortcut.Dock=DockStyle.Fill;_bossShortcut.Margin=new Padding(0,3,0,3);shortcut.Controls.Add(_bossShortcut,1,0);AddLabel("显示/隐藏快捷键：",5);table.Controls.Add(shortcut,1,5);table.SetColumnSpan(shortcut,2);
        var bossActions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty};bossActions.Controls.Add(new Label{Text="按下快捷键后：",AutoSize=true,Margin=new Padding(0,6,8,0)});bossActions.Controls.Add(_bossHide);bossActions.Controls.Add(_bossExit);table.Controls.Add(bossActions,1,6);table.SetColumnSpan(bossActions,2);
        var common=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty};common.Controls.Add(_topMost);common.Controls.Add(_tray);table.Controls.Add(common,1,7);table.SetColumnSpan(common,2);
        var reset=new Button{Text="一键重置(&R)",Size=new Size(120,26),Anchor=AnchorStyles.None,UseVisualStyleBackColor=true};reset.Click+=(_,_)=>LoadControls(new AppSettings());table.Controls.Add(reset,0,8);table.SetColumnSpan(reset,3);
        _boss.CheckedChanged+=(_,_)=>{_bossHide.Enabled=_boss.Checked;_bossExit.Enabled=_boss.Checked;_bossShortcut.Enabled=_boss.Checked;}; _bossShortcut.Enter+=(_,_)=>_bossShortcut.SelectAll(); _bossShortcut.MouseDown+=(_,_)=>_bossShortcut.SelectAll(); _bossShortcut.KeyDown+=CaptureShortcut; _tips.SetToolTip(_bossShortcut,"点击后直接按组合键；Backspace 或 Delete 清空");
        _background.Click+=ChooseBackground;_tips.SetToolTip(_boss,"开启后，按下指定快捷键程序可以自动隐藏、退出");page.Controls.Add(table);return page;
        void AddLabel(string text,int row){var label=new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,AutoEllipsis=false,Margin=new Padding(0,0,10,0)};table.Controls.Add(label,0,row);}
        void AddRow(int row,string labelText,Control input,string note){AddLabel(labelText,row);input.Dock=DockStyle.Fill;input.Margin=new Padding(0,3,10,3);table.Controls.Add(input,1,row);if(note.Length>0)table.Controls.Add(new Label{Text=note,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true},2,row);}
    }

    private TabPage BuildChartPage()
    {
        var page=Page("股价图");
        var open=Box("                                         ",21,15,288,59); open.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; Place(_chart,16,-1,108,16); Place(_details,158,-1,96,16); _details.Anchor=AnchorStyles.Top|AnchorStyles.Right; Place(_singleClick,15,28,107,16); _singleClick.MaximumSize=Size.Empty; Place(_doubleClick,158,28,107,16); _doubleClick.MaximumSize=Size.Empty; _doubleClick.Anchor=AnchorStyles.Top|AnchorStyles.Right; open.Controls.Add(_chart);open.Controls.Add(_details);open.Controls.Add(_singleClick);open.Controls.Add(_doubleClick);
        var content=Box("股价图显示内容",21,86,288,72); content.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; Place(_chartType,31,23,188,20); _chartType.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; Place(_quickChart,9,51,120,16); Place(_advancedChart,171,51,72,16); _advancedChart.Anchor=AnchorStyles.Top|AnchorStyles.Right; content.Controls.Add(_chartType);content.Controls.Add(_quickChart);content.Controls.Add(_advancedChart);
        var background=Box("背景及透明度",21,177,288,65); background.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; background.Controls.Add(LabelAt("背景：",11,32)); Place(_chartBackground,51,28,80,20); background.Controls.Add(_chartBackground); background.Controls.Add(LabelAt("透明度：",146,32)); Place(_chartOpacity,195,28,80,20); _chartOpacity.Anchor=AnchorStyles.Top|AnchorStyles.Right;background.Controls.Add(_chartOpacity);
        page.Controls.Add(open);page.Controls.Add(content);page.Controls.Add(background);return page;
    }

    private TabPage BuildOtherPage()
    {
        var page=Page("其他"); Place(_align,22,19,72,16); Place(_balloon,185,45,132,16); _balloon.Anchor=AnchorStyles.Top|AnchorStyles.Right; Place(_sound,185,71,132,16); _sound.Anchor=AnchorStyles.Top|AnchorStyles.Right; Place(_mouseThrough,185,95,72,16); _mouseThrough.Anchor=AnchorStyles.Top|AnchorStyles.Right; Place(_profit,22,45,120,16);
        var divider=new Label{BackColor=Color.Silver,Location=new Point(5,214),Size=new Size(313,1),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};
        var export=new Button{Text="导出配置到文件(&E)",Location=new Point(28,225),Size=new Size(120,26),Anchor=AnchorStyles.Bottom|AnchorStyles.Left,UseVisualStyleBackColor=true};
        var import=new Button{Text="从文件导入配置(&I)",Location=new Point(173,225),Size=new Size(120,26),Anchor=AnchorStyles.Bottom|AnchorStyles.Right,UseVisualStyleBackColor=true};
        export.Click+=ExportConfig;import.Click+=ImportConfig;
        page.Controls.Add(_align);page.Controls.Add(_balloon);page.Controls.Add(_sound);page.Controls.Add(_mouseThrough);page.Controls.Add(_profit);page.Controls.Add(divider);page.Controls.Add(export);page.Controls.Add(import);return page;
    }

    private void LoadControls(AppSettings s)
    {
        foreach (var stock in s.Stocks) AddStockRow(CloneStock(stock));
        Select(_codeMode, s.CodeDisplayMode); Select(_nameMode, s.NameDisplayMode); Select(_priceMode, s.PriceDisplayMode); Select(_changeMode, s.ChangeDisplayMode); Select(_noteMode, s.NoteDisplayMode);
        _riseSymbol.Text=s.RiseSymbol; _fallSymbol.Text=s.FallSymbol; _percentSymbol.Text=s.PercentSymbol; _sealVolume.Checked=s.ShowSealVolume;
        _fontSize.Text=s.FontSize.ToString("0"); _spacing.SelectedIndex=Math.Clamp(s.RowSpacing/3,0,_spacing.Items.Count-1); _opacity.Text=s.OpacityPercent+"%"; _refresh.Text=s.RefreshSeconds+"s";
        _background.BackColor=Color.FromArgb(s.BackgroundColorArgb); _boss.Checked=s.EnableBossKey; _bossShortcut.Text=FormatShortcut(s.BossKeyModifiers,s.BossKey); _bossShortcut.Enabled=s.EnableBossKey; _bossExit.Checked=s.BossKeyExits; _bossHide.Checked=!s.BossKeyExits;
        _topMost.Checked=s.AlwaysOnTop; _tray.Checked=s.ShowTrayIcon; _chart.Checked=s.EnableChart; _chartType.Text=s.ChartType; _details.Checked=s.OpenDetailsOnDoubleClick; _doubleClick.Checked=s.OpenDetailsOnDoubleClick; _singleClick.Checked=!s.OpenDetailsOnDoubleClick;
        _mouseThrough.Checked=s.MouseThrough; _balloon.Checked=s.EnableBalloonAlert; _sound.Checked=s.EnableSoundAlert; _align.Checked=s.AlignText; _profit.Checked=s.ShowProfit; UpdateSample();
    }

    private bool ReadControls()
    {
        Result.Stocks=_stocks.Rows.Cast<DataGridViewRow>().Select(x=>(StockItem)x.Tag!).ToList(); Result.CodeDisplayMode=_codeMode.SelectedIndex; Result.NameDisplayMode=_nameMode.SelectedIndex; Result.PriceDisplayMode=_priceMode.SelectedIndex; Result.ChangeDisplayMode=_changeMode.SelectedIndex; Result.NoteDisplayMode=_noteMode.SelectedIndex;
        Result.RiseSymbol=_riseSymbol.Text; Result.FallSymbol=_fallSymbol.Text; Result.PercentSymbol=_percentSymbol.Text; Result.ShowSealVolume=_sealVolume.Checked; Result.FontSize=(float)Number(_fontSize.Text,11); Result.RowSpacing=_spacing.SelectedIndex*3; Result.OpacityPercent=(int)Number(_opacity.Text,100); Result.RefreshSeconds=(int)Number(_refresh.Text,3); Result.BackgroundColorArgb=_background.BackColor.ToArgb(); Result.TransparentBackground=_background.BackColor.ToArgb()==Color.White.ToArgb();
        Result.EnableBossKey=_boss.Checked; ReadShortcut(_bossShortcut.Text,out var modifiers,out var key); Result.BossKeyModifiers=modifiers; Result.BossKey=key; Result.BossKeyExits=_bossExit.Checked; Result.AlwaysOnTop=_topMost.Checked; Result.ShowTrayIcon=_tray.Checked; Result.EnableChart=_chart.Checked; Result.ChartType=_chartType.Text; Result.OpenDetailsOnDoubleClick=_doubleClick.Checked; Result.MouseThrough=_mouseThrough.Checked; Result.EnableBalloonAlert=_balloon.Checked; Result.EnableSoundAlert=_sound.Checked; Result.AlignText=_align.Checked; Result.ShowProfit=_profit.Checked; Result.ShowCode=Result.CodeDisplayMode!=3; Result.ShowName=Result.NameDisplayMode!=5; Result.ShowCurrent=Result.PriceDisplayMode!=1; Result.ShowChange=Result.PriceDisplayMode==2; Result.ShowChangePercent=Result.ChangeDisplayMode!=2; Result.Normalize(); return true;
    }

    private void UpdateSample()
    {
        var code=_codeMode.SelectedIndex switch{1=>"000",2=>"00",3=>"",_=>"sh600000"};
        var baseName=_nameMode.SelectedIndex switch{1=>"浦发",2=>"浦",3=>"银行",4=>"行",5=>"",6=>"浦发银行",_=>"浦发银行"};
        var name=_noteMode.SelectedIndex switch{1=>baseName+"(自选)",2=>"自选",_=>baseName};
        var rise=string.IsNullOrEmpty(_riseSymbol.Text)?"+":_riseSymbol.Text;
        var percent=string.IsNullOrEmpty(_percentSymbol.Text)?"%":_percentSymbol.Text;
        var price=_priceMode.SelectedIndex switch{1=>"",2=>$"10.00  {rise}0.12",_=>"10.00"};
        var change=_changeMode.SelectedIndex==2?"":$"{rise}1.20{percent}";
        var sealedText=_sealVolume.Checked?"(999手)":"";
        _sample.Text=string.Join("  ",new[]{code,name,price,change,sealedText}.Where(x=>x.Length>0));
        _sample.ForeColor=_changeMode.SelectedIndex switch{1=>Color.FromArgb(Result.FlatColorArgb),3=>Color.FromArgb(Result.RiseColorArgb),2=>SystemColors.ControlText,_=>Color.Red};
    }
    private void SearchChanged(object? sender,EventArgs e){if(_search.ForeColor==Color.Gray)return;_searchTimer.Stop();if(_search.Text.Trim().Length==0){_suggestions.Visible=false;return;}_suggestions.Items.Clear();_suggestions.Items.Add("正在搜索，请稍候...");_suggestions.Visible=true;_suggestions.BringToFront();_searchTimer.Start();}
    private void SearchKeyDown(object? sender,KeyEventArgs e){if(e.KeyCode==Keys.Down&&_suggestions.Visible){_suggestions.Focus();_suggestions.SelectedIndex=0;e.Handled=true;}else if(e.KeyCode==Keys.Enter&&_suggestions.Visible){if(_suggestions.SelectedIndex<0)_suggestions.SelectedIndex=0;AddSearchSelection();e.Handled=true;}}
    private async Task RunSearchAsync(){var key=_search.Text.Trim();_searchCts?.Cancel();_searchCts=new CancellationTokenSource();try{var results=await _searchService.SearchAsync(key,_searchCts.Token);if(key!=_search.Text.Trim())return;_suggestions.Items.Clear();foreach(var r in results)_suggestions.Items.Add(r);if(results.Count==0)_suggestions.Items.Add(key+"        手动添加");}catch(OperationCanceledException){}catch{_suggestions.Items.Clear();_suggestions.Items.Add(key+"        手动添加");}}
    private void AddSearchSelection(){if(_suggestions.SelectedItem is StockSearchResult r){if(IsDuplicate(r.Code)){MessageBox.Show("该股票代码已存在！");return;}AddStockRow(new StockItem{Code=r.Code,DisplayName=r.Name});_search.Text="";_suggestions.Visible=false;return;}AddStock(_search.Text.Trim());}
    private void AddStock(string code=""){using var d=new StockEditForm(new StockItem{Code=code});if(d.ShowDialog(this)==DialogResult.OK&&!IsDuplicate(d.Result.Code))AddStockRow(d.Result);}
    private bool IsDuplicate(string code)=>_stocks.Rows.Cast<DataGridViewRow>().Any(x=>((StockItem)x.Tag!).NormalizedCode==StockCode.Normalize(code));
    private void EditSelected(){if(_stocks.CurrentRow?.Tag is not StockItem s)return;using var d=new StockEditForm(s);if(d.ShowDialog(this)==DialogResult.OK)SetRow(_stocks.CurrentRow,d.Result);}
    private void DeleteSelected(){if(_stocks.CurrentRow is{}r)_stocks.Rows.Remove(r);Renumber();}
    private void MoveSelected(int d){if(_stocks.CurrentRow is not{}r)return;var n=r.Index+d;if(n<0||n>=_stocks.Rows.Count)return;_stocks.Rows.Remove(r);_stocks.Rows.Insert(n,r);_stocks.CurrentCell=r.Cells[0];Renumber();}
    private void AddStockRow(StockItem s){var i=_stocks.Rows.Add();SetRow(_stocks.Rows[i],s);Renumber();}
    private static void SetRow(DataGridViewRow r,StockItem s){r.Tag=s;r.SetValues("",s.Code,string.IsNullOrWhiteSpace(s.DisplayName)?s.Code:s.DisplayName);}
    private void Renumber(){for(var i=0;i<_stocks.Rows.Count;i++)_stocks.Rows[i].Cells[0].Value=i+1;}
    private void CaptureShortcut(object? sender,KeyEventArgs e)
    {
        e.SuppressKeyPress=true;e.Handled=true;
        if(e.KeyCode is Keys.Back or Keys.Delete){_bossShortcut.Clear();_boss.Checked=false;return;}
        if(e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)return;
        var parts=new List<string>();if(e.Control)parts.Add("Ctrl");if(e.Alt)parts.Add("Alt");if(e.Shift)parts.Add("Shift");parts.Add(e.KeyCode.ToString());_bossShortcut.Text=string.Join("+",parts);_bossShortcut.SelectAll();
    }
    private static string FormatShortcut(string modifiers,string key)=>string.Join("+",new[]{modifiers,key}.Where(x=>!string.IsNullOrWhiteSpace(x)));
    private static void ReadShortcut(string value,out string modifiers,out string key){var parts=value.Split('+',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);key=parts.Length==0?"T":parts[^1];modifiers=parts.Length<=1?"":string.Join("+",parts[..^1]);}

    private void ChooseCustomColors()
    {
        if(!IsHandleCreated)return;
        using var d=new CustomColorForm(Color.FromArgb(Result.RiseColorArgb),Color.FromArgb(Result.FlatColorArgb),Color.FromArgb(Result.FallColorArgb));
        d.ShowDialog(this);Result.RiseColorArgb=d.RiseColor.ToArgb();Result.FlatColorArgb=d.FlatColor.ToArgb();Result.FallColorArgb=d.FallColor.ToArgb();UpdateSample();
    }
    private void ChooseBackground(object? s,EventArgs e){using var d=new ColorDialog{Color=_background.BackColor,FullOpen=true};if(d.ShowDialog(this)==DialogResult.OK)_background.BackColor=d.Color;}
    private void ExportConfig(object? s,EventArgs e){ReadControls();using var d=new SaveFileDialog{Filter="盯盘配置 (*.json)|*.json",FileName="StockTickerLite-settings.json"};if(d.ShowDialog(this)==DialogResult.OK)File.WriteAllText(d.FileName,JsonSerializer.Serialize(Result,new JsonSerializerOptions{WriteIndented=true}));}
    private void ImportConfig(object? s,EventArgs e){using var d=new OpenFileDialog{Filter="盯盘配置 (*.json)|*.json"};if(d.ShowDialog(this)!=DialogResult.OK)return;try{var x=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(d.FileName))!;Result=x;_stocks.Rows.Clear();LoadControls(x);}catch{MessageBox.Show("配置文件无法读取。");}}
    protected override void OnFormClosed(FormClosedEventArgs e){_searchTimer.Stop();_searchCts?.Cancel();_searchService.Dispose();base.OnFormClosed(e);}
    private static TabPage Page(string text)=>new(text){Size=new Size(323,263),Padding=new Padding(3)};
    private static GroupBox Box(string text,int x,int y,int w,int h)=>new(){Text=text,Location=new Point(x,y),Size=new Size(w,h)};
    private static Label LabelAt(string text,int x,int y)=>new(){Text=text,Location=new Point(x,y),AutoSize=true};
    private static ComboBox Combo(params string[] items){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};c.Items.AddRange(items);if(items.Length>0)c.SelectedIndex=0;return c;}
    private static void Place(Control c,int x,int y,int w,int h){c.Location=new Point(x,y);c.Size=new Size(w,h);}
    private static void Select(ComboBox c,int i)=>c.SelectedIndex=Math.Clamp(i,0,c.Items.Count-1);
    private static decimal Number(string s,decimal fallback)=>decimal.TryParse(new string(s.Where(x=>char.IsDigit(x)||x=='.').ToArray()),out var n)?n:fallback;
    private static AppSettings Clone(AppSettings s)=>JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
    private static StockItem CloneStock(StockItem s)=>JsonSerializer.Deserialize<StockItem>(JsonSerializer.Serialize(s))!;
}

