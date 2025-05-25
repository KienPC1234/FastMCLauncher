using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;

namespace MCLauncher.Views.Pages
{
    public partial class DashboardPage : Page, INavigableView<object>
    {
        private readonly ILogger<DashboardPage> _logger;
        private readonly TaiKhoanPage _taiKhoanPage;
        private readonly SettingsPage _settingsPage;
        private readonly string _configFilePath;
        private ConfigData _configData;
        private bool _isLaunching;
        private CancellationTokenSource _cancellationTokenSource;
        private ObservableCollection<StatusLogEntry> _statusLog;

        public object ViewModel => null;

        public DashboardPage(ILogger<DashboardPage> logger, TaiKhoanPage taiKhoanPage, SettingsPage settingsPage)
        {
            _logger = logger;
            _taiKhoanPage = taiKhoanPage;
            _settingsPage = settingsPage;
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            _isLaunching = false;
            _statusLog = new ObservableCollection<StatusLogEntry>();
            InitializeComponent();
            StatusLogList.ItemsSource = _statusLog;
            Loaded += DashboardPage_Loaded;

            // Subscribe to account changes
            
            _taiKhoanPage.AccountSelected += TaiKhoanPage_AccountSelected;

            // Optimize download speed for .NET Framework
#if NETFRAMEWORK
            ServicePointManager.DefaultConnectionLimit = 256;
            _logger.LogDebug("Set ServicePointManager.DefaultConnectionLimit to 256 for .NET Framework");
#endif
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _taiKhoanPage.LoadAccountsAsync();
            await InitializeDashboardAsync();
        }

        private void TaiKhoanPage_AccountSelected(object sender, MSession session)
        {
            Dispatcher.Invoke(() =>
            {
                AccountInfoText.Text = session != null
                    ? $"Tài khoản: {session.Username}"
                    : "Tài khoản: Chưa chọn";
                UpdateStatus(session != null && _configData?.MinecraftVersion != null
                    ? "Sẵn sàng"
                    : "Vui lòng chọn tài khoản và modpack", session != null ? Brushes.Blue : Brushes.Red);
            });
        }

        private async Task InitializeDashboardAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogDebug("Initializing DashboardPage");

                // Load config
                await LoadConfigAsync();

