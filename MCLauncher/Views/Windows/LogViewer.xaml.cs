using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace MCLauncher.Views.Windows
{
    public partial class LogViewer : Wpf.Ui.Controls.FluentWindow
    {
        private readonly string _logFilePath;
        private readonly RichTextBox _richTextBox;
        private readonly Process _process;
        private readonly DispatcherTimer _timer;
        private long _lastFilePosition;
        private bool _isProcessing;
        private const int MaxLines = 10000;

        public LogViewer(string gameDir, Process process)
        {
            InitializeComponent();
            _logFilePath = Path.Combine(gameDir, "logs", "latest.log");
            _process = process;
            _richTextBox = CreateRichTextBox();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += async (s, e) => await ReadNewLinesAsync();
            SetupUI();
            LoadInitialLogAsync();
        }

        private RichTextBox CreateRichTextBox() => new()
        {
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Margin = new Thickness(10),
            Background = Brushes.Black,
            Foreground = Brushes.White
        };

        private void SetupUI()
        {
            Grid.SetColumn(_richTextBox, 0);
            Grid.SetRow(_richTextBox, 1);
            MainGrid.Children.Add(_richTextBox);
            AutoScrollCheckBox.IsChecked = true; // Default to auto-scroll
        }

        private async void LoadInitialLogAsync()
        {
            try
            {
                if (!File.Exists(_logFilePath)) return;

                using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    await ProcessLogLineAsync(await reader.ReadLineAsync());
                }
                _lastFilePosition = stream.Position;
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR]: Failed to load initial log: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ReadNewLinesAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length < _lastFilePosition) // Handle log file truncation
                {
                    _richTextBox.Document.Blocks.Clear();
                    _lastFilePosition = 0;
                }

                stream.Seek(_lastFilePosition, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    await ProcessLogLineAsync(line);
                }
                _lastFilePosition = stream.Position;
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR]: Failed to read new lines: {ex.Message}", Brushes.Red);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task ProcessLogLineAsync(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            var color = DetermineLogColor(line);
            await Dispatcher.InvokeAsync(() => AppendLog(line, color));
        }

        private static Brush DetermineLogColor(string line) => line switch
        {
            _ when line.Contains("[ERROR]") || line.Contains("ERROR") => Brushes.Red,
            _ when line.Contains("[WARN]") || line.Contains("WARN") => Brushes.Yellow,
            _ when line.Contains("[INFO]") || line.Contains("INFO") => Brushes.Cyan,
            _ when line.Contains("[FATAL]") => Brushes.MediumPurple,
            _ => Brushes.White
        };

        private void AppendLog(string text, Brush color)
        {
            var paragraph = new Paragraph(new Run(text))
            {
                Foreground = color,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _richTextBox.Document.Blocks.Add(paragraph);

            // Limit to 10,000 lines
            while (_richTextBox.Document.Blocks.Count > MaxLines)
            {
                _richTextBox.Document.Blocks.Remove(_richTextBox.Document.Blocks.FirstBlock);
            }

            // Auto-scroll if checkbox is checked
            if (AutoScrollCheckBox.IsChecked == true)
            {
                _richTextBox.ScrollToEnd();
            }
        }

        private void KillButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    AppendLog("[INFO]: Process terminated.", Brushes.Cyan);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR]: Failed to kill process: {ex.Message}", Brushes.Red);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}