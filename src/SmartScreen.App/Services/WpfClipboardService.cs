using System.Windows;
using System.Runtime.InteropServices;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class WpfClipboardService(ITextLocalizer textLocalizer) : IClipboardService
{
    private const int MaxClipboardAttempts = 6;
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(90);

    public async Task CopyImageAsync(ScreenshotResult screenshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bitmap = BitmapSourceFactory.FromScreenshot(screenshot);

        await SetClipboardWithRetryAsync(
            () => System.Windows.Clipboard.SetImage(bitmap),
            cancellationToken);
    }

    public async Task CopyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await SetClipboardWithRetryAsync(
            () => System.Windows.Clipboard.SetText(text),
            cancellationToken);
    }

    private async Task SetClipboardWithRetryAsync(Action action, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxClipboardAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
                return;
            }
            catch (Exception exception) when (IsClipboardBusyException(exception) && attempt < MaxClipboardAttempts)
            {
                lastException = exception;
                await Task.Delay(ClipboardRetryDelay * attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            textLocalizer.GetString(
                "clipboard.error.busy",
                "Буфер обміну тимчасово зайнятий іншою програмою."),
            lastException);
    }

    private static bool IsClipboardBusyException(Exception exception) =>
        exception is COMException { HResult: unchecked((int)0x800401D0) } or ExternalException;
}
