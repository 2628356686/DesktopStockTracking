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
    private readonly SinaRankingService _rankingService = new();
    private readonly SinaQuoteService _rankingQuoteService = new();
    private readonly SinaFuturesService _futuresService = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 180 };
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _rankingCts;
    private CancellationTokenSource? _limitUpCts;
    private CancellationTokenSource? _rankingChartCts;
    private CancellationTokenSource? _futuresCts;
    private readonly ComboBox _codeMode = Combo("完整代码", "最后3位代码", "最后2位代码", "不显示");
    private readonly CheckBox _showBoard = new() { Text = "显示证券板块", AutoSize = true };
    private readonly ComboBox _nameMode = Combo("完整名称", "前2个字", "第1个字", "最后2个字", "最后1个字", "不显示", "强制4字符");
    private readonly CheckBox _industryComparison = new() { Text = "板块对比", AutoSize = true };
    private readonly ComboBox _priceMode = Combo("显示现价", "不显示", "显示现价+涨跌额");
    private readonly ComboBox _changeMode = Combo("红绿显示", "黑色显示", "不显示", "自定义颜色");
    private readonly CheckBox _sealVolume = new() { Text = "涨/跌停时显示封单量", AutoSize = true };
    private readonly ComboBox _chartType = Combo("分时图", "日K线", "周K线", "月K线", "5分钟", "15分钟", "30分钟", "60分钟");
    private readonly ComboBox _chartBackground = Combo("同主界面", "白色", "黑色", "透明");
    private readonly ComboBox _chartOpacity = Combo("同主界面", "30%", "50%", "70%", "100%");
    private readonly CheckBox _quickChart = new() { Text = "显示快速切换列表", AutoSize = true };
    private readonly CheckBox _advancedChart = new() { Text = "高级筛选", AutoSize = true };
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
    private readonly CheckBox _monitorDragonTiger = new() { Text = "龙虎榜异动", AutoSize = true };
    private readonly CheckBox _monitorSevereAbnormal = new() { Text = "严重异常异动", AutoSize = true };
    private readonly CheckBox _monitorSealAbnormal = new() { Text = "封板异动", AutoSize = true };
    private readonly CheckBox _showStocksPage = PageOption("关注的股票", true);
    private readonly CheckBox _showRankingPage = PageOption("排行");
    private readonly CheckBox _showLimitUpLadderPage = PageOption("涨停天梯");
    private readonly CheckBox _showFuturesPage = PageOption("期货");
    private readonly CheckBox _showDisplayPage = PageOption("显示", true);
    private readonly CheckBox _showAdvancedPage = PageOption("高级", true);
    private readonly CheckBox _showChartPage = PageOption("股价图");
    private readonly CheckBox _showMonitorPage = PageOption("监控");
    private readonly CheckBox _showOtherPage = PageOption("其他", true);
    private readonly DataGridView _hotStockRanking = RankingGrid("热度");
    private readonly DataGridView _capitalInflowRanking = RankingGrid("净流入");
    private readonly DataGridView _capitalOutflowRanking = RankingGrid("净流出");
    private readonly DataGridView _industryRanking = IndustryRankingGrid();
    private readonly FlowLayoutPanel _limitUpLadder = new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.White,Padding=new Padding(0)};
    private readonly Label _limitUpTitle = new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("宋体",18,FontStyle.Bold)};
    private readonly Label _limitUpSummary = new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(110,55,145),Font=new Font("宋体",9,FontStyle.Bold)};
    private readonly Label _limitUpIndustrySummary = new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(65,95,145),Font=new Font("宋体",8),AutoEllipsis=true};
    private readonly Label _limitUpPromotionSummary = new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(110,55,145),Font=new Font("宋体",7.5f)};
    private IReadOnlyList<LimitUpLadderItem> _limitUpItems=[];
    private bool _renderingLimitUps;
    private bool _loadingLimitUpTradingDay;
    private int _lastLimitUpCardsPerRow=-1;
    private readonly System.Windows.Forms.Timer _limitUpResizeTimer=new(){Interval=220};
    private readonly Button _refreshRankings = new() { Text = "刷新排行", AutoSize = true, UseVisualStyleBackColor = true };
    private readonly Label _rankingStatus = new() { Text = "等待加载", AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _refreshLimitUps = new() { Text = "刷新天梯", AutoSize = true, UseVisualStyleBackColor = true };
    private readonly ComboBox _limitUpDistributionMode=Combo("连板分布","板块分布");
    private readonly Button _previousLimitUpDay = new() { Text = "← 上一交易日", AutoSize = true, UseVisualStyleBackColor = true };
    private readonly Button _nextLimitUpDay = new() { Text = "下一交易日 →", AutoSize = true, UseVisualStyleBackColor = true };
    private DateTime _limitUpTradingDate=DateTime.Today;
    private readonly Label _limitUpStatus = new() { Text = "等待加载", AutoSize = true, ForeColor = Color.DimGray };
    private readonly DataGridView _allFutures=FuturesGrid();
    private readonly HorizontalFlowPanel _risingFuturesStrip=new();
    private readonly HorizontalFlowPanel _fallingFuturesStrip=new();
    private readonly Label _futuresStatus=new(){Text="等待加载",AutoSize=true,ForeColor=Color.DimGray};
    private readonly Button _refreshFutures=new(){Text="刷新期货",AutoSize=true,UseVisualStyleBackColor=true};
    private readonly System.Windows.Forms.Timer _futuresTimer=new(){Interval=5000};
    private readonly Dictionary<string,decimal> _futuresMa10=new(StringComparer.OrdinalIgnoreCase);
    private DateTime _futuresMa10Updated=DateTime.MinValue;
    private readonly ToolTip _tips = new();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings source)
    {
        Result = Clone(source);
        _limitUpResizeTimer.Tick+=(_,_)=>{_limitUpResizeTimer.Stop();RenderLimitUpLadder();};
        Text = "设置"; StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true; MinimizeBox = false; ClientSize = new Size(1100, 800); MinimumSize = new Size(355, 349);
        Font = new Font("宋体", 9);
        var tabs = new TabControl();
        var stocksPage=BuildStocksPage();var rankingPage=BuildRankingPage();var limitUpPage=BuildLimitUpLadderPage();var futuresPage=BuildFuturesPage();
        var displayPage=BuildDisplayPage();var advancedPage=BuildAdvancedPage();var chartPage=BuildChartPage();var monitorPage=BuildMonitorPage();var otherPage=BuildOtherPage();
        var orderedPages=new (TabPage Page,CheckBox Option)[]{(stocksPage,_showStocksPage),(rankingPage,_showRankingPage),(limitUpPage,_showLimitUpLadderPage),(futuresPage,_showFuturesPage),(chartPage,_showChartPage),(monitorPage,_showMonitorPage),(displayPage,_showDisplayPage),(advancedPage,_showAdvancedPage),(otherPage,_showOtherPage)};
        var limitUpLoaded=false;var futuresLoaded=false;var loadingPageOptions=true;
        void ApplyPageVisibility()
        {
            if(loadingPageOptions)return;
            var selected=tabs.SelectedTab;
            tabs.SuspendLayout();tabs.TabPages.Clear();
            foreach(var item in orderedPages)if(item.Option.Checked)tabs.TabPages.Add(item.Page);
            if(selected is not null&&tabs.TabPages.Contains(selected))tabs.SelectedTab=selected;else tabs.SelectedTab=otherPage;
            tabs.ResumeLayout();
        }
        foreach(var option in new[]{_showRankingPage,_showLimitUpLadderPage,_showFuturesPage,_showChartPage,_showMonitorPage})option.CheckedChanged+=(_,_)=>ApplyPageVisibility();
        foreach(var item in orderedPages)tabs.TabPages.Add(item.Page);
        tabs.Selected+=async(_,_)=>{_futuresTimer.Enabled=tabs.SelectedTab==futuresPage;if(tabs.SelectedTab==limitUpPage&&!limitUpLoaded){limitUpLoaded=true;ShowLimitUpLoadingSkeleton(0);await Task.Yield();await LoadLimitUpLadderAsync();}if(tabs.SelectedTab==futuresPage&&!futuresLoaded){futuresLoaded=true;await LoadFuturesAsync();}};
        _futuresTimer.Tick+=async(_,_)=>await LoadFuturesAsync();
        var ok = new Button { Text = "确定", Size = new Size(80, 29), FlatStyle = FlatStyle.System, UseVisualStyleBackColor = true };
        var cancel = new Button { Text = "取消", Size = new Size(80, 29), FlatStyle = FlatStyle.System, UseVisualStyleBackColor = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (ReadControls()) { DialogResult = DialogResult.OK; Close(); } };
        Controls.Add(tabs); Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        void LayoutControls()
        {
            tabs.SetBounds(12, 12, Math.Max(100, ClientSize.Width - 24), Math.Max(100, ClientSize.Height - 60));
            cancel.Location = new Point(ClientSize.Width - cancel.Width - 14, ClientSize.Height - cancel.Height - 10);
            ok.Location = new Point(cancel.Left - ok.Width - 6, cancel.Top);
        }
        ClientSizeChanged += (_, _) => LayoutControls();
        LayoutControls();
        LoadControls(source);
        loadingPageOptions=false;ApplyPageVisibility();
        Shown+=async(_,_)=>await LoadRankingsAsync();
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
        var code=NewGroup("股票代码");var codeTable=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10,16,10,8),ColumnCount=1,RowCount=2};codeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));codeTable.RowStyles.Add(new RowStyle(SizeType.Absolute,34));codeTable.RowStyles.Add(new RowStyle(SizeType.Percent,100));_codeMode.Dock=DockStyle.Fill;_codeMode.Margin=new Padding(0,2,0,5);codeTable.Controls.Add(_codeMode,0,0);_showBoard.Anchor=AnchorStyles.Left;_showBoard.Margin=new Padding(2,5,0,2);codeTable.Controls.Add(_showBoard,0,1);code.Controls.Add(codeTable);
        var name=NewGroup("股票名称");var nameTable=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8,8,8,5),ColumnCount=1,RowCount=2};nameTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));nameTable.RowStyles.Add(new RowStyle(SizeType.Percent,50));nameTable.RowStyles.Add(new RowStyle(SizeType.Percent,50));_nameMode.Dock=DockStyle.Fill;_nameMode.Margin=new Padding(0,2,0,3);nameTable.Controls.Add(_nameMode,0,0);_industryComparison.Anchor=AnchorStyles.Left;_industryComparison.Margin=new Padding(2,4,0,2);nameTable.Controls.Add(_industryComparison,0,1);name.Controls.Add(nameTable);_tips.SetToolTip(_industryComparison,"在股票名称后显示所属申万行业板块的实时涨跌幅。");
        var price=NewGroup("现价及涨跌额");var priceHost=Host();SetupWide(_priceMode);priceHost.Controls.Add(_priceMode);price.Controls.Add(priceHost);
        var change=NewGroup("涨跌幅");var changeTable=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8,8,8,6),ColumnCount=1,RowCount=2};changeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));changeTable.RowStyles.Add(new RowStyle(SizeType.Percent,55));changeTable.RowStyles.Add(new RowStyle(SizeType.Percent,45));_changeMode.Dock=DockStyle.Fill;_changeMode.Margin=new Padding(0,2,0,4);changeTable.Controls.Add(_changeMode,0,0);_sealVolume.Dock=DockStyle.None;_sealVolume.Anchor=AnchorStyles.Left;_sealVolume.Margin=new Padding(2,2,0,2);changeTable.Controls.Add(_sealVolume,0,1);change.Controls.Add(changeTable);
        layout.Controls.Add(code,0,0);layout.Controls.Add(name,1,0);layout.Controls.Add(price,0,1);layout.Controls.Add(change,1,1);
        var samplePanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(8,0,8,0)};samplePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));samplePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));samplePanel.Controls.Add(new Label{Text="示例：",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,0);_sample.Dock=DockStyle.Fill;samplePanel.Controls.Add(_sample,1,0);layout.Controls.Add(samplePanel,0,2);layout.SetColumnSpan(samplePanel,2);
        foreach(var box in new[]{_codeMode,_nameMode,_priceMode,_changeMode})box.SelectedIndexChanged+=(_,_)=>UpdateSample(); _showBoard.CheckedChanged+=(_,_)=>UpdateSample(); _industryComparison.CheckedChanged+=(_,_)=>UpdateSample(); _sealVolume.CheckedChanged+=(_,_)=>UpdateSample();
        _changeMode.SelectedIndexChanged+=(_,_)=>{_sealVolume.Enabled=_changeMode.SelectedIndex!=2;if(_changeMode.SelectedIndex==3)ChooseCustomColors();};
        page.Controls.Add(layout);return page;
        static GroupBox NewGroup(string text)=>new(){Text=text,Dock=DockStyle.Fill,Margin=new Padding(7,5,7,5)};
        static Panel Host()=>new(){Dock=DockStyle.Fill,Padding=new Padding(10,20,10,10)};
        static void SetupWide(ComboBox box){box.Dock=DockStyle.Top;box.Margin=Padding.Empty;}
    }

    private TabPage BuildRankingPage()
    {
        var page=Page("排行");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8,8,8,6),ColumnCount=1,RowCount=2};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _rankingStatus.Anchor=AnchorStyles.Left;_rankingStatus.Margin=new Padding(4,0,4,0);toolbar.Controls.Add(_rankingStatus,0,0);
        _refreshRankings.Anchor=AnchorStyles.Right;_refreshRankings.Margin=new Padding(4,2,2,2);_refreshRankings.Click+=async(_,_)=>await LoadRankingsAsync();toolbar.Controls.Add(_refreshRankings,1,0);
        var rankingTabs=new TabControl{Dock=DockStyle.Fill,Margin=new Padding(0,3,0,0)};
        rankingTabs.TabPages.Add(RankingTab("热股排行",_hotStockRanking));rankingTabs.TabPages.Add(RankingTab("流入排行",_capitalInflowRanking));rankingTabs.TabPages.Add(RankingTab("流出排行",_capitalOutflowRanking));rankingTabs.TabPages.Add(RankingTab("行业排行",_industryRanking));
        ConfigureRankingContextMenu(_hotStockRanking,_capitalInflowRanking,_capitalOutflowRanking);
        layout.Controls.Add(toolbar,0,0);layout.Controls.Add(rankingTabs,0,1);
        page.Controls.Add(layout);return page;
        static TabPage RankingTab(string title,Control grid){var tab=new TabPage(title){Padding=new Padding(4)};tab.Controls.Add(grid);return tab;}
    }

    private TabPage BuildLimitUpLadderPage()
    {
        var page=Page("涨停天梯");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(4,4,4,4),ColumnCount=1,RowCount=5};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
        var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _limitUpStatus.Anchor=AnchorStyles.Left;_limitUpStatus.Margin=new Padding(4,0,4,0);toolbar.Controls.Add(_limitUpStatus,0,0);
        var distributionHost=new FlowLayoutPanel{AutoSize=true,WrapContents=false,FlowDirection=FlowDirection.LeftToRight,Margin=new Padding(4,3,8,2)};distributionHost.Controls.Add(new Label{Text="显示：",AutoSize=true,Margin=new Padding(0,5,3,0)});_limitUpDistributionMode.Width=105;_limitUpDistributionMode.Margin=Padding.Empty;_limitUpDistributionMode.SelectedIndexChanged+=(_,_)=>{_lastLimitUpCardsPerRow=-1;UpdateLimitUpTitle();RenderLimitUpLadder();};distributionHost.Controls.Add(_limitUpDistributionMode);toolbar.Controls.Add(distributionHost,1,0);
        _refreshLimitUps.Anchor=AnchorStyles.Right;_refreshLimitUps.Margin=new Padding(4,2,2,2);_refreshLimitUps.Click+=async(_,_)=>await LoadLimitUpLadderAsync();toolbar.Controls.Add(_refreshLimitUps,2,0);
        var summaries=new TableLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty,ColumnCount=1,RowCount=3};summaries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));summaries.RowStyles.Add(new RowStyle(SizeType.Percent,34));summaries.RowStyles.Add(new RowStyle(SizeType.Percent,33));summaries.RowStyles.Add(new RowStyle(SizeType.Percent,33));summaries.Controls.Add(_limitUpSummary,0,0);summaries.Controls.Add(_limitUpIndustrySummary,0,1);summaries.Controls.Add(_limitUpPromotionSummary,0,2);
        var titleBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _previousLimitUpDay.Anchor=AnchorStyles.Left;_nextLimitUpDay.Anchor=AnchorStyles.Right;_previousLimitUpDay.Click+=async(_,_)=>await ChangeLimitUpTradingDayAsync(-1);_nextLimitUpDay.Click+=async(_,_)=>await ChangeLimitUpTradingDayAsync(1);titleBar.Controls.Add(_previousLimitUpDay,0,0);titleBar.Controls.Add(_limitUpTitle,1,0);titleBar.Controls.Add(_nextLimitUpDay,2,0);
        layout.Controls.Add(titleBar,0,0);layout.Controls.Add(summaries,0,1);layout.Controls.Add(toolbar,0,2);layout.Controls.Add(_limitUpLadder,0,3);
        layout.Controls.Add(new Label{Text="涨停股池来自东方财富；当日ST按实时行情补充，历史日期以涨停池数据为准",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.DimGray,AutoEllipsis=true},0,4);
        _limitUpLadder.SizeChanged+=(_,_)=>{_limitUpResizeTimer.Stop();_limitUpResizeTimer.Start();};page.Controls.Add(layout);return page;
    }

    private TabPage BuildFuturesPage()
    {
        var page=Page("期货");var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(8,8,8,6),ColumnCount=1,RowCount=4};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,138));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,138));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _futuresStatus.Anchor=AnchorStyles.Left;_futuresStatus.Margin=new Padding(4,0,4,0);toolbar.Controls.Add(_futuresStatus,0,0);_refreshFutures.Anchor=AnchorStyles.Right;_refreshFutures.Click+=async(_,_)=>await LoadFuturesAsync();toolbar.Controls.Add(_refreshFutures,1,0);
        layout.Controls.Add(toolbar,0,0);layout.Controls.Add(FuturesStripGroup("热门上涨",_risingFuturesStrip,Color.FromArgb(210,35,35)),0,1);layout.Controls.Add(FuturesStripGroup("热门下跌",_fallingFuturesStrip,Color.FromArgb(0,135,65)),0,2);layout.Controls.Add(_allFutures,0,3);
        ConfigureFuturesContextMenu(_allFutures);page.Controls.Add(layout);return page;
        static Control FuturesStripGroup(string title,Control strip,Color color){var group=new GroupBox{Text=title,Dock=DockStyle.Fill,Margin=new Padding(0,3,0,3),ForeColor=color,Font=new Font("宋体",9,FontStyle.Bold),Padding=new Padding(8,21,8,7)};group.Controls.Add(strip);return group;}
    }

    private void ConfigureFuturesContextMenu(params DataGridView[] grids)
    {
        var menu=new ContextMenuStrip();var minute=menu.Items.Add("查看分时图");var daily=menu.Items.Add("查看K线图");
        minute.Click+=async(_,_)=>await OpenSelectedFuturesChartAsync(menu,"分时图");daily.Click+=async(_,_)=>await OpenSelectedFuturesChartAsync(menu,"日K线");
        foreach(var grid in grids){grid.ContextMenuStrip=menu;grid.MouseDown+=(_,e)=>{if(e.Button!=MouseButtons.Right)return;var hit=grid.HitTest(e.X,e.Y);if(hit.RowIndex<0)return;grid.ClearSelection();grid.Rows[hit.RowIndex].Selected=true;grid.CurrentCell=grid.Rows[hit.RowIndex].Cells[0];};}
    }

    private async Task LoadFuturesAsync()
    {
        if(!_refreshFutures.Enabled)return;_futuresCts?.Cancel();_futuresCts?.Dispose();_futuresCts=new CancellationTokenSource();var token=_futuresCts.Token;_refreshFutures.Enabled=false;
        try
        {
            _futuresStatus.Text="正在加载期货实时行情...";var items=await _futuresService.GetQuotesAsync(token);if(token.IsCancellationRequested||IsDisposed)return;
            await RefreshFuturesMa10Async(items,token);if(token.IsCancellationRequested||IsDisposed)return;
            var aboveMa10=items.Where(x=>_futuresMa10.TryGetValue(x.Symbol,out var ma10)&&x.Current>=ma10).OrderByDescending(x=>(x.Current-_futuresMa10[x.Symbol])/_futuresMa10[x.Symbol]).Take(5);
            var belowMa10=items.Where(x=>_futuresMa10.TryGetValue(x.Symbol,out var ma10)&&x.Current<ma10).OrderBy(x=>(x.Current-_futuresMa10[x.Symbol])/_futuresMa10[x.Symbol]).Take(5);
            FillFuturesGrid(_allFutures,items);FillFuturesStrip(_risingFuturesStrip,aboveMa10,true);FillFuturesStrip(_fallingFuturesStrip,belowMa10,false);
            _futuresStatus.Text=$"{items.Count}个连续合约，更新于 {DateTime.Now:HH:mm:ss}";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!IsDisposed)_futuresStatus.Text="期货行情加载失败："+ex.Message;}
        finally{if(!IsDisposed&&!token.IsCancellationRequested)_refreshFutures.Enabled=true;}
    }

    private async Task RefreshFuturesMa10Async(IReadOnlyList<FuturesQuote> items,CancellationToken token)
    {
        var missing=items.Where(x=>!_futuresMa10.ContainsKey(x.Symbol)).ToArray();
        if(missing.Length==0&&DateTime.Now-_futuresMa10Updated<TimeSpan.FromMinutes(10))return;
        _futuresStatus.Text="正在计算期货10日线...";
        using var gate=new SemaphoreSlim(6);
        await Task.WhenAll(items.Select(async item=>
        {
            await gate.WaitAsync(token);
            try
            {
                var points=await _futuresService.GetChartAsync(item.Symbol,"日K线",token);var ma10=points.LastOrDefault()?.Ma10;
                if(ma10 is>0)lock(_futuresMa10)_futuresMa10[item.Symbol]=ma10.Value;
            }
            catch(OperationCanceledException){throw;}
            catch{}
            finally{gate.Release();}
        }));
        _futuresMa10Updated=DateTime.Now;
    }

    private static void FillFuturesGrid(DataGridView grid,IEnumerable<FuturesQuote> source)
    {
        var firstVisible=grid.Rows.Count>0?grid.FirstDisplayedScrollingRowIndex:-1;var selectedSymbol=grid.CurrentRow?.Tag is FuturesQuote selected?selected.Symbol:null;
        grid.SuspendLayout();try
        {
            grid.Rows.Clear();var index=0;foreach(var item in source){var row=grid.Rows.Add(++index,item.Symbol,item.Name,item.Current.ToString("0.####"),item.Change.ToString("+0.####;-0.####;0"),item.ChangePercent.ToString("+0.00;-0.00;0.00")+"%",item.Volume.ToString("N0"),item.Position.ToString("N0"),item.QuoteTime?.ToString("HH:mm:ss")??"--");grid.Rows[row].Tag=item;var color=item.ChangePercent>0?Color.Red:item.ChangePercent<0?Color.FromArgb(0,145,70):Color.Black;grid.Rows[row].Cells[3].Style.ForeColor=color;grid.Rows[row].Cells[4].Style.ForeColor=color;grid.Rows[row].Cells[5].Style.ForeColor=color;}
            if(!string.IsNullOrWhiteSpace(selectedSymbol)){var selectedRow=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(x=>x.Tag is FuturesQuote quote&&quote.Symbol.Equals(selectedSymbol,StringComparison.OrdinalIgnoreCase));if(selectedRow is not null){selectedRow.Selected=true;grid.CurrentCell=selectedRow.Cells[0];}}
            if(firstVisible>=0&&grid.Rows.Count>0)grid.FirstDisplayedScrollingRowIndex=Math.Min(firstVisible,grid.Rows.Count-1);
        }
        finally{grid.ResumeLayout();}
    }

    private void FillFuturesStrip(HorizontalFlowPanel strip,IEnumerable<FuturesQuote> source,bool rising)
    {
        var scroll=Math.Abs(strip.AutoScrollPosition.X);strip.SuspendLayout();try
        {
            strip.Controls.Clear();foreach(var item in source)
            {
                var color=rising?Color.FromArgb(205,42,42):Color.FromArgb(0,132,67);var background=rising?Color.FromArgb(255,245,245):Color.FromArgb(242,252,247);
                var card=new TableLayoutPanel{Width=210,Height=76,Margin=new Padding(5,6,5,6),Padding=new Padding(8,6,8,6),BackColor=background,CellBorderStyle=TableLayoutPanelCellBorderStyle.Single,ColumnCount=2,RowCount=2,Cursor=Cursors.Hand};
                card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));card.RowStyles.Add(new RowStyle(SizeType.Percent,52));card.RowStyles.Add(new RowStyle(SizeType.Percent,48));
                var name=new Label{Text=item.Name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(45,45,45),Font=new Font("宋体",10,FontStyle.Bold),AutoEllipsis=true,Cursor=Cursors.Hand};
                var percent=new Label{Text=item.ChangePercent.ToString("+0.00;-0.00;0.00")+"%",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=color,Font=new Font("Consolas",10,FontStyle.Bold),Cursor=Cursors.Hand};
                var symbol=new Label{Text=item.Symbol,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.DimGray,Font=new Font("Consolas",8.5f),Cursor=Cursors.Hand};
                var price=new Label{Text=item.Current.ToString("0.####"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=color,Font=new Font("Consolas",10,FontStyle.Bold),Cursor=Cursors.Hand};
                card.Controls.Add(name,0,0);card.Controls.Add(percent,1,0);card.Controls.Add(symbol,0,1);card.Controls.Add(price,1,1);
                var menu=new ContextMenuStrip();menu.Items.Add("查看分时图",null,async(_,_)=>await OpenFuturesChartAsync(item,"分时图"));menu.Items.Add("查看K线图",null,async(_,_)=>await OpenFuturesChartAsync(item,"日K线"));card.ContextMenuStrip=menu;foreach(Control child in card.Controls){child.ContextMenuStrip=menu;strip.EnableDrag(child);}card.Disposed+=(_,_)=>menu.Dispose();strip.EnableDrag(card);strip.Controls.Add(card);
            }
            strip.AutoScrollPosition=new Point(Math.Min(scroll,Math.Max(0,strip.DisplayRectangle.Width-strip.ClientSize.Width)),0);
        }
        finally{strip.ResumeLayout();}
    }

    private async Task OpenSelectedFuturesChartAsync(ContextMenuStrip menu,string chartType)
    {
        if(menu.SourceControl is not DataGridView grid||grid.CurrentRow?.Tag is not FuturesQuote item)return;
        await OpenFuturesChartAsync(item,chartType);
    }

    private async Task OpenFuturesChartAsync(FuturesQuote item,string chartType)
    {
        var quote=new StockQuote(item.Symbol,item.Name,item.Current,item.PreviousSettlement,item.Open,item.High,item.Low,item.Volume,0,0,0,0,0,item.QuoteTime);
        var stock=new StockItem{Code=item.Symbol,DisplayName=item.Name};
        var form=new StockDetailsForm(stock,quote,[],chartType,(int)Number(_refresh.Text,3),(type,token)=>_futuresService.GetChartAsync(item.Symbol,type,token));form.Show(this);await Task.CompletedTask;
    }

    private void ConfigureRankingContextMenu(params DataGridView[] grids)
    {
        var menu=new ContextMenuStrip();
        var add=menu.Items.Add("加入关注的股票");
        menu.Items.Add(new ToolStripSeparator());
        var chart=menu.Items.Add("查看分时图");
        var kline=menu.Items.Add("查看K线图");
        menu.Opening+=(_,e)=>
        {
            if(menu.SourceControl is not DataGridView grid||grid.CurrentRow?.Tag is not string code)
            {
                e.Cancel=true;return;
            }
            var exists=IsDuplicate(code);add.Text=exists?"已在关注列表中":"加入关注的股票";add.Enabled=!exists;
        };
        add.Click+=(sender,e)=>AddSelectedRankingStock(menu);
        chart.Click+=async(sender,e)=>await OpenSelectedRankingChartAsync(menu,"分时图");
        kline.Click+=async(sender,e)=>await OpenSelectedRankingChartAsync(menu,"日K线");
        foreach(var grid in grids)
        {
            grid.ContextMenuStrip=menu;
            grid.MouseDown+=(_,e)=>
            {
                if(e.Button!=MouseButtons.Right)return;
                var hit=grid.HitTest(e.X,e.Y);if(hit.RowIndex<0)return;
                grid.ClearSelection();grid.Rows[hit.RowIndex].Selected=true;grid.CurrentCell=grid.Rows[hit.RowIndex].Cells[0];
            };
        }
    }

    private void AddSelectedRankingStock(ContextMenuStrip menu)
    {
        if(menu.SourceControl is not DataGridView grid||grid.CurrentRow?.Tag is not string code||IsDuplicate(code))return;
        var name=Convert.ToString(grid.CurrentRow.Cells[2].Value);if(string.IsNullOrWhiteSpace(name))name=code;
        AddStockRow(new StockItem{Code=code,DisplayName=name});_rankingStatus.Text=$"已加入关注：{name}";
    }

    private async Task OpenSelectedRankingChartAsync(ContextMenuStrip menu,string chartType)
    {
        if(menu.SourceControl is not DataGridView grid||grid.CurrentRow?.Tag is not string code)return;
        var name=Convert.ToString(grid.CurrentRow.Cells[2].Value);if(string.IsNullOrWhiteSpace(name))name=code;
        await OpenRankingChartAsync(code,name,chartType);
    }

    private async Task OpenRankingChartAsync(string code,string name,string chartType)
    {
        _rankingChartCts?.Cancel();_rankingChartCts?.Dispose();_rankingChartCts=new CancellationTokenSource();
        var cancellationToken=_rankingChartCts.Token;_rankingStatus.Text=$"正在获取 {name} 的{chartType}行情...";
        try
        {
            var quotes=await _rankingQuoteService.GetQuotesAsync([code],cancellationToken);
            if(cancellationToken.IsCancellationRequested||IsDisposed)return;
            var normalized=StockCode.Normalize(code);
            if(!quotes.TryGetValue(normalized,out var quote)){MessageBox.Show(this,"暂时无法获取该股票行情。","分时图",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            var stock=new StockItem{Code=code,DisplayName=name};
            var form=new StockDetailsForm(stock,quote,[],chartType,(int)Number(_refresh.Text,3));form.Show(this);
            _rankingStatus.Text=$"已打开{chartType}：{name}";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,chartType+"打开失败："+ex.Message,chartType,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private TabPage BuildAdvancedPage()
    {
        var page=Page("高级");
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18,12,18,8),ColumnCount=3,RowCount=8};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
        for(var i=0;i<5;i++)table.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,132));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        AddRow(0,"文字大小：",_fontSize,"宋体；常规");
        AddRow(1,"行距：",_spacing,"");
        AddLabel("背景颜色：",2);var colorHost=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};_background.Location=new Point(0,2);_background.Size=new Size(24,24);colorHost.Controls.Add(_background);table.Controls.Add(colorHost,1,2);table.Controls.Add(new Label{Text="白色为透明背景",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},2,2);
        AddRow(3,"透明度：",_opacity,"数值越小越透明");
        AddRow(4,"刷新间隔：",_refresh,"可设置 1–10 秒");
        var shortcutGroup=new GroupBox{Text="显示/隐藏快捷键",Dock=DockStyle.Fill,Margin=new Padding(6,4,6,6)};
        var shortcutGrid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14,9,14,9),ColumnCount=3,RowCount=2};
        shortcutGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));shortcutGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,72));shortcutGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shortcutGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));shortcutGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        shortcutGrid.Controls.Add(ShortcutLabel("快捷键："),0,0);
        _boss.Anchor=AnchorStyles.Left;_boss.Margin=new Padding(0);shortcutGrid.Controls.Add(_boss,1,0);
        _bossShortcut.Dock=DockStyle.Fill;_bossShortcut.MaximumSize=Size.Empty;_bossShortcut.Margin=new Padding(0,6,0,6);shortcutGrid.Controls.Add(_bossShortcut,2,0);
        shortcutGrid.Controls.Add(ShortcutLabel("按键动作："),0,1);
        var bossActions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,6,0,0)};_bossHide.Margin=new Padding(0,2,22,0);_bossExit.Margin=new Padding(0,2,0,0);bossActions.Controls.Add(_bossHide);bossActions.Controls.Add(_bossExit);shortcutGrid.Controls.Add(bossActions,1,1);shortcutGrid.SetColumnSpan(bossActions,2);
        shortcutGroup.Controls.Add(shortcutGrid);table.Controls.Add(shortcutGroup,0,5);table.SetColumnSpan(shortcutGroup,3);
        var common=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(121,5,0,0)};_topMost.Margin=new Padding(0,4,18,0);_tray.Margin=new Padding(0,4,0,0);common.Controls.Add(_topMost);common.Controls.Add(_tray);table.Controls.Add(common,0,6);table.SetColumnSpan(common,3);
        var reset=new Button{Text="一键重置(&R)",Size=new Size(120,26),Anchor=AnchorStyles.None,UseVisualStyleBackColor=true};reset.Click+=(_,_)=>LoadControls(new AppSettings());table.Controls.Add(reset,0,7);table.SetColumnSpan(reset,3);
        _boss.CheckedChanged+=(_,_)=>{_bossHide.Enabled=_boss.Checked;_bossExit.Enabled=_boss.Checked;_bossShortcut.Enabled=_boss.Checked;}; _bossShortcut.Enter+=(_,_)=>_bossShortcut.SelectAll(); _bossShortcut.MouseDown+=(_,_)=>_bossShortcut.SelectAll(); _bossShortcut.KeyDown+=CaptureShortcut; _tips.SetToolTip(_bossShortcut,"点击后直接按组合键；Backspace 或 Delete 清空");
        _background.Click+=ChooseBackground;_tips.SetToolTip(_boss,"开启后，按下指定快捷键程序可以自动隐藏、退出");page.Controls.Add(table);return page;
        void AddLabel(string text,int row){var label=new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,AutoEllipsis=true,Margin=new Padding(0,0,10,0)};table.Controls.Add(label,0,row);}
        void AddRow(int row,string labelText,Control input,string note){AddLabel(labelText,row);input.Dock=DockStyle.Fill;input.Margin=new Padding(0,8,10,8);table.Controls.Add(input,1,row);if(note.Length>0)table.Controls.Add(new Label{Text=note,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Margin=new Padding(0)},2,row);}
        static Label ShortcutLabel(string text)=>new(){Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,AutoSize=false,AutoEllipsis=true,Margin=new Padding(0,0,12,0),UseCompatibleTextRendering=false};
    }

    private TabPage BuildChartPage()
    {
        var page=Page("股价图");
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,Height=330,Padding=new Padding(18,10,18,0),ColumnCount=1,RowCount=3};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,102));

        var open=new GroupBox{Text="开启方式",Dock=DockStyle.Fill,Margin=new Padding(4,3,4,6)};
        var openGrid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18,8,18,7),ColumnCount=2,RowCount=2};
        openGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));openGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        openGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));openGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        AddOption(openGrid,_chart,0,0);AddOption(openGrid,_details,1,0);AddOption(openGrid,_singleClick,0,1);AddOption(openGrid,_doubleClick,1,1);
        open.Controls.Add(openGrid);

        var content=new GroupBox{Text="默认显示内容",Dock=DockStyle.Fill,Margin=new Padding(4,3,4,6)};
        var contentGrid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10,5,10,4),ColumnCount=2,RowCount=2};
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        contentGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,34));contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        _chartType.Dock=DockStyle.Fill;_chartType.Margin=new Padding(0,3,0,5);contentGrid.Controls.Add(_chartType,0,0);contentGrid.SetColumnSpan(_chartType,2);
        AddOption(contentGrid,_quickChart,0,1);AddOption(contentGrid,_advancedChart,1,1);content.Controls.Add(contentGrid);

        var background=new GroupBox{Text="背景及透明度",Dock=DockStyle.Fill,Margin=new Padding(4,3,4,3)};
        var backgroundGrid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10,12,10,10),ColumnCount=4,RowCount=1};
        backgroundGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));backgroundGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));backgroundGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));backgroundGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        backgroundGrid.Controls.Add(new Label{Text="背景：",AutoSize=true,Anchor=AnchorStyles.Right},0,0);_chartBackground.Dock=DockStyle.None;_chartBackground.Anchor=AnchorStyles.Left|AnchorStyles.Right;_chartBackground.Margin=new Padding(0,0,18,0);backgroundGrid.Controls.Add(_chartBackground,1,0);
        backgroundGrid.Controls.Add(new Label{Text="透明度：",AutoSize=true,Anchor=AnchorStyles.Right},2,0);_chartOpacity.Dock=DockStyle.None;_chartOpacity.Anchor=AnchorStyles.Left|AnchorStyles.Right;_chartOpacity.Margin=Padding.Empty;backgroundGrid.Controls.Add(_chartOpacity,3,0);background.Controls.Add(backgroundGrid);

        layout.Controls.Add(open,0,0);layout.Controls.Add(content,0,1);layout.Controls.Add(background,0,2);page.Controls.Add(layout);return page;
        static void AddOption(TableLayoutPanel panel,Control option,int column,int row){option.Dock=DockStyle.None;option.Anchor=AnchorStyles.Left;option.AutoSize=true;option.MinimumSize=new Size(0,24);option.Margin=new Padding(4,4,4,4);panel.Controls.Add(option,column,row);}
    }

    private TabPage BuildOtherPage()
    {
        var page=Page("其他");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24,18,24,14),ColumnCount=1,RowCount=3};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,190));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
        var group=new GroupBox{Text="设置页面显示",Dock=DockStyle.Fill,Margin=Padding.Empty};
        var options=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18,12,18,10),ColumnCount=3,RowCount=3};
        for(var i=0;i<3;i++)options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
        for(var i=0;i<3;i++)options.RowStyles.Add(new RowStyle(SizeType.Percent,33.333f));
        var pageOptions=new[]{_showStocksPage,_showDisplayPage,_showAdvancedPage,_showOtherPage,_showRankingPage,_showLimitUpLadderPage,_showFuturesPage,_showChartPage,_showMonitorPage};
        for(var i=0;i<pageOptions.Length;i++){pageOptions[i].Anchor=AnchorStyles.Left;pageOptions[i].Margin=new Padding(8,5,8,5);options.Controls.Add(pageOptions[i],i%3,i/3);}
        group.Controls.Add(options);layout.Controls.Add(group,0,0);
        layout.Controls.Add(new Label{Text="关注的股票、显示、高级和其他为固定页面，不能隐藏。",Dock=DockStyle.Top,Height=34,ForeColor=Color.DimGray,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(8,8,8,0)},0,1);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(0,8,0,0)};
        var export=new Button{Text="导出配置到文件(&E)",Size=new Size(150,29),Margin=new Padding(0,0,12,0),UseVisualStyleBackColor=true};
        var import=new Button{Text="从文件导入配置(&I)",Size=new Size(150,29),UseVisualStyleBackColor=true};
        export.Click+=ExportConfig;import.Click+=ImportConfig;
        buttons.Controls.Add(export);buttons.Controls.Add(import);layout.Controls.Add(buttons,0,2);page.Controls.Add(layout);return page;
    }

    private TabPage BuildMonitorPage()
    {
        var page=Page("监控");
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,Height=224,Padding=new Padding(28,22,28,12),ColumnCount=1,RowCount=4};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        _monitorDragonTiger.Anchor=AnchorStyles.Left;_monitorDragonTiger.Margin=new Padding(8,6,8,6);
        _monitorSevereAbnormal.Anchor=AnchorStyles.Left;_monitorSevereAbnormal.Margin=new Padding(8,6,8,6);
        _monitorSealAbnormal.Anchor=AnchorStyles.Left;_monitorSealAbnormal.Margin=new Padding(8,6,8,6);
        layout.Controls.Add(_monitorDragonTiger,0,0);layout.Controls.Add(_monitorSevereAbnormal,0,1);layout.Controls.Add(_monitorSealAbnormal,0,2);
        layout.Controls.Add(new Label{Text="启用后，将鼠标悬浮在关注股票行上即可查看监控结果。",Dock=DockStyle.Fill,ForeColor=Color.DimGray,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(8,4,8,4)},0,3);
        _tips.SetToolTip(_monitorDragonTiger,"龙虎榜异动规则：\r\n沪深主板：单日偏离±7%、振幅15%、换手率20%，或3日累计偏离±20%；\r\n创业板/科创板：单日涨跌±15%、振幅30%、换手率30%，或3日累计偏离±30%；\r\n北交所：单日涨跌±20%、振幅30%、换手率20%，或3日累计偏离±40%。");
        _tips.SetToolTip(_monitorSevereAbnormal,"严重异常异动规则：\r\n沪深主板、创业板、科创板：10日偏离+100%/-50%，30日偏离+200%/-70%；\r\n北交所：10日偏离+150%/-60%，30日偏离+300%/-75%。\r\n连续出现多次同向普通异动需结合交易所公告重置口径，暂不自动计算。");
        _tips.SetToolTip(_monitorSealAbnormal,"封板异动规则：\r\n股票处于涨停或跌停封板状态时，若约10秒内封单量下降达到30%，触发封板异动提示。\r\n检测频率跟随设置中的行情刷新时间，触发结果保留30秒。");
        page.Controls.Add(layout);return page;
    }

    private void LoadControls(AppSettings s)
    {
        foreach (var stock in s.Stocks) AddStockRow(CloneStock(stock));
        Select(_codeMode, s.CodeDisplayMode); Select(_nameMode, s.NameDisplayMode); Select(_priceMode, s.PriceDisplayMode); Select(_changeMode, s.ChangeDisplayMode); _showBoard.Checked=s.ShowBoard; _industryComparison.Checked=s.ShowIndustryComparison;
        _sealVolume.Checked=s.ShowSealVolume;
        _fontSize.Text=s.FontSize.ToString("0"); _spacing.SelectedIndex=Math.Clamp(s.RowSpacing/3,0,_spacing.Items.Count-1); _opacity.Text=s.OpacityPercent+"%"; _refresh.Text=s.RefreshSeconds+"s";
        _background.BackColor=Color.FromArgb(s.BackgroundColorArgb); _boss.Checked=s.EnableBossKey; _bossShortcut.Text=FormatShortcut(s.BossKeyModifiers,s.BossKey); _bossShortcut.Enabled=s.EnableBossKey; _bossExit.Checked=s.BossKeyExits; _bossHide.Checked=!s.BossKeyExits;
        _topMost.Checked=s.AlwaysOnTop; _tray.Checked=s.ShowTrayIcon; _chart.Checked=s.EnableChart; _chartType.Text=s.ChartType; _details.Checked=s.OpenDetailsOnDoubleClick; _doubleClick.Checked=s.OpenDetailsOnDoubleClick; _singleClick.Checked=!s.OpenDetailsOnDoubleClick;
        _monitorDragonTiger.Checked=s.MonitorDragonTiger; _monitorSevereAbnormal.Checked=s.MonitorSevereAbnormal; _monitorSealAbnormal.Checked=s.MonitorSealAbnormal; UpdateSample();
        _showRankingPage.Checked=s.ShowRankingPage;_showLimitUpLadderPage.Checked=s.ShowLimitUpLadderPage;_showFuturesPage.Checked=s.ShowFuturesPage;_showChartPage.Checked=s.ShowChartPage;_showMonitorPage.Checked=s.ShowMonitorPage;
    }

    private bool ReadControls()
    {
        Result.Stocks=_stocks.Rows.Cast<DataGridViewRow>().Select(x=>(StockItem)x.Tag!).ToList(); Result.CodeDisplayMode=_codeMode.SelectedIndex; Result.ShowBoard=_showBoard.Checked; Result.NameDisplayMode=_nameMode.SelectedIndex; Result.ShowIndustryComparison=_industryComparison.Checked; Result.PriceDisplayMode=_priceMode.SelectedIndex; Result.ChangeDisplayMode=_changeMode.SelectedIndex;
        Result.ShowSealVolume=_sealVolume.Checked; Result.FontSize=(float)Number(_fontSize.Text,11); Result.RowSpacing=_spacing.SelectedIndex*3; Result.OpacityPercent=(int)Number(_opacity.Text,100); Result.RefreshSeconds=(int)Number(_refresh.Text,3); Result.BackgroundColorArgb=_background.BackColor.ToArgb(); Result.TransparentBackground=_background.BackColor.ToArgb()==Color.White.ToArgb();
        Result.EnableBossKey=_boss.Checked; ReadShortcut(_bossShortcut.Text,out var modifiers,out var key); Result.BossKeyModifiers=modifiers; Result.BossKey=key; Result.BossKeyExits=_bossExit.Checked; Result.AlwaysOnTop=_topMost.Checked; Result.ShowTrayIcon=_tray.Checked; Result.EnableChart=_chart.Checked; Result.ChartType=_chartType.Text; Result.OpenDetailsOnDoubleClick=_doubleClick.Checked; Result.MonitorDragonTiger=_monitorDragonTiger.Checked; Result.MonitorSevereAbnormal=_monitorSevereAbnormal.Checked; Result.MonitorSealAbnormal=_monitorSealAbnormal.Checked;
        Result.ShowRankingPage=_showRankingPage.Checked;Result.ShowLimitUpLadderPage=_showLimitUpLadderPage.Checked;Result.ShowFuturesPage=_showFuturesPage.Checked;Result.ShowChartPage=_showChartPage.Checked;Result.ShowMonitorPage=_showMonitorPage.Checked;
        Result.ShowCode=Result.CodeDisplayMode!=3; Result.ShowName=Result.NameDisplayMode!=5; Result.ShowCurrent=Result.PriceDisplayMode!=1; Result.ShowChange=Result.PriceDisplayMode==2; Result.ShowChangePercent=Result.ChangeDisplayMode!=2; Result.Normalize(); return true;
    }

    private void UpdateSample()
    {
        var code=_codeMode.SelectedIndex switch{1=>"000",2=>"00",3=>"",_=>"sh600000"};if(_showBoard.Checked)code="主 "+code;
        var baseName=_nameMode.SelectedIndex switch{1=>"浦发",2=>"浦",3=>"银行",4=>"行",5=>"",6=>"浦发银行",_=>"浦发银行"};
        var name=baseName;if(_industryComparison.Checked&&name.Length>0)name+="（银行+0.85%）";
        var price=_priceMode.SelectedIndex switch{1=>"",2=>"10.00  +0.12",_=>"10.00"};
        var change=_changeMode.SelectedIndex==2?"":"+1.20%";
        var sealedText=_sealVolume.Checked?"(999手)":"";
        _sample.Text=string.Join("  ",new[]{code,name,price,change,sealedText}.Where(x=>x.Length>0));
        _sample.ForeColor=_changeMode.SelectedIndex switch{1=>Color.FromArgb(Result.FlatColorArgb),3=>Color.FromArgb(Result.RiseColorArgb),2=>SystemColors.ControlText,_=>Color.Red};
    }

    private async Task LoadRankingsAsync()
    {
        _rankingCts?.Cancel();_rankingCts?.Dispose();_rankingCts=new CancellationTokenSource();
        var cancellationToken=_rankingCts.Token;
        _refreshRankings.Enabled=false;_rankingStatus.Text="正在加载排行...";_tips.SetToolTip(_rankingStatus,string.Empty);
        try
        {
            var results=await Task.WhenAll(
                LoadRankingAsync("热股排行",_rankingService.GetHotStocksAsync(cancellationToken),_hotStockRanking,0,cancellationToken),
                LoadRankingAsync("流入排行",_rankingService.GetCapitalInflowAsync(cancellationToken),_capitalInflowRanking,1,cancellationToken),
                LoadRankingAsync("流出排行",_rankingService.GetCapitalOutflowAsync(cancellationToken),_capitalOutflowRanking,2,cancellationToken),
                LoadIndustryRankingAsync(cancellationToken));
            if(cancellationToken.IsCancellationRequested||IsDisposed)return;
            var errors=results.Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
            _rankingStatus.Text=errors.Length==0?$"更新于 {DateTime.Now:HH:mm:ss}":errors.Length==4?"排行加载失败":$"部分加载失败（{errors.Length}项）";
            _tips.SetToolTip(_rankingStatus,string.Join(Environment.NewLine,errors));
        }
        finally
        {
            if(!IsDisposed&&!cancellationToken.IsCancellationRequested)_refreshRankings.Enabled=true;
        }
    }

    private async Task LoadLimitUpLadderAsync()
    {
        _limitUpCts?.Cancel();_limitUpCts?.Dispose();_limitUpCts=new CancellationTokenSource();var cancellationToken=_limitUpCts.Token;
        _refreshLimitUps.Enabled=false;_previousLimitUpDay.Enabled=false;_nextLimitUpDay.Enabled=false;_limitUpStatus.Text=$"正在加载 {_limitUpTradingDate:M月d日} 涨停股票...";_tips.SetToolTip(_limitUpStatus,string.Empty);
        try
        {
            var items=await _rankingService.GetLimitUpLadderAsync(_limitUpTradingDate,cancellationToken);
            if(IsDisposed)return;
            FillLimitUpLadder(items);
            _limitUpStatus.Text=$"{items.Count(x=>!x.IsPreviousLimitUpFailure)}只涨停，更新于 {DateTime.Now:HH:mm:ss}";
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            if(IsDisposed)return;_limitUpStatus.Text="涨停天梯加载失败";_tips.SetToolTip(_limitUpStatus,ex.Message);
        }
        finally{if(!IsDisposed){_refreshLimitUps.Enabled=true;_previousLimitUpDay.Enabled=true;_nextLimitUpDay.Enabled=_limitUpTradingDate<DateTime.Today;}}
    }

    private async Task ChangeLimitUpTradingDayAsync(int direction)
    {
        _limitUpCts?.Cancel();_limitUpCts?.Dispose();_limitUpCts=new CancellationTokenSource();var token=_limitUpCts.Token;
        _refreshLimitUps.Enabled=false;_previousLimitUpDay.Enabled=false;_nextLimitUpDay.Enabled=false;
        ShowLimitUpLoadingSkeleton(direction);await Task.Yield();
        try
        {
            var date=await _rankingService.FindLimitUpTradingDayAsync(_limitUpTradingDate,direction,token);
            if(token.IsCancellationRequested||IsDisposed)return;
            if(date is null){FillLimitUpLadder(_limitUpItems);_limitUpStatus.Text=direction>0?"已经是最新交易日":"未找到更早的涨停数据";return;}
            _limitUpTradingDate=date.Value;await LoadLimitUpLadderAsync();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!IsDisposed){FillLimitUpLadder(_limitUpItems);_limitUpStatus.Text="交易日切换失败";_tips.SetToolTip(_limitUpStatus,ex.Message);}}
        finally{if(!IsDisposed){_refreshLimitUps.Enabled=true;_previousLimitUpDay.Enabled=true;_nextLimitUpDay.Enabled=_limitUpTradingDate<DateTime.Today;}}
    }

    private void ShowLimitUpLoadingSkeleton(int direction)
    {
        _loadingLimitUpTradingDay=true;_limitUpResizeTimer.Stop();_lastLimitUpCardsPerRow=-1;
        _limitUpTitle.Text=direction<0?"正在切换到上一交易日...":direction>0?"正在切换到下一交易日...":"正在加载涨停天梯...";
        _limitUpSummary.Text="正在加载涨停统计";_limitUpIndustrySummary.Text="";_limitUpPromotionSummary.Text="";_limitUpStatus.Text="正在查找交易日并加载数据...";
        _limitUpLadder.SuspendLayout();
        try
        {
            _limitUpLadder.Controls.Clear();var width=Math.Max(280,_limitUpLadder.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-6);
            for(var row=0;row<5;row++)
            {
                var section=new Panel{Width=width,Height=70,Margin=Padding.Empty,BackColor=Color.FromArgb(248,248,248),BorderStyle=BorderStyle.FixedSingle};
                section.Controls.Add(new Panel{Location=new Point(14,20),Size=new Size(38,28),BackColor=Color.FromArgb(222,222,222)});
                for(var card=0;card<6;card++)section.Controls.Add(new Panel{Location=new Point(72+card*112,12),Size=new Size(92,46),BackColor=Color.FromArgb(232,232,232)});
                _limitUpLadder.Controls.Add(section);
            }
        }
        finally{_limitUpLadder.ResumeLayout(true);}
        Update();
    }

    private void FillLimitUpLadder(IReadOnlyList<LimitUpLadderItem> items)
    {
        _loadingLimitUpTradingDay=false;
        _limitUpItems=items;
        _lastLimitUpCardsPerRow=-1;
        UpdateLimitUpTitle();
        var current=items.Where(x=>!x.IsPreviousLimitUpFailure).ToArray();var failed=items.Count(x=>x.IsPreviousLimitUpFailure);
        var highest=current.Length==0?0:current.Max(x=>x.ConsecutiveBoards);var breaks=current.Sum(x=>x.BreakCount);
        var industryGroups=current.Select(x=>(Parent:string.IsNullOrWhiteSpace(x.PrimaryIndustry)?x.IndustryBoard.Trim():x.PrimaryIndustry.Trim(),Detail:x.IndustryBoard.Trim())).Where(x=>!string.IsNullOrWhiteSpace(x.Parent)).GroupBy(x=>x.Parent,StringComparer.OrdinalIgnoreCase).OrderByDescending(x=>x.Count()).ThenBy(x=>x.Key,StringComparer.CurrentCulture).ToArray();
        var industries=industryGroups.Select(x=>$"{x.Key}*{x.Count()}");
        var promotion=items.Where(x=>x.PreviousConsecutiveBoards>0).GroupBy(x=>x.PreviousConsecutiveBoards).OrderBy(x=>x.Key)
            .Select(group=>{var total=group.Count();var success=group.Count(x=>!x.IsPreviousLimitUpFailure);var rate=total==0?0:success*100m/total;return $"{group.Key}进{group.Key+1}成功率：{success}/{total}（{rate:0.#}%）";});
        _limitUpSummary.Text=$"当日涨停：{current.Length}只    前日断板：{failed}只    最高：{highest}板    累计炸板：{breaks}次";
        _limitUpIndustrySummary.Text=string.Join("    ",industries);
        _tips.SetToolTip(_limitUpIndustrySummary,string.Join(Environment.NewLine,industryGroups.Select(group=>$"{group.Key}*{group.Count()}："+string.Join("、",group.Where(x=>!string.IsNullOrWhiteSpace(x.Detail)).GroupBy(x=>x.Detail).OrderByDescending(x=>x.Count()).Select(x=>$"{x.Key}*{x.Count()}")))));
        _limitUpPromotionSummary.Text=string.Join("    ",promotion);
        RenderLimitUpLadder();
    }

    private void UpdateLimitUpTitle()=>_limitUpTitle.Text=$"{_limitUpTradingDate:M月d日 dddd}  {(_limitUpDistributionMode.SelectedIndex==1?"板块涨停分布":"连板天梯")}";

    private void RenderLimitUpLadder()
    {
        if(_loadingLimitUpTradingDay||_renderingLimitUps||_limitUpLadder.ClientSize.Width<100)return;
        _renderingLimitUps=true;_limitUpLadder.SuspendLayout();
        try
        {
            var width=Math.Max(280,_limitUpLadder.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-6);var boardDistribution=_limitUpDistributionMode.SelectedIndex==1;var titleWidth=boardDistribution?96:58;
            var cardsPerRow=Math.Max(1,(width-titleWidth-4)/96);
            if(cardsPerRow==_lastLimitUpCardsPerRow&&_limitUpLadder.Controls.Count>0)
            {
                foreach(Control section in _limitUpLadder.Controls)section.Width=width;
                return;
            }
            _limitUpLadder.Controls.Clear();_lastLimitUpCardsPerRow=cardsPerRow;
            if(boardDistribution)
            {
                foreach(var group in _limitUpItems.Where(x=>!x.IsPreviousLimitUpFailure)
                    .GroupBy(x=>string.IsNullOrWhiteSpace(x.PrimaryIndustry)?string.IsNullOrWhiteSpace(x.IndustryBoard)?"未分类":x.IndustryBoard.Trim():x.PrimaryIndustry.Trim(),StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x=>x.Count()).ThenBy(x=>x.Key,StringComparer.CurrentCulture))AddSection($"{group.Key}\r\n({group.Count()})",group);
            }
            else foreach(var group in _limitUpItems.GroupBy(x=>x.ConsecutiveBoards).OrderByDescending(x=>x.Key))AddSection(group.Key<=1?$"首板\r\n({group.Count()})":$"{group.Key}板",group);

            void AddSection(string title,IEnumerable<LimitUpLadderItem> source)
            {
                var values=source.ToArray();var rows=(int)Math.Ceiling(values.Length/(double)_lastLimitUpCardsPerRow);var height=Math.Max(72,rows*68+6);
                var section=new TableLayoutPanel{Width=width,Height=height,Margin=new Padding(0),BorderStyle=BorderStyle.FixedSingle,BackColor=Color.White,ColumnCount=2,RowCount=1};
                section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,titleWidth));section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));section.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                section.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("宋体",boardDistribution?9:10,FontStyle.Bold),BackColor=Color.FromArgb(250,250,250),AutoEllipsis=true},0,0);
                var cards=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=false,Padding=new Padding(3),Margin=Padding.Empty};
                foreach(var item in values)cards.Controls.Add(CreateLimitUpCard(item));
                section.Controls.Add(cards,1,0);_limitUpLadder.Controls.Add(section);
            }
        }
        finally{_limitUpLadder.ResumeLayout();_renderingLimitUps=false;}
    }

    private Control CreateLimitUpCard(LimitUpLadderItem item)
    {
        var host=new Panel{Width=92,Height=62,Margin=new Padding(1),BackColor=Color.White,Cursor=Cursors.Hand};
        var card=new TableLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty,ColumnCount=1,RowCount=3,BackColor=Color.White,Cursor=Cursors.Hand};
        card.RowStyles.Add(new RowStyle(SizeType.Absolute,18));card.RowStyles.Add(new RowStyle(SizeType.Absolute,24));card.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var time=item.SealTime?.ToString("HH:mm")??"--";if(item.BreakCount>0)time+=$"  炸{item.BreakCount}";
        var nameColor=item.ConsecutiveBoards>1?Color.FromArgb(205,45,35):Color.FromArgb(35,90,175);
        var timeColor=Color.DimGray;var industryColor=Color.FromArgb(55,55,55);
        if(item.IsPreviousLimitUpFailure){nameColor=FadeColor(nameColor);timeColor=FadeColor(timeColor);industryColor=FadeColor(industryColor);}
        card.Controls.Add(new Label{Text=time,Dock=DockStyle.Fill,TextAlign=ContentAlignment.BottomCenter,ForeColor=timeColor,Font=new Font("宋体",8)},0,0);
        card.Controls.Add(new Label{Text=item.Name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=nameColor,Font=new Font("宋体",10,FontStyle.Bold),AutoEllipsis=true},0,1);
        card.Controls.Add(new Label{Text=string.IsNullOrWhiteSpace(item.Industry)?"未分类":item.Industry,Dock=DockStyle.Fill,TextAlign=ContentAlignment.TopCenter,ForeColor=industryColor,Font=new Font("宋体",8),AutoEllipsis=true},0,2);
        var menu=new ContextMenuStrip();var add=menu.Items.Add(IsDuplicate(item.Code)?"已在关注列表中":"加入关注的股票");add.Enabled=!IsDuplicate(item.Code);menu.Items.Add(new ToolStripSeparator());var minute=menu.Items.Add("查看分时图");var daily=menu.Items.Add("查看K线图");
        add.Click+=(_,_)=>{if(IsDuplicate(item.Code))return;AddStockRow(new StockItem{Code=item.Code,DisplayName=item.Name});_limitUpStatus.Text=$"已加入关注：{item.Name}";};
        minute.Click+=async(_,_)=>await OpenRankingChartAsync(item.Code,item.Name,"分时图");daily.Click+=async(_,_)=>await OpenRankingChartAsync(item.Code,item.Name,"日K线");
        foreach(Control control in card.Controls)control.ContextMenuStrip=menu;card.ContextMenuStrip=menu;host.ContextMenuStrip=menu;host.Controls.Add(card);
        var tipLines=new List<string>{item.Name};
        if(!string.IsNullOrWhiteSpace(item.Industry)&&!string.IsNullOrWhiteSpace(item.ThemeType))tipLines.Add($"匹配板块：{item.Industry}（{item.ThemeType}）");
        if(!string.IsNullOrWhiteSpace(item.LimitUpReason))tipLines.Add("涨停原因："+item.LimitUpReason);
        else if(!item.IsPreviousLimitUpFailure)tipLines.Add("涨停原因：暂无数据");
        if(item.IsPreviousLimitUpFailure)
        {
            var overlay=new CrossOverlay{Dock=DockStyle.Fill,Cursor=Cursors.Hand,ContextMenuStrip=menu};
            tipLines.Add($"{item.ConsecutiveBoards}进{item.ConsecutiveBoards+1}失败，当前 {item.ChangePercent:+0.00;-0.00;0.00}%");
            var tip=string.Join(Environment.NewLine,tipLines);
            _tips.SetToolTip(host,tip);_tips.SetToolTip(card,tip);foreach(Control control in card.Controls)_tips.SetToolTip(control,tip);_tips.SetToolTip(overlay,tip);
            host.Controls.Add(overlay);overlay.BringToFront();
        }
        else
        {
            var tip=string.Join(Environment.NewLine,tipLines);
            _tips.SetToolTip(host,tip);_tips.SetToolTip(card,tip);foreach(Control control in card.Controls)_tips.SetToolTip(control,tip);
        }
        host.Disposed+=(_,_)=>menu.Dispose();return host;
    }

    private async Task<string?> LoadRankingAsync(string title,Task<IReadOnlyList<StockRankingItem>> request,DataGridView grid,int type,CancellationToken cancellationToken)
    {
        try
        {
            var items=await request;
            if(cancellationToken.IsCancellationRequested||IsDisposed)return null;
            FillRankingGrid(grid,items,type);return null;
        }
        catch(OperationCanceledException){return null;}
        catch(Exception ex){return title+"："+ex.Message;}
    }

    private async Task<string?> LoadIndustryRankingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items=await _rankingService.GetIndustryRankingAsync(cancellationToken);if(cancellationToken.IsCancellationRequested||IsDisposed)return null;
            _industryRanking.Rows.Clear();
            for(var i=0;i<items.Count;i++)
            {
                var item=items[i];var row=_industryRanking.Rows.Add(i+1,item.Name,item.ChangePercent.ToString("+0.00;-0.00;0.00")+"%",item.LeadingStock,item.LeadingStockChangePercent.ToString("+0.00;-0.00;0.00")+"%");
                _industryRanking.Rows[row].Cells[2].Style.ForeColor=ChangeColor(item.ChangePercent);_industryRanking.Rows[row].Cells[4].Style.ForeColor=ChangeColor(item.LeadingStockChangePercent);
            }
            return null;
        }
        catch(OperationCanceledException){return null;}
        catch(Exception ex){return "行业排行："+ex.Message;}
        static Color ChangeColor(decimal value)=>value>0?Color.Red:value<0?Color.FromArgb(0,145,70):Color.Black;
    }

    private static void FillRankingGrid(DataGridView grid,IReadOnlyList<StockRankingItem> items,int type)
    {
        grid.Rows.Clear();
        for(var i=0;i<items.Count;i++)
        {
            var item=items[i];
            var metric=type==0?item.Metric.ToString("N0"):FormatMoney(type==2?Math.Abs(item.Metric):item.Metric);
            var change=item.ChangePercent.ToString("+0.00;-0.00;0.00")+"%";
            var rowIndex=grid.Rows.Add(i+1,item.Code,item.Name,item.Industry,change,metric);grid.Rows[rowIndex].Tag=item.Code;
            grid.Rows[rowIndex].Cells[4].Style.ForeColor=item.ChangePercent>0?Color.Red:item.ChangePercent<0?Color.FromArgb(0,145,70):Color.Black;
            grid.Rows[rowIndex].Cells[5].Style.ForeColor=type switch{1=>Color.Red,2=>Color.FromArgb(0,145,70),_=>Color.FromArgb(35,90,160)};
        }
    }

    private static string FormatMoney(decimal value)
    {
        var absolute=Math.Abs(value);
        if(absolute>=100_000_000m)return (value/100_000_000m).ToString("0.00")+"亿";
        if(absolute>=10_000m)return (value/10_000m).ToString("0.00")+"万";
        return value.ToString("0")+"元";
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
    protected override void OnFormClosed(FormClosedEventArgs e){_searchTimer.Stop();_limitUpResizeTimer.Stop();_futuresTimer.Stop();_limitUpResizeTimer.Dispose();_futuresTimer.Dispose();_searchCts?.Cancel();_rankingCts?.Cancel();_rankingCts?.Dispose();_limitUpCts?.Cancel();_limitUpCts?.Dispose();_rankingChartCts?.Cancel();_rankingChartCts?.Dispose();_futuresCts?.Cancel();_futuresCts?.Dispose();_searchService.Dispose();_rankingService.Dispose();_rankingQuoteService.Dispose();_futuresService.Dispose();base.OnFormClosed(e);}
    private static TabPage Page(string text)=>new(text){Size=new Size(323,263),Padding=new Padding(3)};
    private static GroupBox Box(string text,int x,int y,int w,int h)=>new(){Text=text,Location=new Point(x,y),Size=new Size(w,h)};
    private static Label LabelAt(string text,int x,int y)=>new(){Text=text,Location=new Point(x,y),AutoSize=true};
    private static CheckBox PageOption(string text,bool fixedPage=false)=>new(){Text=text,AutoSize=true,Checked=true,Enabled=!fixedPage};
    private static ComboBox Combo(params string[] items){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};c.Items.AddRange(items);if(items.Length>0)c.SelectedIndex=0;return c;}
    private static DataGridView RankingGrid(string metricHeader)
    {
        var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoGenerateColumns=false,ReadOnly=true,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=SystemColors.Window,BorderStyle=BorderStyle.Fixed3D};
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="排名",Width=52,MinimumWidth=46,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{Alignment=DataGridViewContentAlignment.MiddleCenter}});
        grid.Columns.Add(Column("股票代码"));
        grid.Columns.Add(Column("股票名称"));
        grid.Columns.Add(Column("板块"));
        grid.Columns.Add(Column("当日涨跌",DataGridViewContentAlignment.MiddleRight));
        grid.Columns.Add(Column(metricHeader,DataGridViewContentAlignment.MiddleRight));
        return grid;
        static DataGridViewTextBoxColumn Column(string header,DataGridViewContentAlignment alignment=DataGridViewContentAlignment.MiddleLeft)=>new(){HeaderText=header,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=100,MinimumWidth=55,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{Alignment=alignment}};
    }
    private static DataGridView IndustryRankingGrid()
    {
        var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoGenerateColumns=false,ReadOnly=true,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=SystemColors.Window,BorderStyle=BorderStyle.Fixed3D};
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="排名",Width=52,MinimumWidth=46,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{Alignment=DataGridViewContentAlignment.MiddleCenter}});
        grid.Columns.Add(Column("行业板块"));grid.Columns.Add(Column("板块涨跌",DataGridViewContentAlignment.MiddleRight));grid.Columns.Add(Column("领涨股"));grid.Columns.Add(Column("领涨股涨跌",DataGridViewContentAlignment.MiddleRight));return grid;
        static DataGridViewTextBoxColumn Column(string header,DataGridViewContentAlignment alignment=DataGridViewContentAlignment.MiddleLeft)=>new(){HeaderText=header,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=100,MinimumWidth=70,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{Alignment=alignment}};
    }
    private static DataGridView FuturesGrid()
    {
        var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoGenerateColumns=false,ReadOnly=true,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=SystemColors.Window,BorderStyle=BorderStyle.Fixed3D};
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="排名",Width=48,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{Alignment=DataGridViewContentAlignment.MiddleCenter}});
        foreach(var column in new[]{("合约","Left"),("名称","Left"),("最新价","Right"),("涨跌额","Right"),("涨跌幅","Right"),("成交量","Right"),("持仓量","Right"),("时间","Center")})grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=column.Item1,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=column.Item1=="名称"?125:100,MinimumWidth=58,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{Alignment=column.Item2=="Right"?DataGridViewContentAlignment.MiddleRight:column.Item2=="Center"?DataGridViewContentAlignment.MiddleCenter:DataGridViewContentAlignment.MiddleLeft}});
        return grid;
    }
    private static void Place(Control c,int x,int y,int w,int h){c.Location=new Point(x,y);c.Size=new Size(w,h);}
    private static void Select(ComboBox c,int i)=>c.SelectedIndex=Math.Clamp(i,0,c.Items.Count-1);
    private static decimal Number(string s,decimal fallback)=>decimal.TryParse(new string(s.Where(x=>char.IsDigit(x)||x=='.').ToArray()),out var n)?n:fallback;
    private static AppSettings Clone(AppSettings s)=>JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
    private static StockItem CloneStock(StockItem s)=>JsonSerializer.Deserialize<StockItem>(JsonSerializer.Serialize(s))!;
    private static Color FadeColor(Color color)=>Color.FromArgb((color.R+255)/2,(color.G+255)/2,(color.B+255)/2);

    private sealed class CrossOverlay:Control
    {
        public CrossOverlay()
        {
            BackColor=Color.FromArgb(225,115,115);TabStop=false;
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);if(Width<=0||Height<=0)return;
            const int p=12,t=3;
            using var path=new System.Drawing.Drawing2D.GraphicsPath();
            path.AddPolygon([new Point(p,p-t),new Point(p-t,p),new Point(Width-p,Height-p+t),new Point(Width-p+t,Height-p)]);
            path.AddPolygon([new Point(Width-p,p-t),new Point(Width-p+t,p),new Point(p,Height-p+t),new Point(p-t,Height-p)]);
            var old=Region;Region=new Region(path);old?.Dispose();
        }
    }

    private sealed class HorizontalFlowPanel:FlowLayoutPanel
    {
        private bool _dragging;private Point _dragStart;private int _scrollStart;
        public HorizontalFlowPanel(){Dock=DockStyle.Fill;AutoScroll=true;FlowDirection=FlowDirection.LeftToRight;WrapContents=false;BackColor=Color.FromArgb(248,248,248);Padding=new Padding(3,2,3,22);Margin=Padding.Empty;EnableDrag(this);}
        public void EnableDrag(Control control)
        {
            control.MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;_dragging=true;_dragStart=control.PointToScreen(e.Location);_scrollStart=Math.Abs(AutoScrollPosition.X);control.Capture=true;};
            control.MouseMove+=(s,e)=>{if(!_dragging||e.Button!=MouseButtons.Left)return;var current=control.PointToScreen(e.Location);var target=Math.Clamp(_scrollStart- (current.X-_dragStart.X),0,Math.Max(0,DisplayRectangle.Width-ClientSize.Width));AutoScrollPosition=new Point(target,0);};
            control.MouseUp+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;_dragging=false;control.Capture=false;};
        }
    }
}

