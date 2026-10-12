using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace tpkursa4
{
    public abstract class Location : INotifyPropertyChanged
    {
        private string _name;
        private int _id;
        private bool _isFavorite;

        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ListTitle));
                }
            }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ListTitle));
                }
            }
        }

        public string ListTitle => IsFavorite ? "★ " + Name : Name;

        protected Location(string name)
        {
            Name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
