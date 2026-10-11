using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace tpkursa4
{
    public abstract class Location : INotifyPropertyChanged
    {
        private string _name;
        private int _id; // Теперь это обычный int для соответствия БД

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
                }
            }
        }

        protected Location(string name)
        {
            Name = name;
            // Убираем Guid.NewGuid(), так как для int дефолтное значение 0, 
            // а реальные ID прилетят из базы данных
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}