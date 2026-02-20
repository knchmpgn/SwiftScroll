using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Appearance;

namespace SwiftScroll
{
    public partial class App : System.Windows.Application
    {
        private TrayIcon? _tray;
        private GlobalMouseHook? _hook;
        private SettingsViewModel? _vm;
        private SwiftScrollEngine? _engine;
        private SettingsWindow? _settingsWindow;
        private AppSettings _settings = null!;
        private readonly object _settingsLock = new(); // Thread-safe access to settings
        private string? _activeProfileKey; // Tracks which profile is currently applied to the engine

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();

            var settingsPath = AppSettings.GetConfigPath();
            var hadSettings = File.Exists(settingsPath);
            _settings = AppSettings.Load();

            // Ensure Windows startup state matches the saved preference
            StartupManager.SetStartup(_settings.StartWithWindows);

            // If this is a first run (no settings file existed), seed with Windows Classic defaults.
            if (!hadSettings)
            {
                ScrollProfile.ApplyWindowsClassic(_settings);
                _settings.Save();
            }

            _vm = new SettingsViewModel(_settings);
            _vm.SettingsChanged += (_, __) =>
            {
                try
                {
                    _tray?.UpdateEnabled(_vm.Enabled);
                    var snapshot = _vm.Snapshot();
                    lock (_settingsLock)
                    {
                        _settings = snapshot;
                    }
                    if (_engine != null) _engine.ApplySettings(snapshot);
                    if (_hook != null) _hook.ShiftKeyHorizontal = snapshot.ShiftKeyHorizontal;
                    _activeProfileKey = null; // force re-evaluation of effective profile
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] SettingsChanged handler error: {ex.Message}");
                }
            };

            _tray = new TrayIcon(_settings);
            _tray.OpenSettingsRequested += (_, __) => ShowSettingsWindow();
            _tray.ExitRequested += (_, __) => Shutdown();

            _engine = new SwiftScrollEngine(_settings);

            _hook = new GlobalMouseHook();
            _hook.ShiftKeyHorizontal = _settings.ShiftKeyHorizontal;

            _hook.MouseWheel += (_, args) =>
            {
                try
                {
                    lock (_settingsLock)
                    {
                        if (!_settings.Enabled) return;

                        // Check per-app exclusion list - use window under cursor for accurate detection
                        var foregroundProcess = ProcessHelper.GetProcessUnderCursor();
                        if (_settings.IsExcluded(foregroundProcess)) return; // Skip smooth scrolling

                        ApplyEffectiveSettingsForProcess(foregroundProcess);

                        args.Handled = true;
                        _engine?.OnWheel(args.Delta);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] MouseWheel handler error: {ex.Message}");
                }
            };
            _hook.MouseHWheel += (_, args) =>
            {
                try
                {
                    lock (_settingsLock)
                    {
                        if (!_settings.Enabled) return;

                        // Check per-app exclusion list - use window under cursor
                        var foregroundProcess = ProcessHelper.GetProcessUnderCursor();
                        if (_settings.IsExcluded(foregroundProcess)) return;

                        ApplyEffectiveSettingsForProcess(foregroundProcess);

                        args.Handled = true;
                        _engine?.OnHWheel(args.Delta);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] MouseHWheel handler error: {ex.Message}");
                }
            };

            UpdateHookState();

            // Open settings on startup and keep reference
            ShowSettingsWindow();
            Current.MainWindow = _settingsWindow;
        }

        private void UpdateHookState()
        {
            if (_hook is null || _vm is null || _engine is null) return;
            if (_vm.Enabled)
            {
                var snapshot = _vm.Snapshot();
                _engine.ApplySettings(snapshot);
                _hook.ShiftKeyHorizontal = snapshot.ShiftKeyHorizontal;
                _engine.Start();
                _hook.Install();
            }
            else
            {
                _hook.Uninstall();
                _engine.Stop();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _hook?.Dispose();
            _engine?.Dispose();
            _tray?.Dispose();
        }

        /// <summary>
        /// Applies the correct profile for the current process to the engine, avoiding redundant work when the profile hasn't changed.
        /// </summary>
        private void ApplyEffectiveSettingsForProcess(string? processName)
        {
            if (_engine == null || _hook == null) return;

            // Determine which profile (if any) should apply
            ScrollProfile? profile = null;
            var profileName = _settings.GetProfileForApp(processName);
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                profile = _settings.GetProfile(profileName);
            }

            var profileKey = profile?.Name ?? "__default__";
            if (profileKey == _activeProfileKey)
                return; // Already using this profile

            _activeProfileKey = profileKey;

            var effectiveSettings = CreateEffectiveSettings(profile);
            _engine.ApplySettings(effectiveSettings);
            _hook.ShiftKeyHorizontal = effectiveSettings.ShiftKeyHorizontal;
        }

        /// <summary>
        /// Builds a lightweight settings object for the engine using the provided profile override (if any).
        /// </summary>
        private AppSettings CreateEffectiveSettings(ScrollProfile? profileOverride)
        {
            var baseSettings = _settings;

            return new AppSettings
            {
                // Master toggle and shared options
                Enabled = baseSettings.Enabled,
                ShiftKeyHorizontal = baseSettings.ShiftKeyHorizontal,
                StartWithWindows = baseSettings.StartWithWindows,

                // Scroll behavior (overridable per profile)
                StepSizePx = profileOverride?.StepSizePx ?? baseSettings.StepSizePx,
                AnimationTimeMs = profileOverride?.AnimationTimeMs ?? baseSettings.AnimationTimeMs,
                AccelerationDeltaMs = profileOverride?.AccelerationDeltaMs ?? baseSettings.AccelerationDeltaMs,
                AccelerationMax = profileOverride?.AccelerationMax ?? baseSettings.AccelerationMax,
                TailToHeadRatio = profileOverride?.TailToHeadRatio ?? baseSettings.TailToHeadRatio,
                AnimationEasing = profileOverride?.AnimationEasing ?? baseSettings.AnimationEasing,
                HorizontalSmoothness = profileOverride?.HorizontalSmoothness ?? baseSettings.HorizontalSmoothness,
                ReverseWheelDirection = profileOverride?.ReverseWheelDirection ?? baseSettings.ReverseWheelDirection
            };
        }

        private void ShowSettingsWindow()
        {
            if (_vm is null) return;
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(_vm);
                _settingsWindow.Closed += (_, __) => _settingsWindow = null;
                _settingsWindow.Owner = null;
                _settingsWindow.Show();
            }
            else
            {
                if (_settingsWindow.WindowState == WindowState.Minimized)
                    _settingsWindow.WindowState = WindowState.Normal;
                _settingsWindow.Activate();
            }
        }
    }
}

