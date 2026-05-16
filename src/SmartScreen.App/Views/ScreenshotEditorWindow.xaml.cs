using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;
using MediaColor = System.Windows.Media.Color;

namespace SmartScreen.App.Views;

public partial class ScreenshotEditorWindow : Window
{
    private readonly ScreenshotResult _originalScreenshot;

    public ScreenshotEditorWindow(ScreenshotResult screenshot)
    {
        InitializeComponent();
        _originalScreenshot = screenshot;
        ScreenshotImage.Source = BitmapSourceFactory.FromScreenshot(screenshot);
        EditingSurface.Width = screenshot.Width;
        EditingSurface.Height = screenshot.Height;
        ScreenshotImage.Width = screenshot.Width;
        ScreenshotImage.Height = screenshot.Height;
        InkCanvas.Width = screenshot.Width;
        InkCanvas.Height = screenshot.Height;
        SetPen(Colors.Red, 3, false);
    }

    public ScreenshotResult? EditedScreenshot { get; private set; }

    private void PenButton_OnClick(object sender, RoutedEventArgs e) => SetPen(Colors.Red, 3, false);

    private void HighlighterButton_OnClick(object sender, RoutedEventArgs e) => SetPen(MediaColor.FromArgb(120, 255, 214, 10), 18, true);

    private void UndoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (InkCanvas.Strokes.Count > 0)
        {
            InkCanvas.Strokes.RemoveAt(InkCanvas.Strokes.Count - 1);
        }
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e) => InkCanvas.Strokes.Clear();

    private void DoneButton_OnClick(object sender, RoutedEventArgs e)
    {
        EditedScreenshot = RenderEditedScreenshot();
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SetPen(MediaColor color, double width, bool highlighter)
    {
        InkCanvas.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = color,
            Width = width,
            Height = width,
            FitToCurve = true,
            IsHighlighter = highlighter
        };
    }

    private ScreenshotResult RenderEditedScreenshot()
    {
        EditingSurface.Measure(new System.Windows.Size(_originalScreenshot.Width, _originalScreenshot.Height));
        EditingSurface.Arrange(new Rect(0, 0, _originalScreenshot.Width, _originalScreenshot.Height));
        EditingSurface.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            _originalScreenshot.Width,
            _originalScreenshot.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(EditingSurface);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        var now = DateTimeOffset.Now;

        return new ScreenshotResult
        {
            ImageBytes = stream.ToArray(),
            MimeType = "image/png",
            Width = _originalScreenshot.Width,
            Height = _originalScreenshot.Height,
            CreatedAt = now,
            SuggestedFileName = $"screenshot_{now:yyyy-MM-dd_HH-mm-ss}.png",
            SourceName = "Edited screenshot"
        };
    }
}
