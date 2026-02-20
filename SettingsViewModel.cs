using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SwiftScroll;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private ScrollProfile _currentProfile = ScrollProfile.CreateDefault();
    private const int STEP_SIZE_MIN = 1;
    private const int STEP_SIZE_MAX = 25;

    public SettingsViewModel(AppSettings settings)
    {
        ExcludedApps = new ObservableCollection<string>();
        Profiles = new ObservableCollection<ScrollProfile>();
        AppProfiles = new ObservableCollection<ApplicationProfile>();
        Apply(settings);
    }

    public void Apply(AppSettings s)
    {
        try
        {
            Enabled = s.Enabled;
            ShiftKeyHorizontal = s.ShiftKeyHorizontal;
            StartWithWindows = s.StartWithWindows;

            ExcludedApps.Clear();
            foreach (var app in s.ExcludedApps)
                ExcludedApps.Add(app);

            Profiles.Clear();
            foreach (var profile in s.Profiles)
                Profiles.Add(profile.Clone());

            AppProfiles.Clear();
            foreach (var appProfile in s.AppProfiles)
                AppProfiles.Add(new ApplicationProfile { AppName = appProfile.AppName, ProfileName = appProfile.ProfileName });

            if (Profiles.Count > 0)
                _currentProfile = Profiles[0].Clone();
            else
                _currentProfile = ScrollProfile.CreateDefault();

            UpdateProfileBindings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Apply error: {ex.Message}");
            // Continue with default profile on error
            _currentProfile = ScrollProfile.CreateDefault();
            UpdateProfileBindings();
        }
    }

    public AppSettings Snapshot() => new()
    {
        Enabled = Enabled,
        StepSizePx = StepSizePx,
        AnimationTimeMs = AnimationTimeMs,
        AccelerationDeltaMs = AccelerationDeltaMs,
        AccelerationMax = AccelerationMax,
        TailToHeadRatio = TailToHeadRatio,
        AnimationEasing = AnimationEasing,
        ShiftKeyHorizontal = ShiftKeyHorizontal,
        HorizontalSmoothness = HorizontalSmoothness,
        ReverseWheelDirection = ReverseWheelDirection,
        StartWithWindows = StartWithWindows,
        ExcludedApps = new List<string>(ExcludedApps),
        Profiles = Profiles.Select(p => p.Clone()).ToList(),
        AppProfiles = AppProfiles.Select(ap => new ApplicationProfile { AppName = ap.AppName, ProfileName = ap.ProfileName }).ToList()
    };

    // Global settings
    private bool _enabled;
    public bool Enabled { get => _enabled; set { if (Set(ref _enabled, value)) OnSettingsChanged(); } }

    private bool _shiftHorizontal;
    public bool ShiftKeyHorizontal { get => _shiftHorizontal; set { if (Set(ref _shiftHorizontal, value)) OnSettingsChanged(); } }

    private bool _startWithWindows;
    public bool StartWithWindows { get => _startWithWindows; set { if (Set(ref _startWithWindows, value)) OnSettingsChanged(); } }

    // Performance settings (from current profile)
    private int _stepSizePx;
    public int StepSizePx
    {
        get => _stepSizePx;
        set
        {
            var clamped = Math.Clamp(value, STEP_SIZE_MIN, STEP_SIZE_MAX);
            if (Set(ref _stepSizePx, clamped))
            {
                _currentProfile.StepSizePx = clamped;
                OnSettingsChanged();
            }
        }
    }

    private int _animationTimeMs;
    public int AnimationTimeMs { get => _animationTimeMs; set { if (Set(ref _animationTimeMs, value)) { _currentProfile.AnimationTimeMs = value; OnSettingsChanged(); } } }

    private int _accelDeltaMs;
    public int AccelerationDeltaMs { get => _accelDeltaMs; set { if (Set(ref _accelDeltaMs, value)) { _currentProfile.AccelerationDeltaMs = value; OnSettingsChanged(); } } }

    private int _accelMax;
    public int AccelerationMax { get => _accelMax; set { if (Set(ref _accelMax, value)) { _currentProfile.AccelerationMax = value; OnSettingsChanged(); } } }

    private int _tailToHead;
    public int TailToHeadRatio { get => _tailToHead; set { if (Set(ref _tailToHead, value)) { _currentProfile.TailToHeadRatio = value; OnSettingsChanged(); } } }

    private bool _easing;
    public bool AnimationEasing { get => _easing; set { if (Set(ref _easing, value)) { _currentProfile.AnimationEasing = value; OnSettingsChanged(); } } }

    private bool _horizontalSmooth;
    public bool HorizontalSmoothness { get => _horizontalSmooth; set { if (Set(ref _horizontalSmooth, value)) { _currentProfile.HorizontalSmoothness = value; OnSettingsChanged(); } } }

    private bool _reverse;
    public bool ReverseWheelDirection { get => _reverse; set { if (Set(ref _reverse, value)) { _currentProfile.ReverseWheelDirection = value; OnSettingsChanged(); } } }

    // Collections
    public ObservableCollection<string> ExcludedApps { get; }
    public ObservableCollection<ScrollProfile> Profiles { get; }
    public ObservableCollection<ApplicationProfile> AppProfiles { get; }

    public void AddExcludedApp(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        if (!ExcludedApps.Contains(appName, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedApps.Add(appName);
            OnSettingsChanged();
        }
    }

    public void RemoveExcludedApp(string appName)
    {
        if (ExcludedApps.Remove(appName))
            OnSettingsChanged();
    }

    public void AddProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;
        if (Profiles.Any(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))) return;
        
        var newProfile = ScrollProfile.CreateDefault();
        newProfile.Name = profileName;
        Profiles.Add(newProfile);
        OnSettingsChanged();
    }

    public void RemoveProfile(string profileName)
    {
        if (profileName == "Default") return; // Can't remove default profile
        var profile = Profiles.FirstOrDefault(p => p.Name == profileName);
        if (profile != null)
        {
            Profiles.Remove(profile);
            // Remove all app profiles that reference this profile
            var toRemove = AppProfiles.Where(ap => ap.ProfileName == profileName).ToList();
            foreach (var ap in toRemove)
                AppProfiles.Remove(ap);
            OnSettingsChanged();
        }
    }

    public void SelectProfile(string profileName)
    {
        var profile = Profiles.FirstOrDefault(p => p.Name == profileName);
        if (profile != null)
        {
            _currentProfile = profile.Clone();
            UpdateProfileBindings();
        }
    }

    public void AssignProfileToApp(string appName, string profileName)
    {
        if (ExcludedApps.Contains(appName, StringComparer.OrdinalIgnoreCase))
            return; // Can't assign profile to excluded app

        var existing = AppProfiles.FirstOrDefault(ap => string.Equals(ap.AppName, appName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            existing.ProfileName = profileName;
        else
            AppProfiles.Add(new ApplicationProfile { AppName = appName, ProfileName = profileName });

        OnSettingsChanged();
    }

    public void RemoveAppProfile(string appName)
    {
        var appProfile = AppProfiles.FirstOrDefault(ap => string.Equals(ap.AppName, appName, StringComparison.OrdinalIgnoreCase));
        if (appProfile != null)
        {
            AppProfiles.Remove(appProfile);
            OnSettingsChanged();
        }
    }

    private void UpdateProfileBindings()
    {
        StepSizePx = _currentProfile.StepSizePx;
        AnimationTimeMs = _currentProfile.AnimationTimeMs;
        AccelerationDeltaMs = _currentProfile.AccelerationDeltaMs;
        AccelerationMax = _currentProfile.AccelerationMax;
        TailToHeadRatio = _currentProfile.TailToHeadRatio;
        AnimationEasing = _currentProfile.AnimationEasing;
        HorizontalSmoothness = _currentProfile.HorizontalSmoothness;
        ReverseWheelDirection = _currentProfile.ReverseWheelDirection;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SettingsChanged;

    private void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

