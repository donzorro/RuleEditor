using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RuleEditor.Models
{
    public class Condition : INotifyPropertyChanged
    {
        private string _propertyName;
        private string _operator;
        private object _value;

        public string PropertyName
        {
            get => _propertyName;
            set
            {
                _propertyName = value;
                OnPropertyChanged();
            }
        }

        public string Operator
        {
            get => _operator;
            set
            {
                _operator = value;
                OnPropertyChanged();
            }
        }

        public object Value
        {
            get => _value;
            set
            {
                _value = value;
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