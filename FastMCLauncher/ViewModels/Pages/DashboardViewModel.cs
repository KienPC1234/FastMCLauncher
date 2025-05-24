
namespace FastMCLauncher.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ILogger _logger;

        public DashboardViewModel(ILogger logger)
        {
            _logger = logger;
        }

        [ObservableProperty]
        private int _counter = 0;

        [RelayCommand]
        private void OnCounterIncrement()
        {
            Counter++;
        }
    }
}
