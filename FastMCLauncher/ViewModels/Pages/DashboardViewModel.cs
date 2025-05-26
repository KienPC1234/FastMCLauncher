using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastMCLauncher.Services;
using Ookii.Dialogs.Wpf;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace FastMCLauncher.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ILogger _logger;
        private readonly IVersionService _versionService;
        private readonly Dispatcher _dispatcher;

        // Design-time constructor
        public DashboardViewModel()
        {
            _logger = null;
            _versionService = null;
            _dispatcher = Dispatcher.CurrentDispatcher;
            ModLoaderTypes = new ObservableCollection<string> { "Forge", "Fabric", "Quilt", "LiteLoader" };
            SelectedModLoaderType = "Forge";
            MinecraftVersions = new ObservableCollection<string> { "1.21.5", "1.21.4", "1.21.3" };
            ModLoaderVersions = new ObservableCollection<string> { "55.0.21", "0.16.14" };
            FileTree = new ObservableCollection<TreeViewItemViewModel>
            {
                new TreeViewItemViewModel("test", true)
                {
                    Children = new ObservableCollection<TreeViewItemViewModel> {
                        new TreeViewItemViewModel("mods") { IsChecked = true, IsSelected = true, Children = new ObservableCollection<TreeViewItemViewModel> { new TreeViewItemViewModel("mod1.jar") { IsChecked = true, IsSelected = true } } },
                        new TreeViewItemViewModel("config") { IsChecked = true, IsSelected = true, Children = new ObservableCollection<TreeViewItemViewModel> { new TreeViewItemViewModel("config.cfg") { IsChecked = true, IsSelected = true } } }
                    }
                }
            };
            // Explicitly initialize the command for design-time
            CreateModpackAsyncCommand = new AsyncRelayCommand(CreateModpackAsync);
            LoadMinecraftVersionsAsyncCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
            LoadModLoaderVersionsAsyncCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
        }

        // Runtime constructor
        public DashboardViewModel(ILogger logger, IVersionService versionService)
        {
            _logger = logger;
            _versionService = versionService;
            _dispatcher = Dispatcher.CurrentDispatcher;

            // Initialize collections
            ModLoaderTypes = new ObservableCollection<string> { "Forge", "Fabric", "Quilt", "LiteLoader" };
            MinecraftVersions = new ObservableCollection<string>();
            ModLoaderVersions = new ObservableCollection<string>();
            FileTree = new ObservableCollection<TreeViewItemViewModel>();

            // Initialize commands
            CreateModpackAsyncCommand = new AsyncRelayCommand(CreateModpackAsync);
            LoadMinecraftVersionsAsyncCommand = new AsyncRelayCommand(LoadMinecraftVersionsAsync);
            LoadModLoaderVersionsAsyncCommand = new AsyncRelayCommand(LoadModLoaderVersionsAsync);

            // Set default values
            SelectedModLoaderType = "Forge";

            // Trigger initial version load
            LoadMinecraftVersionsAsyncCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadMinecraftVersionsAsyncCommand { get; }
        public IAsyncRelayCommand LoadModLoaderVersionsAsyncCommand { get; }
        public IAsyncRelayCommand CreateModpackAsyncCommand { get; private set; } // Explicitly declare the command property

        [ObservableProperty]
        private string _modpackName = string.Empty;

        [ObservableProperty]
        private int _minimumRam = 4096;

        [ObservableProperty]
        private ObservableCollection<string> _minecraftVersions;

        [ObservableProperty]
        private string _selectedMinecraftVersion;

        [ObservableProperty]
        private ObservableCollection<string> _modLoaderTypes;

        [ObservableProperty]
        private string _selectedModLoaderType;

        [ObservableProperty]
        private ObservableCollection<string> _modLoaderVersions;

        [ObservableProperty]
        private string _selectedModLoaderVersion;

        [ObservableProperty]
        private string _modpackPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<TreeViewItemViewModel> _fileTree;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isCreating;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public bool IsNotLoading => !IsLoading;

        partial void OnSelectedMinecraftVersionChanged(string value)
        {
            _logger?.Information("Selected Minecraft version changed to: {Version}", value);
            if (LoadModLoaderVersionsAsyncCommand != null)
            {
                LoadModLoaderVersionsAsyncCommand.Execute(null);
            }
        }

        partial void OnSelectedModLoaderTypeChanged(string value)
        {
            _logger?.Information("Selected mod loader type changed to: {Type}", value);
            if (LoadModLoaderVersionsAsyncCommand != null)
            {
                LoadModLoaderVersionsAsyncCommand.Execute(null);
            }
        }

        partial void OnModpackPathChanged(string value)
        {
            _logger?.Information("Modpack path changed to: {Path}", value);
            LoadFileTree();
        }

        [RelayCommand]
        private void BrowseModpackPath()
        {
            try
            {
                var dialog = new VistaFolderBrowserDialog
                {
                    Description = "Select Modpack Folder",
                    UseDescriptionForTitle = true
                };
                if (dialog.ShowDialog() == true)
                {
                    ModpackPath = dialog.SelectedPath;
                    _logger?.Information("Selected modpack path: {Path}", ModpackPath);
                }
                else
                {
                    _logger?.Information("Modpack path selection cancelled");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to browse modpack path");
                MessageBox.Show("Failed to select modpack path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CreateModpackAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(ModpackName))
                {
                    _logger?.Warning("Modpack name is empty");
                    MessageBox.Show("Please enter a modpack name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new VistaFolderBrowserDialog
                {
                    Description = "Select Save Location for Modpack",
                    UseDescriptionForTitle = true
                };
                if (dialog.ShowDialog() == true)
                {
                    IsCreating = true;
                    StatusMessage = "Preparing to create modpack...";

                    var savePath = Path.Combine(dialog.SelectedPath, ModpackName);
                    var modpackDir = Path.Combine(savePath, "modpack");
                    Directory.CreateDirectory(modpackDir);

                    // Save cfg.json
                    var settings = new
                    {
                        ModpackName,
                        MinimumRam,
                        MinecraftVersion = SelectedMinecraftVersion,
                        ModLoaderType = SelectedModLoaderType,
                        ModLoaderVersion = SelectedModLoaderVersion
                    };
                    var cfgPath = Path.Combine(savePath, "config.json");
                    File.WriteAllText(cfgPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
                    _logger?.Information("Saved cfg.json to {Path}", cfgPath);

                    // Copy selected files/folders with status updates
                    var selectedItems = FileTree.SelectMany(node => GetSelectedItems(node)).ToList();
                    int totalItems = selectedItems.Count;
                    int currentItem = 0;

                    foreach (var item in selectedItems)
                    {
                        currentItem++;
                        StatusMessage = $"Copying {Path.GetFileName(item.Path)} ({currentItem}/{totalItems})...";
                        await Task.Delay(1); // Allow UI update

                        var relativePath = Path.GetRelativePath(ModpackPath, item.Path);
                        var destPath = Path.Combine(modpackDir, relativePath);
                        if (item.IsDirectory)
                        {
                            CopyDirectory(item.Path, destPath, item);
                            _logger?.Information("Copied directory {Source} to {Dest}", item.Path, destPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                            File.Copy(item.Path, destPath, true);
                            _logger?.Information("Copied file {Source} to {Dest}", item.Path, destPath);
                        }
                    }

                    StatusMessage = "Modpack creation completed!";
                    _logger?.Information("Created modpack at {Path} with {ItemCount} items", savePath, selectedItems.Count);
                    MessageBox.Show($"Modpack created at: {savePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger?.Information("Modpack save path selection cancelled");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to create modpack");
                StatusMessage = "Error creating modpack. Check logs.";
                MessageBox.Show("Failed to create modpack. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCreating = false;
                StatusMessage = string.Empty;
            }
        }

        private async Task LoadMinecraftVersionsAsync()
        {
            if (_versionService == null)
            {
                _logger?.Warning("VersionService is null, skipping Minecraft versions load (design-time)");
                return;
            }

            try
            {
                IsLoading = true;
                _logger.Information("Fetching Minecraft versions...");
                var versions = await _versionService.GetMinecraftVersionsAsync();
                await _dispatcher.InvokeAsync(() =>
                {
                    MinecraftVersions.Clear();
                    foreach (var version in versions)
                    {
                        MinecraftVersions.Add(version);
                        _logger.Information("Added version: {Version}", version);
                    }

                    if (versions.Any())
                    {
                        var latestVersion = versions.First();
                        SelectedMinecraftVersion = latestVersion;
                        _logger.Information("Loaded {Count} Minecraft versions, selected latest: {Version}", versions.Count, latestVersion);
                        LoadModLoaderVersionsAsyncCommand.Execute(null);
                    }
                    else
                    {
                        _logger.Warning("No Minecraft versions loaded");
                        MessageBox.Show("No Minecraft versions available. Check logs for details.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load Minecraft versions");
                MessageBox.Show("Failed to load Minecraft versions. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                _logger.Information("IsLoading set to false, ComboBoxes should be enabled");
            }
        }

        private async Task LoadModLoaderVersionsAsync()
        {
            if (_versionService == null)
            {
                _logger?.Warning("VersionService is null, skipping mod loader versions load (design-time)");
                return;
            }

            if (string.IsNullOrEmpty(SelectedMinecraftVersion) || string.IsNullOrEmpty(SelectedModLoaderType))
            {
                await _dispatcher.InvokeAsync(() => ModLoaderVersions.Clear());
                _logger.Information("Skipping mod loader versions load due to empty Minecraft version or mod loader type");
                return;
            }

            try
            {
                IsLoading = true;
                _logger.Information("Fetching {ModLoader} versions for Minecraft {Version}...", SelectedModLoaderType, SelectedMinecraftVersion);
                var versions = await _versionService.GetModLoaderVersionsAsync(SelectedMinecraftVersion, SelectedModLoaderType);
                await _dispatcher.InvokeAsync(() =>
                {
                    ModLoaderVersions.Clear();
                    foreach (var version in versions)
                    {
                        ModLoaderVersions.Add(version);
                    }

                    if (versions.Any())
                    {
                        SelectedModLoaderVersion = versions.First();
                        _logger.Information("Loaded {Count} {ModLoader} versions, selected: {Version}", versions.Count, SelectedModLoaderType, SelectedModLoaderVersion);
                    }
                    else
                    {
                        _logger.Warning("No {ModLoader} versions loaded for Minecraft {Version}", SelectedModLoaderType, SelectedMinecraftVersion);
                        MessageBox.Show($"No {SelectedModLoaderType} versions available for Minecraft {SelectedMinecraftVersion}.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load {ModLoader} versions", SelectedModLoaderType);
                MessageBox.Show($"Failed to load {SelectedModLoaderType} versions. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                _logger.Information("IsLoading set to false, mod loader ComboBox should be enabled");
            }
        }

        private void LoadFileTree()
        {
            try
            {
                FileTree.Clear();
                if (string.IsNullOrEmpty(ModpackPath) || !Directory.Exists(ModpackPath))
                {
                    _logger?.Warning("Invalid or empty modpack path: {Path}", ModpackPath);
                    return;
                }

                _logger.Information("Loading file tree for modpack path: {Path}", ModpackPath);
                var rootNode = new TreeViewItemViewModel(ModpackPath, true) { IsChecked = true };
                LoadDirectory(ModpackPath, rootNode);
                FileTree.Add(rootNode);
                _logger.Information("Loaded file tree with {Count} top-level nodes", FileTree.Count);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to load file tree");
                MessageBox.Show("Failed to load modpack files. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDirectory(string path, TreeViewItemViewModel parent)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirNode = new TreeViewItemViewModel(dir);
                    parent.Children.Add(dirNode);
                    LoadDirectory(dir, dirNode);
                    // Auto-check only mods and config that are direct children of the root
                    if (parent.Path == ModpackPath && (dirNode.Name.Equals("mods", StringComparison.OrdinalIgnoreCase) || dirNode.Name.Equals("config", StringComparison.OrdinalIgnoreCase)))
                    {
                        dirNode.IsChecked = true;
                        dirNode.IsSelected = true;
                        dirNode.PropagateCheckedToChildren();
                    }
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    var fileNode = new TreeViewItemViewModel(file);
                    // If parent is the root mods or config, set file as checked
                    if (parent.IsChecked && (parent.Name.Equals("mods", StringComparison.OrdinalIgnoreCase) || parent.Name.Equals("config", StringComparison.OrdinalIgnoreCase)))
                    {
                        fileNode.IsChecked = true;
                    }
                    parent.Children.Add(fileNode);
                }
                _logger.Information("Loaded {DirCount} directories and {FileCount} files in {Path}", Directory.GetDirectories(path).Length, Directory.GetFiles(path).Length, path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.Warning(ex, "Access denied to directory: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error loading directory: {Path}", path);
            }
        }

        private IEnumerable<TreeViewItemViewModel> GetSelectedItems(TreeViewItemViewModel node)
        {
            if (node.IsChecked)
            {
                yield return node;
            }
            foreach (var child in node.Children)
            {
                foreach (var selected in GetSelectedItems(child))
                {
                    yield return selected;
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destDir, TreeViewItemViewModel node)
        {
            Directory.CreateDirectory(destDir);

            // Copy files that are checked
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var relativePath = Path.GetRelativePath(node.Path, file);
                var fileNode = node.Children.FirstOrDefault(c => c.Path == file && !c.IsDirectory);
                if (fileNode != null && fileNode.IsChecked)
                {
                    var destFile = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                    _logger?.Information("Copied checked file {Source} to {Dest}", file, destFile);
                }
            }

            // Copy subdirectories that are checked
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var relativePath = Path.GetRelativePath(node.Path, dir);
                var dirNode = node.Children.FirstOrDefault(c => c.Path == dir && c.IsDirectory);
                if (dirNode != null && dirNode.IsChecked)
                {
                    var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                    CopyDirectory(dir, destSubDir, dirNode);
                    _logger?.Information("Copied checked directory {Source} to {Dest}", dir, destSubDir);
                }
            }
        }
    }
}