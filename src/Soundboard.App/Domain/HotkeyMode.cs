using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soundboard.Core;

/// <summary>
/// How global hotkeys are acquired.
/// <see cref="Game"/> observes keys without stealing them;
/// <see cref="Desktop"/> uses <c>RegisterHotKey</c> and consumes the combo.
/// </summary>
[JsonConverter(typeof(HotkeyModeJsonConverter))]
public enum HotkeyMode
{
    /// <summary>GetAsyncKeyState + Raw Input. Default. Does not steal keys.</summary>
    Game = 0,

    /// <summary>RegisterHotKey for keyboard (steals the combo). Mouse side buttons stay Raw Input.</summary>
    Desktop = 1
}

public static class HotkeyModeParser
{
    public static HotkeyMode Normalize(HotkeyMode value) =>
        Enum.IsDefined(value) ? value : HotkeyMode.Game;

    /// <summary>
    /// Parses config text. Missing, empty, or unknown values become <see cref="HotkeyMode.Game"/>.
    /// Accepts <c>game</c> / <c>desktop</c> (any case) and the numeric enum values 0 / 1.
    /// </summary>
    public static HotkeyMode Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return HotkeyMode.Game;
        }

        var trimmed = text.Trim();
        if (string.Equals(trimmed, "desktop", StringComparison.OrdinalIgnoreCase))
        {
            return HotkeyMode.Desktop;
        }

        if (string.Equals(trimmed, "game", StringComparison.OrdinalIgnoreCase))
        {
            return HotkeyMode.Game;
        }

        if (int.TryParse(trimmed, out var number) && Enum.IsDefined((HotkeyMode)number))
        {
            return (HotkeyMode)number;
        }

        return HotkeyMode.Game;
    }

    public static string ToConfigString(HotkeyMode mode) =>
        mode == HotkeyMode.Desktop ? "desktop" : "game";
}

public sealed class HotkeyModeJsonConverter : JsonConverter<HotkeyMode>
{
    public override HotkeyMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return HotkeyModeParser.Parse(reader.GetString());
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var n) && Enum.IsDefined((HotkeyMode)n)
                    ? (HotkeyMode)n
                    : HotkeyMode.Game;
            case JsonTokenType.Null:
                return HotkeyMode.Game;
            default:
                reader.Skip();
                return HotkeyMode.Game;
        }
    }

    public override void Write(Utf8JsonWriter writer, HotkeyMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(HotkeyModeParser.ToConfigString(HotkeyModeParser.Normalize(value)));
}
