using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RuleEditor.Models
{
    public class Rule : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private ObservableCollection<Condition> _conditions;
        private string _logicalOperator; // "AND" or "OR"

        public Rule()
        {
            Conditions = new ObservableCollection<Condition>();
            LogicalOperator = "AND";
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

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Condition> Conditions
        {
            get => _conditions;
            set
            {
                _conditions = value;
                OnPropertyChanged();
            }
        }

        public string LogicalOperator
        {
            get => _logicalOperator;
            set
            {
                _logicalOperator = value;
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