using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.Devices;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace MCLauncher.Views.Pages
{
    public partial class SettingsPage : Page, INavigableView<object>
    {
        private readonly ILogger<SettingsPage> _logger;
        private readonly string _configFilePath;
        private readonly string _settingsFilePath;
        private ConfigData _configData;
        private SettingsData _settingsData;
        private long _totalSystemRamMb;

        public SettingsData SettingsData => _settingsData;

        public object ViewModel => null;

        public SettingsPage(ILogger<SettingsPage> logger)
        {
            _logger = logger;
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            InitializeComponent();
            Loaded += async (s, e) => await InitializeSettingsAsync();
        }

        private async Task InitializeSettingsAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogDebug("Initializing SettingsPage");

                // Query system RAM
                var computerInfo = new ComputerInfo();
                _totalSystemRamMb = (long)(computerInfo.TotalPhysicalMemory / 1024 / 1024);
                _logger.LogDebug("Detected system RAM: {TotalRamMb} MB", _totalSystemRamMb);

                // Initialize RAM slider
                RamSlider.Maximum = _totalSystemRamMb;
                RamSlider.Value = _totalSystemRamMb * 2 / 3;
                RamValueText.Text = $"{(int)RamSlider.Value} MB";

                // Load config file
                await LoadConfigAsync();

                // Load settings file
                await LoadSettingsAsync();

                // Set theme radio buttons
                var currentTheme = ApplicationThemeManager.GetAppTheme();
                LightThemeRadio.IsChecked = currentTheme == ApplicationTheme.Light;
                DarkThemeRadio.IsChecked = currentTheme == ApplicationTheme.Dark;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SettingsPage");
                MessageBox.Show("Không thể tải cài đặt.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug("SettingsPage initialized in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task LoadConfigAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger.LogInformation("No config file found at {Path}", _configFilePath);
                    _configData = new ConfigData();
                    ModpackInfoText.Text = "Không tìm thấy file config.json";
                    return;
                }

                var json = await File.ReadAllTextAsync(_configFilePath);
                _configData = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
                _logger.LogDebug("Loaded config: ModpackName={ModpackName}, MinimumRam={MinimumRam}",
                    _configData.ModpackName, _configData.MinimumRam);

                ModpackInfoText.Text = $"Modpack: {_configData.ModpackName}\n" +
                                       $"Phiên bản Minecraft: {_configData.MinecraftVersion}\n" +
                                       $"Mod Loader: {_configData.ModLoaderType} {_configData.ModLoaderVersion}\n" +
                                       $"RAM Tối Thiểu: {_configData.MinimumRam} MB";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config file");
                ModpackInfoText.Text = "Lỗi khi đọc file config.json";
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger.LogInformation("No settings file found at {Path}, using defaults", _settingsFilePath);
                    _settingsData = new SettingsData
                    {
                        MaxRamMb = (int)(_totalSystemRamMb * 2 / 3),
                        ServerIp = "",
                        ServerPort = "",
                        FullScreen = false,
                        ScreenWidth = 1600,
                        ScreenHeight = 900,
                        JvmArguments = ""
                    };
                }
                else
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    _settingsData = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                    _logger.LogDebug("Loaded settings: MaxRamMb={MaxRamMb}, ServerIp={ServerIp}, FullScreen={FullScreen}",
                        _settingsData.MaxRamMb, _settingsData.ServerIp, _settingsData.FullScreen);
                }

                // Apply settings to UI
                RamSlider.Value = Math.Min(_settingsData.MaxRamMb, _totalSystemRamMb);
                RamValueText.Text = $"{_settingsData.MaxRamMb} MB";
                ServerIpTextBox.Text = _settingsData.ServerIp;
                ServerPortTextBox.Text = _settingsData.ServerPort;
                FullScreenCheckBox.IsChecked = _settingsData.FullScreen;
                ScreenWidthBox.Value = _settingsData.ScreenWidth;
                ScreenHeightBox.Value = _settingsData.ScreenHeight;
                JvmArgumentsTextBox.Text = _settingsData.JvmArguments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings file");
                MessageBox.Show("Không thể tải cài đặt từ file.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                _logger.LogDebug("Saving settings to {Path}", _settingsFilePath);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Validate RAM
                int selectedRamMb = (int)RamSlider.Value;
                if (_configData != null && selectedRamMb < _configData.MinimumRam)
                {
                    _logger.LogWarning("Selected RAM {SelectedRamMb} MB is below minimum {MinimumRam} MB",
                        selectedRamMb, _configData.MinimumRam);
                    MessageBox.Show($"RAM tối đa ({selectedRamMb} MB) nhỏ hơn yêu cầu tối thiểu ({_configData.MinimumRam} MB) của modpack.",
                        "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _settingsData = new SettingsData
                {
                    MaxRamMb = selectedRamMb,
                    ServerIp = ServerIpTextBox.Text?.Trim() ?? "",
                    ServerPort = ServerPortTextBox.Text?.Trim() ?? "",
                    FullScreen = FullScreenCheckBox.IsChecked ?? false,
                    ScreenWidth = (int)ScreenWidthBox.Value,
                    ScreenHeight = (int)ScreenHeightBox.Value,
                    JvmArguments = JvmArgumentsTextBox.Text?.Trim() ?? ""
                };

                var json = JsonSerializer.Serialize(_settingsData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFilePath, json);
                stopwatch.Stop();
                _logger.LogDebug("Saved settings in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                MessageBox.Show("Đã lưu cài đặt!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                MessageBox.Show("Không thể lưu cài đặt.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamValueText != null)
            {
                RamValueText.Text = $"{(int)RamSlider.Value} MB";
            }
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == LightThemeRadio && LightThemeRadio.IsChecked == true)
            {
                _logger.LogDebug("Applying Light theme");
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
            }
            else if (sender == DarkThemeRadio && DarkThemeRadio.IsChecked == true)
            {
                _logger.LogDebug("Applying Dark theme");
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            }
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            await SaveSettingsAsync();
        }
    }

    public class ConfigData
    {
        public string ModpackName { get; set; } = "Unknown";
        public int MinimumRam { get; set; } = 1024;
        public string MinecraftVersion { get; set; } = "Unknown";
        public string ModLoaderType { get; set; } = "Vanilla";
        public string ModLoaderVersion { get; set; } = "Unknown";
    }

    public class SettingsData
    {
        public int MaxRamMb { get; set; }
        public string ServerIp { get; set; }
        public string ServerPort { get; set; }
        public bool FullScreen { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public string JvmArguments { get; set; }
    }
}