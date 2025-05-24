using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;

namespace FastMCLauncher.ViewModels.Pages
{
    public partial class TreeViewItemViewModel : ObservableObject
    {
        private ObservableCollection<TreeViewItemViewModel> _children;

        public TreeViewItemViewModel(string path, bool isRoot = false)
        {
            Path = path;
            Name = isRoot ? System.IO.Path.GetFileName(path) : System.IO.Path.GetFileName(path) == string.Empty ? path : System.IO.Path.GetFileName(path);
            _children = new ObservableCollection<TreeViewItemViewModel>();
            IsDirectory = Directory.Exists(path);
            IsChecked = false;

            // Auto-select mods and config folders
            if (!isRoot && (Name.Equals("mods", StringComparison.OrdinalIgnoreCase) || Name.Equals("config", StringComparison.OrdinalIgnoreCase)))
            {
                IsChecked = true;
                IsSelected = true;
            }
        }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _path;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isChecked;

        public ObservableCollection<TreeViewItemViewModel> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        partial void OnIsCheckedChanged(bool value)
        {
            IsSelected = value;
            if (IsDirectory)
            {
                foreach (var child in Children)
                {
                    child.IsChecked = value;
                }
            }
        }
    }
}