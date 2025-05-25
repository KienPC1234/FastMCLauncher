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

            // Calculate file size for files
            if (!IsDirectory && File.Exists(path))
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    FileSize = FormatFileSize(fileInfo.Length);
                }
                catch (Exception)
                {
                    FileSize = "N/A";
                }
            }
            else
            {
                FileSize = string.Empty; // Directories don't show size
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

        [ObservableProperty]
        private string _fileSize;

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

        public void PropagateCheckedToChildren()
        {
            if (IsDirectory && IsChecked)
            {
                foreach (var child in Children)
                {
                    child.IsChecked = true;
                    child.PropagateCheckedToChildren();
                }
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}