using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SmartScreen.Domain.Models;
using DomainThemeMode = SmartScreen.Domain.Enums.ThemeMode;
using MediaColor = System.Windows.Media.Color;

namespace SmartScreen.App.Services;

public static class ThemeResourceService
{
    public static void Apply(ThemeSettings settings)
    {
        var mode = settings.Mode == DomainThemeMode.System ? ResolveSystemTheme() : settings.Mode;
        var accent = TryParseColor(settings.AccentColor, out var parsedAccent)
            ? parsedAccent
            : MediaColor.FromRgb(56, 189, 248);
        var palette = CreatePalette(mode, accent);

        SetBrush("AppBackgroundBrush", palette.AppBackground);
        SetBrush("SurfaceBrush", palette.Surface);
        SetBrush("PanelBrush", palette.Panel);
        SetBrush("TextBrush", palette.Text);
        SetBrush("MutedTextBrush", palette.MutedText);
        SetBrush("AccentBrush", palette.Accent);
        SetBrush("AccentSoftBrush", palette.AccentSoft);
        SetBrush("BorderBrush", palette.Border);
        SetBrush("GlassPanelBrush", palette.GlassPanel);
        SetBrush("GlassPanelStrongBrush", palette.GlassPanelStrong);
        SetBrush("GlassPanelSoftBrush", palette.GlassPanelSoft);
        SetBrush("RailBrush", palette.Rail);
        SetBrush("ControlBrush", palette.Control);
        SetBrush("ControlHoverBrush", palette.ControlHover);
        SetBrush("ControlSelectedBrush", palette.ControlSelected);
        SetBrush("PopupBrush", palette.Popup);
        SetBrush("GlassBorderBrush", palette.GlassBorder);
        SetBrush("PopupBorderBrush", palette.PopupBorder);
        SetBrush("HoverBorderBrush", palette.HoverBorder);
        SetBrush("FocusBorderBrush", palette.FocusBorder);
        SetBrush("SuccessSoftBrush", palette.SuccessSoft);
        SetBrush("SuccessBorderBrush", palette.SuccessBorder);
        SetBrush("WarningSoftBrush", palette.WarningSoft);
        SetBrush("WarningBorderBrush", palette.WarningBorder);
        SetBrush("ThumbnailBrush", palette.Thumbnail);
        SetColor("AccentColor", palette.Accent);
        SetGlassBackground(palette);
        SetSpecialSurfaceBrushes(mode, palette);
    }

