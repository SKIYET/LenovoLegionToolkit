﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Settings;

namespace LenovoLegionToolkit.WPF.Pages
{
    public partial class SettingsPage
    {
        private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

        private readonly Vantage _vantage = IoCContainer.Resolve<Vantage>();
        private readonly LegionZone _legionZone = IoCContainer.Resolve<LegionZone>();
        private readonly FnKeys _fnKeys = IoCContainer.Resolve<FnKeys>();
        private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
        private readonly RGBKeyboardBacklightController _rgbKeyboardBacklightController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
        private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();
        private readonly ThemeManager _themeManager = IoCContainer.Resolve<ThemeManager>();

        private bool _isRefreshing;

        public SettingsPage()
        {
            InitializeComponent();

            Loaded += SettingsPage_Loaded;
            IsVisibleChanged += SettingsPage_IsVisibleChanged;

            _themeManager.ThemeApplied += ThemeManager_ThemeApplied;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void SettingsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        }

        private void ThemeManager_ThemeApplied(object? sender, EventArgs e)
        {
            if (!_isRefreshing)
                UpdateAccentColorPicker();
        }

        private async Task RefreshAsync()
        {
            _isRefreshing = true;

            var loadingTask = Task.Delay(250);

            var languages = LocalizationHelper.Languages.OrderBy(ci => ci.Name, StringComparer.InvariantCultureIgnoreCase).ToArray();
            var language = await LocalizationHelper.GetLanguageAsync();
            if (languages.Length > 1)
            {
                _langComboBox.SetItems(languages, language, cc => cc.NativeName.Transform(cc, To.TitleCase));
                _langComboBox.Visibility = Visibility.Visible;
            }
            else
            {
                _langCardControl.Visibility = Visibility.Collapsed;
            }

            _themeComboBox.SetItems(Enum.GetValues<Theme>(), _settings.Store.Theme, t => t.GetDisplayName());

            UpdateAccentColorPicker();
            _accentColorSourceComboBox.SetItems(Enum.GetValues<AccentColorSource>(), _settings.Store.AccentColorSource, t => t.GetDisplayName());

            _autorunComboBox.SetItems(Enum.GetValues<AutorunState>(), Autorun.State, t => t.GetDisplayName());
            _minimizeOnCloseToggle.IsChecked = _settings.Store.MinimizeOnClose;

            var vantageStatus = await _vantage.GetStatusAsync();
            _vantageCard.Visibility = vantageStatus != SoftwareStatus.NotFound ? Visibility.Visible : Visibility.Collapsed;
            _vantageToggle.IsChecked = vantageStatus == SoftwareStatus.Disabled;

            var legionZoneStatus = await _legionZone.GetStatusAsync();
            _legionZoneCard.Visibility = legionZoneStatus != SoftwareStatus.NotFound ? Visibility.Visible : Visibility.Collapsed;
            _legionZoneToggle.IsChecked = legionZoneStatus == SoftwareStatus.Disabled;

            var fnKeysStatus = await _fnKeys.GetStatusAsync();
            _fnKeysCard.Visibility = fnKeysStatus != SoftwareStatus.NotFound ? Visibility.Visible : Visibility.Collapsed;
            _fnKeysToggle.IsChecked = fnKeysStatus == SoftwareStatus.Disabled;

            _smartKeySinglePressActionCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
            _smartKeyDoublePressActionCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;

            _notificationsCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
            _excludeRefreshRatesCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;

            _powerPlansCard.Visibility = await _powerModeFeature.IsSupportedAsync() ? Visibility.Visible : Visibility.Collapsed;

            await loadingTask;

            _themeComboBox.Visibility = Visibility.Visible;
            _autorunComboBox.Visibility = Visibility.Visible;
            _minimizeOnCloseToggle.Visibility = Visibility.Visible;
            _vantageToggle.Visibility = Visibility.Visible;
            _legionZoneToggle.Visibility = Visibility.Visible;
            _fnKeysToggle.Visibility = Visibility.Visible;

            _isRefreshing = false;
        }

        private async void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            if (!_langComboBox.TryGetSelectedItem(out CultureInfo? cultureInfo) || cultureInfo is null)
                return;

            await LocalizationHelper.SetLanguageAsync(cultureInfo);

            App.Current.RestartMainWindow();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            if (!_themeComboBox.TryGetSelectedItem(out Theme state))
                return;

            _settings.Store.Theme = state;
            _settings.SynchronizeStore();

            _themeManager.Apply();
        }

        private void AccentColorPicker_Changed(object sender, EventArgs e)
        {
            if (_isRefreshing)
                return;

            if (_settings.Store.AccentColorSource != AccentColorSource.Custom)
                return;

            _settings.Store.AccentColor = _accentColorPicker.SelectedColor.ToRGBColor();
            _settings.SynchronizeStore();

            _themeManager.Apply();
        }

        private void AccentColorSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            if (!_accentColorSourceComboBox.TryGetSelectedItem(out AccentColorSource state))
                return;

