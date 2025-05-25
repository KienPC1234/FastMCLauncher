using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using Microsoft.Extensions.Logging;
using MCLauncher.Models;
using Wpf.Ui.Abstractions.Controls;
using CmlLib.Core.Auth.Microsoft.Sessions;
using XboxAuthNet.OAuth;

namespace MCLauncher.Views.Pages
{
    public partial class TaiKhoanPage : Page, INavigableView<object>
    {
        private readonly ILogger<TaiKhoanPage> _logger;
        private readonly string _accountFilePath;
        private ObservableCollection<Account> _accounts;
        private string _selectedAccountId;
        private bool _isProcessing;
        private DateTime _lastLoadTime;

        public event EventHandler<MSession> AccountSelected;

        public object ViewModel => null;

        public TaiKhoanPage(ILogger<TaiKhoanPage> logger)
        {
            _logger = logger;
            _accountFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Account", "accounts.json");
            _accounts = new ObservableCollection<Account>();
            _isProcessing = false;
            _lastLoadTime = DateTime.MinValue;
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogDebug("TaiKhoanPage navigated, checking if reload is needed");
                if ((DateTime.Now - _lastLoadTime).TotalSeconds < 5)
                {
                    _logger.LogDebug("Skipping reload, last load was recent");
                    stopwatch.Stop();
                    return;
                }
                await LoadAccountsAsync();
                _lastLoadTime = DateTime.Now;
                stopwatch.Stop();
                _logger.LogDebug("TaiKhoanPage loaded in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
            };
        }

