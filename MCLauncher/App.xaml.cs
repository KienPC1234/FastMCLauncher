using MCLauncher.Services;
using MCLauncher.ViewModels.Windows;
using MCLauncher.Views.Pages;
using MCLauncher.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace MCLauncher
{
    public partial class App
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )
                    .WriteTo.File(
                        path: "Logs/log-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
                    );
            })
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();
                services.AddHostedService<ApplicationHostService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<TaiKhoanPage>();
                services.AddSingleton<SettingsPage>();
            })
            .Build();

        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Application starting up");
            await _host.StartAsync();
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Application shutting down");
            await _host.StopAsync();
            _host.Dispose();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogError(e.Exception, "Unhandled exception in {Source}: {Message}", e.Exception.Source, e.Exception.Message);
            MessageBox.Show($"Lỗi không xác định: {e.Exception.Message}\nXem chi tiết trong Logs/log-.log", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}