            _settings.Store.AccentColorSource = state;
            _settings.SynchronizeStore();

            UpdateAccentColorPicker();

            _themeManager.Apply();
        }

        private void UpdateAccentColorPicker()
        {
            _accentColorPicker.Visibility = _settings.Store.AccentColorSource == AccentColorSource.Custom ? Visibility.Visible : Visibility.Collapsed;
            _accentColorPicker.SelectedColor = _themeManager.AccentColor.ToColor();
        }

        private void AutorunComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            if (!_autorunComboBox.TryGetSelectedItem(out AutorunState state))
                return;

            Autorun.Set(state);
        }

        private void SmartKeySinglePressActionCard_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            var window = new SelectSmartKeyPipelinesWindow
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.ShowDialog();
        }

        private void SmartKeyDoublePressActionCard_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            var window = new SelectSmartKeyPipelinesWindow(isDoublePress: true)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.ShowDialog();
        }

        private void MinimizeOnCloseToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            var state = _minimizeOnCloseToggle.IsChecked;
            if (state is null)
                return;

            _settings.Store.MinimizeOnClose = state.Value;
            _settings.SynchronizeStore();
        }

        private async void VantageToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            _vantageToggle.IsEnabled = false;

            var state = _vantageToggle.IsChecked;
            if (state is null)
                return;

            if (state.Value)
            {
                try
                {
                    await _vantage.DisableAsync();
                }
                catch
                {
                    await SnackbarHelper.ShowAsync(Resource.SettingsPage_DisableVantage_Error_Title, Resource.SettingsPage_DisableVantage_Error_Message, true);
                    return;
                }

                try
                {
                    if (_rgbKeyboardBacklightController.IsSupported())
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Setting light control owner and restoring preset...");

                        await _rgbKeyboardBacklightController.SetLightControlOwnerAsync(true, true);
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't set light control owner or current preset.", ex);
                }
            }
            else
            {
                try
                {
                    if (_rgbKeyboardBacklightController.IsSupported())
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Setting light control owner...");

                        await _rgbKeyboardBacklightController.SetLightControlOwnerAsync(false);
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't set light control owner.", ex);
                }

                try
                {
                    await _vantage.EnableAsync();
                }
                catch
                {
                    await SnackbarHelper.ShowAsync(Resource.SettingsPage_EnableVantage_Error_Title, Resource.SettingsPage_EnableVantage_Error_Message, true);
                    return;
                }
            }

            _vantageToggle.IsEnabled = true;
        }

        private async void LegionZoneToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            _legionZoneToggle.IsEnabled = false;

            var state = _legionZoneToggle.IsChecked;
            if (state is null)
                return;

            if (state.Value)
            {
                try
                {
                    await _legionZone.DisableAsync();
                }
                catch
                {
                    await SnackbarHelper.ShowAsync(Resource.SettingsPage_DisableLegionZone_Error_Title, Resource.SettingsPage_DisableLegionZone_Error_Message, true);
                    return;
                }
            }
            else
            {
                try
                {
                    await _legionZone.EnableAsync();
                }
                catch
                {
                    await SnackbarHelper.ShowAsync(Resource.SettingsPage_EnableLegionZone_Error_Title, Resource.SettingsPage_EnableLegionZone_Error_Message, true);
                    return;
                }
            }

            _legionZoneToggle.IsEnabled = true;
        }

        private async void FnKeysToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            _fnKeysToggle.IsEnabled = false;

            var state = _fnKeysToggle.IsChecked;
            if (state is null)
                return;

            if (state.Value)
            {
                try
                {
                    await _fnKeys.DisableAsync();
                }
                catch
                {
                    await SnackbarHelper.ShowAsync(Resource.SettingsPage_DisableLenovoHotkeys_Error_Title, Resource.SettingsPage_DisableLenovoHotkeys_Error_Message, true);
                    return;
                }
            }
            else
            {
                try
                {
                    await _fnKeys.EnableAsync();
                }
                catch
                {
                    await SnackbarHelper.ShowAsync(Resource.SettingsPage_EnableLenovoHotkeys_Error_Title, Resource.SettingsPage_EnableLenovoHotkeys_Error_Message, true);
                    return;
                }
            }

            _fnKeysToggle.IsEnabled = true;

            _smartKeySinglePressActionCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
            _smartKeyDoublePressActionCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
            _notificationsCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
            _excludeRefreshRatesCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NotificationsCard_Click(object sender, RoutedEventArgs e)
        {
            var window = new NotificationsSettingsWindow
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.ShowDialog();
        }

        private void ExcludeRefreshRates_Click(object sender, RoutedEventArgs e)
        {
            var window = new ExcludeRefreshRatesWindow
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.ShowDialog();
        }

        private void PowerPlans_Click(object sender, RoutedEventArgs e)
        {
            var window = new PowerPlansWindow
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.ShowDialog();
        }

        private void CPUBoostModes_Click(object sender, RoutedEventArgs e)
        {
            var window = new CPUBoostModesWindow
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.ShowDialog();
        }
    }
}
