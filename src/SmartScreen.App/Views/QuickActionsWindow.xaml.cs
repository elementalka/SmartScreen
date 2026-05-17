using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SmartScreen.App.Services;
using SmartScreen.App.ViewModels;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using ColorConverter = System.Windows.Media.ColorConverter;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MediaColor = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using RectangleShape = System.Windows.Shapes.Rectangle;
using TextBox = System.Windows.Controls.TextBox;

namespace SmartScreen.App.Views;

public partial class QuickActionsWindow : Window
{
    private const double MinimumCropSize = 8;
    private const double MinimumEffectSize = 6;
    private const int BlurRadius = 7;
    private const int PixelateBlockSize = 12;

    private readonly QuickActionsViewModel _viewModel;
    private readonly Stack<IEditorHistoryAction> _undoStack = new();
    private readonly Stack<IEditorHistoryAction> _redoStack = new();
    private Point _startPoint;
    private UIElement? _previewElement;
    private EditorTool _activeTool = EditorTool.Pen;
    private Rect? _cropRect;
    private RectangleShape? _cropOverlay;
    private MediaColor _activeColor = Colors.Red;
    private bool _isEditMode;

    public QuickActionsWindow(QuickActionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += Close;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        Loaded += (_, _) =>
        {
            viewModel.LoadCommand.Execute(null);
            InitializeSurface(viewModel.Screenshot);
        };
    }

