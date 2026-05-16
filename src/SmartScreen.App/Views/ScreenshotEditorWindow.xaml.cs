using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SmartScreen.App.Services;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;
using Brushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using RectangleShape = System.Windows.Shapes.Rectangle;
using TextBox = System.Windows.Controls.TextBox;

namespace SmartScreen.App.Views;

public partial class ScreenshotEditorWindow : Window
{
    private readonly ScreenshotResult _originalScreenshot;
    private readonly Stack<Action> _undoStack = new();
    private Point _startPoint;
    private UIElement? _previewElement;
    private EditorTool _activeTool = EditorTool.Pen;

    public ScreenshotEditorWindow(ScreenshotResult screenshot)
    {
        InitializeComponent();
        _originalScreenshot = screenshot;
        ScreenshotImage.Source = BitmapSourceFactory.FromScreenshot(screenshot);
        EditingSurface.Width = screenshot.Width;
        EditingSurface.Height = screenshot.Height;
        ScreenshotImage.Width = screenshot.Width;
        ScreenshotImage.Height = screenshot.Height;
        AnnotationCanvas.Width = screenshot.Width;
        AnnotationCanvas.Height = screenshot.Height;
        InkCanvas.Width = screenshot.Width;
        InkCanvas.Height = screenshot.Height;
        InkCanvas.StrokeCollected += InkCanvas_OnStrokeCollected;
        SetTool(EditorTool.Pen);
    }

    public ScreenshotResult? EditedScreenshot { get; private set; }

    private void PenButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Pen);

    private void HighlighterButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Highlighter);

    private void LineButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Line);

    private void ArrowButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Arrow);

    private void RectangleButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Rectangle);

    private void EllipseButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Ellipse);

    private void TextButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Text);

    private void UndoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 0)
        {
            _undoStack.Pop().Invoke();
        }
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        InkCanvas.Strokes.Clear();
        AnnotationCanvas.Children.Clear();
        _undoStack.Clear();
    }

    private void DoneButton_OnClick(object sender, RoutedEventArgs e)
    {
        EditedScreenshot = RenderEditedScreenshot();
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SetTool(EditorTool tool)
    {
        _activeTool = tool;

        if (tool is EditorTool.Pen or EditorTool.Highlighter)
        {
            InkCanvas.IsHitTestVisible = true;
            AnnotationCanvas.IsHitTestVisible = false;
            InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            SetPen(
                tool == EditorTool.Pen ? Colors.Red : MediaColor.FromArgb(120, 255, 214, 10),
                tool == EditorTool.Pen ? 3 : 18,
                tool == EditorTool.Highlighter);
            return;
        }

        InkCanvas.IsHitTestVisible = false;
        AnnotationCanvas.IsHitTestVisible = true;
        InkCanvas.EditingMode = InkCanvasEditingMode.None;
    }

    private void InkCanvas_OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        var stroke = e.Stroke;
        _undoStack.Push(() => InkCanvas.Strokes.Remove(stroke));
    }

    private void AnnotationCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_activeTool is EditorTool.Pen or EditorTool.Highlighter)
        {
            return;
        }

        _startPoint = e.GetPosition(AnnotationCanvas);
        AnnotationCanvas.CaptureMouse();

        if (_activeTool == EditorTool.Text)
        {
            AddTextBox(_startPoint);
            AnnotationCanvas.ReleaseMouseCapture();
            return;
        }

        _previewElement = CreateElement(_startPoint, _startPoint);
        AnnotationCanvas.Children.Add(_previewElement);
    }

    private void AnnotationCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_previewElement is null || !AnnotationCanvas.IsMouseCaptured)
        {
            return;
        }

        var currentPoint = e.GetPosition(AnnotationCanvas);
        UpdateElement(_previewElement, _startPoint, currentPoint);
    }

    private void AnnotationCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_previewElement is null)
        {
            return;
        }

        var element = _previewElement;
        _previewElement = null;
        AnnotationCanvas.ReleaseMouseCapture();
        _undoStack.Push(() => AnnotationCanvas.Children.Remove(element));
    }

    private UIElement CreateElement(Point start, Point end)
    {
        return _activeTool switch
        {
            EditorTool.Line => CreateLine(start, end),
            EditorTool.Arrow => CreateArrow(start, end),
            EditorTool.Rectangle => CreateRectangle(start, end),
            EditorTool.Ellipse => CreateEllipse(start, end),
            _ => CreateRectangle(start, end)
        };
    }

    private void UpdateElement(UIElement element, Point start, Point end)
    {
        switch (element)
        {
            case Line line:
                line.X1 = start.X;
                line.Y1 = start.Y;
                line.X2 = end.X;
                line.Y2 = end.Y;
                break;
            case Canvas arrow:
                UpdateArrow(arrow, start, end);
                break;
            case Shape shape:
                PositionShape(shape, start, end);
                break;
        }
    }

    private static Line CreateLine(Point start, Point end) => new()
    {
        X1 = start.X,
        Y1 = start.Y,
        X2 = end.X,
        Y2 = end.Y,
        Stroke = Brushes.Red,
        StrokeThickness = 3,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round
    };

    private static Canvas CreateArrow(Point start, Point end)
    {
        var canvas = new Canvas();
        canvas.Children.Add(CreateLine(start, end));
        canvas.Children.Add(new Polygon
        {
            Fill = Brushes.Red,
            Stroke = Brushes.Red
        });
        UpdateArrow(canvas, start, end);
        return canvas;
    }

    private static RectangleShape CreateRectangle(Point start, Point end)
    {
        var rectangle = new RectangleShape
        {
            Stroke = Brushes.Red,
            StrokeThickness = 3,
            Fill = Brushes.Transparent
        };
        PositionShape(rectangle, start, end);
        return rectangle;
    }

    private static Ellipse CreateEllipse(Point start, Point end)
    {
        var ellipse = new Ellipse
        {
            Stroke = Brushes.Red,
            StrokeThickness = 3,
            Fill = Brushes.Transparent
        };
        PositionShape(ellipse, start, end);
        return ellipse;
    }

    private void AddTextBox(Point point)
    {
        var textBox = new TextBox
        {
            Text = "Текст",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Red,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Red,
            BorderThickness = new Thickness(1),
            MinWidth = 120,
            Padding = new Thickness(4)
        };

        Canvas.SetLeft(textBox, point.X);
        Canvas.SetTop(textBox, point.Y);
        AnnotationCanvas.Children.Add(textBox);
        textBox.Focus();
        textBox.SelectAll();
        _undoStack.Push(() => AnnotationCanvas.Children.Remove(textBox));
    }

    private static void PositionShape(Shape shape, Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        shape.Width = Math.Abs(end.X - start.X);
        shape.Height = Math.Abs(end.Y - start.Y);
        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
    }

    private static void UpdateArrow(Canvas arrow, Point start, Point end)
    {
        if (arrow.Children[0] is Line line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
        }

        if (arrow.Children[1] is not Polygon head)
        {
            return;
        }

        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        const double headLength = 16;
        const double headAngle = Math.PI / 7;

        var point1 = new Point(
            end.X - headLength * Math.Cos(angle - headAngle),
            end.Y - headLength * Math.Sin(angle - headAngle));
        var point2 = new Point(
            end.X - headLength * Math.Cos(angle + headAngle),
            end.Y - headLength * Math.Sin(angle + headAngle));

        head.Points = [end, point1, point2];
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