        public async Task LoadAccountsAsync()
        {
            try
            {
                _logger.LogDebug("Starting to load accounts from {Path}", _accountFilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(_accountFilePath));
                if (!File.Exists(_accountFilePath))
                {
                    _accounts = new ObservableCollection<Account>();
                    AccountListView.ItemsSource = _accounts;
                    _logger.LogInformation("No accounts file found, initialized empty list");
                    return;
                }

                var fileInfo = new FileInfo(_accountFilePath);
                if (fileInfo.Length > 2 * 1024 * 1024)
                {
                    _logger.LogWarning("Accounts file is too large: {Size} bytes", fileInfo.Length);
                    MessageBox.Show("File tài khoản quá lớn, vui lòng xóa bớt tài khoản.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var json = await File.ReadAllTextAsync(_accountFilePath);
                var data = JsonSerializer.Deserialize<AccountData>(json);
                stopwatch.Stop();
                _logger.LogDebug("Deserialized accounts in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                if (data?.Accounts?.Count > 30)
                {
                    _logger.LogWarning("Too many accounts: {Count}", data.Accounts.Count);
                    MessageBox.Show("Số lượng tài khoản vượt quá giới hạn (30). Vui lòng xóa bớt.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    data.Accounts = new ObservableCollection<Account>(data.Accounts.Take(30));
                }

                _accounts = data?.Accounts ?? new ObservableCollection<Account>();
                _selectedAccountId = data?.SelectedAccountId;
                if (!string.IsNullOrEmpty(_selectedAccountId))
                {
                    var selectedAccount = _accounts.FirstOrDefault(a => a.Identifier == _selectedAccountId);
                    if (selectedAccount != null)
                    {
                        selectedAccount.IsSelected = true;
                    }
                }

                stopwatch.Restart();
                AccountListView.ItemsSource = _accounts;
                stopwatch.Stop();
                _logger.LogDebug("Updated ListView in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Loaded {Count} accounts from {Path}", _accounts.Count, _accountFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load accounts");
                MessageBox.Show("Không thể tải danh sách tài khoản.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveAccountsAsync()
        {
            try
            {
                _logger.LogDebug("Starting to save accounts to {Path}", _accountFilePath);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var data = new AccountData
                {
                    Accounts = _accounts,
                    SelectedAccountId = _accounts.FirstOrDefault(a => a.IsSelected)?.Identifier
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_accountFilePath, json);
                stopwatch.Stop();
                _logger.LogDebug("Saved accounts in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Saved {Count} accounts to {Path}", _accounts.Count, _accountFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save accounts");
                MessageBox.Show("Không thể lưu danh sách tài khoản.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddOfflineAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Add offline account request ignored, processing another operation");
                return;
            }

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Processing add offline account");
                var accountName = OfflineAccountNameTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(accountName))
                {
                    _logger.LogWarning("Attempted to add offline account with empty name");
                    MessageBox.Show("Vui lòng nhập tên tài khoản!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (accountName.Length < 3)
                {
                    _logger.LogWarning("Offline account name too short: {Name}", accountName);
                    MessageBox.Show("Tên tài khoản phải có ít nhất 3 ký tự!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_accounts.Any(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Attempted to add duplicate offline account: {Name}", accountName);
                    MessageBox.Show("Tài khoản với tên này đã tồn tại!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var account = new Account
                {
                    Type = "offline",
                    Name = accountName,
                    Identifier = Guid.NewGuid().ToString(),
                    IsSelected = !_accounts.Any(a => a.IsSelected)
                };
                _accounts.Add(account);
                await SaveAccountsAsync();
                OfflineAccountNameTextBox.Text = string.Empty;
                _logger.LogInformation("Added offline account: {Name}", account.Name);
                if (account.IsSelected)
                {
                    AccountSelected?.Invoke(this, MSession.CreateOfflineSession(account.Name));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add offline account");
                MessageBox.Show("Không thể thêm tài khoản.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void AuthenticateOnline_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Authenticate online request ignored, processing another operation");
                return;
            }

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Starting online authentication");
                var loginHandler = JELoginHandlerBuilder.BuildDefault();
                MSession session = null;

                try
                {
                    session = await loginHandler.AuthenticateSilently();
                }
                catch (MicrosoftOAuthException ex)
                {
                    _logger.LogInformation("Silent authentication failed, attempting interactive login: {Message}", ex.Message);
                    session = await loginHandler.AuthenticateInteractively();
                }

                if (session == null)
                {
                    _logger.LogWarning("Authentication failed, no session returned");
                    MessageBox.Show("Không thể đăng nhập tài khoản Microsoft.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var accounts = loginHandler.AccountManager.GetAccounts();
                foreach (var account in accounts)
                {
                    if (account is not JEGameAccount jeAccount)
                        continue;

                    if (_accounts.Any(a => a.Identifier == jeAccount.Identifier))
                    {
                        _logger.LogWarning("Duplicate online account: {Name}", jeAccount.XboxTokens?.XstsToken?.XuiClaims?.Gamertag);
                        continue;
                    }

                    var newAccount = new Account
                    {
                        Type = "online",
                        Name = jeAccount.XboxTokens?.XstsToken?.XuiClaims?.Gamertag ?? jeAccount.Profile?.Username,
                        Identifier = jeAccount.Identifier,
                        IsSelected = !_accounts.Any(a => a.IsSelected)
                    };
                    _accounts.Add(newAccount);
                    _logger.LogInformation("Added online account: {Name}", newAccount.Name);
                    if (newAccount.IsSelected)
                    {
                        AccountSelected?.Invoke(this, session);
                    }
                }
                await SaveAccountsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate online account");
                MessageBox.Show($"Lỗi đăng nhập: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void ActionAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Action account request ignored, processing another operation");
                return;
            }

            if (sender is not Button button || button.Tag is not Account account)
                return;

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Processing action for account: {Name}", account.Name);
                if (account.Type == "offline")
                {
                    _accounts.Remove(account);
                    await SaveAccountsAsync();
                    _logger.LogInformation("Deleted offline account: {Name}", account.Name);
                }
                else if (account.Type == "online")
                {
                    var loginHandler = JELoginHandlerBuilder.BuildDefault();
                    var accounts = loginHandler.AccountManager.GetAccounts();
                    var selectedAccount = accounts.GetAccount(account.Identifier);
                    await loginHandler.SignoutWithBrowser(selectedAccount);
                    _accounts.Remove(account);
                    await SaveAccountsAsync();
                    _logger.LogInformation("Signed out and removed online account: {Name}", account.Name);
                }
                if (_accounts.Any(a => a.IsSelected))
                {
                    var newSelected = _accounts.FirstOrDefault(a => a.IsSelected);
                    AccountSelected?.Invoke(this, await GetSessionAsync());
                }
                else
                {
                    AccountSelected?.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform action on account: {Name}", account.Name);
                MessageBox.Show("Không thể thực hiện hành động.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void SelectAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Select account request ignored, processing another operation");
                return;
            }

            if (sender is not Button button || button.Tag is not Account account)
                return;

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Selecting account: {Name}", account.Name);
                foreach (var acc in _accounts)
                {
                    acc.IsSelected = acc == account;
                }
                AccountListView.Items.Refresh();
                await SaveAccountsAsync();
                _logger.LogInformation("Selected account: {Name}", account.Name);
                var session = await GetSessionAsync();
                AccountSelected?.Invoke(this, session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select account: {Name}", account.Name);
                MessageBox.Show("Không thể chọn tài khoản.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void DeleteSelectedAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Delete selected account request ignored, processing another operation");
                return;
            }

            _isProcessing = true;
            try
            {
                var account = _accounts.FirstOrDefault(a => a.IsSelected);
                if (account == null)
                {
                    _logger.LogWarning("No account selected for deletion");
                    MessageBox.Show("Vui lòng chọn một tài khoản để xóa!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _logger.LogDebug("Deleting selected account: {Name}", account.Name);
                if (account.Type == "offline")
                {
                    _accounts.Remove(account);
                    await SaveAccountsAsync();
                    _logger.LogInformation("Deleted offline account: {Name}", account.Name);
                }
                else if (account.Type == "online")
                {
                    var loginHandler = JELoginHandlerBuilder.BuildDefault();
                    var accounts = loginHandler.AccountManager.GetAccounts();
                    var selectedAccount = accounts.GetAccount(account.Identifier);
                    await loginHandler.SignoutWithBrowser(selectedAccount);
                    _accounts.Remove(account);
                    await SaveAccountsAsync();
                    _logger.LogInformation("Signed out and removed online account: {Name}", account.Name);
                }
                if (_accounts.Any(a => a.IsSelected))
                {
                    var newSelected = _accounts.FirstOrDefault(a => a.IsSelected);
                    AccountSelected?.Invoke(this, await GetSessionAsync());
                }
                else
                {
                    AccountSelected?.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete selected account");
                MessageBox.Show("Không thể xóa tài khoản.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void SaveAccounts_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Save accounts request ignored, processing another operation");
                return;
            }

            _isProcessing = true;
            try
            {
                _logger.LogDebug("Manual save triggered");
                await SaveAccountsAsync();
                MessageBox.Show("Đã lưu danh sách tài khoản!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save accounts manually");
                MessageBox.Show("Không thể lưu danh sách tài khoản.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public async Task<MSession> GetSessionAsync()
        {
            var account = _accounts.FirstOrDefault(a => a.IsSelected);
            if (account == null)
            {
                _logger.LogWarning("No account selected for session");
                MessageBox.Show("Vui lòng chọn một tài khoản!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            try
            {
                _logger.LogDebug("Getting session for account: {Name}", account.Name);
                if (account.Type == "offline")
                {
                    var session = MSession.CreateOfflineSession(account.Name);
                    AccountSelected?.Invoke(this, session);
                    return session;
                }
                else if (account.Type == "online")
                {
                    var loginHandler = JELoginHandlerBuilder.BuildDefault();
                    var accounts = loginHandler.AccountManager.GetAccounts();
                    var selectedAccount = accounts.GetAccount(account.Identifier);
                    var session = await loginHandler.AuthenticateSilently(selectedAccount);
                    if (session == null)
                    {
                        _logger.LogWarning("Silent authentication failed for account: {Name}, attempting interactive login", account.Name);
                        session = await loginHandler.AuthenticateInteractively(selectedAccount);
                    }
                    if (session != null)
                    {
                        AccountSelected?.Invoke(this, session);
                    }
                    return session;
                }
                return null;
            }
            catch (MicrosoftOAuthException ex)
            {
                _logger.LogError(ex, "Failed to authenticate online account: {Name}", account.Name);
                MessageBox.Show("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication for account: {Name}", account.Name);
                MessageBox.Show($"Lỗi đăng nhập: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }

    public class AccountData
    {
        public ObservableCollection<Account> Accounts { get; set; }
        public string SelectedAccountId { get; set; }
    }
}