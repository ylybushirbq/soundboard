using System.Drawing.Drawing2D;

namespace Soundboard.Ui;

internal static class IconFactory
{
    public static Icon CreateAppIcon()
    {
        try
        {
            var fromExe = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (fromExe is not null)
            {
                return fromExe;
            }
        }
        catch
        {
            // fall through to drawn icon
        }

        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(32, 120, 96));
            g.FillEllipse(bg, 1, 1, 30, 30);
            using var fg = new SolidBrush(Color.White);
            g.FillRectangle(fg, 8, 12, 6, 8);
            Point[] cone = [new(14, 12), new(20, 8), new(20, 24), new(14, 20)];
            g.FillPolygon(fg, cone);
        }

        var handle = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(handle);
            return (Icon)tmp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
