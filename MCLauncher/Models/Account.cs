using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MCLauncher.Models
{
    public class Account : INotifyPropertyChanged
    {
        private string _type;
        private string _name;
        private string _identifier;
        private bool _isSelected;

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Identifier
        {
            get => _identifier;
            set
            {
                _identifier = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}