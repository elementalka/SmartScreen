using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SmartScreen.Domain.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace SmartScreen.App.Views;

public partial class ScreenshotOverlayWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;

    public ScreenshotOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public ScreenRegion? SelectedRegion { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Canvas.SetLeft(HintPanel, Math.Max(28, (Width - HintPanel.ActualWidth) / 2));
        OverlayCanvas.Focus();
    }

    private void OverlayCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isSelecting = true;
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _startPoint.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);
        var x = Math.Min(current.X, _startPoint.X);
        var y = Math.Min(current.Y, _startPoint.Y);
        var width = Math.Abs(current.X - _startPoint.X);
        var height = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;

        SizeBadge.Visibility = Visibility.Visible;
        SizeBadgeText.Text = $"{Math.Round(width)} × {Math.Round(height)}";
        Canvas.SetLeft(SizeBadge, x);
        Canvas.SetTop(SizeBadge, Math.Max(8, y - 34));
    }

    private void OverlayCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        OverlayCanvas.ReleaseMouseCapture();

        var x = Canvas.GetLeft(SelectionRectangle);
        var y = Canvas.GetTop(SelectionRectangle);
        var width = SelectionRectangle.Width;
        var height = SelectionRectangle.Height;

        if (width < 4 || height < 4)
        {
            DialogResult = false;
            return;
        }

        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        SelectedRegion = new ScreenRegion(
            (int)Math.Round((Left + x) * transform.M11),
            (int)Math.Round((Top + y) * transform.M22),
            (int)Math.Round(width * transform.M11),
            (int)Math.Round(height * transform.M22));

        DialogResult = true;
    }

    private void OverlayCanvas_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
        }
    }
}
