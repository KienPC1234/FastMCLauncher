using MCLauncher.Models;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace MCLauncher.Converters
{

    public class BooleanNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SelectedAccountNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Account account)
            {
                return account.IsSelected ? $"{account.Name} (Đang Chọn)" : account.Name;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string type = value?.ToString();
            if (parameter?.ToString() == "Tooltip")
            {
                return type == "online" ? "Đăng Xuất" : "Xóa";
            }
            if (targetType == typeof(SymbolRegular))
            {
                return type == "online" ? SymbolRegular.XboxConsole24 : SymbolRegular.Person24;
            }
            return type == "online" ? "Microsoft Account" : "Offline";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TypeToActionIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string type = value?.ToString();
            if (parameter?.ToString() == "Tooltip")
            {
                return type == "online" ? "Đăng Xuất" : "Xóa";
            }
            return type == "online" ? SymbolRegular.SignOut24 : SymbolRegular.Delete24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is Wpf.Ui.Controls.ListViewItem item && ItemsControl.ItemsControlFromItemContainer(item) is Wpf.Ui.Controls.ListView listView)
                {
                    int index = listView.Items.IndexOf(item.DataContext);
                    return index % 2 == 0 ? "Even" : "Odd";
                }
            }
            catch
            {
                // Fallback if ListViewItem isn't found
            }
            return "Even";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}