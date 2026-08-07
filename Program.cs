using System.Text;

namespace StockTickerLite;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, "StockTickerLite.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("程序已经在运行。", "轻量桌面盯盘",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new MainForm());
    }
}
