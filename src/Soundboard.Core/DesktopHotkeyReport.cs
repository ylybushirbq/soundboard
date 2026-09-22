namespace Soundboard.Core;

/// <summary>
/// Pure status/dialog copy for desktop-mode RegisterHotKey results.
/// </summary>
public static class DesktopHotkeyReport
{
    public static string StatusBar(int registeredKeyboard, int failedKeyboard, int mouseObserved, string? dataDirectory = null)
    {
        var data = string.IsNullOrWhiteSpace(dataDirectory)
            ? ""
            : $" 数据目录：{dataDirectory}";

        if (registeredKeyboard == 0 && failedKeyboard == 0 && mouseObserved == 0)
        {
            return $"桌面模式（RegisterHotKey）。尚未设置热键。{data}".TrimEnd();
        }

        if (failedKeyboard == 0)
        {
            var ok = $"桌面模式：已注册 {registeredKeyboard} 个热键";
            if (mouseObserved > 0)
            {
                ok += $"；Raw Input 观察 {mouseObserved} 个鼠标侧键";
            }

            return (ok + "。" + data).TrimEnd();
        }

        var mix = $"桌面模式：已注册 {registeredKeyboard} 个热键，失败 {failedKeyboard} 个";
        if (mouseObserved > 0)
        {
            mix += $"；Raw Input 观察 {mouseObserved} 个鼠标侧键";
        }

        return (mix + "。" + data).TrimEnd();
    }

    public static string FailureSummary(IEnumerable<(string Owner, string Binding, string Error)> failures)
    {
        var lines = failures
            .Select(f => $"{f.Owner}（{f.Binding}）：{f.Error}")
            .ToList();
        if (lines.Count == 0)
        {
            return "";
        }

        return "下列热键无法注册（桌面模式）：\n" + string.Join("\n", lines);
    }

    public static string SuccessBalloon(int registeredKeyboard) =>
        $"桌面模式：已注册 {registeredKeyboard} 个热键（会抢走该组合键）";
}
