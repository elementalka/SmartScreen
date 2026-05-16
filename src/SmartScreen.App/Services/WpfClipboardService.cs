using System.Windows;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class WpfClipboardService : IClipboardService
{
    public Task CopyImageAsync(ScreenshotResult screenshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Clipboard.SetImage(BitmapSourceFactory.FromScreenshot(screenshot));
        });

        return Task.CompletedTask;
    }

    public Task CopyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Clipboard.SetText(text);
        });

        return Task.CompletedTask;
    }
}