    private double ActiveStrokeThickness => StrokeSlider?.Value ?? 4;
    private double ActiveTextSize => TextSizeSlider?.Value ?? 24;

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= Close;
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        base.OnClosed(e);
    }

    private void InitializeSurface(ScreenshotResult screenshot)
    {
        WorkspaceImage.Source = BitmapSourceFactory.FromScreenshot(screenshot);
        EditingSurface.Width = screenshot.Width;
        EditingSurface.Height = screenshot.Height;
        WorkspaceImage.Width = screenshot.Width;
        WorkspaceImage.Height = screenshot.Height;
        AnnotationCanvas.Width = screenshot.Width;
        AnnotationCanvas.Height = screenshot.Height;
        InkCanvas.Width = screenshot.Width;
        InkCanvas.Height = screenshot.Height;
        InkCanvas.StrokeCollected -= InkCanvas_OnStrokeCollected;
        InkCanvas.StrokeCollected += InkCanvas_OnStrokeCollected;
        SetTool(EditorTool.Pen);
        SetEditMode(false);
    }

    private void SetEditMode(bool isEditMode)
    {
        _isEditMode = isEditMode;
        EditorPanel.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateActionPanelVisibility();
        InkCanvas.IsHitTestVisible = isEditMode && (_activeTool is EditorTool.Pen or EditorTool.Highlighter);
        AnnotationCanvas.IsHitTestVisible = isEditMode && _activeTool is not (EditorTool.Pen or EditorTool.Highlighter);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuickActionsViewModel.IsAiPanelOpen) or nameof(QuickActionsViewModel.AiPanelVisibility))
        {
            UpdateActionPanelVisibility();
        }
    }

    private void UpdateActionPanelVisibility()
    {
        ActionPanel.Visibility = !_isEditMode && !_viewModel.IsAiPanelOpen
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void EditOverlayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        SetEditMode(true);
        SetTool(EditorTool.Pen);
    }

    private async void DoneEditButton_OnClick(object sender, RoutedEventArgs e)
    {
        var edited = RenderEditedScreenshot();
        ClearAllAnnotations();
        await _viewModel.ApplyEditedScreenshotAsync(edited);
        InitializeSurface(edited);
    }

    private void CancelEditButton_OnClick(object sender, RoutedEventArgs e)
    {
        ClearAllAnnotations();
        _undoStack.Clear();
        _redoStack.Clear();
        InitializeSurface(_viewModel.Screenshot);
    }

    private void PenButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Pen);
    private void HighlighterButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Highlighter);
    private void LineButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Line);
    private void ArrowButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Arrow);
    private void RectangleButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Rectangle);
    private void EllipseButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Ellipse);
    private void TextButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Text);
    private void CropButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Crop);
    private void BlurButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Blur);
    private void PixelateButton_OnClick(object sender, RoutedEventArgs e) => SetTool(EditorTool.Pixelate);
    private void UndoButton_OnClick(object sender, RoutedEventArgs e) => UndoLastAction();
    private void RedoButton_OnClick(object sender, RoutedEventArgs e) => RedoLastAction();
    private void ClearButton_OnClick(object sender, RoutedEventArgs e) => ClearAnnotations();

    private void ColorButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            _activeColor = (MediaColor)ColorConverter.ConvertFromString(hex);
            SetTool(_activeTool);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!_isEditMode)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }

            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBox)
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
                    DoneEditButton_OnClick(this, new RoutedEventArgs());
                    e.Handled = true;
                    return;
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is TextBox)
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
            case Key.B:
                SetTool(EditorTool.Blur);
                e.Handled = true;
                break;
            case Key.X:
                SetTool(EditorTool.Pixelate);
                e.Handled = true;
                break;
            case Key.Escape:
                CancelEditButton_OnClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private void SetTool(EditorTool tool)
    {
        _activeTool = tool;

        if (tool is EditorTool.Pen or EditorTool.Highlighter)
        {
            InkCanvas.IsHitTestVisible = _isEditMode;
            AnnotationCanvas.IsHitTestVisible = false;
            InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            SetPen(
                tool == EditorTool.Pen ? _activeColor : MediaColor.FromArgb(120, 255, 214, 10),
                tool == EditorTool.Pen ? ActiveStrokeThickness : Math.Max(14, ActiveStrokeThickness * 4),
                tool == EditorTool.Highlighter);
            return;
        }

        InkCanvas.IsHitTestVisible = false;
        AnnotationCanvas.IsHitTestVisible = _isEditMode;
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
        if (!_isEditMode || _activeTool is EditorTool.Pen or EditorTool.Highlighter)
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

        UpdateElement(_previewElement, _startPoint, ClampToCanvas(e.GetPosition(AnnotationCanvas)));
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
                RemoveElement(cropOverlay);
                return;
            }

            ApplyCropSelection(cropRect, cropOverlay);
            return;
        }

        if (_activeTool is EditorTool.Blur or EditorTool.Pixelate)
        {
            var effectRect = CreateNormalizedRect(_startPoint, endPoint);
            RemoveElement(element);

            if (effectRect.Width < MinimumEffectSize || effectRect.Height < MinimumEffectSize)
            {
                return;
            }

            ApplyPrivacyEffect(effectRect, _activeTool);
            return;
        }

        AddHistory(new EditorHistoryAction(
            undo: () => RemoveElement(element),
            redo: () => AddElement(element)));
    }

    private UIElement CreateElement(Point start, Point end) => _activeTool switch
    {
        EditorTool.Line => CreateLine(start, end),
        EditorTool.Arrow => CreateArrow(start, end),
        EditorTool.Rectangle => CreateRectangle(start, end),
        EditorTool.Ellipse => CreateEllipse(start, end),
        EditorTool.Crop => CreateCropRectangle(start, end),
        EditorTool.Blur => CreateEffectRectangle(start, end),
        EditorTool.Pixelate => CreateEffectRectangle(start, end),
        _ => CreateRectangle(start, end)
    };

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

    private Line CreateLine(Point start, Point end) => new()
    {
        X1 = start.X,
        Y1 = start.Y,
        X2 = end.X,
        Y2 = end.Y,
        Stroke = new SolidColorBrush(_activeColor),
        StrokeThickness = ActiveStrokeThickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round
    };

    private Canvas CreateArrow(Point start, Point end)
    {
        var canvas = new Canvas();
        canvas.Children.Add(CreateLine(start, end));
        canvas.Children.Add(new Polygon
        {
            Fill = new SolidColorBrush(_activeColor),
            Stroke = new SolidColorBrush(_activeColor)
        });
        UpdateArrow(canvas, start, end);
        return canvas;
    }

    private RectangleShape CreateRectangle(Point start, Point end)
    {
        var rectangle = new RectangleShape
        {
            Stroke = new SolidColorBrush(_activeColor),
            StrokeThickness = ActiveStrokeThickness,
            Fill = Brushes.Transparent
        };
        PositionShape(rectangle, start, end);
        return rectangle;
    }

    private Ellipse CreateEllipse(Point start, Point end)
    {
        var ellipse = new Ellipse
        {
            Stroke = new SolidColorBrush(_activeColor),
            StrokeThickness = ActiveStrokeThickness,
            Fill = Brushes.Transparent
        };
        PositionShape(ellipse, start, end);
        return ellipse;
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

    private static RectangleShape CreateEffectRectangle(Point start, Point end)
    {
        var rectangle = new RectangleShape
        {
            Stroke = new SolidColorBrush(MediaColor.FromRgb(217, 119, 6)),
            StrokeThickness = 2,
            StrokeDashArray = [5, 3],
            Fill = new SolidColorBrush(MediaColor.FromArgb(38, 217, 119, 6))
        };
        PositionShape(rectangle, start, end);
        return rectangle;
    }

    private void AddTextBox(Point point)
    {
        var textBox = new TextBox
        {
            Text = "Текст",
            FontSize = ActiveTextSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_activeColor),
            Background = new SolidColorBrush(MediaColor.FromArgb(210, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(_activeColor),
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

        head.Points =
        [
            end,
            new Point(end.X - headLength * Math.Cos(angle - headAngle), end.Y - headLength * Math.Sin(angle - headAngle)),
            new Point(end.X - headLength * Math.Cos(angle + headAngle), end.Y - headLength * Math.Sin(angle + headAngle))
        ];
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

    private void ApplyPrivacyEffect(Rect effectRect, EditorTool effectTool)
    {
        var surfaceBitmap = RenderSurfaceBitmap(hideCropOverlay: true);
        var sourceRect = ToPixelRect(effectRect, surfaceBitmap.PixelWidth, surfaceBitmap.PixelHeight);
        var cropped = new CroppedBitmap(surfaceBitmap, sourceRect);
        BitmapSource processed = effectTool == EditorTool.Blur
            ? ApplyBoxBlur(cropped, BlurRadius)
            : ApplyPixelate(cropped, PixelateBlockSize);

        if (processed.CanFreeze)
        {
            processed.Freeze();
        }

        var effectImage = new Image
        {
            Source = processed,
            Width = effectRect.Width,
            Height = effectRect.Height,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true
        };

        Canvas.SetLeft(effectImage, effectRect.Left);
        Canvas.SetTop(effectImage, effectRect.Top);
        AddElement(effectImage);

        AddHistory(new EditorHistoryAction(
            undo: () => RemoveElement(effectImage),
            redo: () => AddElement(effectImage)));
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

    private ScreenshotResult RenderEditedScreenshot()
    {
        var renderedBitmap = RenderSurfaceBitmap(hideCropOverlay: true);
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

    private RenderTargetBitmap RenderSurfaceBitmap(bool hideCropOverlay)
    {
        EditingSurface.Measure(new System.Windows.Size(_viewModel.Screenshot.Width, _viewModel.Screenshot.Height));
        EditingSurface.Arrange(new Rect(0, 0, _viewModel.Screenshot.Width, _viewModel.Screenshot.Height));
        EditingSurface.UpdateLayout();

        var previousCropOverlayVisibility = _cropOverlay?.Visibility;
        if (hideCropOverlay && _cropOverlay is not null)
        {
            _cropOverlay.Visibility = Visibility.Hidden;
            EditingSurface.UpdateLayout();
        }

        var renderedBitmap = new RenderTargetBitmap(
            _viewModel.Screenshot.Width,
            _viewModel.Screenshot.Height,
            96,
            96,
            PixelFormats.Pbgra32);

        try
        {
            renderedBitmap.Render(EditingSurface);
        }
        finally
        {
            if (hideCropOverlay && _cropOverlay is not null && previousCropOverlayVisibility is not null)
            {
                _cropOverlay.Visibility = previousCropOverlayVisibility.Value;
            }
        }

        return renderedBitmap;
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
        var left = Math.Clamp(Math.Min(start.X, end.X), 0, _viewModel.Screenshot.Width);
        var top = Math.Clamp(Math.Min(start.Y, end.Y), 0, _viewModel.Screenshot.Height);
        var right = Math.Clamp(Math.Max(start.X, end.X), 0, _viewModel.Screenshot.Width);
        var bottom = Math.Clamp(Math.Max(start.Y, end.Y), 0, _viewModel.Screenshot.Height);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private Point ClampToCanvas(Point point) => new(
        Math.Clamp(point.X, 0, _viewModel.Screenshot.Width),
        Math.Clamp(point.Y, 0, _viewModel.Screenshot.Height));

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

    private static BitmapSource ApplyPixelate(BitmapSource source, int blockSize)
    {
        var (pixels, width, height, stride) = CopyBgra32Pixels(source);
        var safeBlockSize = Math.Max(2, blockSize);

        for (var y = 0; y < height; y += safeBlockSize)
        {
            for (var x = 0; x < width; x += safeBlockSize)
            {
                var blockWidth = Math.Min(safeBlockSize, width - x);
                var blockHeight = Math.Min(safeBlockSize, height - y);
                AverageBlock(pixels, stride, x, y, blockWidth, blockHeight, out var b, out var g, out var r, out var a);

                for (var blockY = y; blockY < y + blockHeight; blockY++)
                {
                    for (var blockX = x; blockX < x + blockWidth; blockX++)
                    {
                        var offset = blockY * stride + blockX * 4;
                        pixels[offset] = b;
                        pixels[offset + 1] = g;
                        pixels[offset + 2] = r;
                        pixels[offset + 3] = a;
                    }
                }
            }
        }

        return CreateBgra32Bitmap(width, height, pixels, stride);
    }

    private static BitmapSource ApplyBoxBlur(BitmapSource source, int radius)
    {
        var (sourcePixels, width, height, stride) = CopyBgra32Pixels(source);
        var safeRadius = Math.Max(1, radius);
        var horizontal = new byte[sourcePixels.Length];
        var result = new byte[sourcePixels.Length];

        BlurHorizontal(sourcePixels, horizontal, width, height, stride, safeRadius);
        BlurVertical(horizontal, result, width, height, stride, safeRadius);

        return CreateBgra32Bitmap(width, height, result, stride);
    }

    private static void BlurHorizontal(byte[] source, byte[] target, int width, int height, int stride, int radius)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var startX = Math.Max(0, x - radius);
                var endX = Math.Min(width - 1, x + radius);
                var count = endX - startX + 1;
                var sumB = 0;
                var sumG = 0;
                var sumR = 0;
                var sumA = 0;

                for (var sourceX = startX; sourceX <= endX; sourceX++)
                {
                    var sourceOffset = y * stride + sourceX * 4;
                    sumB += source[sourceOffset];
                    sumG += source[sourceOffset + 1];
                    sumR += source[sourceOffset + 2];
                    sumA += source[sourceOffset + 3];
                }

                var targetOffset = y * stride + x * 4;
                target[targetOffset] = (byte)(sumB / count);
                target[targetOffset + 1] = (byte)(sumG / count);
                target[targetOffset + 2] = (byte)(sumR / count);
                target[targetOffset + 3] = (byte)(sumA / count);
            }
        }
    }

    private static void BlurVertical(byte[] source, byte[] target, int width, int height, int stride, int radius)
    {
        for (var y = 0; y < height; y++)
        {
            var startY = Math.Max(0, y - radius);
            var endY = Math.Min(height - 1, y + radius);
            var count = endY - startY + 1;

            for (var x = 0; x < width; x++)
            {
                var sumB = 0;
                var sumG = 0;
                var sumR = 0;
                var sumA = 0;

                for (var sourceY = startY; sourceY <= endY; sourceY++)
                {
                    var sourceOffset = sourceY * stride + x * 4;
                    sumB += source[sourceOffset];
                    sumG += source[sourceOffset + 1];
                    sumR += source[sourceOffset + 2];
                    sumA += source[sourceOffset + 3];
                }

                var targetOffset = y * stride + x * 4;
                target[targetOffset] = (byte)(sumB / count);
                target[targetOffset + 1] = (byte)(sumG / count);
                target[targetOffset + 2] = (byte)(sumR / count);
                target[targetOffset + 3] = (byte)(sumA / count);
            }
        }
    }

    private static void AverageBlock(
        byte[] pixels,
        int stride,
        int x,
        int y,
        int width,
        int height,
        out byte b,
        out byte g,
        out byte r,
        out byte a)
    {
        var count = width * height;
        var sumB = 0L;
        var sumG = 0L;
        var sumR = 0L;
        var sumA = 0L;

        for (var blockY = y; blockY < y + height; blockY++)
        {
            for (var blockX = x; blockX < x + width; blockX++)
            {
                var offset = blockY * stride + blockX * 4;
                sumB += pixels[offset];
                sumG += pixels[offset + 1];
                sumR += pixels[offset + 2];
                sumA += pixels[offset + 3];
            }
        }

        b = (byte)(sumB / count);
        g = (byte)(sumG / count);
        r = (byte)(sumR / count);
        a = (byte)(sumA / count);
    }

    private static (byte[] Pixels, int Width, int Height, int Stride) CopyBgra32Pixels(BitmapSource source)
    {
        BitmapSource normalized = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = normalized.PixelWidth;
        var height = normalized.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        normalized.CopyPixels(pixels, stride, 0);
        return (pixels, width, height, stride);
    }

    private static BitmapSource CreateBgra32Bitmap(int width, int height, byte[] pixels, int stride) =>
        BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

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
