namespace StockTickerLite;

public sealed class CustomColorForm : Form
{
    private readonly Button _rise=new();
    private readonly Button _flat=new();
    private readonly Button _fall=new();
    public Color RiseColor=>_rise.BackColor;
    public Color FlatColor=>_flat.BackColor;
    public Color FallColor=>_fall.BackColor;

    public CustomColorForm(Color rise,Color flat,Color fall)
    {
        Text="自定义字体颜色";ClientSize=new Size(147,112);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;Font=new Font("宋体",9);
        Controls.Add(MakeLabel("上涨：",34,20));Controls.Add(MakeLabel("平盘：",34,49));Controls.Add(MakeLabel("下跌：",34,79));
        Setup(_rise,81,14,rise);Setup(_flat,81,43,flat);Setup(_fall,81,73,fall);
        Controls.Add(_rise);Controls.Add(_flat);Controls.Add(_fall);
        _rise.Click+=Pick;_flat.Click+=Pick;_fall.Click+=Pick;
    }
    private static Label MakeLabel(string text,int x,int y)=>new(){Text=text,Location=new Point(x,y),AutoSize=true};
    private static void Setup(Button b,int x,int y,Color color){b.Location=new Point(x,y);b.Size=new Size(24,24);b.BackColor=color;b.UseVisualStyleBackColor=false;}
    private void Pick(object? sender,EventArgs e){if(sender is not Button b)return;using var d=new ColorDialog{Color=b.BackColor,FullOpen=true};if(d.ShowDialog(this)==DialogResult.OK)b.BackColor=d.Color;}
    protected override void OnDeactivate(EventArgs e){base.OnDeactivate(e);}
}
