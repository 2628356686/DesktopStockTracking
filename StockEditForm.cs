using System.ComponentModel;
using StockTickerLite.Models;

namespace StockTickerLite;

public sealed class StockEditForm : Form
{
    private readonly TextBox _code = new();
    private readonly TextBox _name = new();
    private readonly TextBox _note = new();
    private readonly NumericUpDown _cost = Number(0, 1000000, 4);
    private readonly NumericUpDown _position = Number(0, 100000000, 0);
    private readonly NumericUpDown _upper = Number(0, 1000000, 4);
    private readonly NumericUpDown _lower = Number(0, 1000000, 4);
    private readonly NumericUpDown _percent = Number(0, 100, 2);
    private readonly CheckBox _hasCost = new() { Text = "设置" };
    private readonly CheckBox _hasPosition = new() { Text = "设置" };
    private readonly CheckBox _hasUpper = new() { Text = "启用" };
    private readonly CheckBox _hasLower = new() { Text = "启用" };
    private readonly CheckBox _hasPercent = new() { Text = "启用" };

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public StockItem Result { get; private set; }

    public StockEditForm(StockItem? source = null)
    {
        Result = source is null ? new StockItem() : Clone(source);
        Text = source is null ? "添加股票" : "股票高级编辑";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(430, 390);
        Font = new Font("Microsoft YaHei UI", 9);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 10 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        for (var i = 0; i < 9; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddRow(table, 0, "股票代码", _code, null);
        AddRow(table, 1, "显示名称", _name, null);
        AddRow(table, 2, "备注", _note, null);
        AddRow(table, 3, "成本价", _cost, _hasCost);
        AddRow(table, 4, "持股数量", _position, _hasPosition);
        AddRow(table, 5, "股价预警上限", _upper, _hasUpper);
        AddRow(table, 6, "股价预警下限", _lower, _hasLower);
        AddRow(table, 7, "涨跌幅预警 %", _percent, _hasPercent);
        var tip = new Label { Text = "预警条件达到后，可在“预警”页选择气泡、声音等方式。", AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left };
        table.Controls.Add(tip, 0, 8);
        table.SetColumnSpan(tip, 3);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = "确定", Width = 86 };
        var cancel = new Button { Text = "取消", Width = 86, DialogResult = DialogResult.Cancel };
        ok.Click += Save;
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 9); table.SetColumnSpan(buttons, 3);
        Controls.Add(table); AcceptButton = ok; CancelButton = cancel;
        _code.Text = source?.Code ?? ""; _name.Text = source?.DisplayName ?? ""; _note.Text = source?.Note ?? "";
        SetOptional(_cost, _hasCost, source?.CostPrice); SetOptional(_position, _hasPosition, source?.Position);
        SetOptional(_upper, _hasUpper, source?.UpperAlert); SetOptional(_lower, _hasLower, source?.LowerAlert); SetOptional(_percent, _hasPercent, source?.ChangePercentAlert);
    }

    private void Save(object? sender, EventArgs e)
    {
        if (!StockCode.IsSupported(_code.Text)) { MessageBox.Show("请输入有效的六位沪、深、北交所代码。", "代码错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); _code.Focus(); return; }
        if (_hasUpper.Checked && _hasLower.Checked && _upper.Value <= _lower.Value) { MessageBox.Show("预警上限必须大于预警下限。", "预警设置", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        Result.Code = StockCode.Normalize(_code.Text); Result.DisplayName = _name.Text.Trim(); Result.Note = _note.Text.Trim();
        Result.CostPrice = _hasCost.Checked ? _cost.Value : null; Result.Position = _hasPosition.Checked ? (int)_position.Value : null;
        Result.UpperAlert = _hasUpper.Checked ? _upper.Value : null; Result.LowerAlert = _hasLower.Checked ? _lower.Value : null; Result.ChangePercentAlert = _hasPercent.Checked ? _percent.Value : null;
        DialogResult = DialogResult.OK; Close();
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control editor, Control? option)
    { table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); editor.Dock = DockStyle.Fill; table.Controls.Add(editor, 1, row); if (option is not null) table.Controls.Add(option, 2, row); }
    private static NumericUpDown Number(decimal min, decimal max, int decimals) => new() { Minimum = min, Maximum = max, DecimalPlaces = decimals, ThousandsSeparator = true, Increment = decimals == 0 ? 1 : 0.01m };
    private static void SetOptional(NumericUpDown number, CheckBox enabled, decimal? value) { enabled.Checked = value.HasValue; if (value.HasValue) number.Value = Math.Clamp(value.Value, number.Minimum, number.Maximum); number.DataBindings.Add(nameof(number.Enabled), enabled, nameof(enabled.Checked)); }
    private static StockItem Clone(StockItem x) => new() { Code=x.Code, DisplayName=x.DisplayName, Note=x.Note, CostPrice=x.CostPrice, Position=x.Position, UpperAlert=x.UpperAlert, LowerAlert=x.LowerAlert, ChangePercentAlert=x.ChangePercentAlert };
}
