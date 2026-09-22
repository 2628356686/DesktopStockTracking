using Microsoft.Win32;

namespace StockTickerLite.Services;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StockTickerLite";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
            throw new InvalidOperationException("无法打开 Windows 启动项。");

        if (enabled)
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue(ValueName, false);
    }
}
