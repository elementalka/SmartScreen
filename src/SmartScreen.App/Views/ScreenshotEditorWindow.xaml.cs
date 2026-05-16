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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using RectangleShape = System.Windows.Shapes.Rectangle;
using TextBox = System.Windows.Controls.TextBox;

namespace SmartScreen.App.Views;

public partial class ScreenshotEditorWindow : Window
{
    private const double MinimumCropSize = 8;

    private readonly ScreenshotResult _originalScreenshot;
    private readonly Stack<IEditorHistoryAction> _undoStack = new();
    private readonly Stack<IEditorHistoryAction> _redoStack = new();
    private Point _startPoint;
    private UIElement? _previewElement;
    private EditorTool _activeTool = EditorTool.Pen;
    private Rect? _cropRect;
    private RectangleShape? _cropOverlay;

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

    private void CropButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Crop);

    private void UndoButton_OnClick(object sender, RoutedEventArgs e) => UndoLastAction();

    private void RedoButton_OnClick(object sender, RoutedEventArgs e) => RedoLastAction();

    private void ClearButton_OnClick(object sender, RoutedEventArgs e) => ClearAnnotations();

    private void DoneButton_OnClick(object sender, RoutedEventArgs e) => FinishEditing();

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        {
            switch (e.Key)
            {
                case Key.Z:
                    UndoLastAction();
                    e.Handled = true;
                    return;
                case Key.Y:
                    RedoLastAction();
                    e.Handled = true;
                    return;
                case Key.S:
                    FinishEditing();
                    e.Handled = true;
                    return;
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.None || IsTextInputFocused())
        {
            return;
        }

        switch (e.Key)
        {
            case Key.P:
                SetTool(EditorTool.Pen);
                e.Handled = true;
                break;
            case Key.M:
                SetTool(EditorTool.Highlighter);
                e.Handled = true;
                break;
            case Key.L:
                SetTool(EditorTool.Line);
                e.Handled = true;
                break;
            case Key.A:
                SetTool(EditorTool.Arrow);
                e.Handled = true;
                break;
            case Key.R:
                SetTool(EditorTool.Rectangle);
                e.Handled = true;
                break;
            case Key.E:
                SetTool(EditorTool.Ellipse);
                e.Handled = true;
                break;
            case Key.T:
                SetTool(EditorTool.Text);
                e.Handled = true;
                break;
            case Key.C:
                SetTool(EditorTool.Crop);
                e.Handled = true;
                break;
            case Key.Escape:
                DialogResult = false;
                e.Handled = true;
                break;
        }
    }

    private static bool IsTextInputFocused() => Keyboard.FocusedElement is TextBox;

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
        AddHistory(new EditorHistoryAction(
            undo: () => RemoveStroke(stroke),
            redo: () => AddStroke(stroke)));
    }

    private void AnnotationCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_activeTool is EditorTool.Pen or EditorTool.Highlighter)
        {
            return;
        }

        _startPoint = ClampToCanvas(e.GetPosition(AnnotationCanvas));
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

        var currentPoint = ClampToCanvas(e.GetPosition(AnnotationCanvas));
        UpdateElement(_previewElement, _startPoint, currentPoint);
    }

    private void AnnotationCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_previewElement is null)
        {
            return;
        }

        var element = _previewElement;
        var endPoint = ClampToCanvas(e.GetPosition(AnnotationCanvas));
        _previewElement = null;
        AnnotationCanvas.ReleaseMouseCapture();

        if (_activeTool == EditorTool.Crop && element is RectangleShape cropOverlay)
        {
            var cropRect = CreateNormalizedRect(_startPoint, endPoint);
            if (cropRect.Width < MinimumCropSize || cropRect.Height < MinimumCropSize)
            {
                AnnotationCanvas.Children.Remove(cropOverlay);
                return;
            }

            ApplyCropSelection(cropRect, cropOverlay);
            return;
        }

        AddHistory(new EditorHistoryAction(
            undo: () => RemoveElement(element),
            redo: () => AddElement(element)));
    }

    private UIElement CreateElement(Point start, Point end)
    {
        return _activeTool switch
        {
            EditorTool.Line => CreateLine(start, end),
            EditorTool.Arrow => CreateArrow(start, end),
            EditorTool.Rectangle => CreateRectangle(start, end),
            EditorTool.Ellipse => CreateEllipse(start, end),
            EditorTool.Crop => CreateCropRectangle(start, end),
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

    private static RectangleShape CreateCropRectangle(Point start, Point end)
    {
        var rectangle = new RectangleShape
        {
            Stroke = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235)),
            StrokeThickness = 2,
            StrokeDashArray = [7, 4],
            Fill = new SolidColorBrush(MediaColor.FromArgb(34, 37, 99, 235))
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
        AddElement(textBox);
        textBox.Focus();
        textBox.SelectAll();

        AddHistory(new EditorHistoryAction(
            undo: () => RemoveElement(textBox),
            redo: () => AddElement(textBox)));
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

    private void ApplyCropSelection(Rect cropRect, RectangleShape cropOverlay)
    {
        var previousCropRect = _cropRect;
        var previousCropOverlay = _cropOverlay;

        if (previousCropOverlay is not null)
        {
            RemoveElement(previousCropOverlay);
        }

        AddElement(cropOverlay);
        _cropRect = cropRect;
        _cropOverlay = cropOverlay;

        AddHistory(new EditorHistoryAction(
            undo: () =>
            {
                RemoveElement(cropOverlay);
                if (previousCropOverlay is not null)
                {
                    AddElement(previousCropOverlay);
                }

                _cropRect = previousCropRect;
                _cropOverlay = previousCropOverlay;
            },
            redo: () =>
            {
                if (previousCropOverlay is not null)
                {
                    RemoveElement(previousCropOverlay);
                }

                AddElement(cropOverlay);
                _cropRect = cropRect;
                _cropOverlay = cropOverlay;
            }));
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

    private void AddHistory(IEditorHistoryAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
    }

    private void UndoLastAction()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
    }

    private void RedoLastAction()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
    }

    private void ClearAnnotations()
    {
        if (InkCanvas.Strokes.Count == 0 && AnnotationCanvas.Children.Count == 0 && _cropRect is null)
        {
            return;
        }

        var previousStrokes = CloneStrokes(InkCanvas.Strokes);
        var previousElements = AnnotationCanvas.Children.Cast<UIElement>().ToList();
        var previousCropRect = _cropRect;
        var previousCropOverlay = _cropOverlay;

        ClearAllAnnotations();

        AddHistory(new EditorHistoryAction(
            undo: () =>
            {
                RestoreStrokes(previousStrokes);
                RestoreElements(previousElements);
                _cropRect = previousCropRect;
                _cropOverlay = previousCropOverlay;
            },
            redo: ClearAllAnnotations));
    }

    private void ClearAllAnnotations()
    {
        InkCanvas.Strokes.Clear();
        AnnotationCanvas.Children.Clear();
        _cropRect = null;
        _cropOverlay = null;
    }

    private void FinishEditing()
    {
        EditedScreenshot = RenderEditedScreenshot();
        DialogResult = true;
    }

    private ScreenshotResult RenderEditedScreenshot()
    {
        EditingSurface.Measure(new System.Windows.Size(_originalScreenshot.Width, _originalScreenshot.Height));
        EditingSurface.Arrange(new Rect(0, 0, _originalScreenshot.Width, _originalScreenshot.Height));
        EditingSurface.UpdateLayout();

        var previousCropOverlayVisibility = _cropOverlay?.Visibility;
        if (_cropOverlay is not null)
        {
            _cropOverlay.Visibility = Visibility.Hidden;
            EditingSurface.UpdateLayout();
        }

        var renderedBitmap = new RenderTargetBitmap(
            _originalScreenshot.Width,
            _originalScreenshot.Height,
            96,
            96,
            PixelFormats.Pbgra32);

        try
        {
            renderedBitmap.Render(EditingSurface);
        }
        finally
        {
            if (_cropOverlay is not null && previousCropOverlayVisibility is not null)
            {
                _cropOverlay.Visibility = previousCropOverlayVisibility.Value;
            }
        }

        var finalBitmap = ApplyCropIfNeeded(renderedBitmap);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(finalBitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        var now = DateTimeOffset.Now;

        return new ScreenshotResult
        {
            ImageBytes = stream.ToArray(),
            MimeType = "image/png",
            Width = finalBitmap.PixelWidth,
            Height = finalBitmap.PixelHeight,
            CreatedAt = now,
            SuggestedFileName = $"screenshot_{now:yyyy-MM-dd_HH-mm-ss}.png",
            SourceName = _cropRect is null ? "Edited screenshot" : "Edited cropped screenshot"
        };
    }

    private BitmapSource ApplyCropIfNeeded(BitmapSource source)
    {
        if (_cropRect is null || _cropRect.Value.Width < MinimumCropSize || _cropRect.Value.Height < MinimumCropSize)
        {
            return source;
        }

        return new CroppedBitmap(source, ToPixelRect(_cropRect.Value, source.PixelWidth, source.PixelHeight));
    }

    private Int32Rect ToPixelRect(Rect rect, int sourceWidth, int sourceHeight)
    {
        var left = (int)Math.Clamp(Math.Floor(rect.Left), 0, sourceWidth - 1);
        var top = (int)Math.Clamp(Math.Floor(rect.Top), 0, sourceHeight - 1);
        var right = (int)Math.Clamp(Math.Ceiling(rect.Right), left + 1, sourceWidth);
        var bottom = (int)Math.Clamp(Math.Ceiling(rect.Bottom), top + 1, sourceHeight);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private Rect CreateNormalizedRect(Point start, Point end)
    {
        var left = Math.Clamp(Math.Min(start.X, end.X), 0, _originalScreenshot.Width);
        var top = Math.Clamp(Math.Min(start.Y, end.Y), 0, _originalScreenshot.Height);
        var right = Math.Clamp(Math.Max(start.X, end.X), 0, _originalScreenshot.Width);
        var bottom = Math.Clamp(Math.Max(start.Y, end.Y), 0, _originalScreenshot.Height);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private Point ClampToCanvas(Point point) => new(
        Math.Clamp(point.X, 0, _originalScreenshot.Width),
        Math.Clamp(point.Y, 0, _originalScreenshot.Height));

    private void AddElement(UIElement element)
    {
        if (!AnnotationCanvas.Children.Contains(element))
        {
            AnnotationCanvas.Children.Add(element);
        }
    }

    private void RemoveElement(UIElement element)
    {
        if (AnnotationCanvas.Children.Contains(element))
        {
            AnnotationCanvas.Children.Remove(element);
        }
    }

    private void AddStroke(Stroke stroke)
    {
        if (!InkCanvas.Strokes.Contains(stroke))
        {
            InkCanvas.Strokes.Add(stroke);
        }
    }

    private void RemoveStroke(Stroke stroke)
    {
        if (InkCanvas.Strokes.Contains(stroke))
        {
            InkCanvas.Strokes.Remove(stroke);
        }
    }

    private static StrokeCollection CloneStrokes(StrokeCollection strokes)
    {
        var clone = new StrokeCollection();

        foreach (var stroke in strokes)
        {
            clone.Add(stroke.Clone());
        }

        return clone;
    }

    private void RestoreStrokes(StrokeCollection strokes)
    {
        InkCanvas.Strokes.Clear();

        foreach (var stroke in strokes)
        {
            InkCanvas.Strokes.Add(stroke.Clone());
        }
    }

    private void RestoreElements(IReadOnlyCollection<UIElement> elements)
    {
        AnnotationCanvas.Children.Clear();

        foreach (var element in elements)
        {
            AnnotationCanvas.Children.Add(element);
        }
    }

    private interface IEditorHistoryAction
    {
        void Undo();
        void Redo();
    }

    private sealed class EditorHistoryAction(Action undo, Action redo) : IEditorHistoryAction
    {
        public void Undo() => undo();
        public void Redo() => redo();
    }
}