                // Update account info
                var session = await _taiKhoanPage.GetSessionAsync();
                AccountInfoText.Text = session != null
                    ? $"Tài khoản: {session.Username}"
                    : "Tài khoản: Chưa chọn";
                UpdateStatus(session != null && _configData?.MinecraftVersion != null
                    ? "Sẵn sàng"
                    : "Vui lòng chọn tài khoản và modpack", session != null ? Brushes.Blue : Brushes.Red);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize DashboardPage");
                UpdateStatus($"Lỗi: {ex.Message}", Brushes.Red);
                System.Windows.MessageBox.Show("Không thể tải dashboard.", "Lỗi", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogDebug("DashboardPage initialized in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
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
                    ModpackInfoText.Text = "Modpack: Chưa tải config";
                    UpdateStatus("Chưa tải config", Brushes.Orange);
                    return;
                }

                var json = await File.ReadAllTextAsync(_configFilePath);
                _configData = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
                _logger.LogDebug("Loaded config: ModpackName={ModpackName}, MinecraftVersion={MinecraftVersion}, ModLoader={ModLoaderType}",
                    _configData.ModpackName, _configData.MinecraftVersion, _configData.ModLoaderType);

                ModpackInfoText.Text = $"Modpack: {_configData.ModpackName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config file");
                ModpackInfoText.Text = "Modpack: Lỗi đọc config";
                UpdateStatus("Lỗi đọc config", Brushes.Red);
            }
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLaunching)
            {
                _logger.LogWarning("Launch request ignored, already launching");
                return;
            }

            _isLaunching = true;
            PlayButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressRing.IsIndeterminate = true;
            UpdateStatus("Đang khởi động...", Brushes.Green);

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await LaunchGameAsync(_cancellationTokenSource.Token);
                UpdateStatus("Game đã kết thúc", Brushes.Blue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch game");
                UpdateStatus($"Lỗi: {ex.Message}", Brushes.Red);
                System.Windows.MessageBox.Show($"Lỗi khi khởi động game: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLaunching = false;
                PlayButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Collapsed;
                ProgressRing.IsIndeterminate = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _logger.LogInformation("Launch cancellation requested");
                UpdateStatus("Đã hủy", Brushes.Orange);
            }
        }

        private async Task LaunchGameAsync(CancellationToken cancellationToken)
        {
            

            // Validate config
            if (_configData == null || string.IsNullOrEmpty(_configData.MinecraftVersion))
            {
                _logger.LogWarning("Invalid or missing config");
                UpdateStatus("Lỗi: Vui lòng cấu hình modpack trong config.json", Brushes.Red);
                throw new InvalidOperationException("Vui lòng cấu hình modpack trong config.json");
            }

            // Get session
            var session = await _taiKhoanPage.GetSessionAsync();
            if (session == null)
            {
                _logger.LogWarning("No account selected");
                UpdateStatus("Lỗi: Vui lòng chọn tài khoản", Brushes.Red);
                throw new InvalidOperationException("Vui lòng chọn tài khoản");
            }
            await _settingsPage.LoadSettingsAsync();
            // Get settings (assumes SettingsPage has public SettingsData property)
            var settings = _settingsPage.SettingsData ?? throw new InvalidOperationException("Settings data not found");

            // Initialize Minecraft path
            var minecraftDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modpack");
            var path = new MinecraftPath(minecraftDir);
            _logger.LogDebug("Using Minecraft path: {BasePath}", path.BasePath);

           

            // Initialize launcher
            var launcher = new MinecraftLauncher(path);
            ProgressBar.Value = 0;
            ProgressStatusText.Text = "Khởi tạo...";

            // Setup progress handlers
            launcher.FileProgressChanged += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressStatusText.Text = $"[{args.EventType}] {args.Name} ({args.ProgressedTasks}/{args.TotalTasks})";
                    if (args.TotalTasks > 0)
                        ProgressBar.Value = (args.ProgressedTasks * 100.0) / args.TotalTasks;
                });
            };
            launcher.ByteProgressChanged += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (args.TotalBytes > 0)
                        ProgressBar.Value = (args.ProgressedBytes * 100.0) / args.TotalBytes;
                });
            };

            // Install mod loader
            string versionName = _configData.MinecraftVersion;
            var modLoaderType = _configData.ModLoaderType.ToLower();
            var modLoaderVersion = _configData.ModLoaderVersion;

            try
            {
                ProgressStatusText.Text = $"Cài đặt {modLoaderType}...";
                UpdateStatus($"Cài đặt {modLoaderType}...", Brushes.Green);
                switch (modLoaderType)
                {
                    case "forge":
                        var forge = new ForgeInstaller(launcher);
                        try
                        {

                            versionName = await forge.Install(_configData.MinecraftVersion, modLoaderVersion, new ForgeInstallOptions
                            {
                                FileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e =>
                                    Dispatcher.Invoke(() => ProgressStatusText.Text = $"[Forge] {e.Name} ({e.ProgressedTasks}/{e.TotalTasks})")),
                                ByteProgress = new SyncProgress<ByteProgress>(e =>
                                    Dispatcher.Invoke(() =>
                                    {
                                        double ratio = e.ToRatio();
                                        double value = ratio * 100;
                                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                                        {
                                            Dispatcher.Invoke(() => ProgressBar.Value = value);
                                        }
                                    }))
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to install Forge {Version} for {MinecraftVersion}", modLoaderVersion, _configData.MinecraftVersion);
                            UpdateStatus($"Lỗi: Không thể cài Forge {modLoaderVersion}", Brushes.Red);
                            throw;
                        }
                        break;

                    case "fabric":
                        var fabric = new FabricInstaller(new HttpClient());
                        try
                        {
                            versionName = await fabric.Install(_configData.MinecraftVersion, modLoaderVersion, path);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to install Fabric {Version} for {MinecraftVersion}", modLoaderVersion, _configData.MinecraftVersion);
                            UpdateStatus($"Lỗi: Không thể cài Fabric {modLoaderVersion}", Brushes.Red);
                            throw;
                        }
                        break;

                    case "quilt":
                        var quilt = new QuiltInstaller(new HttpClient());
                        try
                        {
                            versionName = await quilt.Install(_configData.MinecraftVersion, modLoaderVersion, path);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to install Quilt {Version} for {MinecraftVersion}", modLoaderVersion, _configData.MinecraftVersion);
                            UpdateStatus($"Lỗi: Không thể cài Quilt {modLoaderVersion}", Brushes.Red);
                            throw;
                        }
                        break;

                    case "liteloader":
                        var liteLoader = new LiteLoaderInstaller(new HttpClient());
                        var loaders = await liteLoader.GetAllLiteLoaders();
                        var loader = loaders.FirstOrDefault(l => l.BaseVersion == _configData.MinecraftVersion && l.Version == modLoaderVersion);
                        if (loader == null)
                        {
                            _logger.LogError("LiteLoader version {Version} not found for {MinecraftVersion}", modLoaderVersion, _configData.MinecraftVersion);
                            UpdateStatus($"Lỗi: LiteLoader {modLoaderVersion} không tồn tại", Brushes.Red);
                            throw new InvalidOperationException($"LiteLoader version {modLoaderVersion} not found for {_configData.MinecraftVersion}");
                        }
                        var version = await launcher.GetVersionAsync(_configData.MinecraftVersion, cancellationToken);
                        versionName = await liteLoader.Install(loader, version, path);
                        break;

                    case "vanilla":
                        // No mod loader installation needed
                        break;

                    default:
                        _logger.LogWarning("Unknown mod loader type: {ModLoaderType}", modLoaderType);
                        UpdateStatus($"Lỗi: Loại mod loader không hỗ trợ: {modLoaderType}", Brushes.Red);
                        throw new InvalidOperationException($"Loại mod loader không hỗ trợ: {modLoaderType}");
                }

                // Install game
                ProgressStatusText.Text = $"Cài đặt Minecraft {_configData.MinecraftVersion}...";
                UpdateStatus($"Cài đặt Minecraft {_configData.MinecraftVersion}...", Brushes.Green);
                await launcher.InstallAsync(versionName, cancellationToken);

                // Build launch options
                var jvmArgs = string.IsNullOrWhiteSpace(settings.JvmArguments)
                    ? Array.Empty<MArgument>()
                    : new[] { MArgument.FromCommandLine(settings.JvmArguments) };

                var launchOption = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = settings.MaxRamMb,
                    ServerIp = string.IsNullOrWhiteSpace(settings.ServerIp) ? null : settings.ServerIp,
                    ServerPort = int.TryParse(settings.ServerPort, out var port) ? port : 25565,
                    FullScreen = settings.FullScreen,
                    ScreenWidth = settings.ScreenWidth > 0 ? settings.ScreenWidth : 800,
                    ScreenHeight = settings.ScreenHeight > 0 ? settings.ScreenHeight : 600,
                    ExtraJvmArguments = jvmArgs,
                    GameLauncherName = "MCLauncher",
                    GameLauncherVersion = "1.0"
                };

                // Launch game
                ProgressStatusText.Text = "Khởi động game...";
                UpdateStatus("Khởi động game...", Brushes.Green);
                var process = await launcher.BuildProcessAsync(versionName, launchOption, cancellationToken);

                // Hide window and start game
                var mainWindow = Application.Current.MainWindow;
                mainWindow.Hide();
                process.Start();

                // Wait for game to exit
                await Task.Run(() => process.WaitForExit(), cancellationToken);
                mainWindow.Show();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Game launch cancelled");
                UpdateStatus("Đã hủy", Brushes.Orange);
            }
        }

        private void UpdateStatus(string message, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                StatusText.Foreground = color;
                _statusLog.Add(new StatusLogEntry
                {
                    Timestamp = DateTime.Now,
                    Message = message,
                    Color = color
                });
                // Keep only the last 50 entries to avoid memory issues
                while (_statusLog.Count > 50)
                    _statusLog.RemoveAt(0);
            });
        }

        private class ConfigData
        {
            public string ModpackName { get; set; } = "Unknown";
            public int MinimumRam { get; set; } = 1024;
            public string MinecraftVersion { get; set; } = "Unknown";
            public string ModLoaderType { get; set; } = "Vanilla";
            public string ModLoaderVersion { get; set; } = "Unknown";
        }

        private class StatusLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; }
            public Brush Color { get; set; }
        }
    }
}