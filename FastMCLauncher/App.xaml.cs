using System.IO;
using System.Windows.Threading;
using FastMCLauncher.Services;
using FastMCLauncher.ViewModels.Pages;
using FastMCLauncher.ViewModels.Windows;
using FastMCLauncher.Views.Pages;
using FastMCLauncher.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace FastMCLauncher
{
    public partial class App
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(
                        path: "Logs/log-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    );
            })
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<Serilog.ILogger>(provider => new LoggerConfiguration()
                    .WriteTo.File("Logs/log-.log", rollingInterval: RollingInterval.Day)
                    .WriteTo.Console()
                    .CreateLogger());

                // Register HttpClient with HttpClientFactory
                services.AddHttpClient();

                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation
                services.AddSingleton<INavigationService, NavigationService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                // Pages and ViewModels
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();

                // Version service for fetching Minecraft and mod loader versions
                services.AddSingleton<IVersionService, VersionService>();
            }).Build();

        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogError(e.Exception, "Unhandled exception!");
            e.Handled = true;
        }
    }
}