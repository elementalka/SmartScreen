using System.Runtime.InteropServices;
using System.Windows.Interop;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class WpfHotkeyService(ILoggingService loggingService) : IHotkeyService
{
    private const int WmHotkey = 0x0312;

    private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = [];
    private int _nextId = 100;
    private bool _isHooked;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public Task RegisterAsync(HotkeySettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMessageHook();
        UnregisterAllAsync(cancellationToken).GetAwaiter().GetResult();
        var registeredCount = 0;

        foreach (var binding in settings.Bindings.Where(binding => binding.IsEnabled))
        {
            if (!HotkeyGestureParser.TryParse(binding.Gesture, out var parsed))
            {
                loggingService.Warning($"Hotkey gesture could not be parsed: {binding.Gesture}");
                continue;
            }

            var id = _nextId++;
            if (RegisterHotKey(IntPtr.Zero, id, parsed.Modifiers | HotkeyGestureParser.ModNoRepeat, parsed.VirtualKey))
            {
                _registeredHotkeys[id] = binding.Action;
                registeredCount++;
                loggingService.Info($"Hotkey registered: {parsed.NormalizedGesture} -> {binding.Action}");
                continue;
            }

            var error = Marshal.GetLastWin32Error();
            loggingService.Warning($"Hotkey '{binding.Gesture}' could not be registered. Win32 error: {error}");
        }

        if (registeredCount == 0)
        {
            loggingService.Warning("No global hotkeys were registered.");
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
