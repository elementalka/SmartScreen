using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;
using Forms = System.Windows.Forms;

namespace SmartScreen.Infrastructure.Imaging;

public sealed class ScreenshotService : IScreenshotService
{
    public Task<ScreenshotResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CaptureRectangle(GetVirtualScreenBounds(), "Full screen");
        }, cancellationToken);

    public Task<ScreenshotResult> CaptureActiveWindowAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
            {
                return CaptureRectangle(GetVirtualScreenBounds(), "Full screen");
            }

            var width = Math.Max(1, rect.Right - rect.Left);
            var height = Math.Max(1, rect.Bottom - rect.Top);
            return CaptureRectangle(new Rectangle(rect.Left, rect.Top, width, height), "Active window");
        }, cancellationToken);

    public Task<ScreenshotResult> CaptureMonitorAsync(int monitorIndex, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var screens = Forms.Screen.AllScreens
                .OrderByDescending(screen => screen.Primary)
                .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (screens.Length == 0)
            {
                return CaptureRectangle(GetVirtualScreenBounds(), "Full screen");
            }

            var safeIndex = Math.Clamp(monitorIndex, 0, screens.Length - 1);
            var screen = screens[safeIndex];
            return CaptureRectangle(screen.Bounds, screen.Primary ? "Primary monitor" : $"Monitor {safeIndex + 1}");
        }, cancellationToken);

    public Task<ScreenshotResult> CaptureRegionAsync(ScreenRegion region, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (region.IsEmpty)
            {
                throw new ArgumentException("Selected region is empty.", nameof(region));
            }

            return CaptureRectangle(new Rectangle(region.X, region.Y, region.Width, region.Height), "Selected region");
        }, cancellationToken);

    private static ScreenshotResult CaptureRectangle(Rectangle bounds, string sourceName)
    {
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var now = DateTimeOffset.Now;

        return new ScreenshotResult
        {
            ImageBytes = stream.ToArray(),
            MimeType = "image/png",
            Width = bounds.Width,
            Height = bounds.Height,
            CreatedAt = now,
            SuggestedFileName = FileNameHelper.CreateScreenshotFileName(now, Domain.Enums.ScreenshotImageFormat.Png),
            SourceName = sourceName
        };
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        var screens = Forms.Screen.AllScreens;
        var left = screens.Min(screen => screen.Bounds.Left);
        var top = screens.Min(screen => screen.Bounds.Top);
        var right = screens.Max(screen => screen.Bounds.Right);
        var bottom = screens.Max(screen => screen.Bounds.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
