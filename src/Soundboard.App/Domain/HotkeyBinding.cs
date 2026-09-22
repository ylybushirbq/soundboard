using System.Text.Json.Serialization;

namespace Soundboard.Core;

/// <summary>
/// A user-configurable key combination stored in JSON.
/// Keyboard bindings: game mode observes with GetAsyncKeyState; desktop mode
/// registers with Win32 RegisterHotKey (which steals the combo). Mouse side
/// buttons use <c>Key</c> = <see cref="KeyXButton1"/> / <see cref="KeyXButton2"/>
/// and are always delivered through Raw Input (RegisterHotKey cannot bind them).
/// </summary>
public sealed class HotkeyBinding : IEquatable<HotkeyBinding>
{
    public const string KeyXButton1 = "XButton1";
    public const string KeyXButton2 = "XButton2";

    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }

    /// <summary>
    /// Primary key: a WinForms <c>Keys</c> name such as F1 / A / Space / D1,
    /// or <see cref="KeyXButton1"/> / <see cref="KeyXButton2"/> for mouse side buttons.
    /// Empty means unset.
    /// </summary>
    public string Key { get; set; } = "";

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Key);

    [JsonIgnore]
    public bool IsMouseButton => IsXButton1 || IsXButton2;

    [JsonIgnore]
    public bool IsXButton1 => IsMouseKey(Key, KeyXButton1);

    [JsonIgnore]
    public bool IsXButton2 => IsMouseKey(Key, KeyXButton2);

    [JsonIgnore]
    public bool HasModifier => Control || Alt || Shift || Win;

    public static bool IsMouseKey(string? key) =>
        IsMouseKey(key, KeyXButton1) || IsMouseKey(key, KeyXButton2);

    public static bool IsMouseKey(string? key, string expected) =>
        !string.IsNullOrWhiteSpace(key)
        && string.Equals(key.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    public string ToDisplayString()
    {
        if (IsEmpty)
        {
            return "（未设置）";
        }

        var parts = new List<string>(5);
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(PrettyKey(Key));
        return string.Join(" + ", parts);
    }

    public static string PrettyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        key = key.Trim();
        if (string.Equals(key, KeyXButton1, StringComparison.OrdinalIgnoreCase))
        {
            return "鼠标侧键1";
        }

        if (string.Equals(key, KeyXButton2, StringComparison.OrdinalIgnoreCase))
        {
            return "鼠标侧键2";
        }

        if (key.Length == 2 && key[0] is 'D' or 'd' && char.IsDigit(key[1]))
        {
            return key[1].ToString();
        }

        if (key.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && key.Length > 6)
        {
            return "Num" + key[6..];
        }

        return key switch
        {
            "OemMinus" or "Subtract" => "-",
            "OemPlus" or "Add" => "+",
            "OemPeriod" => ".",
            "Oemcomma" or "OemComma" => ",",
            "OemQuestion" => "/",
            "Oemtilde" or "OemTilde" => "`",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "OemPipe" => "\\",
            "OemQuotes" => "'",
            "OemSemicolon" => ";",
            "PageUp" or "Prior" => "PageUp",
            "PageDown" or "Next" => "PageDown",
            _ => key
        };
    }

    public HotkeyBinding Clone() => new()
    {
        Control = Control,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        Key = Key
    };

    public bool Equals(HotkeyBinding? other)
    {
        if (other is null)
        {
            return false;
        }

        return Control == other.Control
               && Alt == other.Alt
               && Shift == other.Shift
               && Win == other.Win
               && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is HotkeyBinding other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Control, Alt, Shift, Win, Key?.ToUpperInvariant());

    public override string ToString() => ToDisplayString();
}
