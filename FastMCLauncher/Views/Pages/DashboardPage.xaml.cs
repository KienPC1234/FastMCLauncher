using FastMCLauncher.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace FastMCLauncher.Views.Pages
{
    public partial class DashboardPage : System.Windows.Controls.Page
    {
        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}