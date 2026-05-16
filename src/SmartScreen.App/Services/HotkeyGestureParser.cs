namespace SmartScreen.App.Services;

internal readonly record struct ParsedHotkeyGesture(uint Modifiers, uint VirtualKey, string NormalizedGesture);

internal static class HotkeyGestureParser
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public static bool TryParse(string? gesture, out ParsedHotkeyGesture parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var modifiers = 0u;
        var virtualKey = 0u;
        var keyName = string.Empty;
        var tokens = gesture
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToUpperInvariant())
            .ToArray();

        foreach (var token in tokens)
        {
            switch (token)
            {
                case "CTRL" or "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "WIN" or "WINDOWS":
                    modifiers |= ModWin;
                    break;
                case "PRINTSCREEN" or "PRTSC" or "PRTSCR":
                    if (virtualKey != 0)
                    {
                        return false;
                    }

                    virtualKey = 0x2C;
                    keyName = "PrintScreen";
                    break;
                default:
                    if (virtualKey != 0)
                    {
                        return false;
                    }

                    if (token.Length == 1 && token[0] is >= 'A' and <= 'Z')
                    {
                        virtualKey = token[0];
                        keyName = token;
                    }
                    else if (token.StartsWith('F') &&
                             int.TryParse(token[1..], out var functionKey) &&
                             functionKey is >= 1 and <= 24)
                    {
                        virtualKey = (uint)(0x70 + functionKey - 1);
                        keyName = $"F{functionKey}";
                    }
                    else
                    {
                        return false;
                    }

                    break;
            }
        }

        if (virtualKey == 0)
        {
            return false;
        }

        parsed = new ParsedHotkeyGesture(modifiers, virtualKey, Normalize(modifiers, keyName));
        return true;
    }

    private static string Normalize(uint modifiers, string keyName)
    {
        var parts = new List<string>(5);

        if ((modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModWin) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(keyName);
        return string.Join("+", parts);
    }
}
