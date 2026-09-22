namespace Soundboard.Core;

/// <summary>
/// Maps <c>RegisterHotKey</c> / <c>GetLastError</c> codes to Chinese UI text.
/// </summary>
public static class RegisterHotKeyError
{
    public const int InvalidParameter = 87;
    public const int InvalidWindowHandle = 1400;
    public const int AlreadyRegistered = 1409;
    public const int NotRegistered = 1419;
    public const int NoWindow = -1;

    public static string ToChinese(int win32Code) => win32Code switch
    {
        AlreadyRegistered => "组合键已被占用",
        InvalidParameter => "按键或修饰键无效",
        InvalidWindowHandle => "注册窗口句柄无效",
        NotRegistered => "热键尚未注册",
        NoWindow => "没有可用于注册的窗口",
        0 => "注册失败（未知原因）",
        _ => $"注册失败（Win32 {win32Code}）"
    };
}
