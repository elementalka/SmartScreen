using System.Runtime.InteropServices;
using System.Windows.Interop;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class WpfHotkeyService(ILoggingService loggingService) : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = [];
    private int _nextId = 100;
    private bool _isHooked;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public Task RegisterAsync(HotkeySettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMessageHook();
        UnregisterAllAsync(cancellationToken).GetAwaiter().GetResult();

        foreach (var binding in settings.Bindings.Where(binding => binding.IsEnabled))
        {
            if (!TryParseGesture(binding.Gesture, out var modifiers, out var virtualKey))
            {
                loggingService.Warning($"Hotkey gesture could not be parsed: {binding.Gesture}");
                continue;
            }

            var id = _nextId++;
            if (RegisterHotKey(IntPtr.Zero, id, modifiers | ModNoRepeat, virtualKey))
            {
                _registeredHotkeys[id] = binding.Action;
                continue;
            }

            var error = Marshal.GetLastWin32Error();
            loggingService.Warning($"Hotkey '{binding.Gesture}' could not be registered. Win32 error: {error}");
        }

        return Task.CompletedTask;
    }

    public Task UnregisterAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var id in _registeredHotkeys.Keys.ToArray())
        {
            UnregisterHotKey(IntPtr.Zero, id);
        }

        _registeredHotkeys.Clear();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        UnregisterAllAsync().GetAwaiter().GetResult();

        if (_isHooked)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            _isHooked = false;
        }
    }

    private void EnsureMessageHook()
    {
        if (_isHooked)
        {
            return;
        }

        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        _isHooked = true;
    }

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WmHotkey)
        {
            return;
        }

        var id = msg.wParam.ToInt32();
        if (_registeredHotkeys.TryGetValue(id, out var action))
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(action));
            handled = true;
        }
    }

    private static bool TryParseGesture(string gesture, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

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
                    virtualKey = 0x2C;
                    break;
                default:
                    if (token.Length == 1 && token[0] is >= 'A' and <= 'Z')
                    {
                        virtualKey = token[0];
                    }
                    else if (token.StartsWith('F') && int.TryParse(token[1..], out var functionKey) && functionKey is >= 1 and <= 24)
                    {
                        virtualKey = (uint)(0x70 + functionKey - 1);
                    }

                    break;
            }
        }

        return virtualKey != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

