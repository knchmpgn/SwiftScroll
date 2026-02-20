using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SwiftScroll;

public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsViewModel _vm;
    private static readonly Regex _numRegex = new("^[0-9]+$");
    private string? _lastFocusedApp;

    // Smart snapping configuration
    private const double SNAP_THRESHOLD = 15; // pixels from tick to snap

    // Preset configurations
    private static readonly Dictionary<string, ScrollPreset> _presets = new()
    {
        ["macOS"] = new ScrollPreset
        {
            StepSizePx = 10,
            AnimationTimeMs = 450,
            AccelerationDeltaMs = 80,
            AccelerationMax = 5,
            TailToHeadRatio = 4,
            AnimationEasing = true,
            ReverseWheelDirection = false
        },
        ["Windows"] = new ScrollPreset
        {
            StepSizePx = 12,
            AnimationTimeMs = 250,
            AccelerationDeltaMs = 60,
            AccelerationMax = 6,
            TailToHeadRatio = 2,
            AnimationEasing = true,
            ReverseWheelDirection = false
        },
        ["Precision"] = new ScrollPreset
        {
            StepSizePx = 6,
            AnimationTimeMs = 200,
            AccelerationDeltaMs = 40,
            AccelerationMax = 3,
            TailToHeadRatio = 1,
            AnimationEasing = false,
            ReverseWheelDirection = false
        },
        ["Cinematic"] = new ScrollPreset
        {
            StepSizePx = 15,
            AnimationTimeMs = 600,
            AccelerationDeltaMs = 100,
            AccelerationMax = 8,
            TailToHeadRatio = 5,
            AnimationEasing = true,
            ReverseWheelDirection = false
        },
        ["Snappy"] = new ScrollPreset
        {
            StepSizePx = 9,
            AnimationTimeMs = 150,
            AccelerationDeltaMs = 30,
            AccelerationMax = 4,
            TailToHeadRatio = 1,
            AnimationEasing = false,
            ReverseWheelDirection = false
        }
    };

    private class ScrollPreset
    {
        public int StepSizePx { get; set; }
        public int AnimationTimeMs { get; set; }
        public int AccelerationDeltaMs { get; set; }
        public int AccelerationMax { get; set; }
        public int TailToHeadRatio { get; set; }
        public bool AnimationEasing { get; set; }
        public bool ReverseWheelDirection { get; set; }
    }

    public SettingsWindow(SettingsViewModel vm)
    {
        try
        {
            SystemThemeWatcher.Watch(this);
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            this.Title = "SwiftScroll";

            // Capture the last focused app before this window was opened
            _lastFocusedApp = ProcessHelper.GetForegroundProcessName();
            if (string.Equals(_lastFocusedApp, "SwiftScroll", StringComparison.OrdinalIgnoreCase))
            {
                _lastFocusedApp = null;
            }

            // Apply theme based on Windows settings
            ApplyTheme();
            ConfigureEvenSliderTicks();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] Constructor error: {ex.Message}");
            throw;
        }
    }

    private void ApplyTheme()
    {
        try
        {
            bool isDark = ThemeHelper.IsDarkMode();
            var accent = ApplicationAccentColorManager.PrimaryAccent;
            var accentHex = accent.ToString(); // #AARRGGBB

            // Update resource dictionary colors
            if (isDark)
            {
                SetBrush("BackgroundBrush", ThemeHelper.Dark.Background);
                SetBrush("SurfaceBrush", ThemeHelper.Dark.Surface);
                SetBrush("SurfaceBorderBrush", ThemeHelper.Dark.SurfaceBorder);
                SetBrush("TextBrush", ThemeHelper.Dark.Text);
                SetBrush("TextSecondaryBrush", ThemeHelper.Dark.TextSecondary);
                SetBrush("AccentBrush", accentHex);
                SetBrush("InputBrush", ThemeHelper.Dark.Input);
                SetBrush("InputBorderBrush", ThemeHelper.Dark.InputBorder);
                SetBrush("TabHoverBrush", "#333333");
                SetBrush("TickMarkBrush", "#505050");
                SetBrush("ScrollBarTrackBrush", "#2A2A2A");
                SetBrush("ScrollBarThumbBrush", "#585858");
                SetBrush("ScrollBarThumbHoverBrush", "#747474");
                SetBrush("ScrollBarThumbDragBrush", "#939393");
            }
            else
            {
                SetBrush("BackgroundBrush", ThemeHelper.Light.Background);
                SetBrush("SurfaceBrush", ThemeHelper.Light.Surface);
                SetBrush("SurfaceBorderBrush", ThemeHelper.Light.SurfaceBorder);
                SetBrush("TextBrush", ThemeHelper.Light.Text);
                SetBrush("TextSecondaryBrush", ThemeHelper.Light.TextSecondary);
                SetBrush("AccentBrush", accentHex);
                SetBrush("InputBrush", ThemeHelper.Light.Input);
                SetBrush("InputBorderBrush", ThemeHelper.Light.InputBorder);
                SetBrush("TabHoverBrush", "#E8E8E8");
                SetBrush("TickMarkBrush", "#C0C0C0");
                SetBrush("ScrollBarTrackBrush", "#F1F1F1");
                SetBrush("ScrollBarThumbBrush", "#B8B8B8");
                SetBrush("ScrollBarThumbHoverBrush", "#9F9F9F");
                SetBrush("ScrollBarThumbDragBrush", "#818181");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] ApplyTheme error: {ex.Message}");
        }
    }

    private void SetBrush(string resourceKey, string colorHex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
            Resources[resourceKey] = new System.Windows.Media.SolidColorBrush(color);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] SetBrush error for {resourceKey}: {ex.Message}");
        }
    }

    private void ConfigureEvenSliderTicks()
    {
        try
        {
            ConfigureSliderTicks(StepSizeSlider);
            ConfigureSliderTicks(AnimTimeSlider);
            ConfigureSliderTicks(AccelDeltaSlider);
            ConfigureSliderTicks(AccelMaxSlider);
            ConfigureSliderTicks(TailHeadSlider);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] ConfigureEvenSliderTicks error: {ex.Message}");
        }
    }

    private static void ConfigureSliderTicks(Slider slider)
    {
        var tickFrequency = slider.TickFrequency;
        if (tickFrequency <= 0)
        {
            return;
        }

        var ticks = new DoubleCollection { slider.Minimum };

        // Start ticks on the first point that aligns with the declared frequency.
        var firstAlignedTick = Math.Ceiling(slider.Minimum / tickFrequency) * tickFrequency;
        var maxTicks = 1000; // guard against extremely small tick sizes
        int count = 0;
        for (double tick = firstAlignedTick; tick < slider.Maximum && count < maxTicks; tick += tickFrequency, count++)
        {
            if (!ticks.Contains(tick))
            {
                ticks.Add(tick);
            }
        }

        if (!ticks.Contains(slider.Maximum))
        {
            ticks.Add(slider.Maximum);
        }

        slider.Ticks = ticks;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapshot = _vm.Snapshot();
            snapshot.Save();

            // Update Windows startup registry based on setting
            StartupManager.SetStartup(snapshot.StartWithWindows);

            this.Title = "SwiftScroll (Saved)";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnSave error: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = System.Windows.MessageBox.Show("Reset all settings to defaults?", "Confirm Reset", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _vm.Apply(AppSettings.CreateDefault());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnResetDefaults error: {ex.Message}");
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnCloseClicked error: {ex.Message}");
        }
    }

    private void OnAddApp(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new AddApplicationDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedProcessName))
            {
                _vm.AddExcludedApp(dialog.SelectedProcessName);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnAddApp error: {ex.Message}");
        }
    }

    private void OnRemoveApp(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ExcludedAppsList.SelectedItem is string selected)
            {
                _vm.RemoveExcludedApp(selected);
            }
            else
            {
                System.Windows.MessageBox.Show("Please select an application from the list to remove.", 
                    "No Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnRemoveApp error: {ex.Message}");
        }
    }

    private void NumericOnly(object sender, TextCompositionEventArgs e)
    {
        try
        {
            e.Handled = !_numRegex.IsMatch(e.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] NumericOnly error: {ex.Message}");
            e.Handled = true;
        }
    }

    private void OnPasteNumeric(object sender, DataObjectPastingEventArgs e)
    {
        try
        {
            if (e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
            {
                var text = (string)e.DataObject.GetData(System.Windows.DataFormats.Text);
                if (!_numRegex.IsMatch(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnPasteNumeric error: {ex.Message}");
            e.CancelCommand();
        }
    }

    // Profile Management Event Handlers
    private void OnCreateProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the TextBox from the visual tree
            var profileNameBox = this.FindName("NewProfileNameBox") as System.Windows.Controls.TextBox;
            if (profileNameBox == null) return;
            
            var profileName = profileNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                System.Windows.MessageBox.Show("Please enter a profile name.", "Input Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (_vm.Profiles.Any(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show("A profile with this name already exists.", "Duplicate Name", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            _vm.AddProfile(profileName);
            profileNameBox.Clear();
            profileNameBox.Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnCreateProfile error: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to create profile: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            var profilesList = this.FindName("ProfilesList") as System.Windows.Controls.ListBox;
            if (profilesList == null || profilesList.SelectedItem is not ScrollProfile selected)
            {
                System.Windows.MessageBox.Show("Please select a profile to delete.", "No Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            if (string.Equals(selected.Name, "Default", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show("Cannot delete the 'Default' profile.", "Protected Profile", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show($"Delete profile '{selected.Name}'?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _vm.RemoveProfile(selected.Name);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnDeleteProfile error: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to delete profile: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnAssignProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            var appNameBox = this.FindName("AppNameBox") as System.Windows.Controls.TextBox;
            var profileCombo = this.FindName("ProfileCombo") as System.Windows.Controls.ComboBox;
            
            if (appNameBox == null || profileCombo == null) return;

            var appName = appNameBox.Text?.Trim();
            var profileName = profileCombo.SelectedValue as string;

            if (string.IsNullOrWhiteSpace(appName))
            {
                System.Windows.MessageBox.Show("Please enter an application name.", "Input Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(profileName))
            {
                System.Windows.MessageBox.Show("Please select a profile.", "No Profile Selected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            _vm.AssignProfileToApp(appName, profileName);
            appNameBox.Clear();
            appNameBox.Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnAssignProfile error: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to assign profile: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnRemoveAppProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string appName)
            {
                _vm.AssignProfileToApp(appName, "Default");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnRemoveAppProfile error: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to remove assignment: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // NEW: Preset click handler
    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string presetName)
                return;

            if (!_presets.TryGetValue(presetName, out var preset))
                return;

            // Apply preset to view model
            _vm.StepSizePx = preset.StepSizePx;
            _vm.AnimationTimeMs = preset.AnimationTimeMs;
            _vm.AccelerationDeltaMs = preset.AccelerationDeltaMs;
            _vm.AccelerationMax = preset.AccelerationMax;
            _vm.TailToHeadRatio = preset.TailToHeadRatio;
            _vm.AnimationEasing = preset.AnimationEasing;
            _vm.ReverseWheelDirection = preset.ReverseWheelDirection;

            // Visual feedback
            this.Title = $"SwiftScroll - {presetName} preset applied";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnPresetClick error: {ex.Message}");
        }
    }

    // NEW: Reset individual parameter to default
    private void OnResetParameter(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string paramName)
                return;

            // Default values
            var defaults = new Dictionary<string, int>
            {
                ["StepSizePx"] = 12,
                ["AnimationTimeMs"] = 250,
                ["AccelerationDeltaMs"] = 60,
                ["AccelerationMax"] = 6,
                ["TailToHeadRatio"] = 2
            };

            if (defaults.TryGetValue(paramName, out var defaultValue))
            {
                // Use reflection to set the property
                var property = _vm.GetType().GetProperty(paramName);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(_vm, defaultValue);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnResetParameter error: {ex.Message}");
        }
    }

    // NEW: Smart snapping for sliders
    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            if (sender is not Slider slider)
                return;

            // Don't snap if user is not actively dragging
            if (!slider.IsMouseCaptureWithin)
                return;

            var value = slider.Value;
            var tickFrequency = slider.TickFrequency;
            
            if (tickFrequency <= 0)
                return;

            // Find nearest explicit tick if available; otherwise, fall back to frequency math.
            double nearestTick = slider.Value;
            double minDistance = double.MaxValue;

            if (slider.Ticks is { Count: > 0 })
            {
                foreach (var tick in slider.Ticks)
                {
                    var distanceToTick = Math.Abs(value - tick);
                    if (distanceToTick < minDistance)
                    {
                        minDistance = distanceToTick;
                        nearestTick = tick;
                    }
                }
            }
            else
            {
                var tickIndex = Math.Round((value - slider.Minimum) / tickFrequency);
                nearestTick = slider.Minimum + (tickIndex * tickFrequency);
                nearestTick = Math.Clamp(nearestTick, slider.Minimum, slider.Maximum);
                minDistance = Math.Abs(value - nearestTick);
            }
            
            var distance = Math.Abs(value - nearestTick);
            
            // Only snap if within threshold (convert to slider units)
            var range = slider.Maximum - slider.Minimum;
            if (slider.ActualWidth <= 0)
                return;
                
            var pixelToValue = range / slider.ActualWidth;
            var snapDistance = SNAP_THRESHOLD * pixelToValue;
            
            if (distance <= snapDistance && Math.Abs(slider.Value - nearestTick) > double.Epsilon)
            {
                slider.Value = nearestTick;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] OnSliderValueChanged error: {ex.Message}");
        }
    }
}

