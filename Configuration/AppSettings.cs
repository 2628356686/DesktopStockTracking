using StockTickerLite.Models;

namespace StockTickerLite.Configuration;

public sealed class AppSettings
{
    public int RefreshSeconds { get; set; } = 3;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ShowTrayIcon { get; set; } = true;
    public bool TransparentBackground { get; set; }
    public int OpacityPercent { get; set; } = 100;
    public float FontSize { get; set; } = 11;
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public int RowSpacing { get; set; } = 1;
    public int CodeDisplayMode { get; set; } = 0;
    public int NameDisplayMode { get; set; } = 0;
    public int PriceDisplayMode { get; set; } = 0;
    public int ChangeDisplayMode { get; set; } = 0;
    public int NoteDisplayMode { get; set; } = 0;
    public bool EnableChart { get; set; } = true;
    public string ChartType { get; set; } = "分时图";
    public bool ShowCode { get; set; } = true;
    public bool ShowName { get; set; } = true;
    public bool ShowCurrent { get; set; } = true;
    public bool ShowChange { get; set; }
    public bool ShowChangePercent { get; set; } = true;
    public bool ShowVolume { get; set; }
    public bool ShowSealVolume { get; set; }
    public bool ShowProfit { get; set; }
    public bool ShowNote { get; set; }
    public bool AlignText { get; set; } = true;
    public bool EnableBalloonAlert { get; set; } = true;
    public bool EnableSoundAlert { get; set; }
    public bool MouseThrough { get; set; }
    public bool EnableBossKey { get; set; }
    public string BossKeyModifiers { get; set; } = "Ctrl+Alt";
    public string BossKey { get; set; } = "T";
    public bool BossKeyExits { get; set; }
    public bool OpenDetailsOnDoubleClick { get; set; } = true;
    public int RiseColorArgb { get; set; } = Color.Red.ToArgb();
    public int FlatColorArgb { get; set; } = Color.Black.ToArgb();
    public int FallColorArgb { get; set; } = Color.FromArgb(0, 145, 70).ToArgb();
    public int BackgroundColorArgb { get; set; } = Color.White.ToArgb();
    public int Left { get; set; } = -1;
    public int Top { get; set; } = -1;
    public List<StockItem> Stocks { get; set; } =
    [
        new() { Code = "sh000001", DisplayName = "上证指数" },
        new() { Code = "sz399001", DisplayName = "深证成指" }
    ];

    public void Normalize()
    {
        RefreshSeconds = Math.Clamp(RefreshSeconds, 1, 10);
        FontSize = Math.Clamp(FontSize, 8, 28);
        OpacityPercent = Math.Clamp(OpacityPercent, 20, 100);
        RowSpacing = Math.Clamp(RowSpacing, 0, 16);
        CodeDisplayMode = Math.Clamp(CodeDisplayMode, 0, 3);
        NameDisplayMode = Math.Clamp(NameDisplayMode, 0, 6);
        PriceDisplayMode = Math.Clamp(PriceDisplayMode, 0, 2);
        ChangeDisplayMode = Math.Clamp(ChangeDisplayMode, 0, 2);
        NoteDisplayMode = Math.Clamp(NoteDisplayMode, 0, 2);
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Microsoft YaHei UI" : FontFamily;
        BossKeyModifiers = string.IsNullOrWhiteSpace(BossKeyModifiers) ? "Ctrl+Alt" : BossKeyModifiers;
        BossKey = string.IsNullOrWhiteSpace(BossKey) ? "T" : BossKey.ToUpperInvariant();
        ChartType = string.IsNullOrWhiteSpace(ChartType) ? "分时图" : ChartType;
        Stocks ??= [];
        Stocks = Stocks
            .Where(x => StockCode.IsSupported(x.Code))
            .GroupBy(x => x.NormalizedCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }
}

