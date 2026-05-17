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
        SetColor("AccentColor", palette.Accent);
        SetGlassBackground(palette);
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
                MediaColor.FromRgb(247, 250, 252));
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
            MediaColor.FromRgb(16, 26, 43));
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
        MediaColor GlassEnd);
}
