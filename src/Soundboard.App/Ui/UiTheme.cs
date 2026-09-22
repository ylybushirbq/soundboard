using Soundboard.Core;

namespace Soundboard.Ui;

internal static class UiTheme
{
    public static readonly Font Font = CreateFont();

    private static Font CreateFont()
    {
        foreach (var name in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" })
        {
            try
            {
                var font = new Font(name, 9.5f);
                if (string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }

                font.Dispose();
            }
            catch
            {
                // try next
            }
        }

        return SystemFonts.MessageBoxFont ?? new Font("Segoe UI", 9.5f);
    }
}
