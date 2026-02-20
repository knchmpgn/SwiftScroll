using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SwiftScroll;

/// <summary>
/// Helper class to detect Windows dark/light mode and provide theme colors.
/// </summary>
public static class ThemeHelper
{
    /// <summary>
    /// Returns true if Windows is in dark mode, false if light mode.
    /// </summary>
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 0; // 0 = dark mode, 1 = light mode
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThemeHelper] Error detecting theme: {ex.Message}");
        }
        return true; // Default to dark mode
    }

    // Dark mode colors (black/white/gray palette)
    public static class Dark
    {
        public const string Background = "#0F1115";
        public const string Surface = "#16191F";
        public const string SurfaceBorder = "#202533";
        public const string Text = "#F5F6FA";
        public const string TextSecondary = "#B6BCC8";
        public const string Accent = "#5CC8FF";
        public const string AccentHover = "#7AD6FF";
        public const string Input = "#141820";
        public const string InputBorder = "#262B38";
    }

    // Light mode colors (black/white/gray palette)
    public static class Light
    {
        public const string Background = "#F7F8FB";
        public const string Surface = "#FFFFFF";
        public const string SurfaceBorder = "#E0E4EC";
        public const string Text = "#0F1115";
        public const string TextSecondary = "#5B6474";
        public const string Accent = "#0078D4";
        public const string AccentHover = "#2B88FF";
        public const string Input = "#FFFFFF";
        public const string InputBorder = "#CBD2DC";
    }
}

