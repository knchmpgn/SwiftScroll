using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftScroll;

public sealed class ScrollProfile
{
    public string Name { get; set; } = "Default";
    public int StepSizePx { get; set; } = 12;
    public int AnimationTimeMs { get; set; } = 250;
    public int AccelerationDeltaMs { get; set; } = 60;
    public int AccelerationMax { get; set; } = 6;
    public int TailToHeadRatio { get; set; } = 2;
    public bool AnimationEasing { get; set; } = true;
    public bool HorizontalSmoothness { get; set; } = true;
    public bool ReverseWheelDirection { get; set; } = false;

    public ScrollProfile Clone() => new()
    {
        Name = Name,
        StepSizePx = StepSizePx,
        AnimationTimeMs = AnimationTimeMs,
        AccelerationDeltaMs = AccelerationDeltaMs,
        AccelerationMax = AccelerationMax,
        TailToHeadRatio = TailToHeadRatio,
        AnimationEasing = AnimationEasing,
        HorizontalSmoothness = HorizontalSmoothness,
        ReverseWheelDirection = ReverseWheelDirection
    };

    public static ScrollProfile CreateDefault() => ApplyWindowsClassic(new ScrollProfile());

    public static ScrollProfile ApplyWindowsClassic(ScrollProfile profile)
    {
        profile.StepSizePx = 12;
        profile.AnimationTimeMs = 250;
        profile.AccelerationDeltaMs = 60;
        profile.AccelerationMax = 6;
        profile.TailToHeadRatio = 2;
        profile.AnimationEasing = true;
        profile.HorizontalSmoothness = true;
        profile.ReverseWheelDirection = false;
        return profile;
    }

    public static void ApplyWindowsClassic(AppSettings settings)
    {
        settings.StepSizePx = 12;
        settings.AnimationTimeMs = 250;
        settings.AccelerationDeltaMs = 60;
        settings.AccelerationMax = 6;
        settings.TailToHeadRatio = 2;
        settings.AnimationEasing = true;
        settings.HorizontalSmoothness = true;
        settings.ReverseWheelDirection = false;

        if (settings.Profiles.Count == 0)
            settings.Profiles.Add(CreateDefault());
        settings.Profiles[0] = ApplyWindowsClassic(settings.Profiles[0]);
    }
}

public sealed class ApplicationProfile
{
    public string AppName { get; set; } = "";
    public string ProfileName { get; set; } = "Default";
}

public sealed class AppSettings
{
    // Master enable
    public bool Enabled { get; set; } = true;

    // Global scroll parameters
    public int StepSizePx { get; set; } = 12;
    public int AnimationTimeMs { get; set; } = 250;
    public int AccelerationDeltaMs { get; set; } = 60;
    public int AccelerationMax { get; set; } = 6;
    public int TailToHeadRatio { get; set; } = 2;

    // Global toggles
    public bool AnimationEasing { get; set; } = true;
    public bool ShiftKeyHorizontal { get; set; } = true;
    public bool HorizontalSmoothness { get; set; } = true;
    public bool ReverseWheelDirection { get; set; } = false;

    public bool StartWithWindows { get; set; } = false;

    // Per-app exclusion list (apps where Swift Scroll is disabled entirely)
    public List<string> ExcludedApps { get; set; } = new();

    // Per-app profiles
    [JsonInclude]
    public List<ScrollProfile> Profiles { get; set; } = new() { ScrollProfile.CreateDefault() };

    [JsonInclude]
    public List<ApplicationProfile> AppProfiles { get; set; } = new();

    public static AppSettings CreateDefault() => new();

    public static string GetConfigPath()
    {
        // Portable mode: store settings beside the executable so they follow the app.
        var exeDir = AppContext.BaseDirectory;
        Directory.CreateDirectory(exeDir);
        return Path.Combine(exeDir, "settings.json");
    }

    public static bool SettingsFileExists()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "settings.json");
        return File.Exists(path);
    }

    private static string GetLegacyPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwiftScroll");
        return Path.Combine(dir, "settings.json");
    }

    public static AppSettings Load()
    {
        try
        {
            var path = GetConfigPath();

            // Migrate legacy settings (AppData) to portable location on first run.
            if (!File.Exists(path))
            {
                var legacy = GetLegacyPath();
                if (File.Exists(legacy))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(legacy, path, overwrite: true);
                }
            }

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s != null)
                {
                    // Ensure default profile exists
                    if (s.Profiles.Count == 0)
                        s.Profiles.Add(ScrollProfile.CreateDefault());
                    return s;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppSettings] Failed to load settings: {ex.Message}");
        }
        return CreateDefault();
    }

    public void Save()
    {
        try
        {
            var path = GetConfigPath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppSettings] Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the given process name is in the exclusion list (case-insensitive).
    /// </summary>
    public bool IsExcluded(string? processName)
    {
        if (string.IsNullOrEmpty(processName)) return false;
        return ExcludedApps.Exists(app => string.Equals(app, processName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the profile name for a given app, or null if no profile is assigned.
    /// </summary>
    public string? GetProfileForApp(string? appName)
    {
        if (string.IsNullOrEmpty(appName)) return null;
        var appProfile = AppProfiles.Find(ap => string.Equals(ap.AppName, appName, StringComparison.OrdinalIgnoreCase));
        return appProfile?.ProfileName;
    }

    /// <summary>
    /// Gets a profile by name, or null if not found.
    /// </summary>
    public ScrollProfile? GetProfile(string profileName)
    {
        return Profiles.Find(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
    }
}