    private static DomainThemeMode ResolveSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue > 0 ? DomainThemeMode.Light : DomainThemeMode.Dark;
        }
        catch
        {
            return DomainThemeMode.Dark;
        }
    }

    private static ThemePalette CreatePalette(DomainThemeMode mode, MediaColor accent)
    {
        if (mode == DomainThemeMode.Light)
        {
            return new ThemePalette(
                MediaColor.FromRgb(245, 247, 251),
                MediaColor.FromRgb(255, 255, 255),
                MediaColor.FromRgb(239, 244, 250),
                MediaColor.FromRgb(17, 24, 39),
                MediaColor.FromRgb(82, 97, 115),
                accent,
                MediaColor.FromArgb(42, accent.R, accent.G, accent.B),
                MediaColor.FromRgb(203, 213, 225),
                MediaColor.FromRgb(239, 244, 250),
                MediaColor.FromRgb(226, 233, 243),
                MediaColor.FromRgb(247, 250, 252),
                MediaColor.FromArgb(236, 255, 255, 255),
                MediaColor.FromArgb(246, 255, 255, 255),
                MediaColor.FromArgb(202, 233, 240, 249),
                MediaColor.FromArgb(246, 239, 244, 250),
                MediaColor.FromArgb(224, 232, 239, 248),
                MediaColor.FromArgb(244, 222, 235, 250),
                MediaColor.FromArgb(78, accent.R, accent.G, accent.B),
                MediaColor.FromArgb(250, 255, 255, 255),
                MediaColor.FromRgb(203, 213, 225),
                MediaColor.FromRgb(148, 163, 184),
                MediaColor.FromArgb(206, accent.R, accent.G, accent.B),
                accent,
                MediaColor.FromArgb(44, 16, 185, 129),
                MediaColor.FromArgb(132, 16, 185, 129),
                MediaColor.FromArgb(42, 245, 158, 11),
                MediaColor.FromArgb(120, 245, 158, 11),
                MediaColor.FromRgb(226, 232, 240));
        }

        return new ThemePalette(
            MediaColor.FromRgb(7, 17, 31),
            MediaColor.FromRgb(16, 24, 39),
            MediaColor.FromRgb(20, 28, 46),
            MediaColor.FromRgb(248, 250, 252),
            MediaColor.FromRgb(170, 184, 204),
            accent,
            MediaColor.FromArgb(58, accent.R, accent.G, accent.B),
            MediaColor.FromRgb(51, 65, 85),
            MediaColor.FromRgb(7, 17, 31),
            MediaColor.FromRgb(14, 26, 45),
            MediaColor.FromRgb(16, 26, 43),
            MediaColor.FromArgb(214, 18, 27, 45),
            MediaColor.FromArgb(224, 16, 23, 42),
            MediaColor.FromArgb(135, 24, 38, 58),
            MediaColor.FromArgb(240, 11, 18, 32),
            MediaColor.FromArgb(184, 24, 38, 58),
            MediaColor.FromArgb(210, 34, 51, 74),
            MediaColor.FromArgb(194, 25, 50, 77),
            MediaColor.FromArgb(240, 17, 26, 45),
            MediaColor.FromArgb(82, 109, 131, 166),
            MediaColor.FromArgb(112, 142, 163, 197),
            MediaColor.FromArgb(109, 166, 199, 243),
            MediaColor.FromRgb(125, 211, 252),
            MediaColor.FromArgb(38, 52, 211, 153),
            MediaColor.FromArgb(102, 52, 211, 153),
            MediaColor.FromArgb(44, 245, 158, 11),
            MediaColor.FromArgb(102, 245, 158, 11),
            MediaColor.FromRgb(30, 41, 59));
    }

    private static bool TryParseColor(string value, out MediaColor color)
    {
        try
        {
            color = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(value);
            return true;
        }
        catch (FormatException)
        {
            color = default;
            return false;
        }
    }

    private static void SetBrush(string key, MediaColor color)
    {
        if (TryFindResource(key, out SolidColorBrush? brush) && brush is { IsFrozen: false })
        {
            brush.Color = color;
            return;
        }

        SetResource(key, new SolidColorBrush(color));
    }

    private static void SetColor(string key, MediaColor color) => SetResource(key, color);

    private static void SetGlassBackground(ThemePalette palette)
    {
        if (!TryFindResource("GlassWindowBackgroundBrush", out LinearGradientBrush? brush) || brush is null || brush.IsFrozen)
        {
            SetResource("GlassWindowBackgroundBrush", new LinearGradientBrush(
                palette.GlassStart,
                palette.GlassEnd,
                45));
            return;
        }

        EnsureGradientStopCount(brush, 3);
        brush.GradientStops[0].Color = palette.GlassStart;
        brush.GradientStops[1].Color = palette.GlassMiddle;
        brush.GradientStops[2].Color = palette.GlassEnd;
    }

    private static void SetSpecialSurfaceBrushes(DomainThemeMode mode, ThemePalette palette)
    {
        if (mode == DomainThemeMode.Light)
        {
            SetBrush("QuickWorkspaceScrimBrush", MediaColor.FromArgb(86, 248, 250, 252));
            SetBrush("OverlayScrimBrush", MediaColor.FromArgb(42, 0, 0, 0));
            SetBrush("OverlayHintBrush", MediaColor.FromArgb(245, 255, 255, 255));
            SetBrush("OverlayHintBorderBrush", MediaColor.FromArgb(170, 203, 213, 225));
            SetBrush("OverlayHintTextBrush", MediaColor.FromRgb(17, 24, 39));
            SetBrush("OverlayHintMutedTextBrush", MediaColor.FromRgb(82, 97, 115));
            SetBrush("OverlaySelectionStrokeBrush", palette.Accent);
            SetBrush("OverlaySelectionFillBrush", MediaColor.FromArgb(56, palette.Accent.R, palette.Accent.G, palette.Accent.B));
            SetBrush("EditorToolButtonBrush", MediaColor.FromArgb(238, 239, 244, 250));
            SetBrush("EditorToolButtonBorderBrush", MediaColor.FromRgb(203, 213, 225));
            SetBrush("EditorToolButtonForegroundBrush", MediaColor.FromRgb(17, 24, 39));
            SetBrush("EditorToolButtonActiveBorderBrush", palette.FocusBorder);
            SetBrush("EditorToolButtonActiveForegroundBrush", MediaColor.FromRgb(7, 17, 31));
            SetBrush("EditorCommitButtonBrush", palette.Accent);
            SetBrush("EditorCommitButtonBorderBrush", palette.FocusBorder);
            SetBrush("EditorCancelButtonBrush", MediaColor.FromArgb(232, 226, 233, 243));
            SetBrush("EditorCancelButtonBorderBrush", MediaColor.FromRgb(203, 213, 225));
            SetBrush("EditorCancelButtonForegroundBrush", MediaColor.FromRgb(31, 41, 55));
            SetBrush("EditorDividerBrush", MediaColor.FromRgb(203, 213, 225));
            SetBrush("EditorOptionLabelBrush", palette.MutedText);
            SetBrush("EditorSwatchBorderBrush", MediaColor.FromRgb(148, 163, 184));
            SetBrush("EditorSwatchActiveBorderBrush", MediaColor.FromRgb(17, 24, 39));
            SetBrush("EditorCropStrokeBrush", palette.Accent);
            SetBrush("EditorCropFillBrush", MediaColor.FromArgb(38, palette.Accent.R, palette.Accent.G, palette.Accent.B));
            SetBrush("EditorEffectStrokeBrush", MediaColor.FromRgb(217, 119, 6));
            SetBrush("EditorEffectFillBrush", MediaColor.FromArgb(34, 217, 119, 6));
            SetBrush("EditorTextInputBackgroundBrush", MediaColor.FromArgb(234, 255, 255, 255));
            return;
        }

        SetBrush("QuickWorkspaceScrimBrush", MediaColor.FromArgb(150, 11, 18, 32));
        SetBrush("OverlayScrimBrush", MediaColor.FromArgb(51, 0, 0, 0));
        SetBrush("OverlayHintBrush", MediaColor.FromArgb(238, 17, 26, 45));
        SetBrush("OverlayHintBorderBrush", MediaColor.FromArgb(135, 109, 131, 166));
        SetBrush("OverlayHintTextBrush", palette.Text);
        SetBrush("OverlayHintMutedTextBrush", palette.MutedText);
        SetBrush("OverlaySelectionStrokeBrush", palette.Accent);
        SetBrush("OverlaySelectionFillBrush", MediaColor.FromArgb(54, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        SetBrush("EditorToolButtonBrush", MediaColor.FromRgb(31, 41, 55));
        SetBrush("EditorToolButtonBorderBrush", MediaColor.FromRgb(56, 71, 95));
        SetBrush("EditorToolButtonForegroundBrush", MediaColor.FromRgb(248, 250, 252));
        SetBrush("EditorToolButtonActiveBorderBrush", MediaColor.FromRgb(134, 165, 255));
        SetBrush("EditorToolButtonActiveForegroundBrush", MediaColor.FromRgb(7, 17, 31));
        SetBrush("EditorCommitButtonBrush", palette.Accent);
        SetBrush("EditorCommitButtonBorderBrush", palette.FocusBorder);
        SetBrush("EditorCancelButtonBrush", MediaColor.FromRgb(42, 52, 70));
        SetBrush("EditorCancelButtonBorderBrush", MediaColor.FromRgb(74, 92, 118));
        SetBrush("EditorCancelButtonForegroundBrush", MediaColor.FromRgb(220, 229, 242));
        SetBrush("EditorDividerBrush", MediaColor.FromRgb(75, 92, 118));
        SetBrush("EditorOptionLabelBrush", MediaColor.FromRgb(203, 213, 225));
        SetBrush("EditorSwatchBorderBrush", MediaColor.FromRgb(124, 147, 180));
        SetBrush("EditorSwatchActiveBorderBrush", MediaColor.FromRgb(255, 255, 255));
        SetBrush("EditorCropStrokeBrush", palette.Accent);
        SetBrush("EditorCropFillBrush", MediaColor.FromArgb(34, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        SetBrush("EditorEffectStrokeBrush", MediaColor.FromRgb(217, 119, 6));
        SetBrush("EditorEffectFillBrush", MediaColor.FromArgb(38, 217, 119, 6));
        SetBrush("EditorTextInputBackgroundBrush", MediaColor.FromArgb(218, 255, 255, 255));
    }

    private static void EnsureGradientStopCount(LinearGradientBrush brush, int count)
    {
        while (brush.GradientStops.Count < count)
        {
            brush.GradientStops.Add(new GradientStop());
        }
    }

    private static bool TryFindResource<T>(string key, out T? value) where T : class
    {
        if (System.Windows.Application.Current?.Resources is null)
        {
            value = null;
            return false;
        }

        var found = TryFindResource(System.Windows.Application.Current.Resources, key, out var resource);
        value = found ? resource as T : null;
        return value is not null;
    }

    private static bool TryFindResource(ResourceDictionary dictionary, string key, out object? value)
    {
        if (dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            if (TryFindResource(mergedDictionary, key, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static void SetResource(string key, object value)
    {
        if (System.Windows.Application.Current?.Resources is null)
        {
            return;
        }

        if (TrySetResource(System.Windows.Application.Current.Resources, key, value))
        {
            return;
        }

        System.Windows.Application.Current.Resources[key] = value;
    }

    private static bool TrySetResource(ResourceDictionary dictionary, string key, object value)
    {
        if (dictionary.Contains(key))
        {
            dictionary[key] = value;
            return true;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            if (TrySetResource(mergedDictionary, key, value))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ThemePalette(
        MediaColor AppBackground,
        MediaColor Surface,
        MediaColor Panel,
        MediaColor Text,
        MediaColor MutedText,
        MediaColor Accent,
        MediaColor AccentSoft,
        MediaColor Border,
        MediaColor GlassStart,
        MediaColor GlassMiddle,
        MediaColor GlassEnd,
        MediaColor GlassPanel,
        MediaColor GlassPanelStrong,
        MediaColor GlassPanelSoft,
        MediaColor Rail,
        MediaColor Control,
        MediaColor ControlHover,
        MediaColor ControlSelected,
        MediaColor Popup,
        MediaColor GlassBorder,
        MediaColor PopupBorder,
        MediaColor HoverBorder,
        MediaColor FocusBorder,
        MediaColor SuccessSoft,
        MediaColor SuccessBorder,
        MediaColor WarningSoft,
        MediaColor WarningBorder,
        MediaColor Thumbnail);
}
